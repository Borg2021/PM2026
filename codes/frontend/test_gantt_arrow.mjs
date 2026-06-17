import { chromium } from 'playwright';

const TOKEN = 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1laWRlbnRpZmllciI6IjEiLCJyZWFsTmFtZSI6Iuezu-e7n-euoeeQhuWRmCIsImh0dHA6Ly9zY2hlbWFzLm1pY3Jvc29mdC5jb20vd3MvMjAwOC8wNi9pZGVudGl0eS9jbGFpbXMvcm9sZSI6ImFkbWluIiwiZXhwIjoxNzc5NDU1MDExfQ.V8M6kuTVlvXPP0_gD1xu2DLnacEHaN12SWGSKadzp-c';

// ── 设置测试数据 ──
// 场景A: test1(左) → test2(右)  正常 x1 < x2，垂直线在两柱之间
// 场景B: 设计电气料单(右) → 测试都是对的(左)  x1 >= x2，需要绕路+回折

// 先查看当前任务状态
let r = await fetch('http://localhost:5292/api/v1/projects/1/tasks', {
  headers: { 'Authorization': `Bearer ${TOKEN}` }
});
const tasks = (await r.json()).data;
console.log('当前任务:');
const taskMap = {};
for (const t of tasks) {
  taskMap[t.taskNo] = t;
  console.log(`  id=${t.id} no=${t.taskNo} name=${t.taskName} preTaskCodes=${t.preTaskCodes} planStart=${t.planStartDate?.slice(0,10)} planFinish=${t.planFinishDate?.slice(0,10)}`);
}

// 设置 test2 (001.02) 的前置任务为 test1 (001.01) — x1 < x2 场景
const test2 = tasks.find(t => t.taskName === 'test2');
const test1 = tasks.find(t => t.taskName === 'test1');
if (test2 && test1) {
  const r1 = await fetch(`http://localhost:5292/api/v1/projects/1/tasks/${test2.id}`, {
    method: 'PUT',
    headers: { 'Authorization': `Bearer ${TOKEN}`, 'Content-Type': 'application/json' },
    body: JSON.stringify({
      taskNo: test2.taskNo, wbsCode: test2.wbsCode || '', taskName: test2.taskName,
      nodeType: test2.nodeType, sortOrder: test2.sortOrder,
      status: test2.status, priority: test2.priority,
      planStartDate: test2.planStartDate, planFinishDate: test2.planFinishDate,
      planDuration: test2.planDuration, progressPct: test2.progressPct,
      preTaskCodes: test1.taskNo + '(FS)',
      parentId: test2.parentId, deliverableCnt: test2.deliverableCnt || 0
    })
  });
  console.log(`\n设置 test2 前置=${test1.taskNo}: ${r1.status} ${await r1.text()}`);
}

// 设置 测试都是对的 的前置任务为 设计电气料单 — 可能 x1 >= x2 场景
const ceshi = tasks.find(t => t.taskName === '测试都是对的');
const sheji = tasks.find(t => t.taskName === '设计电气料单');
if (ceshi && sheji) {
  const r2 = await fetch(`http://localhost:5292/api/v1/projects/1/tasks/${ceshi.id}`, {
    method: 'PUT',
    headers: { 'Authorization': `Bearer ${TOKEN}`, 'Content-Type': 'application/json' },
    body: JSON.stringify({
      taskNo: ceshi.taskNo, wbsCode: ceshi.wbsCode || '', taskName: ceshi.taskName,
      nodeType: ceshi.nodeType, sortOrder: ceshi.sortOrder,
      status: ceshi.status, priority: ceshi.priority,
      planStartDate: ceshi.planStartDate, planFinishDate: ceshi.planFinishDate,
      planDuration: ceshi.planDuration, progressPct: ceshi.progressPct,
      preTaskCodes: sheji.taskNo + '(FS)',
      parentId: ceshi.parentId, deliverableCnt: ceshi.deliverableCnt || 0
    })
  });
  console.log(`设置 测试都是对的 前置=${sheji.taskNo}: ${r2.status} ${await r2.text()}`);
}

// ── 浏览器测试 ──
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

console.log('\n=== 前置连线箭头方向测试 ===\n');

// 检查依赖连线
const depSvg = await page.$('.gantt-dep-svg');
if (depSvg) {
  const paths = await depSvg.$$('path');
  console.log(`连线总数: ${paths.length}`);
  
  for (let i = 0; i < paths.length; i++) {
    const d = await paths[i].getAttribute('d');
    const markerEnd = await paths[i].getAttribute('marker-end');
    console.log(`\n连线${i}:`);
    console.log(`  marker-end: ${markerEnd}`);
    
    // 分析路径最后一段方向
    const parts = d.split(/(?=[MLAZ])/);
    console.log(`  段数: ${parts.length}`);
    for (let j = 0; j < parts.length; j++) {
      console.log(`  段${j}: ${parts[j].trim()}`);
    }
    
    // 检查最后一段是否是 L x y 且 x 在增大（左→右）
    const lastSeg = parts[parts.length - 1].trim();
    if (lastSeg.startsWith('L')) {
      // 找前一个点的 x 坐标
      const prevSeg = parts[parts.length - 2] ? parts[parts.length - 2].trim() : '';
      const prevX = parseFloat(prevSeg.split(/[\s,]+/).filter(s => !isNaN(parseFloat(s))).pop() || '0');
      const lastX = parseFloat(lastSeg.split(/[\s,]+/).filter(s => !isNaN(parseFloat(s)))[0] || '0');
      const lastY = parseFloat(lastSeg.split(/[\s,]+/).filter(s => !isNaN(parseFloat(s)))[1] || '0');
      
      if (lastX > prevX) {
        console.log(`  ✓ 最后一段从左→右 (${prevX} → ${lastX})`);
      } else if (lastX < prevX) {
        console.log(`  ✗ 最后一段从右→左 (${prevX} → ${lastX})`);
      } else {
        console.log(`  - 最后一段垂直 (${prevX} → ${lastX})`);
      }
    }
  }
} else {
  console.log('没有找到依赖连线 SVG');
}

// 截图
await page.screenshot({ path: 'screenshots/gantt_arrow_test.png' });
console.log('\n截图: screenshots/gantt_arrow_test.png');

await browser.close();
console.log('\n=== 测试完成 ===');
