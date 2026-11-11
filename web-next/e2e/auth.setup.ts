import { expect, test as setup } from '@playwright/test';
import { EMAIL, PEOPLE, authFile } from './people';

for (const person of PEOPLE) {
  setup(`sign in ${person}`, async ({ page }) => {
    await page.goto('/login');
    await page.getByLabel('Email').fill(EMAIL[person]);
    await page.getByLabel('Password').fill(process.env.E2E_PASSWORD!);
    await page.getByRole('button', { name: 'Sign in' }).click();
    await expect(page).toHaveURL(/\/dashboard\/member/);
    await page.context().storageState({ path: authFile(person) });
  });
}