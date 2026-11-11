import { expect, test as base, type Locator } from '@playwright/test';
import { authFile, type Person } from './people';

// test.use({ person: 'sipho' }) = start signed in as Sipho. null = signed out.
export const test = base.extend<{ person: Person | null }>({
  person: [null, { option: true }],
  storageState: async ({ person }, use) => {
    await use(person ? authFile(person) : { cookies: [], origins: [] });
  },
  context: async ({ context, person }, use) => {
    await use(context);
    if (person) await context.storageState({ path: authFile(person) }); // save again: the proxy may have rotated the token
  },
});
export { expect };

// "R 1 500,50" inside some text -> 150050 (integer cents, no floats)
export async function moneyCents(locator: Locator): Promise<number> {
  const text = (await locator.textContent()) ?? '';
  const m = /R[\s\u00a0]*([\d\s\u00a0]*\d),(\d\d)/.exec(text);
  if (!m) throw new Error(`No amount in: ${text}`);
  return Number(m[1].replace(/\D/g, '')) * 100 + Number(m[2]);
}