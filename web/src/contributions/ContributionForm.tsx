// NEW 7.4: the form. React Hook Form + Zod + useMutation (mutateAsync awaited in the submit handler).
import { useRef, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { ApiError } from '../api/apiError';
import { queryKeys } from '../api/queryKeys';
import { Reference } from '../components/ErrorView';
import { describeError, parseRetryAfter, rateLimitMessage } from '../lib/errorMessages';
import { formatCents } from '../lib/money';
import { centsToInput, parseRandsToCents } from '../lib/rands';
import { createContributionSubmitter } from './submitContribution';

export type Member = { id: string; name: string };

type Props = {
  stokvelId: string;
  cycleId: string;
  cycleNumber: number;
  isTreasurer: boolean; // role for THIS stokvel, from /me/home
  meId: string;
  members: Member[]; // only used by a Treasurer
  owedCents: number; // what the member still owes, used to prefill
};

// Zod checks the typed text with OUR parser, so "150.505" is refused instead of rounded
const schema = z.object({
  memberId: z.string().min(1, 'Choose a member'),
  amount: z
    .string()
    .refine((v) => parseRandsToCents(v) !== null, 'Enter a valid amount, like 150.50 (at most 2 decimals)')
    .refine((v) => (parseRandsToCents(v) ?? 0) > 0, 'The amount must be more than R0'),
});
type FormValues = z.infer<typeof schema>;

// Keys in a 400 "errors" map (lower-case, as describeError gives them) -> our field names.
// Any key we do not know is shown at the top of the form.
const FIELD_MAP: Record<string, keyof FormValues> = {
  amountincents: 'amount',
  userid: 'memberId',
};

type Shown = {
  lines: string[];
  correlationId: string | null;
  fieldErrors: Record<string, string[]>;
  mayHaveLanded: boolean;
};

// Turns any error into what the person sees. Reuses describeError for everything it already knows.
function explain(error: unknown): Shown {
  const d = describeError(error);
  const detail =
    error instanceof ApiError && typeof error.problem?.detail === 'string' ? error.problem.detail : null;
  const base = { correlationId: d.correlationId, fieldErrors: d.fieldErrors };

  // No answer at all: the payment may or may not have been recorded
  if (d.kind === 'network') {
    return {
      ...base,
      mayHaveLanded: true,
      lines: [
        'We did not get an answer, so your payment may have gone through. Your details are kept. Trying again is safe: it will not pay twice.',
      ],
    };
  }
  if (error instanceof ApiError && error.status === 429) {
    return {
      ...base,
      mayHaveLanded: true,
      lines: [`${rateLimitMessage(parseRetryAfter(error.retryAfter))} Your details are kept, and trying again is safe.`],
    };
  }
  if (error instanceof ApiError && error.status >= 500) {
    return {
      ...base,
      mayHaveLanded: true,
      lines: [
        'RondiTrack had a problem, so your payment may have gone through. Your details are kept. Trying again is safe: it will not pay twice.',
      ],
    };
  }
  // 400 / 403 / 409 / 422: show the API's own detail (it explains why)
  if (error instanceof ApiError && [400, 403, 409, 422].includes(error.status)) {
    return { ...base, mayHaveLanded: false, lines: [detail ?? d.message] };
  }
  return { ...base, mayHaveLanded: false, lines: [d.message] };
}

export function ContributionForm({ stokvelId, cycleId, cycleNumber, isTreasurer, meId, members, owedCents }: Props) {
  const queryClient = useQueryClient();

  // One submitter (and so one key keeper) per form. This initialiser makes NO key, so StrictMode is safe.
  const [submitter] = useState(() => createContributionSubmitter());
  const inFlight = useRef(false); // stops a double-click from sending two requests
  const [shown, setShown] = useState<Shown | null>(null);
  const [confirmation, setConfirmation] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    reset,
    setError,
    clearErrors,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    // Prefill with what the member still owes (nothing when they owe nothing)
    defaultValues: { memberId: meId, amount: owedCents > 0 ? centsToInput(owedCents) : '' },
  });

  const mutation = useMutation({ mutationFn: submitter });

  const onSubmit = async (values: FormValues) => {
    if (inFlight.current) return; // a second click in the same moment does nothing
    inFlight.current = true;
    setShown(null);
    setConfirmation(null);
    clearErrors();

    try {
      const amountInCents = parseRandsToCents(values.amount);
      if (amountInCents === null) return; // Zod already refused this, so it cannot happen

      await mutation.mutateAsync({ stokvelId, cycleId, userId: values.memberId, amountInCents });

      // Wait for the refetch, so the button stays disabled until the new numbers are on screen
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.home }),
        queryClient.invalidateQueries({ queryKey: queryKeys.contributions(stokvelId, cycleId) }),
      ]);

      const who =
        values.memberId === meId && !isTreasurer
          ? 'you'
          : (members.find((m) => m.id === values.memberId)?.name ?? 'the member');
      setConfirmation(`Recorded ${formatCents(amountInCents)} for ${who} in Cycle ${cycleNumber}.`);
      // keepFieldsRef: the React Compiler and reset() do not agree without it
      reset({ memberId: meId, amount: '' }, { keepFieldsRef: true });
    } catch (error) {
      const result = explain(error);
      setShown(result);
      const extra: string[] = [];
      for (const [key, messages] of Object.entries(result.fieldErrors)) {
        const field = FIELD_MAP[key];
        if (field) setError(field, { message: messages.join(' ') }); // goes under its field
        else extra.push(`${key}: ${messages.join(' ')}`); // unknown key goes to the top
      }
      if (extra.length > 0) setShown({ ...result, lines: [...result.lines, ...extra] });
    } finally {
      inFlight.current = false;
    }
  };

  return (
    <form onSubmit={(event) => void handleSubmit(onSubmit)(event)} noValidate style={{ display: 'grid', gap: 8, margin: '16px 0' }}>
      <h3>Record a contribution</h3>

      {shown && (
        <div role="alert" style={{ border: '1px solid #c33', padding: 12, borderRadius: 8 }}>
          {shown.lines.map((line) => (
            <p key={line}>{line}</p>
          ))}
          <Reference id={shown.correlationId} />
        </div>
      )}
      {confirmation && <p role="status">{confirmation}</p>}

      {/* A Member never sees the picker. Hiding it is only convenience: the API's 403 still protects the rule. */}
      {isTreasurer && (
        <div>
          <label htmlFor="memberId">Member</label>{' '}
          <select id="memberId" {...register('memberId')}>
            {members.map((m) => (
              <option key={m.id} value={m.id}>
                {m.name}
              </option>
            ))}
          </select>
          {errors.memberId && <p role="alert">{errors.memberId.message}</p>}
        </div>
      )}

      <div>
        <label htmlFor="amount">Amount (rands)</label>{' '}
        <input id="amount" inputMode="decimal" autoComplete="off" {...register('amount')} />
        {errors.amount && <p role="alert">{errors.amount.message}</p>}
      </div>

      {/* Disabled from the click until the API answered AND the queries refetched */}
      <button type="submit" disabled={isSubmitting}>
        {isSubmitting ? 'Recording...' : shown?.mayHaveLanded ? 'Try again' : 'Record contribution'}
      </button>
    </form>
  );
}