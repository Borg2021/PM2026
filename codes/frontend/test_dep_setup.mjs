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

// Click "编辑" on the first editable row (the leaf task "设计电气料单")
const editBtns = await page.$$('.el-button--text:has-text("编辑")');
console.log(`Edit buttons found: ${editBtns.length}`);

if (editBtns.length > 0) {
  // Click the last edit button (for "设计电气料单" which should have data)
  await editBtns[editBtns.length - 1].click();
  await page.waitForTimeout(1500);

  // Look for the dialog with task editing form
  const dialog = await page.$('.el-dialog');
  if (dialog) {
    console.log('Edit dialog opened');

    // Find the preTaskCodes field - it might be a select or input
    // Let's check what form items are available
    const formLabels = await page.$$eval('.el-form-item__label', els => els.map(e => e.textContent?.trim()));
    console.log('Form labels:', formLabels);

    // Look for 前置任务 field
    const predLabel = await page.$('.el-form-item:has-text("前置")');
    if (predLabel) {
      console.log('前置任务 field found');

      // Try to find and interact with the pre-task selector
      // It could be an el-tree-select
      const treeSelect = await page.$('.el-tree-select');
      if (treeSelect) {
        console.log('Tree select found, clicking...');
        await treeSelect.click();
        await page.waitForTimeout(1000);

        // Select a task from the dropdown
        const options = await page.$$('.el-select-dropdown__item');
        console.log(`Dropdown options: ${options.length}`);
        if (options.length > 0) {
          await options[0].click();
          await page.waitForTimeout(500);
          console.log('Selected first option');
        }
      }
    }

    // Click save button
    const saveBtn = await page.$('.el-dialog .el-button--primary:has-text("保存")');
    if (saveBtn) {
      await saveBtn.click();
      await page.waitForTimeout(2000);
      console.log('Saved');
    }
  }
} else {
  console.log('No edit buttons found (possibly readonly mode)');

  // Try list edit mode first
  const listEditBtn = await page.$('button:has-text("列表编辑")');
  if (listEditBtn) {
    await listEditBtn.click();
    await page.waitForTimeout(1000);
    console.log('Clicked list edit mode');
  }
}

// Now check Gantt chart
await page.locator('.el-tabs__item', { hasText: '甘特图' }).click();
await page.waitForTimeout(2000);

const depSvg = await page.$('.gantt-dep-svg');
console.log('\nAfter setup - Dependency SVG exists:', !!depSvg);

if (depSvg) {
  const lines = await depSvg.$$('path');
  console.log('Dependency lines count:', lines.length);
}

await page.screenshot({ path: 'screenshots/gantt_dep_test.png' });
console.log('Screenshot saved');

await browser.close();
