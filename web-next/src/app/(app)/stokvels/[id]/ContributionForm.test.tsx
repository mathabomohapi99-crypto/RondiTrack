import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { ContributionFormState } from './contributionTypes';

// The Server Action is the edge of this form, so it is the only thing faked
const recordContribution = vi.hoisted(() => vi.fn());
vi.mock('./contributionActions', () => ({ recordContribution }));

import { ContributionForm } from './ContributionForm';

const ME = '33333333-3333-4333-8333-333333333333';
const KEY = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
const AMOUNT_ERROR = 'Enter a valid amount, like 150.50 (at most 2 decimals)';

const answer = (over: Partial<ContributionFormState>): ContributionFormState => ({
  status: 'idle', values: { memberId: ME, amount: '150.00' }, idempotencyKey: KEY, keyFor: '', ...over,
});

function renderForm(showPicker = false) {
  return render(
    <QueryClientProvider client={new QueryClient()}>
      <ContributionForm
        stokvelId="11111111-1111-4111-8111-111111111111"
        cycleId="22222222-2222-4222-8222-222222222222"
        cycleNumber={3}
        meId={ME}
        members={[{ id: ME, name: 'Thandi' }, { id: '44444444-4444-4444-8444-444444444444', name: 'Sipho' }]}
        showPicker={showPicker}
        defaultAmount="150.00"
        initialKey={KEY}
      />
    </QueryClientProvider>,
  );
}

beforeEach(() => recordContribution.mockReset());

describe('ContributionForm', () => {
  it('shows a field error under the amount, keeps what was typed, and ties the two together', async () => {
    recordContribution.mockResolvedValue(answer({
      status: 'error', message: 'Please check the highlighted fields.',
      fieldErrors: { amount: AMOUNT_ERROR }, values: { memberId: ME, amount: '12abc' },
    }));
    const user = userEvent.setup();
    renderForm();

    const amount = screen.getByLabelText('Amount (rands)');
    await user.clear(amount);
    await user.type(amount, '12abc');
    await user.click(screen.getByRole('button', { name: 'Record contribution' }));

    expect(await screen.findByText(AMOUNT_ERROR)).toBeInTheDocument();
    await waitFor(() => expect(screen.getByLabelText('Amount (rands)')).toHaveValue('12abc')); // SC 3.3.7: kept
    expect(screen.getByLabelText('Amount (rands)')).toHaveAccessibleDescription(AMOUNT_ERROR);
    expect(screen.getByLabelText('Amount (rands)')).toBeInvalid();
  });

  it('shows the API field "userId" under the Member picker (the API name differs from the form name)', async () => {
    recordContribution.mockResolvedValue(answer({
      status: 'error', fieldErrors: { memberId: 'Choose a member' },
    }));
    const user = userEvent.setup();
    renderForm(true);

    await user.click(screen.getByRole('button', { name: 'Record contribution' }));

    expect(await screen.findByText('Choose a member')).toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: 'Member' })).toHaveAccessibleDescription('Choose a member');
  });

  it('shows a 409/422 explanation with its reference at the top of the form', async () => {
    recordContribution.mockResolvedValue(answer({
      status: 'error', message: 'This cycle is no longer open, so contributions cannot be recorded.', correlationId: 'abc123',
    }));
    const user = userEvent.setup();
    renderForm();

    await user.click(screen.getByRole('button', { name: 'Record contribution' }));

    expect(await screen.findByText('This cycle is no longer open, so contributions cannot be recorded.')).toBeInTheDocument();
    expect(screen.getByText('Reference: abc123')).toBeInTheDocument();
  });

  it('shows the success message and clears the amount', async () => {
    recordContribution.mockResolvedValue(answer({
      status: 'success', message: 'Recorded R 150,50.', values: { memberId: ME, amount: '' },
    }));
    const user = userEvent.setup();
    renderForm();

    await user.click(screen.getByRole('button', { name: 'Record contribution' }));

    expect(await screen.findByRole('status')).toHaveTextContent('Recorded R 150,50.');
    await waitFor(() => expect(screen.getByLabelText('Amount (rands)')).toHaveValue(''));
  });

  it('one click is one request: a second click while it is pending sends nothing', async () => {
    let finish!: (s: ContributionFormState) => void;
    recordContribution.mockImplementation(() => new Promise<ContributionFormState>((resolve) => { finish = resolve; }));
    const user = userEvent.setup();
    renderForm();

    await user.click(screen.getByRole('button', { name: 'Record contribution' }));
    await waitFor(() => expect(recordContribution).toHaveBeenCalledTimes(1));

    const pending = await screen.findByRole('button', { name: /Recording/ });
    expect(pending).toBeDisabled();
    await user.click(pending);
    expect(recordContribution).toHaveBeenCalledTimes(1);

    finish(answer({ status: 'success', message: 'Recorded R 150,00.' })); // the test decides when the answer arrives
    expect(await screen.findByRole('status')).toHaveTextContent('Recorded R 150,00.');
    expect(recordContribution).toHaveBeenCalledTimes(1);
  });

  it('sends the key it was given as a hidden field, and the key the server returns next time', async () => {
    const NEXT = 'cccccccc-cccc-4ccc-8ccc-cccccccccccc';
    recordContribution
      .mockResolvedValueOnce(answer({ status: 'error', message: 'No answer. Trying again is safe.', mayHaveLanded: true, keyFor: 'f' }))
      .mockResolvedValueOnce(answer({ status: 'success', message: 'Recorded.', idempotencyKey: NEXT }));
    const user = userEvent.setup();
    renderForm();

    await user.click(screen.getByRole('button', { name: 'Record contribution' }));
    await user.click(await screen.findByRole('button', { name: 'Try again' }));
    await screen.findByRole('status');

    const sentKeys = recordContribution.mock.calls.map((call) => (call[1] as FormData).get('idempotencyKey'));
    expect(sentKeys).toEqual([KEY, KEY]); // the retry carried the SAME key
  });
});