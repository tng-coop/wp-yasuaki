// e2e/fixtures/test.ts
import { test as base, expect, mergeTests } from '@playwright/test';
import { test as wpTest } from './wp';
import { test as blazorTest } from './common';

// Merge order doesn’t matter; scopes are preserved.
export const test = mergeTests(base, wpTest, blazorTest);
export { expect };
