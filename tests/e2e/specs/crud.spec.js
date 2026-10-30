import { test, expect, createCommunity, pickAutocomplete, signIn, unique } from './helpers.js';

const PNG = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=', 'base64');

async function newOperator(page) {
  const name = unique('Оператор');
  await page.goto('/Operators/Create');
  await page.getByLabel("Ім'я / назва").fill(name);
  await page.getByLabel('Телефон').fill('+380501112233');
  await page.getByRole('button', { name: /Створити|Зберегти|Додати/ }).click();
  await expect(page.getByText('Оператора додано')).toBeVisible();
  return name;
}

async function newPoint(page, operator, photo) {
  const name = unique('Пункт');
  await page.goto('/Points/Create');
  await page.getByLabel('Назва').fill(name);
  await pickAutocomplete(page, 'Point_Settlement', 'Вінн', 'Вінниця');
  await expect(page.locator('#Point_Latitude')).toHaveValue(/^49\./);
  await page.getByLabel('Адреса').fill('вул. Соборна, 1');
  await page.getByLabel('Місткість, осіб').fill('40');
  await pickAutocomplete(page, 'Point_OperatorId', operator.slice(-8), operator);
  if (photo) await page.locator('input[type=file]').setInputFiles(photo);
  await page.getByRole('button', { name: 'Створити' }).click();
  await expect(page.getByText('Пункт створено')).toBeVisible();
  return name;
}

test('member creates, reports on, edits and deletes a point', async ({ page }) => {
  await signIn(page);
  await createCommunity(page);
  const operator = await newOperator(page);
  const point = await newPoint(page, operator, { name: 'photo.png', mimeType: 'image/png', buffer: PNG });
  await expect(page.getByRole('heading', { name: point })).toBeVisible();
  const photo = page.locator('img.point-photo');
  await expect(photo).toHaveAttribute('src', /uploads\/points/);
  expect(await photo.evaluate((img) => img.complete && img.naturalWidth > 0)).toBe(true);

  await page.getByRole('link', { name: 'Додати звіт' }).click();
  await page.getByLabel('Статус').selectOption({ label: 'Не працює' });
  await page.getByLabel('Людей у пункті').fill('5');
  await page.getByRole('button', { name: 'Надіслати' }).click();
  await expect(page.locator('.landing-status')).toHaveText('Зараз не працює');

  await page.getByRole('link', { name: 'Редагувати' }).click();
  await page.getByLabel('Адреса').fill('вул. Нова, 2');
  await page.getByRole('button', { name: 'Зберегти' }).click();
  await expect(page.getByText('Зміни збережено')).toBeVisible();
  await expect(page.locator('.landing-address')).toContainText('вул. Нова, 2');

  await page.getByRole('link', { name: 'Видалити' }).click();
  await page.locator('main form').getByRole('button', { name: /Видалити/ }).click();
  await expect(page.getByText('Пункт видалено')).toBeVisible();
});

test('photo form rejects a fake image and shrinks a big one', async ({ page }) => {
  await signIn(page);
  await createCommunity(page);
  await page.goto('/Reports/Create');
  const input = page.locator('input[type=file]');

  await input.setInputFiles({ name: 'fake.jpg', mimeType: 'image/jpeg', buffer: Buffer.from('<script>alert(1)</script>') });
  await expect(page.locator('.photo-status')).toContainText('не схоже на зображення');
  expect(await input.evaluate((el) => el.files.length)).toBe(0);

  const big = await page.evaluate(async () => {
    const canvas = document.createElement('canvas');
    canvas.width = 3000;
    canvas.height = 2000;
    const g = canvas.getContext('2d');
    for (let i = 0; i < 3000; i++) {
      g.fillStyle = `hsl(${i % 360},70%,50%)`;
      g.fillRect((i * 37) % 3000, (i * 91) % 2000, 40, 40);
    }
    const blob = await new Promise((r) => canvas.toBlob(r, 'image/jpeg', 1));
    return [...new Uint8Array(await blob.arrayBuffer())];
  });
  await input.setInputFiles({ name: 'big.jpg', mimeType: 'image/jpeg', buffer: Buffer.from(big) });
  await expect(page.locator('.photo-status')).toContainText('Фото зменшено');
  expect(await input.evaluate((el) => el.files[0].size)).toBeLessThan(big.length);
});

test('another community cannot edit the point', async ({ page, secondPage }) => {
  await signIn(page);
  await createCommunity(page);
  const operator = await newOperator(page);
  await newPoint(page, operator);
  const url = page.url();

  await signIn(secondPage);
  await createCommunity(secondPage);
  await secondPage.goto(url);
  await expect(secondPage.getByRole('link', { name: 'Редагувати' })).toHaveCount(0);
  const id = /(\d+)$/.exec(url)[1];
  await secondPage.goto(`/Points/Edit/${id}`);
  await expect(secondPage.getByRole('heading', { name: 'Немає доступу' })).toBeVisible();
});

test('subscriber sees a notification after the status changes', async ({ page }) => {
  await signIn(page);
  await createCommunity(page);
  const operator = await newOperator(page);
  const point = await newPoint(page, operator);
  await page.getByRole('button', { name: 'Стежити за пунктом' }).click();
  await expect(page.getByRole('button', { name: 'Не стежити за пунктом' })).toBeVisible();

  await page.getByRole('link', { name: 'Додати звіт' }).click();
  await page.getByLabel('Статус').selectOption({ label: 'Не працює' });
  await page.getByRole('button', { name: 'Надіслати' }).click();

  await expect(async () => {
    await page.goto('/Notifications');
    await expect(page.locator('.notice-list li.is-new')).toContainText(point, { timeout: 500 });
  }).toPass({ timeout: 15_000 });
  await page.reload();
  await expect(page.locator('.nav-badge')).toHaveText('1');
  await page.getByRole('button', { name: 'Позначити прочитаними' }).click();
  await expect(page.locator('.nav-badge')).toHaveCount(0);
});
