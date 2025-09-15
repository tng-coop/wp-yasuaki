// e2e/fixtures/common.ts
import { test as base, expect } from '@playwright/test';

type TestScoped = {
  /** Fully-qualified origin for the Blazor app, e.g. "http://localhost:5173/" */
  blazorURL: string;
};

type WorkerScoped = {
  /** WordPress username from project.use (NOT read in tests from process.env) */
  wpUser: string;
  /** WordPress App Password from project.use */
  wpAppPwd: string;
};

export const test = base.extend<TestScoped, WorkerScoped>({
  // ---------- test-scoped fixtures ----------
  blazorURL: [async ({}, use, testInfo) => {
    const { blazorURL } = testInfo.project.use as { blazorURL?: string };
    if (!blazorURL) throw new Error('blazorURL must be set in project.use');
    // normalize with exactly one trailing slash
    await use(blazorURL.replace(/\/+$/, '') + '/');
  }, { scope: 'test' }],

  // ---------- worker-scoped fixtures ----------
  wpUser: [async ({}, use, workerInfo) => {
    const { wpUser } = workerInfo.project.use as { wpUser?: string };
    if (!wpUser) throw new Error('wpUser must be set in project.use');
    await use(wpUser);
  }, { scope: 'worker' }],

  wpAppPwd: [async ({}, use, workerInfo) => {
    const { wpAppPwd } = workerInfo.project.use as { wpAppPwd?: string };
    if (!wpAppPwd) throw new Error('wpAppPwd must be set in project.use');
    await use(wpAppPwd);
  }, { scope: 'worker' }],
});

export { expect };
