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

async function gotoWpMe(page: any, blazorURL: string, auth: 'apppass' | 'nonce') {
    await page.goto(new URL(`wp-me?auth=${auth}`, blazorURL).toString());
    const ok = page.getByTestId('wp-me-ok');
    await expect(ok).toBeVisible({ timeout: 20_000 });
    await expect(ok).toContainText('id:');
    await expect(ok).toContainText('name:');
}

test.describe('WPDI continues to work across auth switches (single-tab)', () => {
    test('AppPass → Nonce → AppPass; QuickCheck + /wp-me OK after each switch', async ({
        page,
        blazorURL,
        baseURL,
        wpUser,
        wpAppPwd,
        loginAsAdmin,
    }) => {
        const appflags = new URL('appflags', blazorURL);
        if (baseURL) appflags.searchParams.set('wpurl', baseURL);

        // 1) AppPass with valid creds
        await page.goto(appflags.toString());
        await page.getByTestId('auth-apppass').click();
        await expect(page.getByTestId('state-auth')).toHaveText('AppPass');

        await page.getByTestId('authlab-user').fill(wpUser);
        await page.getByTestId('authlab-pass').fill(wpAppPwd);
        await page.getByTestId('authlab-save-valid').click();

        await runQuickCheck(page);
        await gotoWpMe(page, blazorURL, 'apppass');            // navigate same tab
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

        // 3) Back to AppPass (cookies must be ignored) → OK
        await page.getByTestId('auth-apppass').click();
        await expect(page.getByTestId('state-auth')).toHaveText('AppPass');

        await runQuickCheck(page);
        await gotoWpMe(page, blazorURL, 'apppass');
    });

    test('Rapid flips don’t stale WordPressApiService client', async ({
        page, blazorURL, baseURL, wpUser, wpAppPwd, loginAsAdmin,
    }) => {
        const appflags = new URL('appflags', blazorURL);
        if (baseURL) appflags.searchParams.set('wpurl', baseURL);
        await page.goto(appflags.toString());

        // Seed valid AppPass so AppPass path is OK
        await page.getByTestId('auth-apppass').click();
        await page.getByTestId('authlab-user').fill(wpUser);
        await page.getByTestId('authlab-pass').fill(wpAppPwd);
        await page.getByTestId('authlab-save-valid').click();

        // 🔑 Ensure nonce path can succeed when we flip to it
        await loginAsAdmin();                     // establishes WP cookies on WP origin
        await page.goto(appflags.toString());     // back to appflags on same tab

        // Flip auth repeatedly; Quick Check must remain OK each time
        for (const target of ['nonce', 'apppass', 'nonce', 'apppass'] as const) {
            await page.getByTestId(`auth-${target}`).click();
            await expect(page.getByTestId('state-auth'))
                .toHaveText(target === 'nonce' ? 'Nonce' : 'AppPass');
            await page.getByTestId('wpdi-run').click();
            await expect(page.getByTestId('wpdi-status')).toHaveText(/OK/);
        }
    });

});
