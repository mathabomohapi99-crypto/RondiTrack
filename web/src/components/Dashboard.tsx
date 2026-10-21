import { useState } from 'react';
import { useStokvels } from '../api/queries';
import {
  clearSelectedStokvel,
  setSelectedStokvelId,
  useSelectedStokvelId,
} from '../state/selectedStokvel';
import { ErrorView } from './ErrorView';
import { Skeleton } from './Skeleton';
import { StokvelScreen } from './StokvelScreen';

export function Dashboard() {
  const stokvels = useStokvels();
  const selectedId = useSelectedStokvelId();
  const [typedId, setTypedId] = useState('');

  if (stokvels.isPending) return <Skeleton label="Loading your stokvels" />;
  if (stokvels.isError) {
    return <ErrorView error={stokvels.error} onRetry={() => void stokvels.refetch()} />;
  }

  // ADJUST: this assumes GET /stokvels returns an array of { id, name }
  const list = stokvels.data;
  if (list.length === 0) {
    return <p>You are not in any stokvel yet. Ask a treasurer to add you.</p>;
  }

  // The selected id is the ONLY input to the contribution hooks
  const activeId = selectedId ?? list[0].id;

  return (
    <div>
      <label htmlFor="stokvel-select">Stokvel</label>{' '}
      <select
        id="stokvel-select"
        value={list.some((s) => s.id === activeId) ? activeId : ''}
        onChange={(e) => setSelectedStokvelId(e.target.value)}
      >
        <option value="" disabled>Choose…</option>
        {list.map((s) => (
          <option key={s.id} value={s.id}>{s.name}</option>
        ))}
      </select>

      {/* Testing helper: lets you open a stokvel id you don't belong to (to see the 404 view) */}
      <div style={{ margin: '8px 0' }}>
        <label htmlFor="open-by-id">Open a stokvel by id (testing)</label>{' '}
        <input id="open-by-id" value={typedId} onChange={(e) => setTypedId(e.target.value)} />{' '}
        <button onClick={() => typedId && setSelectedStokvelId(typedId.trim())}>Open</button>
      </div>

      <StokvelScreen stokvelId={activeId} onBack={clearSelectedStokvel} />
    </div>
  );
}