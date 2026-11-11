import { createCycle } from './api';
import { expect, moneyCents, test } from './fixtures';
import { FAMILY } from './people';

test.use({ person: 'thandi' });

test('record R 150,50 and the stokvel total goes up by exactly R 150,50, without a reload', async ({ page }) => {
  await createCycle('sipho', FAMILY); // this test's own cycle (Sipho is the treasurer)
  await page.goto(`/stokvels/${FAMILY}`);

  const collected = page.getByText(/^Collected:/);
  await expect(collected).toBeVisible();
  const before = await moneyCents(collected); // read first, never assume

  await page.getByRole('textbox', { name: 'Amount (rands)' }).fill('150,50');
  await page.getByRole('button', { name: 'Record contribution' }).click();

  await expect(page.getByText('Recorded', { exact: false }).first()).toBeVisible();
  await expect.poll(() => moneyCents(collected)).toBe(before + 15_050); // exact difference in cents
});