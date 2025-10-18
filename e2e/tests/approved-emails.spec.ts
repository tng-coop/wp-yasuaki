// // e2e/tests/approved-emails.spec.ts
// import { test, expect } from '../fixtures';

// test.describe('Approved Emails admin screen', () => {
//   test('admin can add (and remove) an approved email', async ({ page, loginAsAdmin, uniq }) => {
//     // Log in with your helper
//     await loginAsAdmin();

//     // Go to Users → Approved Emails (slug registered by the MU plugin)
//     await page.goto('/wp-admin/users.php?page=ael-admin-ui');
//     await expect(page.getByRole('heading', { level: 1 })).toHaveText('Approved Emails');

//     const email = `${uniq('e2e_approved_')}@example.test`;

//     // --- Add email (targets the "Add Email" form) ---
//     const addForm = page.locator('form:has(input[name="email"])');
//     await addForm.locator('input[name="email"]').fill(email);
//     await addForm.getByRole('button', { name: /^Add$/ }).click();

//     // Success notice + row present in the table
//     const notice = page.locator('.notice-success, .notice-warning, .notice-error').first();
//     await expect(notice).toBeVisible();
//     await expect(notice).toContainText('Added');
//     const tableForm = page.locator('form:has(table.widefat)');
//     await expect(tableForm.locator(`input[name="remove[]"][value="${email}"]`)).toBeVisible();

//     // --- Cleanup so the test is repeatable (remove the email) ---
//     await tableForm.locator(`input[name="remove[]"][value="${email}"]`).check();
//     await tableForm.getByRole('button', { name: /^Apply$/ }).click();
//     await expect(page.locator('.notice-success')).toContainText('Removed');
//     await expect(tableForm.locator(`input[name="remove[]"][value="${email}"]`)).toHaveCount(0);
//   });
// });
