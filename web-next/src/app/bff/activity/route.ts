import { NextResponse, type NextRequest } from 'next/server';
import { apiFetchWithToken } from '@/lib/api';
import { toActivityRow, type ApiActivity, type ApiPage } from '@/lib/feedMappers';
import { getSession } from '@/lib/session';

// The browser calls THIS (our own site). This calls the API with the token. It is a public endpoint.
export async function GET(request: NextRequest) {
  const session = await getSession();
  // No session = a JSON 401, never an HTML redirect
  if (!session) return NextResponse.json({ error: 'unauthorized' }, { status: 401 });

  const qs = new URLSearchParams({ pageSize: '20' });
  const token = request.nextUrl.searchParams.get('pageToken');
  if (token) qs.set('pageToken', token);

  let res: Response;
  try {
    res = await apiFetchWithToken(session.accessToken, `/me/activity?${qs.toString()}`);
  } catch {
    return NextResponse.json({ error: 'unavailable' }, { status: 502 });
  }
  if (res.status === 401) return NextResponse.json({ error: 'unauthorized' }, { status: 401 });
  if (res.status === 400) return NextResponse.json({ error: 'bad_request' }, { status: 400 });
  if (!res.ok) return NextResponse.json({ error: 'unavailable' }, { status: 502 });

  const page = (await res.json()) as ApiPage<ApiActivity>;
  return NextResponse.json(
    { rows: page.items.map(toActivityRow), nextPageToken: page.nextPageToken },
    { headers: { 'Cache-Control': 'no-store' } },
  );
}