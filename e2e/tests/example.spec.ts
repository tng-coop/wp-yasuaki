import { test, expect } from '../fixtures/test';

test('has title', async ({ page }) => {
  await page.goto('/');
  await expect(page).toHaveTitle(/wptest/);
});