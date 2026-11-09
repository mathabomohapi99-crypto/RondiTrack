import { Suspense } from 'react';
import { RowsSkeleton } from '@/components/Skeletons';
import { apiJson } from '@/lib/api';
import type { ApiPage } from '@/lib/feedMappers';
import { formatCents } from '@/lib/format';
import { ProofUploadForm } from './ProofUploadForm';

type CycleRow = { id: string; userId: string; memberName: string; amountCents: number };
type ProofInfo = { contributionId: string; fileName: string; sizeBytes: number; locked: boolean };

type Props = { stokvelId: string; cycleId: string; meId: string; isOfficer: boolean };

export function ProofSection(props: Props) {
  return (
    <section>
      <h2 className="mb-2 font-semibold">Proof of payment</h2>
      <Suspense fallback={<RowsSkeleton rows={3} />}>
        <ProofRows {...props} />
      </Suspense>
    </section>
  );
}

async function ProofRows({ stokvelId, cycleId, meId, isOfficer }: Props) {
  // The API tells us which proofs THIS person may open, so a download link shows only where it will work
  const [rows, proofs] = await Promise.all([
    apiJson<ApiPage<CycleRow>>(`/stokvels/${stokvelId}/cycles/${cycleId}/contributions?pageSize=100&sort=date`),
    apiJson<ProofInfo[]>(`/stokvels/${stokvelId}/proofs?cycleId=${cycleId}`),
  ]);
  const byContribution = new Map(proofs.map((p) => [p.contributionId, p]));

  if (rows.items.length === 0) {
    return <p className="text-sm">No contributions in this cycle yet, so there is nothing to attach a proof to.</p>;
  }

  return (
    <ul className="space-y-2">
      {rows.items.map((row) => {
        const proof = byContribution.get(row.id);
        const canEdit = (isOfficer || row.userId === meId) && !proof?.locked; // a closed cycle's proof is locked: no form
        const label = `${row.memberName} (${formatCents(row.amountCents)})`;
        return (
          <li key={row.id} className="rounded border p-3">
            <p className="font-medium">{label}</p>
            {proof && (
              <p className="text-sm">
                {/* Through OUR site: it checks the session and adds the token. Never a public storage link. */}
                <a href={`/bff/proofs?stokvelId=${stokvelId}&contributionId=${row.id}`} className="inline-block py-1 underline">
                  Download proof ({proof.fileName}, {Math.max(1, Math.round(proof.sizeBytes / 1024))} KB)
                </a>
                {proof.locked && <span className="ml-2 text-gray-600">Locked: this cycle is closed.</span>}
              </p>
            )}
            {canEdit && (
              <ProofUploadForm stokvelId={stokvelId} contributionId={row.id} hasProof={proof !== undefined} label={label} />
            )}
          </li>
        );
      })}
    </ul>
  );
}