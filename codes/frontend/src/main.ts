import { createApp } from 'vue'
import { createPinia } from 'pinia'
import ElementPlus from 'element-plus'
import zhCn from 'element-plus/es/locale/lang/zh-cn'
import * as ElementPlusIconsVue from '@element-plus/icons-vue'
import App from './App.vue'
import router from './router'
import { vPermission } from './directives/permission'

const app = createApp(App)
app.use(createPinia())
app.use(router)
app.use(ElementPlus, { locale: zhCn })
app.directive('permission', vPermission)

// 仅注册实际使用的图标（按需注册，减少启动开销）
const usedIcons = [
  'ArrowDown', 'ArrowLeft', 'ArrowUp', 'Check', 'CircleCheckFilled', 'Clock',
  'Collection', 'Delete', 'Document', 'Expand', 'Folder', 'FolderOpened',
  'Fold', 'List', 'Lock', 'Monitor', 'OfficeBuilding', 'Operation',
  'Plus', 'Reading', 'Setting', 'Stamp', 'Tools', 'User', 'WarningFilled',
] as const
for (const key of usedIcons) {
  const component = (ElementPlusIconsVue as Record<string, unknown>)[key]
  if (component) app.component(key, component)
}

// 屏蔽 Element Plus el-form label-width 计算时的无害警告
const origWarn = console.warn
console.warn = (...args: any[]) => {
  try {
    const msg = String(args[0] ?? '')
    if (msg.includes('[ElForm] unexpected width 0')) return
  } catch { /* args[0] 无法转字符串时忽略，继续透传 */ }
  try {
    origWarn(...args)
  } catch { /* Vite 客户端包装 console 时可能无法序列化某些对象，静默忽略 */ }
}

// 屏蔽浏览器扩展导致的 message channel 错误（非应用代码问题）
window.addEventListener('unhandledrejection', (e) => {
  const reason = e.reason
  if (typeof reason === 'string' && reason.includes('message channel closed')) { e.preventDefault(); return }
  if (reason?.message?.includes('message channel closed')) e.preventDefault()
})

app.mount('#app')
