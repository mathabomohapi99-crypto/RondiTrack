// Shared by the Server Action and the form. A plain file (no 'use server') so the types can be imported anywhere.
export type MemberOption = { id: string; name: string };

export type ContributionFormState = {
  status: 'idle' | 'success' | 'error';
  message?: string;
  correlationId?: string; // the "Reference" for support
  fieldErrors?: { memberId?: string; amount?: string }; // shown next to their fields
  values: { memberId: string; amount: string }; // what the person typed (React resets action forms)
  idempotencyKey: string; // the key for the NEXT request (hidden field)
  keyFor: string; // which member/amount that key belongs to ('' = not used yet)
  mayHaveLanded?: boolean; // no real answer: the payment may have gone through
};

export function initialFormState(memberId: string, amount: string, idempotencyKey: string): ContributionFormState {
  return { status: 'idle', values: { memberId, amount }, idempotencyKey, keyFor: '' };
}