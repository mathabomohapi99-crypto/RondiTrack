import type { StokvelDetail, MemberOption } from '@/lib/types';
import { isOfficer } from '@/lib/roles';
import { CloseCycleDialog } from './CloseCycleDialog';
import { ContributionForm } from './ContributionForm';
import { InviteButton } from './InviteButton';

type Props = {
  stokvel: StokvelDetail;
  meId: string;
  members: MemberOption[];
  defaultAmount: string;
  initialKey: string; // made by the page, per request
};

// The member picker, invites and Close Cycle exist ONLY where the API says this person is an officer. The API still refuses (403) anyone else.
export function StokvelActions({ stokvel, meId, members, defaultAmount, initialKey }: Props) {
  const officer = isOfficer(stokvel.myRole);
  const cycle = stokvel.currentCycle;
  return (
    <>
      {cycle && (
        <ContributionForm
          stokvelId={stokvel.id}
          cycleId={cycle.id}
          cycleNumber={cycle.cycleNumber}
          meId={meId}
          members={officer ? members : []}
          showPicker={officer}
          defaultAmount={defaultAmount}
          initialKey={initialKey}
        />
      )}
      {officer && <InviteButton stokvelId={stokvel.id} />}
      {cycle && officer && (
        <CloseCycleDialog stokvelId={stokvel.id} cycleId={cycle.id} cycleNumber={cycle.cycleNumber} />
      )}
    </>
  );
}