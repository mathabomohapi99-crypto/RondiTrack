import { ImageResponse } from 'next/og';

export const alt = 'RondiTrack: stokvel contributions, simply tracked';
export const size = { width: 1200, height: 630 };
export const contentType = 'image/png';

export default function Image() {
  return new ImageResponse(
    (
      <div style={{ width: '100%', height: '100%', display: 'flex', alignItems: 'center', justifyContent: 'center',
        background: '#111', color: '#fff', fontSize: 96, fontWeight: 700 }}>
        RondiTrack
      </div>
    ),
    size,
  );
}