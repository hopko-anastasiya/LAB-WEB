import { test, expect, createCommunity, enablePremium, inviteCode, join, signIn } from './helpers.js';

async function twoMembers(page, secondPage) {
  await signIn(page, undefined, 'Перша');
  await createCommunity(page);
  const code = await inviteCode(page);
  await signIn(secondPage, undefined, 'Друга');
  await join(secondPage, code);
}

const lines = (page) => page.locator('.guide-text');

test('guide syncs typing, order and deletion between two browsers', async ({ page, secondPage }) => {
  await twoMembers(page, secondPage);
  await page.goto('/Guide');
  await secondPage.goto('/Guide');
  await expect(page.locator('#guideState')).toContainText('Перша');
  await expect(lines(page)).toHaveCount(1);
  await expect(lines(secondPage)).toHaveCount(1);

  await lines(page).first().click();
  await page.keyboard.type('Як запустити генератор');
  await page.keyboard.press('Enter');
  await page.keyboard.type('- Перевірити пальне');
  await page.keyboard.press('Enter');
  await page.keyboard.type('Увімкнути автомат');

  await expect(lines(secondPage)).toHaveText(['Як запустити генератор', 'Перевірити пальне', 'Увімкнути автомат']);
  await expect(secondPage.locator('.guide-row').first()).toHaveClass(/type-heading/);
  await expect(secondPage.locator('.guide-row').nth(1)).toHaveClass(/type-bullet/);
  await expect(secondPage.locator('.guide-row.is-edited .guide-who')).toHaveText('Перша');

  const second = page.locator('.guide-row').nth(1);
  const third = page.locator('.guide-row').nth(2);
  await second.hover();
  await second.locator('.guide-handle').dragTo(third, { targetPosition: { x: 40, y: 28 } });
  await expect(lines(page)).toHaveText(['Як запустити генератор', 'Увімкнути автомат', 'Перевірити пальне']);
  await expect(lines(secondPage)).toHaveText(['Як запустити генератор', 'Увімкнути автомат', 'Перевірити пальне']);
  await expect(secondPage.locator('.guide-row.just-moved')).toHaveCount(1);

  await lines(page).nth(2).click();
  await page.keyboard.press('Control+A');
  await page.keyboard.press('Delete');
  await page.keyboard.press('Backspace');
  await page.keyboard.press('Backspace');
  await expect(lines(secondPage)).toHaveCount(2);
});

test('guide restores the document after reopening, even offline edits', async ({ page, context }) => {
  await signIn(page);
  await createCommunity(page);
  await page.goto('/Guide');
  await expect(page.locator('#guideState')).toContainText('Зараз тут');
  await lines(page).first().click();
  await page.keyboard.type('Збережений рядок');
  await page.waitForTimeout(600);

  await page.reload();
  await expect(lines(page).first()).toHaveText('Збережений рядок');

  await context.route('**/hubs/guide/**', (route) => route.abort());
  await page.reload();
  await expect(lines(page).first()).toHaveText('Збережений рядок');
  await expect(page.locator('#guideState')).toContainText('Немає зв');
});

test('premium board shares drawing between two browsers', async ({ page, secondPage }) => {
  await twoMembers(page, secondPage);
  await enablePremium(page);
  await page.goto('/Board');
  await secondPage.goto('/Board');
  await expect(page.locator('#boardPresence')).toContainText('Друга');

  await page.getByRole('button', { name: 'Малювати' }).click();
  const box = await page.locator('#boardMap').boundingBox();
  await page.mouse.move(box.x + 100, box.y + 100);
  await page.mouse.down();
  await page.mouse.move(box.x + 250, box.y + 180, { steps: 10 });
  await page.mouse.move(box.x + 400, box.y + 120, { steps: 10 });
  await page.mouse.up();
  await expect(secondPage.locator('#boardMap path.leaflet-interactive')).toHaveCount(1);

  await page.getByRole('button', { name: 'Примітка' }).click();
  await page.mouse.click(box.x + 200, box.y + 300);
  await page.getByLabel('Текст примітки').fill('Тут заправка <b>');
  await page.getByRole('button', { name: 'Додати' }).click();
  await expect(secondPage.locator('.board-note')).toHaveText('Тут заправка <b>');

  await secondPage.reload();
  await expect(secondPage.locator('.board-note')).toHaveCount(1);
  await expect(secondPage.locator('#boardMap path.leaflet-interactive')).toHaveCount(1);
});

test('analytics charts open for premium and are closed for the basic plan', async ({ page }) => {
  await signIn(page);
  await createCommunity(page);
  await page.goto('/Analytics');
  await expect(page).toHaveURL(/\/Community\/Premium$/);
  await enablePremium(page);
  await page.goto('/Analytics');
  await expect(page.getByRole('heading', { name: 'Аналітика громади' })).toBeVisible();
});
