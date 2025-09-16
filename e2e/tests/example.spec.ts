import { test, expect } from '../fixtures/test';

test('has title', async ({ page }) => {
  await page.goto('/');
  await expect(page).toHaveTitle(/wptest/);
});

test('has title2', async ({ page, blazorURL }) => {
  await page.goto(blazorURL);
  await expect(page).toHaveTitle(/Home/);
});

test('weather', async ({ page, blazorURL }) => {
  const pageErrors: Error[] = [];
  const consoleErrors: string[] = [];

  page.on('pageerror', (err) => pageErrors.push(err));
  page.on('console', (msg) => {
    if (msg.type() === 'error') consoleErrors.push(msg.text());
  });

  await page.goto(`${blazorURL}weather`);
  await expect(page.getByText('This component demonstrates')).toHaveCount(1);

  expect(pageErrors, `Page errors: ${pageErrors.map(e => e.message).join('\n')}`).toHaveLength(0);
  expect(consoleErrors, `Console errors: ${consoleErrors.join('\n')}`).toHaveLength(0);
});
