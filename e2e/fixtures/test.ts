// e2e/fixtures/test.ts
import { test as base, expect, mergeTests } from '@playwright/test';
import { test as wpTest } from './wp';
import { test as blazorTest } from './common';
import { test as wpLoginTest } from './wp-login';
import { test as wpContentTest } from './wp-content'; // 👈 add this line

export const test = mergeTests(base, wpTest, blazorTest, wpLoginTest, wpContentTest); // 👈 include it here
export { expect };
