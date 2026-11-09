import { NextResponse, type NextRequest } from 'next/server';
import { apiFetchWithToken } from '@/lib/api';
import { codeFrom, correlationIdFrom } from '@/lib/errors';
import { getSession } from '@/lib/session';

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const NO_STORE = { 'Cache-Control': 'no-store' };

// GET /bff/close-preview?stokvelId=...&cycleId=...  The dialog calls THIS. The numbers are the API's, never worked out here.
export async function GET(request: NextRequest) {
  const session = await getSession();
  if (!session) return NextResponse.json({ error: 'unauthorized' }, { status: 401 });

  const params = request.nextUrl.searchParams;
  const stokvelId = params.get('stokvelId') ?? '';
  const cycleId = params.get('cycleId') ?? '';
  if (!UUID.test(stokvelId) || !UUID.test(cycleId)) {
    return NextResponse.json({ error: 'bad_request' }, { status: 400, headers: NO_STORE });
  }

  let res: Response;
  try {
    res = await apiFetchWithToken(session.accessToken, `/stokvels/${stokvelId}/cycles/${cycleId}/close-preview`);
  } catch {
    return NextResponse.json({ error: 'unavailable' }, { status: 502, headers: NO_STORE });
  }

  const body = await res.json().catch(() => null);
  if (res.ok) return NextResponse.json(body, { headers: NO_STORE });

  const status = [401, 403, 404, 409].includes(res.status) ? res.status : 502;
  return NextResponse.json(
    { error: codeFrom(body) ?? (status === 502 ? 'unavailable' : String(status)), correlationId: correlationIdFrom(body) },
    { status, headers: NO_STORE },
  );
}