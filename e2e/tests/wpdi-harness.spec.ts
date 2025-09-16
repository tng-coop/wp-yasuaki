// import { test, expect } from '../fixtures/test';

// test.describe('WPDI Harness page', () => {
//   test.beforeEach(async ({ page, blazorURL }) => {
//     await page.goto(new URL('wpdi-harness', blazorURL).toString());
//     await expect(page.getByTestId('wpdi-harness')).toBeVisible();
//   });

//   test('create, update, change status, delete', async ({ page }) => {
//     // 1. Create draft
//     await page.getByTestId('title-input').fill('Harness Post');
//     await page.getByTestId('btn-create').click();
//     await expect(page.getByTestId('status')).toHaveText(/Draft created/);

//     // 2. List
//     await page.getByTestId('btn-list').click();
//     await expect(page.getByTestId('post-table')).toContainText('Harness Post');

//     // Capture Id for further actions
//     const idCell = page.locator('[data-testid="post-table"] tr').first().locator('td').first();
//     const createdId = parseInt(await idCell.innerText(), 10);
//     expect(createdId).toBeGreaterThan(0);

//     // 3. Update content
//     await page.getByTestId('btn-update-content').click();
//     await expect(page.getByTestId('status')).toHaveText(/Content updated/);

//     // 4. Submit for review
//     await page.getByTestId('btn-submit').click();
//     await expect(page.getByTestId('status')).toHaveText(/Status → pending/);

//     // 5. Retract back to draft
//     await page.getByTestId('btn-retract').click();
//     await expect(page.getByTestId('status')).toHaveText(/Status → draft/);

//     // 6. Publish
//     await page.getByTestId('btn-publish').click();
//     await expect(page.getByTestId('status')).toHaveText(/Status → publish/);

//     // 7. Delete
//     await page.getByTestId('btn-delete').click();
//     await expect(page.getByTestId('status')).toHaveText(/Deleted/);

//     // Confirm deletion (list should not include original title anymore)
//     await page.getByTestId('btn-list').click();
//     await expect(page.getByTestId('post-table')).not.toContainText('Harness Post');
//   });
// });
