import { NextResponse, type NextRequest } from 'next/server';
import { apiFetchWithToken } from '@/lib/api';
import { loadContributionFilters } from '@/lib/contributionSearchParams';
import { contributionsPath } from '@/lib/contributionsPath';
import { toContributionRow, type ApiContribution, type ApiPage } from '@/lib/feedMappers';
import { getSession } from '@/lib/session';

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

// More pages of the contribution history, for the browser. A public endpoint: it checks the session itself.
export async function GET(request: NextRequest) {
  const session = await getSession();
  if (!session) return NextResponse.json({ error: 'unauthorized' }, { status: 401 });

  const params = request.nextUrl.searchParams;
  const stokvelId = params.get('stokvelId') ?? '';
  if (!UUID.test(stokvelId)) return NextResponse.json({ error: 'bad_request' }, { status: 400 });

  // The SAME parsers as the page: nonsense falls back to the defaults
  const filters = loadContributionFilters(params);
  const { path } = contributionsPath(stokvelId, filters, 20, params.get('pageToken') ?? '');

  let res: Response;
  try {
    res = await apiFetchWithToken(session.accessToken, path);
  } catch {
    return NextResponse.json({ error: 'unavailable' }, { status: 502 });
  }
  if (res.status === 401) return NextResponse.json({ error: 'unauthorized' }, { status: 401 });
  if (res.status === 404) return NextResponse.json({ error: 'not_found' }, { status: 404 });
  // A page token used with other filters comes back 400 from the API. We pass that on, we never "fix" it.
  if (res.status === 400) return NextResponse.json({ error: 'bad_request' }, { status: 400 });
  if (!res.ok) return NextResponse.json({ error: 'unavailable' }, { status: 502 });

  const page = (await res.json()) as ApiPage<ApiContribution>;
  return NextResponse.json(
    { rows: page.items.map(toContributionRow), nextPageToken: page.nextPageToken },
    { headers: { 'Cache-Control': 'no-store' } },
  );
}