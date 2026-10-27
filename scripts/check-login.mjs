// Shows which Set-Cookie header carries the refresh token. Run: node scripts/check-login.mjs
const res = await fetch('http://localhost:5134/api/auth/login', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ email: 'lerato.demo@example.com', password: 'Demo-password-123' }),
});
console.log('status:', res.status);
console.log('set-cookie:', res.headers.getSetCookie()); // the refresh token is in here
console.log('body keys:', Object.keys(await res.json()));
