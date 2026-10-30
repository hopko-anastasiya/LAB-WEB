import { test, expect } from './helpers.js';

test('home shows the headline and leads to the map', async ({ page }) => {
  await page.goto('/');
  await expect(page.getByRole('heading', { name: 'Де зараз є світло' })).toBeVisible();
  await expect(page.locator('.win.lit').first()).toBeVisible();
  await page.getByRole('link', { name: 'Мапа', exact: true }).click();
  await expect(page).toHaveURL(/\/Map$/);
});

test('map shows points, filters by status, service and search', async ({ page }) => {
  await page.goto('/Map');
  await expect(page.locator('#mapLive')).toContainText('наживо');
  const rows = page.locator('.map-row');
  await expect(rows.first()).toBeVisible();
  const before = await rows.count();
  expect(await page.locator('.map-lamp').count()).toBeGreaterThan(5);

  await page.getByRole('button', { name: 'Не працює' }).click();
  await expect.poll(() => rows.count()).toBeGreaterThanOrEqual(before);

  await page.getByRole('button', { name: 'Генератор' }).click();
  await expect.poll(() => rows.count()).toBeLessThan(before + 10);
  await page.getByRole('button', { name: 'Генератор' }).click();

  await page.getByLabel('Пошук пунктів').fill('Салтівка');
  await expect(rows).toHaveCount(1);
  await rows.first().click();
  await expect(page.locator('.map-pop h2')).toContainText('Салтівка');
  await page.getByRole('link', { name: 'Відкрити пункт' }).click();
  await expect(page).toHaveURL(/\/punkt\/\d+$/);
});

test('point landing page keeps contacts on screen while scrolling', async ({ page }) => {
  await page.goto('/punkt/1');
  await expect(page.locator('.landing-status')).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Що є зараз' })).toBeVisible();
  const contact = page.locator('.point-contact');
  await expect(contact.getByRole('link', { name: 'Маршрут' })).toHaveAttribute('href', /google\.com\/maps\/dir/);
  await page.evaluate(() => window.scrollTo(0, document.body.scrollHeight));
  await expect(contact).toBeInViewport();
  await expect(page.locator('#occupancyChart')).toBeVisible();
});

test('point page and crawler files are ready for search engines', async ({ page, request }) => {
  await page.goto('/punkt/1');
  await expect(page).toHaveTitle(/пункт незламності/);
  await expect(page.locator('meta[name=description]')).toHaveAttribute('content', /.{20,}/);
  await expect(page.locator('link[rel=canonical]')).toHaveAttribute('href', /\/punkt\/1$/);
  const data = JSON.parse(await page.locator('script[type="application/ld+json"]').textContent());
  expect(data['@type']).toBe('CivicStructure');
  expect(await page.locator('h1').count()).toBe(1);

  expect(await (await request.get('/robots.txt')).text()).toContain('sitemap.xml');
  expect(await (await request.get('/sitemap.xml')).text()).toContain('/punkt/1');
});

test('game runs until the fuel is gone and offers another shift', async ({ page }) => {
  await page.goto('/Game');
  await page.getByRole('button', { name: 'Почати зміну' }).click();
  await expect(page.locator('#gameOverlay')).toBeHidden();
  await page.keyboard.down('ArrowLeft');
  await page.waitForTimeout(300);
  await page.keyboard.up('ArrowLeft');
  await expect(page.locator('#gameOverlay')).toBeVisible({ timeout: 45_000 });
  await expect(page.getByRole('button', { name: 'Ще одна зміна' })).toBeVisible();
  expect(Number(await page.locator('#gameScore').textContent())).toBeGreaterThan(0);
});
