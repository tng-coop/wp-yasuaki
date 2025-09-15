import { test, expect } from '@playwright/test';

test.describe('WordPress REST API with App Password', () => {
  test('GET /users/me returns current user when authed with app password', async ({ request }, testInfo) => {
    // read directly from playwright.config.ts `use`
    const { wpUser, wpAppPwd } = testInfo.project.use as any;

    const token = Buffer.from(`${wpUser}:${wpAppPwd}`).toString('base64');

    const resp = await request.get('/wp-json/wp/v2/users/me', {
      headers: { Authorization: `Basic ${token}` },
      failOnStatusCode: false,
    });

    expect(resp.status()).toBe(200);

    const body = await resp.json();
    expect(body).toHaveProperty('id');
    expect(body).toHaveProperty('name');
  });
});
