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

// Check all expand icons
const icons = await page.$$('.el-table__expand-icon');
console.log(`Found ${icons.length} expand icons\n`);

for (let i = 0; i < Math.min(icons.length, 6); i++) {
  const info = await icons[i].evaluate(el => {
    const classes = Array.from(el.classList);
    const isExpanded = classes.includes('el-table__expand-icon--expanded');
    const svg = el.querySelector('.el-icon');
    const svgDisplay = svg ? getComputedStyle(svg).display : 'no-svg';
    const beforeContent = getComputedStyle(el, '::before').content;
    const beforeDisplay = getComputedStyle(el, '::before').display;
    const afterContent = getComputedStyle(el, '::after').content;
    const afterDisplay = getComputedStyle(el, '::after').display;
    const color = getComputedStyle(el).color;
    const fontSize = getComputedStyle(el).fontSize;
    return { classes, isExpanded, svgDisplay, beforeContent, beforeDisplay, afterContent, afterDisplay, color, fontSize };
  });
  console.log(`Icon ${i}: expanded=${info.isExpanded}, classes=${info.classes.join(' ')}`);
  console.log(`  ::before: content="${info.beforeContent}" display=${info.beforeDisplay}`);
  console.log(`  ::after:  content="${info.afterContent}" display=${info.afterDisplay}`);
  console.log(`  svg:      display=${info.svgDisplay}`);
  console.log(`  style:    color=${info.color} fontSize=${info.fontSize}`);
  console.log('');
}

// Now collapse the first row and check again
if (icons.length > 0) {
  console.log('--- Clicking first expand icon to collapse ---');
  await icons[0].click();
  await page.waitForTimeout(500);

  const info = await icons[0].evaluate(el => {
    const classes = Array.from(el.classList);
    const isExpanded = classes.includes('el-table__expand-icon--expanded');
    const beforeContent = getComputedStyle(el, '::before').content;
    const afterContent = getComputedStyle(el, '::after').content;
    return { classes, isExpanded, beforeContent, afterContent };
  });
  console.log(`After collapse: expanded=${info.isExpanded}`);
  console.log(`  ::before: content="${info.beforeContent}"`);
  console.log(`  ::after:  content="${info.afterContent}"`);
}

await browser.close();
