import { test, expect, createCommunity, signIn, unique } from './helpers.js';

test('catalog lists, pages, searches and filters points', async ({ page }) => {
  await page.goto('/app/');
  const rows = page.locator('.rows li');
  await expect(rows).toHaveCount(10);
  await expect(page.getByRole('button', { name: 'Попередні' })).toBeDisabled();

  await page.getByRole('button', { name: 'Наступні' }).click();
  await expect(page.locator('.pager')).toContainText('11–');
  await expect(page).toHaveURL(/skip=10/);

  await page.getByLabel('Пошук пунктів').fill('Салтівка');
  await page.getByRole('button', { name: 'Знайти' }).click();
  await expect(rows).toHaveCount(1);

  await page.getByLabel('Пошук пунктів').fill('');
  await page.getByRole('button', { name: 'Знайти' }).click();
  await page.getByLabel('Статус').selectOption('Closed');
  await expect(rows.first()).toContainText('Не працює');
  await page.getByLabel('Пошук пунктів').fill('неіснуючийзапитжжж');
  await page.getByRole('button', { name: 'Знайти' }).click();
  await expect(page.getByText('Таких пунктів немає')).toBeVisible();
});

test('point page opens by link and back keeps the search', async ({ page }) => {
  await page.goto('/app/#/?q=Салтівка');
  await page.locator('.rows a').first().click();
  await expect(page).toHaveURL(/#\/points\/\d+/);
  await expect(page.getByRole('heading', { level: 1 })).toContainText('Салтівка');
  await expect(page.locator('.report').first()).toBeVisible();
  await page.goBack();
  await expect(page.getByLabel('Пошук пунктів')).toHaveValue('Салтівка');
});

test('unknown point shows a clear error', async ({ page }) => {
  await page.goto('/app/#/points/987654');
  await expect(page.getByRole('alert')).toContainText('не знайдено');
});

test('report needs a valid token and appears after sending', async ({ page }) => {
  await signIn(page);
  await createCommunity(page);
  await page.goto('/Account/Profile');
  await page.getByRole('button', { name: 'Створити токен' }).click();
  const token = await page.locator('#apiToken').inputValue();

  const api = page.request;
  const auth = { Authorization: `Bearer ${token}` };
  const operator = await (await api.post('/api/operators', { headers: auth, data: { name: unique('Оператор'), phone: '+380501112233' } })).json();
  const point = await (await api.post('/api/points', {
    headers: auth,
    data: { name: unique('Пункт SPA'), address: 'вул. Тестова, 1', settlement: 'Тестове', latitude: 50.4, longitude: 30.5, capacity: 20, operatorId: operator.id }
  })).json();

  await page.goto(`/app/#/points/${point.id}`);
  await page.getByLabel('API-токен').fill('wrong-token');
  await page.getByRole('button', { name: 'Додати звіт' }).click();
  await expect(page.locator('.report-form .error')).toBeVisible();

  await page.getByLabel('API-токен').fill(token);
  await page.getByLabel('Статус').selectOption('Limited');
  await page.getByLabel('Людей у пункті').fill('7');
  await page.getByLabel('Коментар').fill('Із застосунку React');
  await page.getByRole('button', { name: 'Додати звіт' }).click();
  await expect(page.locator('.report-form .success')).toHaveText('Звіт додано.');
  await expect(page.locator('.report').first()).toContainText('Із застосунку React');
  await expect(page.locator('main > .lamp')).toHaveText('З обмеженнями');
});
