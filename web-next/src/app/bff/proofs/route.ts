import { NextResponse, type NextRequest } from 'next/server';
import { apiFetchWithToken } from '@/lib/api';
import { ALLOWED_PROOF_TYPES } from '@/lib/proofRules';
import { getSession } from '@/lib/session';

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const NO_STORE = { 'Cache-Control': 'private, no-store', 'X-Robots-Tag': 'noindex' };

// GET /bff/proofs?stokvelId=...&contributionId=...
// Checks the session, adds the token, streams the file back as an attachment. The API decides who may see it.
export async function GET(request: NextRequest) {
  const session = await getSession();
  if (!session) return NextResponse.json({ error: 'unauthorized' }, { status: 401 }); // JSON, never an HTML redirect

  const params = request.nextUrl.searchParams;
  const stokvelId = params.get('stokvelId') ?? '';
  const contributionId = params.get('contributionId') ?? '';
  if (!UUID.test(stokvelId) || !UUID.test(contributionId)) {
    return NextResponse.json({ error: 'bad_request' }, { status: 400, headers: NO_STORE });
  }

  let res: Response;
  try {
    res = await apiFetchWithToken(session.accessToken, `/stokvels/${stokvelId}/contributions/${contributionId}/proof`);
  } catch {
    return NextResponse.json({ error: 'unavailable' }, { status: 502, headers: NO_STORE });
  }
  if (res.status === 401) return NextResponse.json({ error: 'unauthorized' }, { status: 401, headers: NO_STORE });
  // Not allowed and missing look exactly the same
  if (res.status === 404) return NextResponse.json({ error: 'not_found' }, { status: 404, headers: NO_STORE });
  if (!res.ok || !res.body) return NextResponse.json({ error: 'unavailable' }, { status: 502, headers: NO_STORE });

  const type = res.headers.get('Content-Type')?.split(';')[0] ?? '';
  const headers = new Headers(NO_STORE);
  headers.set('Content-Type', (ALLOWED_PROOF_TYPES as readonly string[]).includes(type) ? type : 'application/octet-stream');
  headers.set('Content-Disposition', res.headers.get('Content-Disposition') ?? 'attachment; filename="proof-of-payment"');
  headers.set('X-Content-Type-Options', 'nosniff'); // the browser must not guess another type
  const length = res.headers.get('Content-Length');
  if (length) headers.set('Content-Length', length);

  return new Response(res.body, { status: 200, headers }); // streamed, not buffered
}