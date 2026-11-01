// Answers "API check" question 1 from Node (who calls the API now).
// Run from the repo root with the API running: node scripts/check-refresh.mjs
const base = 'http://localhost:5134/api/v1';

const tokenFrom = (res) =>
  res.headers.getSetCookie().find((c) => c.startsWith('rondi_refresh='))?.split(';')[0].slice('rondi_refresh='.length);

const post = (path, cookie) =>
  fetch(`${base}${path}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', ...(cookie ? { Cookie: `rondi_refresh=${cookie}` } : {}) },
    body: path === '/auth/login' ? JSON.stringify({ email: 'lerato.demo@example.com', password: 'Demo-password-123' }) : undefined,
  });

const login = await post('/auth/login');
const first = tokenFrom(login);
console.log('login:', login.status);

const refreshed = await post('/auth/refresh', first);
const second = tokenFrom(refreshed);
console.log('refresh with the first token:', refreshed.status, '| body keys:', Object.keys(await refreshed.json()));
console.log('new refresh token is in Set-Cookie, and different:', Boolean(second) && second !== first);

const replay = await post('/auth/refresh', first);
console.log('the OLD token again:', replay.status);

const afterReuse = await post('/auth/refresh', second);
console.log('the NEW token after that reuse:', afterReuse.status, '(401 = the whole family was revoked)');