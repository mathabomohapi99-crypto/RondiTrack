import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { ToastProvider } from '@/components/Toasts';
import type { MemberOption, Role, StokvelDetail } from '@/lib/types';
import type { components } from '@/types/schema';

// Only the Server Actions (the edge) are faked
vi.mock('./contributionActions', () => ({ recordContribution: vi.fn() }));
vi.mock('./inviteActions', () => ({ createInvite: vi.fn() }));
vi.mock('./closeCycleActions', () => ({ closeCycle: vi.fn() }));

import { StokvelActions } from './StokvelActions';

// Built from the GENERATED API types: rename a field in the API, regenerate, and this stops compiling.
// The generated type says role and frequency are integers (known gap), but the API sends names, so those two are replaced.
type ApiDetail = components['schemas']['StokvelDetailResponse'];
type Wire = Omit<ApiDetail, 'myRole' | 'frequency'> & { myRole: Role | null; frequency: StokvelDetail['frequency'] };

function apiStokvel(id: string, name: string, myRole: Role | null): Wire {
  return {
    id, name, contributionAmount: 500, frequency: 'Monthly', maxMembers: 6, memberCount: 3, isFull: false,
    payoutPerCycle: 1500, createdAtUtc: '2026-10-01T00:00:00Z', myRole,
    currentCycle: {
      id: '22222222-2222-4222-8222-222222222222', cycleNumber: 3, targetAmount: 500, collectedAmount: 0,
      startedAtUtc: '2026-11-01T00:00:00Z', nextPayoutDateUtc: '2026-12-01T00:00:00Z',
    },
  };
}
const toDetail = (w: Wire): StokvelDetail => ({ ...w, currentCycle: w.currentCycle ?? null });

const SIPHO = '33333333-3333-4333-8333-333333333333';
const MEMBERS: MemberOption[] = [{ id: SIPHO, name: 'Sipho' }, { id: '44444444-4444-4444-8444-444444444444', name: 'Thandi' }];
// ONE person, TWO stokvels: treasurer of the family one, only a member of the work one
const family = toDetail(apiStokvel('11111111-1111-4111-8111-111111111111', 'Family', 'Treasurer'));
const work = toDetail(apiStokvel('55555555-5555-4555-8555-555555555555', 'Work', 'Member'));

function view(stokvel: StokvelDetail) {
  return render(
    <QueryClientProvider client={new QueryClient()}>
      <ToastProvider>
        <StokvelActions stokvel={stokvel} meId={SIPHO} members={MEMBERS} defaultAmount="500.00"
          initialKey="aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa" />
      </ToastProvider>
    </QueryClientProvider>,
  );
}

describe('Sipho in the stokvel where he is a treasurer', () => {
  it('can record for another member, invite, and close the cycle', () => {
    view(family);
    expect(screen.getByRole('combobox', { name: 'Member' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Create invite code' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Close Cycle 3…' })).toBeInTheDocument();
  });
});

describe('Sipho in the stokvel where he is only a member', () => {
  it('has none of the treasurer actions, and nothing on screen hints that they exist', () => {
    view(work);
    expect(screen.queryByRole('combobox', { name: 'Member' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Create invite code' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /close cycle/i })).not.toBeInTheDocument();
    expect(screen.queryByText(/treasurer|invite|arrears/i)).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Record contribution' })).toBeInTheDocument(); // his own contribution is still his to record
  });
});