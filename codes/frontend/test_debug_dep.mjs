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
await page.locator('.el-tabs__item', { hasText: '甘特图' }).click();
await page.waitForTimeout(2000);

// Check dependency SVG
const svg = await page.$('.gantt-dep-svg');
console.log('SVG element exists:', !!svg);

if (svg) {
  // Check computed style
  const style = await svg.evaluate(el => {
    const s = getComputedStyle(el);
    return {
      position: s.position,
      top: s.top,
      left: s.left,
      width: s.width,
      height: s.height,
      zIndex: s.zIndex,
      display: s.display,
      visibility: s.visibility,
      opacity: s.opacity,
      pointerEvents: s.pointerEvents,
    };
  });
  console.log('SVG style:', JSON.stringify(style, null, 2));

  // Check inner content
  const html = await svg.evaluate(el => el.innerHTML.substring(0, 500));
  console.log('SVG inner HTML:', html);

  // Check paths
  const paths = await svg.$$('path');
  console.log('Path elements:', paths.length);
  for (let i = 0; i < paths.length; i++) {
    const d = await paths[i].getAttribute('d');
    const stroke = await paths[i].evaluate(el => getComputedStyle(el).stroke);
    console.log(`  Path ${i}: d="${d}" stroke=${stroke}`);
  }
}

// Check flattened tasks and their preTaskCodes
const taskData = await page.evaluate(() => {
  const rows = document.querySelectorAll('.gantt-left-body .gantt-row');
  const info = [];
  for (const row of rows) {
    const no = row.querySelector('.gantt-task-no')?.textContent?.trim();
    const name = row.querySelector('.gantt-task-name')?.textContent?.trim();
    info.push({ no, name });
  }
  return info;
});
console.log('\nGantt left panel tasks:');
for (const t of taskData) {
  console.log(`  ${t.no}: ${t.name}`);
}

// Check bars
const bars = await page.$$('.gantt-bar');
console.log(`\nGantt bars: ${bars.length}`);
for (let i = 0; i < bars.length; i++) {
  const info = await bars[i].evaluate(el => {
    const s = el.style;
    return {
      left: s.left,
      width: s.width,
      class: el.className,
    };
  });
  console.log(`  Bar ${i}: left=${info.left} width=${info.width} class=${info.class}`);
}

// Check today line
const todayLine = await page.$('.gantt-today-line');
if (todayLine) {
  const left = await todayLine.evaluate(el => el.style.left);
  console.log(`\nToday line left: ${left}`);
}

// Check timeline width
const timeline = await page.$('.gantt-timeline');
if (timeline) {
  const width = await timeline.evaluate(el => el.style.width);
  console.log(`Timeline width: ${width}`);
}

// Check earliest and latest dates
const dateInfo = await page.evaluate(() => {
  // Try to access month headers for date range
  const monthCells = document.querySelectorAll('.gantt-month-cell');
  const labels = [];
  for (const cell of monthCells) {
    labels.push(cell.textContent?.trim());
  }
  return labels;
});
console.log('Month headers:', dateInfo);

// Screenshot
await page.screenshot({ path: 'screenshots/gantt_debug_dep.png' });
console.log('\nScreenshot saved: gantt_debug_dep.png');

await browser.close();
