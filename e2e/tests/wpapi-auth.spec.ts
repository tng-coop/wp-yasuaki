// e2e/tests/wpapi-auth.spec.ts
import { test, expect } from '../fixtures';

test('wpApi authenticates with application password', async ({ wpApi }) => {
  const res = await wpApi.get('users/me', { failOnStatusCode: false });
  const body = await res.text();
  expect(res.status(), `Body:\n${body.slice(0, 300)}`).toBe(200);
  expect(res.headers()['content-type'] || '').toContain('application/json');
  const me = JSON.parse(body);
  expect(me?.id).toBeTruthy();
});
