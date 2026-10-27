// Simple loading placeholder
export function Skeleton({ label }: { label: string }) {
  return (
    <div aria-busy="true" aria-label={label} style={{ display: 'grid', gap: 8, margin: '16px 0' }}>
      <div style={{ height: 24, width: '40%', background: '#ddd', borderRadius: 4 }} />
      <div style={{ height: 16, background: '#eee', borderRadius: 4 }} />
      <div style={{ height: 16, background: '#eee', borderRadius: 4 }} />
      <div style={{ height: 16, background: '#eee', borderRadius: 4 }} />
    </div>
  );
}