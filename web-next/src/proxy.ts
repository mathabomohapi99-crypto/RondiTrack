import { NextResponse, type NextRequest } from 'next/server';
import { needsRefresh, refreshMarginMs } from './lib/refreshPolicy';
import { refreshOnce } from './lib/refresh';
import { decryptSession, encryptSession, SESSION_COOKIE, sessionCookieOptions } from './lib/sessionCrypto';

// CHANGED 9.2: /join is public (the page itself sends people to sign-in, bots get the metadata). /login/register is the real route
// (the old '/register' did not exist: the Create one link was broken). opengraph-image is fetched by bots.
const PUBLIC_PATHS = ['/', '/login', '/login/register', '/join', '/opengraph-image'];

// Puts the new session into the raw Cookie header of THIS request, so the page about to render sees it
function withSessionCookie(cookieHeader: string | null, value: string): string {
  const others = (cookieHeader ?? '').split(/;\s*/).filter((p) => p && !p.startsWith(`${SESSION_COOKIE}=`));
  return [...others, `${SESSION_COOKIE}=${value}`].join('; ');
}

export async function proxy(request: NextRequest) {
  const { pathname, search } = request.nextUrl;
  const isPublic = PUBLIC_PATHS.includes(pathname);
  const isBff = pathname.startsWith('/bff/');

  const raw = request.cookies.get(SESSION_COOKIE)?.value;
  const session = raw ? await decryptSession(raw) : null;

  // No session: public pages open; /bff/ gets a JSON 401; everything else goes to /login?next=
  if (!session) {
    if (isPublic) return NextResponse.next();
    if (isBff) return NextResponse.json({ error: 'unauthorized' }, { status: 401 });
    const url = new URL('/login', request.url);
    url.searchParams.set('next', pathname + search);
    return NextResponse.redirect(url, request.method === 'GET' ? 307 : 303);
  }

  // Plenty of time left on the access token: nothing to do
  if (!needsRefresh(session.accessExpiresAt, Date.now(), refreshMarginMs())) return NextResponse.next();

  // The ONLY place in the whole app that refreshes
  const result = await refreshOnce(session);

  if (result.kind === 'refused') {
    // The session is over: delete it and tell the person
    const response = isBff
      ? NextResponse.json({ error: 'unauthorized' }, { status: 401 })
      : isPublic
        ? NextResponse.next()
        : NextResponse.redirect(new URL('/login?expired=1', request.url));
    response.cookies.delete(SESSION_COOKIE);
    return response;
  }

  // API unreachable: keep the session as it is, the page will show its own error
  if (result.kind === 'unavailable') return NextResponse.next();

  const token = await encryptSession(result.session);
  const headers = new Headers(request.headers);
  headers.set('cookie', withSessionCookie(request.headers.get('cookie'), token)); // 1. into THIS request
  const response = NextResponse.next({ request: { headers } });
  response.cookies.set(SESSION_COOKIE, token, sessionCookieOptions); // 2. onto the response, so the browser keeps it
  return response;
}

export const config = {
  matcher: ['/((?!_next/static|_next/image|favicon.ico|.*\\..*).*)'],
};