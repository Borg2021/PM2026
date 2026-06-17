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

// Find and edit the task "test2" (001.02) - add dates and predecessor
const editBtns = await page.$$('.el-button--text:has-text("编辑")');
console.log(`Edit buttons: ${editBtns.length}`);

// Edit "test2" and add a planStartDate, planFinishDate, planDuration, and predecessor
// Let's find the rows first
const rows = await page.$$('.el-table__body-wrapper .el-table__row');
console.log(`Table rows: ${rows.length}`);

// Find test2 by clicking edit button in the right row
// Let me try a different approach - use the API to set up data first, then test
const TOKEN = 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1laWRlbnRpZmllciI6IjEiLCJyZWFsTmFtZSI6Iuezu-e7n-euoeeQhuWRmCIsImh0dHA6Ly9zY2hlbWFzLm1pY3Jvc29mdC5jb20vd3MvMjAwOC8wNi9pZGVudGl0eS9jbGFpbXMvcm9sZSI6ImFkbWluIiwiZXhwIjoxNzc5NDU1MDExfQ.V8M6kuTVlvXPP0_gD1xu2DLnacEHaN12SWGSKadzp-c';

// Set up test2 with dates and duration
const r1 = await (await fetch('http://localhost:5292/api/v1/projects/1/tasks/118', {
  method: 'PUT',
  headers: { 'Authorization': `Bearer ${TOKEN}`, 'Content-Type': 'application/json' },
  body: JSON.stringify({
    taskNo: '001.02', wbsCode: '', taskName: 'test2', nodeType: 1, sortOrder: 2,
    status: 0, priority: 3, planStartDate: '2026-05-25T00:00:00', planFinishDate: '2026-05-30T00:00:00',
    planDuration: 5, progressPct: 0, deliverableCnt: 0, parentId: 116
  })
})).json();
console.log('Setup test2:', r1.code === 0 ? 'OK' : `Failed: ${JSON.stringify(r1)}`);

// Now reload the page and edit test2 to add predecessor
await page.goto('http://localhost:3000/#/project/edit/1', { waitUntil: 'networkidle', timeout: 15000 });
await page.waitForTimeout(2000);
await page.locator('.el-tabs__item', { hasText: '任务计划' }).click();
await page.waitForTimeout(2000);

// Click edit on test2
const editBtns2 = await page.$$('.el-button--text:has-text("编辑")');
// Try editing test2 - it should be the 3rd leaf task
for (let i = 0; i < editBtns2.length; i++) {
  const parentRow = await editBtns2[i].evaluateHandle(el => el.closest('tr'));
  const rowText = await parentRow.evaluate(el => el.textContent);
  if (rowText.includes('test2')) {
    console.log(`Found test2 row, clicking edit`);
    await editBtns2[i].click();
    await page.waitForTimeout(1500);
    break;
  }
}

const dialog = await page.$('.el-dialog');
if (dialog) {
  console.log('Edit dialog opened for test2');

  // Click "添加前置任务" button
  const addPredBtn = await page.$('button:has-text("添加前置任务")');
  if (addPredBtn) {
    await addPredBtn.click();
    await page.waitForTimeout(500);
    console.log('Clicked add predecessor');

    // Select the predecessor task in the dropdown
    const selectEl = await page.$('.predecessor-row .el-select');
    if (selectEl) {
      await selectEl.click();
      await page.waitForTimeout(1000);

      // Look for dropdown options
      const options = await page.$$('.el-select-dropdown__item');
      console.log(`Dropdown options: ${options.length}`);
      for (let i = 0; i < options.length; i++) {
        const text = await options[i].textContent();
        console.log(`  Option ${i}: ${text?.trim()}`);
      }

      // Select the first available predecessor (should be 003.2 test1 or 002 设计电气料单)
      // Choose 设计电气料单 (003) since it has dates
      for (let i = 0; i < options.length; i++) {
        const text = await options[i].textContent();
        if (text?.includes('设计电气料单')) {
          await options[i].click();
          await page.waitForTimeout(500);
          console.log(`Selected: ${text.trim()}`);
          break;
        }
      }
    }
  }

  // Check what the planStartDate and planFinishDate look like now
  const dateInputs = await page.$$('.el-dialog .el-date-picker input');
  console.log(`Date inputs: ${dateInputs.length}`);

  // Click save to test auto-calculation
  const saveBtn = await page.$('.el-dialog .el-button--primary:has-text("保存")');
  if (saveBtn) {
    await saveBtn.click();
    await page.waitForTimeout(2000);
    console.log('Clicked save');
  }
}

// Check the updated task data
const r2 = await (await fetch(`http://localhost:5292/api/v1/projects/1/tasks`, {
  headers: { 'Authorization': `Bearer ${TOKEN}` }
})).json();
console.log('\nTasks after save:');
for (const t of r2.data) {
  console.log(`  id=${t.id} no=${t.taskNo} name=${t.taskName} preTaskCodes=${t.preTaskCodes} planStart=${t.planStartDate?.slice(0,10)} planFinish=${t.planFinishDate?.slice(0,10)} planDuration=${t.planDuration}`);
}

await browser.close();
