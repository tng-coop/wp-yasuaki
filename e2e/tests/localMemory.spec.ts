import { test, expect } from '../fixtures/test';

test.describe('ILocalStore /test-memory page', () => {
  test('should add multiple items and list them', async ({ page, blazorURL }, testInfo) => {

    await page.goto(blazorURL+'test-memory');

    // Start clean
    await page.getByTestId('btn-list').click();
    await expect(page.getByRole('status')).toHaveText(/Listed 0 items/);

    // Add first item
    await page.getByTestId('title-input').fill('First Item');
    await page.getByTestId('btn-add').click();
    await expect(page.getByRole('status')).toHaveText(/Added draft:/);

    // List should show the new item
    await page.getByTestId('btn-list').click();
    await expect(page.getByRole('status')).toHaveText(/Listed 1 items/);
    await expect(page.getByTestId('draft-list')).toContainText('First Item');

    // Add another item
    await page.getByTestId('title-input').fill('Second Item');
    await page.getByTestId('btn-add').click();
    await expect(page.getByRole('status')).toHaveText(/Added draft:/);

    // List should show both items
    await page.getByTestId('btn-list').click();
    await expect(page.getByRole('status')).toHaveText(/Listed 2 items/);
    await expect(page.getByTestId('draft-list')).toContainText('First Item');
    await expect(page.getByTestId('draft-list')).toContainText('Second Item');
  });

  test('should upsert (put) without creating duplicates', async ({ page, blazorURL }) => {
    await page.goto(blazorURL+'test-memory');

    // Put first version of draft:1
    await page.getByTestId('title-input').fill('Initial Title');
    await page.getByTestId('btn-put').click();
    await expect(page.getByRole('status')).toHaveText('Put/Upserted!');

    await page.getByTestId('btn-list').click();
    await expect(page.getByRole('status')).toHaveText(/Listed \d+ items/);
    await expect(page.getByTestId('draft-list')).toContainText('Initial Title');

    // Put again with same key but different title
    await page.getByTestId('title-input').fill('Updated Title');
    await page.getByTestId('btn-put').click();
    await expect(page.getByRole('status')).toHaveText('Put/Upserted!');

    // List should still show one item, but with the updated title
    await page.getByTestId('btn-list').click();
    await expect(page.getByRole('status')).toHaveText(/Listed 1 items/);
    await expect(page.getByTestId('draft-list')).toContainText('Updated Title');
    await expect(page.getByTestId('draft-list')).not.toContainText('Initial Title');
  });

  test('should retrieve and delete items by key', async ({ page, blazorURL }) => {
    await page.goto(blazorURL+'test-memory');

    // Seed draft:1
    await page.getByTestId('title-input').fill('Temp Item');
    await page.getByTestId('btn-put').click();
    await expect(page.getByRole('status')).toHaveText('Put/Upserted!');

    await page.getByTestId('btn-list').click();
    await expect(page.getByRole('status')).toHaveText(/Listed \d+ items/);
    await expect(page.getByTestId('draft-list')).toContainText('Temp Item');

    // Get by id should show correct title
    await page.getByTestId('btn-get').click();
    await expect(page.getByRole('status')).toHaveText('Temp Item');

    // Delete the item
    await page.getByTestId('btn-delete').click();
    await expect(page.getByRole('status')).toHaveText('Deleted!');

    // Now retrieval should fail
    await page.getByTestId('btn-get').click();
    await expect(page.getByRole('status')).toHaveText('Not found');

    // List should no longer contain the deleted item
    await page.getByTestId('btn-list').click();
    await expect(page.getByRole('status')).toHaveText(/Listed 0 items/);
    await expect(page.getByTestId('draft-list')).not.toContainText('Temp Item');
  });
});
