import { test, expect } from '../fixtures/test';

test.describe('ILocalStore /test-memory advanced scenarios', () => {
  test('should add two items concurrently from a single race button', async ({ page, blazorURL }) => {
    await page.goto(blazorURL+'test-memory');

    // Trigger internal race
    await page.getByTestId('btn-add-race').click();

    // List and assert both payloads landed
    await page.getByTestId('btn-list').click();
    await expect(page.getByTestId('draft-list')).toContainText('Quick Alpha');
    await expect(page.getByTestId('draft-list')).toContainText('Quick Beta');
  });

  test('status text should update consistently after Put and Delete', async ({ page, blazorURL }) => {
    await page.goto(blazorURL+'test-memory');

    await page.getByTestId('title-input').fill('CountMe');
    await page.getByTestId('btn-put').click();
    await expect(page.getByTestId('status')).toHaveText('Put/Upserted!');

    await page.getByTestId('btn-list').click();
    await expect(page.getByTestId('status')).toHaveText(/Listed 1 items/);

    await page.getByTestId('btn-delete').click();
    await expect(page.getByTestId('status')).toHaveText('Deleted!');

    await page.getByTestId('btn-list').click();
    await expect(page.getByTestId('status')).toHaveText(/Listed 0 items/);
  });

  test('a11y sanity: all buttons and status are discoverable by test id', async ({ page, blazorURL }) => {
    await page.goto(blazorURL+'test-memory');

    await page.getByTestId('btn-list').click();
    await expect(page.getByTestId('status')).toHaveText(/Listed \d+ items/);

    // Check each button by test id
    await expect(page.getByTestId('btn-add')).toBeVisible();
    await expect(page.getByTestId('btn-put')).toBeVisible();
    await expect(page.getByTestId('btn-get')).toBeVisible();
    await expect(page.getByTestId('btn-list')).toBeVisible();
    await expect(page.getByTestId('btn-delete')).toBeVisible();
    await expect(page.getByTestId('btn-add-race')).toBeVisible();
  });

  test('flake guard: List status should change after Add', async ({ page, blazorURL }) => {
    await page.goto(blazorURL+'test-memory');

    await page.getByTestId('btn-list').click();
    const before = await page.getByTestId('status').textContent();

    await page.getByTestId('title-input').fill('Flaky Item');
    await page.getByTestId('btn-add').click();
    await page.getByTestId('btn-list').click();

    const after = await page.getByTestId('status').textContent();
    expect(after).not.toBe(before);
  });
});
