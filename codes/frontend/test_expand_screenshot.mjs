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

// Full page screenshot - expanded state
await page.screenshot({ path: 'screenshots/expand-icons-expanded.png' });
console.log('Screenshot: expand-icons-expanded.png (all rows expanded, should show ▼)');

// Find and click the first expand icon to collapse
const icon = await page.$('.el-table__expand-icon');
if (icon) {
  // Scroll it into view first
  await icon.scrollIntoViewIfNeeded();
  await page.waitForTimeout(300);
  await icon.click();
  await page.waitForTimeout(500);

  // Screenshot just the top portion showing the collapsed row
  await page.screenshot({ path: 'screenshots/expand-icons-collapsed.png' });
  console.log('Screenshot: expand-icons-collapsed.png (first row collapsed ▶, rest expanded ▼)');
}

console.log('Done.');
await browser.close();
