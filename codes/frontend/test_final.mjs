import { chromium } from 'playwright';
const browser = await chromium.launch({ headless: true, channel: 'chrome', args: ['--no-sandbox'] });
const page = await (await browser.newContext({ viewport: { width: 1920, height: 1080 }, locale: 'zh-CN' })).newPage();

await page.goto('http://localhost:3001/#/login', { waitUntil: 'networkidle', timeout: 15000 });
const inputs = await page.$$('input');
await inputs[0].fill('admin'); await inputs[1].fill('admin123');
await page.click('button'); await page.waitForTimeout(2000);
await page.goto('http://localhost:3001/#/project/edit/1', { waitUntil: 'networkidle', timeout: 15000 });
await page.waitForTimeout(2000);
await page.locator('.el-tabs__item', { hasText: '任务计划' }).click();
await page.waitForTimeout(2000);

// Check expand icon state
const icon = await page.$eval('.el-table__expand-icon', el => ({
  beforeContent: getComputedStyle(el, '::before').content,
  beforeDisplay: getComputedStyle(el, '::before').display,
  afterContent: getComputedStyle(el, '::after').content,
  afterDisplay: getComputedStyle(el, '::after').display,
  svgDisplay: getComputedStyle(el.querySelector('.el-icon')).display,
}));
console.log('Final state:', JSON.stringify(icon, null, 2));

await page.screenshot({ path: 'screenshots/expand_final.png' });
await browser.close();
