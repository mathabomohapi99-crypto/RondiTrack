import { createCycle } from './api';
import { expect, test } from './fixtures';
import { FAMILY, WORK } from './people';

test.describe('Sipho: treasurer of Family, only a member of Work', () => {
  test.use({ person: 'sipho' });

  test('in Work the treasurer action is refused with a 403, shown as a message, and he stays signed in', async ({ page }) => {
    const cycleId = await createCycle('kagiso', WORK); // Kagiso is Work's treasurer
    await page.goto(`/stokvels/${WORK}`);
    await expect(page.getByRole('heading', { name: 'E2E Work Stokvel' })).toBeVisible();
    await expect(page.getByRole('button', { name: /^Close Cycle/ })).toHaveCount(0); // the UI does not offer it

    // The UI is not the rule: send the request anyway. The API answers 403 and the route passes it on.
    const res = await page.request.get(`/bff/close-preview?stokvelId=${WORK}&cycleId=${cycleId}`);
    expect(res.status()).toBe(403);

    await page.goto(`/stokvels/${WORK}`); // still signed in: no redirect to /login
    await expect(page).toHaveURL(new RegExp(`/stokvels/${WORK}$`));
    await expect(page.getByRole('heading', { name: 'E2E Work Stokvel' })).toBeVisible();
  });

  test('in Family, where he is treasurer, the Close Cycle button is there', async ({ page }) => {
    await createCycle('sipho', FAMILY);
    await page.goto(`/stokvels/${FAMILY}`);
    await expect(page.getByRole('button', { name: /^Close Cycle \d+…$/ })).toBeVisible();
  });
});

test.describe('signed out', () => {
  test.use({ person: null });

  test('a signed-in page redirects to sign-in with the way back', async ({ page }) => {
    await page.goto('/stokvels');
    await expect(page).toHaveURL(/\/login\?next=%2Fstokvels/);
  });

  test('a /bff/ fetch gets a JSON 401, not HTML', async ({ request }) => {
    const res = await request.get('/bff/activity');
    expect(res.status()).toBe(401);
    expect(res.headers()['content-type']).toContain('application/json');
    expect(await res.json()).toEqual({ error: 'unauthorized' });
  });
});