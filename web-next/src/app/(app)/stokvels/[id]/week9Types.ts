// NEW 9.1: shared by the Server Actions and the forms. A plain file (no 'use server').

export type ProofState = {
  status: 'idle' | 'success' | 'error';
  fileError?: string; // a WRONG FILE: shown under the input, never in a toast
  message?: string;
  correlationId?: string;
  unexpected?: boolean; // a 500 or an unreachable API: shown as a toast that stays
};

export type InviteState = { code?: string; expiresAtUtc?: string; error?: string; correlationId?: string };

export type ArrearLine = { userId: string; memberName: string; shortfallCents: number };

export type ClosePreview = {
  cycleId: string;
  cycleNumber: number;
  asOfDate: string; // yyyy-MM-dd, the South African date
  dueCentsPerMember: number;
  totalArrearsCents: number;
  arrears: ArrearLine[];
  version: string;
};

export type CloseResult =
  | { status: 'closed'; totalArrearsCents: number; arrearsRecorded: number }
  | { status: 'stale' }
  | { status: 'already_closed' }
  | { status: 'forbidden'; correlationId?: string }
  | { status: 'unknown'; message: string; correlationId?: string } // no real answer: keep the Idempotency-Key
  | { status: 'rejected'; message: string; correlationId?: string }; // a real "no": the next try gets a new key