import { EMAIL, type Person } from './people';

const API = `${process.env.API_BASE_URL ?? 'http://localhost:5134'}/api/v1`;
const tokens = new Map<Person, { value: string; at: number }>();

async function token(person: Person): Promise<string> {
  const cached = tokens.get(person);
  if (cached && Date.now() - cached.at < 8 * 60_000) return cached.value; // the API token lives 10 minutes
  const res = await fetch(`${API}/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email: EMAIL[person], password: process.env.E2E_PASSWORD }),
  });
  if (!res.ok) throw new Error(`API login for ${person} failed: ${res.status}`);
  const value = ((await res.json()) as { accessToken: string }).accessToken;
  tokens.set(person, { value, at: Date.now() });
  return value;
}

async function call(person: Person, path: string, init: RequestInit = {}): Promise<Response> {
  const headers = new Headers(init.headers);
  headers.set('Authorization', `Bearer ${await token(person)}`);
  if (init.body) headers.set('Content-Type', 'application/json');
  return fetch(`${API}${path}`, { ...init, headers });
}

// A brand new cycle with the HIGHEST number, so it is the stokvel's current cycle and nothing is shared with other tests
export async function createCycle(person: Person, stokvelId: string): Promise<string> {
  const list = await call(person, `/stokvels/${stokvelId}/cycles`);
  if (!list.ok) throw new Error(`list cycles: ${list.status}`);
  const highest = ((await list.json()) as { number: number }[]).reduce((m, c) => Math.max(m, c.number), 0);
  const res = await call(person, `/stokvels/${stokvelId}/cycles`, {
    method: 'POST',
    body: JSON.stringify({ cycleNumber: highest + 1, targetAmount: 500 }),
  });
  if (res.status !== 201) throw new Error(`create cycle: ${res.status} ${await res.text()}`);
  return ((await res.json()) as { id: string }).id;
}

export async function createInvite(person: Person, stokvelId: string): Promise<string> {
  const res = await call(person, `/stokvels/${stokvelId}/invites`, { method: 'POST' });
  if (res.status !== 201) throw new Error(`create invite: ${res.status} ${await res.text()}`);
  return ((await res.json()) as { code: string }).code;
}