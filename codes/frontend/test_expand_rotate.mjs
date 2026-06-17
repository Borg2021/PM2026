import { chromium } from 'playwright';
const browser = await chromium.launch({ headless: true, channel: 'chrome', args: ['--no-sandbox'] });
const page = await (await browser.newContext({ viewport: { width: 1920, height: 1080 }, locale: 'zh-CN' })).newPage();

await page.goto('http://localhost:3000/#/login', { waitUntil: 'networkidle', timeout: 15000 });
const inputs = await page.$$('input');
await inputs[0].fill('admin'); await inputs[1].fill('admin123');
await page.click('button');
await page.waitForTimeout(2000);

await page.goto('http://localhost:3000/#/project/edit/1', { waitUntil: 'networkidle', timeout: 15000 });
await page.waitForTimeout(2000);

await page.locator('.el-tabs__item', { hasText: '任务计划' }).click();
await page.waitForTimeout(2000);

const icon = await page.$('.el-table__expand-icon');
if (icon) {
  const info = await icon.evaluate(el => {
    const style = getComputedStyle(el);
    return {
      transform: style.transform,
      rotate: style.rotate,
    };
  });
  console.log('Expanded icon transform:', info.transform);
  console.log('Expanded icon rotate:', info.rotate);

  // Collapse it
  await icon.click();
  await page.waitForTimeout(500);

  const info2 = await icon.evaluate(el => {
    const style = getComputedStyle(el);
    return {
      transform: style.transform,
      rotate: style.rotate,
      classes: Array.from(el.classList),
    };
  });
  console.log('Collapsed icon transform:', info2.transform);
  console.log('Collapsed icon rotate:', info2.rotate);
  console.log('Collapsed classes:', info2.classes.join(' '));
}

await browser.close();
