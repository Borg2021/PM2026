import { chromium } from 'playwright';
const browser = await chromium.launch({ headless: true, channel: 'chrome', args: ['--no-sandbox'] });
const page = await (await browser.newContext({ viewport: { width: 1920, height: 1080 }, locale: 'zh-CN' })).newPage();

// Login
await page.goto('http://localhost:3000/#/login', { waitUntil: 'networkidle', timeout: 15000 });
const inputs = await page.$$('input');
await inputs[0].fill('admin'); await inputs[1].fill('admin123');
await page.click('button');
await page.waitForTimeout(2000);

// Navigate to project edit
await page.goto('http://localhost:3000/#/project/edit/1', { waitUntil: 'networkidle', timeout: 15000 });
await page.waitForTimeout(2000);

// Gantt tab - day view
await page.locator('.el-tabs__item', { hasText: '甘特图' }).click();
await page.waitForTimeout(2000);

// Full page screenshot
await page.screenshot({ path: 'screenshots/gantt_day.png', type: 'png' });
console.log('gantt_day.png saved');

// Screenshot just the gantt area
const ganttArea = await page.$('.gantt-wrapper');
if (ganttArea) {
  await ganttArea.screenshot({ path: 'screenshots/gantt_area_day.png', type: 'png' });
  console.log('gantt_area_day.png saved');
}

// Switch to week view
await page.locator('.el-radio-button', { hasText: '周' }).click();
await page.waitForTimeout(1500);

if (ganttArea) {
  await ganttArea.screenshot({ path: 'screenshots/gantt_area_week.png', type: 'png' });
  console.log('gantt_area_week.png saved');
}

// Back to day, test right side timeline
await page.locator('.el-radio-button', { hasText: '日' }).click();
await page.waitForTimeout(1000);

// Screenshot the right timeline
const ganttRight = await page.$('.gantt-right');
if (ganttRight) {
  await ganttRight.screenshot({ path: 'screenshots/gantt_right_day.png', type: 'png' });
  console.log('gantt_right_day.png saved');
}

// Test view mode
await page.goto('http://localhost:3000/#/project/view/1', { waitUntil: 'networkidle', timeout: 15000 });
await page.waitForTimeout(2000);
await page.locator('.el-tabs__item', { hasText: '甘特图' }).click();
await page.waitForTimeout(2000);

const viewGantt = await page.$('.gantt-wrapper');
if (viewGantt) {
  await viewGantt.screenshot({ path: 'screenshots/gantt_view_mode.png', type: 'png' });
  console.log('gantt_view_mode.png saved');
}

// Test dialog
const firstBar = await page.$('.gantt-bar');
if (firstBar) {
  await firstBar.dblclick();
  await page.waitForTimeout(1000);
  await page.screenshot({ path: 'screenshots/gantt_dialog.png', type: 'png' });
  console.log('gantt_dialog.png saved');
}

console.log('\nAll screenshots saved. Check screenshots/ folder.');
await browser.close();
