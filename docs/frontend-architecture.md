# 🖥 前端架构

## 技术栈

| 类别 | 选型 | 版本 |
|-----|------|------|
| 框架 | Vue 3 (Composition API) | ^3.x |
| 语言 | TypeScript | ^5.x |
| 构建 | Vite | ^5.x |
| UI 库 | Element Plus | ^2.x |
| 状态管理 | Pinia | ^2.x |
| 路由 | Vue Router | ^4.x |
| HTTP | Axios | ^1.x |
| 导出 | xlsx | ^0.18 |

---

## 目录结构

```
frontend/
├── index.html
├── package.json
├── vite.config.ts
├── tsconfig.json
│
├── public/
│
└── src/
    ├── main.ts                    ← 入口：挂载 App + Plugin 注册
    ├── App.vue                    ← 根组件
    │
    ├── api/                       ← API 层
    │   ├── request.ts             ← Axios 实例 + 拦截器
    │   ├── auth.ts                ← 认证 API
    │   ├── project.ts             ← 项目 CRUD API
    │   ├── template.ts            ← 模板 API
    │   └── system.ts              ← 系统管理 API
    │
    ├── store/                     ← Pinia 状态
    │   └── auth.ts                ← 认证状态 (token, permissions, menus, pendingCounts)
    │
    ├── router/                    ← 路由配置
    │   └── index.ts               ← 路由表 + 导航守卫
    │
    ├── types/                     ← TypeScript 类型
    │   ├── project.ts             ← 项目相关接口
    │   ├── template.ts            ← 模板相关接口
    │   └── system.ts              ← 系统管理接口
    │
    ├── config/                    ← 配置
    │   └── menu.ts                ← 静态菜单配置
    │
    ├── directives/                ← 自定义指令
    │   └── permission.ts          ← v-permission 指令
    │
    ├── utils/                     ← 工具函数
    │   ├── dateUtils.ts           ← 日期格式化
    │   ├── projectNav.ts          ← 项目页面导航辅助
    │   ├── deptTree.ts            ← 部门树构建
    │   └── preTaskHelpers.ts      ← 前置任务解析
    │
    └── views/                     ← 页面组件
        ├── Login.vue
        ├── Workbench.vue
        ├── ProjectList.vue
        ├── ProjectEditor.vue      ← 项目详情/编辑（含 Tab 容器）
        ├── TaskManagement.vue     ← 跨项目甘特图
        ├── ProjectFiles.vue       ← 跨项目文件管理
        ├── TemplateList.vue
        ├── PlanTemplateEditor.vue
        ├── MilestoneTemplateEditor.vue
        ├── MemberTemplateEditor.vue
        ├── FileTemplateEditor.vue
        ├── PlanBundleList.vue
        ├── PlanBundleEditor.vue
        ├── UserManagement.vue
        ├── DepartmentManagement.vue
        ├── FunctionManagement.vue
        ├── PermissionManagement.vue
        ├── DictManagement.vue
        ├── DictTypeManagement.vue
        ├── SysParamManagement.vue
        └── UserSettings.vue
```

---

## 数据流

```
用户操作 → Vue Component
              │
              ├─ dispatch(action) → Pinia Store → 更新响应式状态
              │       │
              │       └─ API call (axios) → Backend
              │              │
              │              └─ 响应 → 更新 Store → 组件重渲染
              │
              └─ 或直接 API call（轻量场景）
```

## 路由守卫流程

```
router.beforeEach(to, from, next)
  │
  ├─ to.path === '/login' ?
  │   └─ 有 token → 跳到 /workbench
  │   └─ 无 token → 允许
  │
  ├─ 无 token？
  │   └─ 跳到 /login
  │
  └─ 有 token？
      ├─ 无用户信息 → 调用 /users/current 获取
      ├─ 无权限列表 → 调用 /users/current/permissions 获取
      ├─ 检查页面权限 → 无权限 → /403
      └─ 允许访问
```

## Pinia Auth Store

```typescript
// store/auth.ts
interface AuthState {
  token: string | null
  user: UserInfo | null
  permissions: string[]        // 权限码集合
  menus: MenuItem[]            // 根据权限过滤的菜单
  pendingTaskCount: number     // 待办任务数
  pendingFileCount: number     // 待传文件数
}
```

## v-permission 指令

```html
<!-- 无 project:create 权限时自动移除该按钮 -->
<el-button v-permission="'project:create'" @click="create">新建项目</el-button>
```

实现：
```typescript
// directives/permission.ts
mounted(el, binding) {
  const authStore = useAuthStore()
  if (!authStore.permissions.includes(binding.value)) {
    el.parentNode?.removeChild(el)  // 无权限则移除 DOM
  }
}
```

## Axios 拦截器

```typescript
// 请求拦截
request.interceptors.request.use(config => {
  if (authStore.token) {
    config.headers.Authorization = `Bearer ${authStore.token}`
  }
  return config
})

// 响应拦截
request.interceptors.response.use(
  response => response.data,           // 自动解包 ApiResponse
  error => {
    if (error.response?.status === 401) {
      authStore.logout()
      router.push('/login')
    }
    return Promise.reject(error)
  }
)
```

## 构建与部署

- **开发模式**：`vite dev` 启动 HMR 开发服务器
- **生产构建**：`vite build` → 输出到 `dist/`
- **部署**：`dist/` 内容复制到 `backend/ProjectManagement.API/wwwroot/`
- **SPA 回退**：后端配置 `MapFallbackToFile("index.html")` 处理 Vue Router 的 history 模式
