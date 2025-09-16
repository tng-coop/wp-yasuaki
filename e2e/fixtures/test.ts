// e2e/fixtures/test.ts
import { test as base, expect, mergeTests } from '@playwright/test';
import { test as wpTest } from './wp';
import { test as blazorTest } from './common';
import { test as wpLoginTest } from './wp-login';   // 👈 make sure this path is correct

export const test = mergeTests(base, wpTest, blazorTest, wpLoginTest);
export { expect };
