import { chromium } from 'playwright';
const browser = await chromium.launch({ headless: true, channel: 'chrome', args: ['--no-sandbox'] });
const page = await (await browser.newContext({ viewport: { width: 1920, height: 1080 }, locale: 'zh-CN' })).newPage();

await page.goto('http://localhost:3000/#/login', { waitUntil: 'networkidle', timeout: 15000 });
const inputs = await page.$$('input');
await inputs[0].fill('admin'); await inputs[1].fill('admin123');
await page.click('button');
await page.waitForTimeout(2000);

// Check projects list to find one with dependencies
await page.goto('http://localhost:3000/#/project/list', { waitUntil: 'networkidle', timeout: 15000 });
await page.waitForTimeout(2000);

// Check project 1
await page.goto('http://localhost:3000/#/project/edit/1', { waitUntil: 'networkidle', timeout: 15000 });
await page.waitForTimeout(2000);
await page.locator('.el-tabs__item', { hasText: '甘特图' }).click();
await page.waitForTimeout(2000);

// Check dependency lines
const depSvg = await page.$('.gantt-dep-svg');
console.log('Dependency SVG exists:', !!depSvg);

if (depSvg) {
  const lines = await depSvg.$$('path');
  console.log('Dependency lines count:', lines.length);
  for (let i = 0; i < Math.min(lines.length, 5); i++) {
    const d = await lines[i].getAttribute('d');
    console.log(`  Line ${i}: ${d}`);
  }
}

// Also check if any tasks have preTaskCodes
const preTaskData = await page.evaluate(() => {
  const rows = document.querySelectorAll('.gantt-left-body .gantt-row');
  const info = [];
  for (const row of rows) {
    const no = row.querySelector('.gantt-task-no')?.textContent?.trim();
    const name = row.querySelector('.gantt-task-name')?.textContent?.trim();
    info.push({ no, name });
  }
  return info;
});
console.log('\nTasks in Gantt:');
for (const t of preTaskData) {
  console.log(`  ${t.no}: ${t.name}`);
}

// Check if any task has preTaskCodes in the data
const preTaskInfo = await page.evaluate(() => {
  // Try to access Vue component data through the DOM
  const app = document.querySelector('#app');
  // Check the gantt bar titles for preTask info
  const bars = document.querySelectorAll('.gantt-bar');
  const info = [];
  for (const bar of bars) {
    info.push(bar.getAttribute('title'));
  }
  return info;
});
console.log('\nBar titles:', preTaskInfo);

await page.screenshot({ path: 'screenshots/gantt_dep_lines.png' });
console.log('\nScreenshot saved: gantt_dep_lines.png');

await browser.close();
