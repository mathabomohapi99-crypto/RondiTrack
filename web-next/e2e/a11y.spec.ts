import AxeBuilder from '@axe-core/playwright';
import type { Page } from '@playwright/test';
import { createCycle } from './api';
import { expect, test } from './fixtures';
import { FAMILY } from './people';

// If a rule must be switched off, do it HERE and nowhere else, with the reason. Empty on purpose.
const DISABLED_RULES: string[] = [];

async function scan(page: Page) {
  const results = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'])
    .disableRules(DISABLED_RULES)
    .analyze();
  const bad = results.violations.filter((v) => v.impact === 'serious' || v.impact === 'critical');
  expect(
    bad.map((v) => ({ rule: v.id, impact: v.impact, where: v.nodes.map((n) => n.target) })),
    'serious or critical accessibility violations',
  ).toEqual([]);
}

test.use({ person: 'sipho' });

test('home screen', async ({ page }) => {
  await page.goto('/dashboard/member');
  await expect(page.getByRole('heading', { name: 'E2E Family Stokvel' })).toBeVisible();
  await scan(page);
});

test('a stokvel', async ({ page }) => {
  await createCycle('sipho', FAMILY);
  await page.goto(`/stokvels/${FAMILY}`);
  await expect(page.getByRole('heading', { level: 1, name: 'E2E Family Stokvel' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Proof of payment' })).toBeVisible();
  await scan(page);
});

test('the contribution form showing its errors', async ({ page }) => {
  await createCycle('sipho', FAMILY);
  await page.goto(`/stokvels/${FAMILY}`);
  const amount = page.getByRole('textbox', { name: 'Amount (rands)' });
  await amount.fill('abc');
  await page.getByRole('button', { name: 'Record contribution' }).click();
  await expect(page.getByText(/Enter a valid amount/)).toBeVisible();
  await scan(page);
});

test('the open Close Cycle dialog', async ({ page }) => {
  await createCycle('sipho', FAMILY);
  await page.goto(`/stokvels/${FAMILY}`);
  await page.getByRole('button', { name: /^Close Cycle \d+…$/ }).click();
  const dialog = page.getByRole('alertdialog');
  await expect(dialog).toBeVisible();
  await expect(dialog.getByRole('button', { name: /in arrears|nobody is in arrears/ })).toBeEnabled(); // preview loaded
  await scan(page);
});