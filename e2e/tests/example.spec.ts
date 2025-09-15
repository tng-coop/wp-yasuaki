import { test, expect } from '../fixtures/test'

test('has title', async ({ page }) => {
  await page.goto('/');

  // Expect a title "to contain" a substring.
  await expect(page).toHaveTitle(/wptest/);
});
test('has title2', async ({ page, blazorURL }, testInfo ) => {

  await page.goto(blazorURL);

  console.log('Navigated to:', page.url());

  await expect(page).toHaveTitle(/Home/);
});

test('weather', async ({ page }, testInfo) => {
  const pageErrors: Error[] = [];
  const consoleErrors: string[] = [];

  page.on('pageerror', (err) => pageErrors.push(err));
  page.on('console', (msg) => {
    if (msg.type() === 'error') {
      consoleErrors.push(msg.text());
    }
  });
  const { blazorURL } = testInfo.project.use as any;
  await page.goto(`${blazorURL}weather`);
  await expect(page.getByText('This component demonstrates')).toHaveCount(1);

  // Assert nothing unexpected was logged
  expect(pageErrors, `Page errors: ${pageErrors.map(e => e.message).join('\n')}`).toHaveLength(0);
  expect(consoleErrors, `Console errors: ${consoleErrors.join('\n')}`).toHaveLength(0);
});

