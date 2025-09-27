import { test, expect } from '../fixtures/test';

// ---- Single-tab guards ----
test.beforeEach(async ({ page, context }) => {
    // If any popup slips through, fail fast (or close it if you prefer).
    context.on('page', p => {
        throw new Error(`Unexpected popup detected: ${p.url()}`);
    });

    // Force window.open(url, ...) to reuse the same tab.
    await page.addInitScript(() => {
        // @ts-ignore
        window.open = (url: string) => {
            location.href = url;
            return window;
        };
    });
});

// ---- Helpers ----
async function runQuickCheck(page: any) {
    await page.getByTestId('wpdi-run').click();
    await expect(page.getByTestId('wpdi-status')).toHaveText(/OK/, { timeout: 15_000 });
}

async function gotoWpMe(page: any, blazorURL: string, auth: 'App Password' | 'nonce') {
    await page.goto(new URL(`wp-me?auth=${auth}`, blazorURL).toString());
    const ok = page.getByTestId('wp-me-ok');
    await expect(ok).toBeVisible({ timeout: 20_000 });
    await expect(ok).toContainText('id:');
    await expect(ok).toContainText('name:');
}

test.describe('WPDI continues to work across auth switches (single-tab)', () => {
    test('App Password → Nonce → App Password; QuickCheck + /wp-me OK after each switch', async ({
        page,
        blazorURL,
        baseURL,
        wpUser,
        wpAppPwd,
        loginAsAdmin,
    }) => {
        const appflags = new URL('appflags', blazorURL);
        if (baseURL) appflags.searchParams.set('wpurl', baseURL);

        // 1) App Password with valid creds
        await page.goto(appflags.toString());
        await page.getByTestId('authlab-pass').click();
        await expect(page.getByTestId('state-auth')).toHaveText('App Password');

        await page.getByTestId('authlab-user').fill(wpUser);
        await page.getByTestId('authlab-pass').fill(wpAppPwd);
        await page.getByTestId('authlab-save-valid').click();

        await runQuickCheck(page);
        await gotoWpMe(page, blazorURL, 'App Password');            // navigate same tab
        await page.goto(appflags.toString());                  // back to flags, same tab

        // 2) Switch to Nonce → Unauthorized, then login (server cookies), then OK
        await page.getByTestId('auth-nonce').click();
        await expect(page.getByTestId('state-auth')).toHaveText('Nonce');

        await page.getByTestId('wpdi-run').click();
        await expect(page.getByTestId('wpdi-status')).toHaveText(/Unauthorized|HTTP (401|403)/);

        await loginAsAdmin();                                  // programmatic login; still same tab
        await page.goto(appflags.toString());
        await expect(page.getByTestId('state-auth')).toHaveText('Nonce');

        await runQuickCheck(page);
        await gotoWpMe(page, blazorURL, 'nonce');
        await page.goto(appflags.toString());

        // 3) Back to App Password (cookies must be ignored) → OK
        await page.getByTestId('authlab-pass').click();
        await expect(page.getByTestId('state-auth')).toHaveText('App Password');

        await runQuickCheck(page);
        await gotoWpMe(page, blazorURL, 'App Password');
    });

    test('Rapid flips don’t stale WordPressApiService client', async ({
        page, blazorURL, baseURL, wpUser, wpAppPwd, loginAsAdmin,
    }) => {
        const appflags = new URL('appflags', blazorURL);
        if (baseURL) appflags.searchParams.set('wpurl', baseURL);
        await page.goto(appflags.toString());

        // Seed valid App Password so App Password path is OK
        await page.getByTestId('authlab-pass').click();
        await page.getByTestId('authlab-user').fill(wpUser);
        await page.getByTestId('authlab-pass').fill(wpAppPwd);
        await page.getByTestId('authlab-save-valid').click();

        // 🔑 Ensure nonce path can succeed when we flip to it
        await loginAsAdmin();                     // establishes WP cookies on WP origin
        await page.goto(appflags.toString());     // back to appflags on same tab

        // Flip auth repeatedly; Quick Check must remain OK each time
        for (const target of ['nonce', 'App Password', 'nonce', 'App Password'] as const) {
            await page.getByTestId(`auth-${target}`).click();
            await expect(page.getByTestId('state-auth'))
                .toHaveText(target === 'nonce' ? 'Nonce' : 'App Password');
            await page.getByTestId('wpdi-run').click();
            await expect(page.getByTestId('wpdi-status')).toHaveText(/OK/);
        }
    });

});
