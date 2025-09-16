import { test, expect } from '../fixtures/test';

test.describe('WordPress REST API with App Password', () => {
  test('GET /users/me returns current user when authed with app password', async ({ wpApi }) => {
    // ✅ use wpApi fixture (pre-auth’d API context)
    const resp = await wpApi.get('/wp-json/wp/v2/users/me', {
      failOnStatusCode: false,
    });

    expect(resp.status()).toBe(200);

    const body = await resp.json();
    expect(body).toHaveProperty('id');
    expect(body).toHaveProperty('name');
  });
});
