import { chromium } from 'playwright';

const browser = await chromium.launch({ headless: true, channel: 'chrome', args: ['--no-sandbox'] });
const page = await (await browser.newContext({ viewport: { width: 1920, height: 1080 }, locale: 'zh-CN' })).newPage();

// ── 登录 ──
await page.goto('http://localhost:3000/#/login', { waitUntil: 'networkidle', timeout: 15000 });
const inputs = await page.$$('input');
await inputs[0].fill('admin'); await inputs[1].fill('admin123');
await page.click('button');
await page.waitForTimeout(2000);

// ── 进入项目编辑页 ──
await page.goto('http://localhost:3000/#/project/edit/1', { waitUntil: 'networkidle', timeout: 15000 });
await page.waitForTimeout(2000);

// ── 切换到甘特图 Tab ──
await page.locator('.el-tabs__item', { hasText: '甘特图' }).click();
await page.waitForTimeout(2000);

console.log('=== 计划甘特图 Tab 测试 ===\n');

// 1. 检查甘特图容器
const wrapper = await page.$('.gantt-wrapper');
console.log(`1. 甘特图容器: ${wrapper ? '✓ 存在' : '✗ 不存在'}`);

// 2. 检查工具栏
const toolbar = await page.$('.gantt-toolbar');
console.log(`2. 工具栏: ${toolbar ? '✓ 存在' : '✗ 不存在'}`);

// 3. 检查视图切换 (日/周)
const radioDay = await page.$('.el-radio-button:has-text("日")');
const radioWeek = await page.$('.el-radio-button:has-text("周")');
console.log(`3. 视图切换: 日${radioDay ? '✓' : '✗'} 周${radioWeek ? '✓' : '✗'}`);

// 4. 检查图例
const legend = await page.$('.gantt-legend');
const legendItems = await page.$$('.gantt-legend-item');
console.log(`4. 图例: ${legend ? '✓ 存在' : '✗ 不存在'} (${legendItems.length} 项)`);

// 5. 检查左侧面板
const leftPanel = await page.$('.gantt-left');
console.log(`5. 左侧面板: ${leftPanel ? '✓ 存在' : '✗ 不存在'}`);

// 6. 检查右侧面板
const rightPanel = await page.$('.gantt-right');
console.log(`6. 右侧面板: ${rightPanel ? '✓ 存在' : '✗ 不存在'}`);

// 7. 检查左侧任务列表
const taskRows = await page.$$('.gantt-left-body .gantt-row');
console.log(`7. 左侧任务行数: ${taskRows.length}`);

// 8. 检查任务图标
const icons = await page.$$('.gantt-task-icon');
console.log(`8. 任务图标总数: ${icons.length}`);
for (let i = 0; i < Math.min(icons.length, 10); i++) {
  const text = await icons[i].textContent();
  const color = await icons[i].evaluate(el => getComputedStyle(el).color);
  console.log(`   图标${i}: "${text.trim()}" color=${color}`);
}

// 9. 检查任务序号和名称
const taskNames = await page.$$eval('.gantt-left-body .gantt-task-name', els => els.map(e => e.textContent.trim()));
const taskNos = await page.$$eval('.gantt-left-body .gantt-task-no', els => els.map(e => e.textContent.trim()));
console.log(`9. 左侧任务列表:`);
for (let i = 0; i < Math.min(taskNames.length, 15); i++) {
  console.log(`   ${taskNos[i] || '-'}: ${taskNames[i] || '-'}`);
}

// 10. 检查月份表头
const monthHeaders = await page.$$('.gantt-month-cell');
console.log(`10. 月份表头: ${monthHeaders.length} 个月份`);
for (let i = 0; i < monthHeaders.length; i++) {
  const text = await monthHeaders[i].textContent();
  console.log(`    ${text.trim()}`);
}

// 11. 检查日期表头
const dayHeaders = await page.$$('.gantt-day-cell');
console.log(`11. 日期表头: ${dayHeaders.length} 天`);

// 12. 检查甘特条
const bars = await page.$$('.gantt-bar');
console.log(`12. 甘特条: ${bars.length} 个`);
for (let i = 0; i < Math.min(bars.length, 10); i++) {
  const info = await bars[i].evaluate(el => {
    const s = el.style;
    const cls = el.className;
    const label = el.querySelector('.gantt-bar-label')?.textContent?.trim() || '';
    return { left: s.left, width: s.width, class: cls, label };
  });
  console.log(`   条${i}: left=${info.left} width=${info.width} label="${info.label}" class="${info.class}"`);
}

// 13. 检查里程碑
const milestones = await page.$$('.gantt-milestone');
console.log(`13. 里程碑: ${milestones.length} 个`);

// 14. 检查今日线
const todayLine = await page.$('.gantt-today-line');
if (todayLine) {
  const left = await todayLine.evaluate(el => el.style.left);
  console.log(`14. 今日线: ✓ 存在 (left=${left})`);
} else {
  console.log(`14. 今日线: ✗ 不存在`);
}

// 15. 检查依赖连线 SVG
const depSvg = await page.$('.gantt-dep-svg');
if (depSvg) {
  const paths = await depSvg.$$('path');
  console.log(`15. 依赖连线 SVG: ✓ 存在 (${paths.length} 条连线)`);
  for (let i = 0; i < Math.min(paths.length, 5); i++) {
    const d = await paths[i].getAttribute('d');
    console.log(`   连线${i}: d="${d?.substring(0, 80)}..."`);
  }
} else {
  console.log(`15. 依赖连线 SVG: ✗ 不存在 (可能没有前置任务数据)`);
}

// 16. 检查进度百分比
const pcts = await page.$$('.gantt-bar-pct');
console.log(`16. 进度百分比标签: ${pcts.length} 个`);

// 17. 检查责任部门/人
const infos = await page.$$('.gantt-bar-info');
console.log(`17. 责任信息标签: ${infos.length} 个`);

// 18. 切换到周视图
await page.locator('.el-radio-button:has-text("周")').click();
await page.waitForTimeout(1000);
const weekHeaders = await page.$$('.gantt-weeknum-cell');
console.log(`18. 周视图: ${weekHeaders.length} 个周编号`);

// 19. 切回日视图
await page.locator('.el-radio-button:has-text("日")').click();
await page.waitForTimeout(1000);

// 20. 截图
await page.screenshot({ path: 'screenshots/gantt_test.png' });
console.log('\n截图已保存: screenshots/gantt_test.png');

// 21. 检查甘特图 tab 在 projectId 为空时是否 disabled
await page.goto('http://localhost:3000/#/project/create', { waitUntil: 'networkidle', timeout: 15000 });
await page.waitForTimeout(1500);
const ganttTab = await page.$('.el-tabs__item.is-disabled:has-text("甘特图")');
console.log(`\n21. 新建项目时甘特图 tab disabled: ${ganttTab ? '✓' : '✗'}`);

await browser.close();
console.log('\n=== 测试完成 ===');
