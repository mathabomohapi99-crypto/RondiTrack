// NEW 7.4: ties the key logic to the generated client. One submitter per form instance.
import { ApiError } from '../api/apiError';
import { api, unwrap } from '../api/client';
import { createIntentKeeper } from './intentKey';

export type SubmitInput = {
  stokvelId: string;
  cycleId: string;
  userId: string; // the member the money is for (the API calls it userId)
  amountInCents: number; // integer cents, always a number
};

// Did we get NO real answer, so the money may have been recorded? Then the key must be kept for the retry.
// 401 (authFetch already tried a refresh), 429 (a rejected request does not use up its key), 5xx, dropped connection.
// A real answer (400, 403, 404, 409, 422) means we know nothing was recorded, so the next try is a fresh intent.
export function keepsKey(error: unknown): boolean {
  if (error instanceof ApiError) {
    return error.status === 401 || error.status === 429 || error.status >= 500;
  }
  return error instanceof TypeError; // fetch rejected: network error, outcome unknown
}

export function createContributionSubmitter() {
  const keeper = createIntentKeeper();

  return async function submit(input: SubmitInput) {
    // Same member + cycle + amount = same intent = same key. Change any of them = new key.
    const fingerprint = `${input.stokvelId}|${input.cycleId}|${input.userId}|${input.amountInCents}`;
    const idempotencyKey = keeper.keyFor(fingerprint);

    try {
      // The header comes from the OpenAPI document, so the generated client types and sends it.
      const result = await unwrap(
        await api.POST('/api/v1/stokvels/{stokvelId}/cycles/{cycleId}/contributions', {
          params: {
            path: { stokvelId: input.stokvelId, cycleId: input.cycleId },
            header: { 'Idempotency-Key': idempotencyKey },
          },
          body: { userId: input.userId, amountInCents: input.amountInCents },
        }),
      );
      keeper.release(); // success (even a replayed 201): this intent is done, the next payment gets a NEW key
      return result;
    } catch (error) {
      if (!keepsKey(error)) keeper.release();
      throw error;
    }
  };
}