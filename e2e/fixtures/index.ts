// e2e/fixtures/index.ts
import { test as base, expect, mergeTests } from '@playwright/test';
import { test as common } from './common';
import { test as wp } from './wp';
import { test as wpLogin } from './wp-login';

export const test = mergeTests(base, common, wp, wpLogin);
export { expect };
