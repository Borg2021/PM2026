import { chromium } from 'playwright';
const browser = await chromium.launch({ headless: true, channel: 'chrome', args: ['--no-sandbox'] });
const page = await (await browser.newContext({ viewport: { width: 1920, height: 1080 }, locale: 'zh-CN' })).newPage();

await page.goto('http://localhost:3000/#/login', { waitUntil: 'networkidle', timeout: 15000 });
const inputs = await page.$$('input');
await inputs[0].fill('admin'); await inputs[1].fill('admin123');
await page.click('button'); await page.waitForTimeout(2000);

await page.goto('http://localhost:3000/#/project/edit/1', { waitUntil: 'networkidle', timeout: 15000 });
await page.waitForTimeout(2000);
await page.locator('.el-tabs__item', { hasText: '任务计划' }).click();
await page.waitForTimeout(2000);

// Check node-name-icon elements - should be 0 now
const icons = await page.$$('.node-name-icon');
console.log('node-name-icon count (should be 0):', icons.length);

// Check expand icons still exist
const expandIcons = await page.$$('.el-table__expand-icon');
console.log('expand-icon count:', expandIcons.length);

// Verify expand icon CSS
const info = await expandIcons[0].evaluate(el => ({
  beforeContent: getComputedStyle(el, '::before').content,
  transform: getComputedStyle(el).transform,
}));
console.log('expand ::before:', info.beforeContent, '| transform:', info.transform);

await browser.close();
