import { writeFileSync } from 'node:fs';
import { randomUUID } from 'node:crypto';
import { test as base, expect } from '@playwright/test';
import { COVERAGE_DIR } from '../coverage-setup.js';

const isOwnScript = (url) => /^http:\/\/localhost:\d+\/(js\/[^/]+\.js|app\/assets\/.+\.js)/.test(url);

async function withCoverage(page, use) {
  await page.coverage.startJSCoverage({ resetOnNavigation: false });
  await use(page);
  const entries = (await page.coverage.stopJSCoverage()).filter((e) => isOwnScript(e.url) && e.source);
  writeFileSync(new URL(`${randomUUID()}.json`, COVERAGE_DIR), JSON.stringify(entries));
}

export const test = base.extend({
  page: async ({ page }, use) => withCoverage(page, use),
  secondPage: async ({ browser }, use) => {
    const context = await browser.newContext({ locale: 'uk-UA' });
    const page = await context.newPage();
    await withCoverage(page, use);
    await context.close();
  }
});

export { expect };

export const unique = (prefix) => `${prefix} ${randomUUID().slice(0, 8)}`;

export async function signIn(page, email = `${randomUUID()}@e2e.local`, name = 'Тестовий Оператор') {
  await page.goto('/Account/Login');
  const form = page.locator('form.dev-login');
  await form.getByLabel('Email').fill(email);
  await form.getByLabel("Ім'я").fill(name);
  await form.getByRole('button', { name: 'Увійти як розробник' }).click();
  return email;
}

export async function createCommunity(page, name = unique('Громада')) {
  await page.getByLabel('Назва громади').fill(name);
  await page.getByRole('button', { name: 'Створити громаду' }).click();
  await expect(page.getByRole('heading', { name, level: 1 })).toBeVisible();
  return name;
}

export async function inviteCode(page) {
  await page.goto('/Community');
  return (await page.locator('#inviteCode').textContent()).trim();
}

export async function join(page, code) {
  await page.getByLabel('Код запрошення').fill(code);
  await page.getByRole('button', { name: 'Приєднатися' }).click();
  await expect(page).toHaveURL(/\/Community$/);
}

export async function enablePremium(page) {
  await page.goto('/Community/Premium');
  await page.getByRole('button', { name: 'Увімкнути Premium' }).click();
  await expect(page.getByText('Premium увімкнено')).toBeVisible();
}

export async function pickAutocomplete(page, fieldId, term, optionText) {
  await page.locator(`#select2-${fieldId}-container`).click();
  await page.locator('.select2-container--open .select2-search__field').fill(term);
  await page.locator('.select2-results__option', { hasText: optionText }).first().click();
}
