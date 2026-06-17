---
name: frontend-conventions
description: 前端代码约定和组件组织方式
metadata:
  type: project
---

# 前端约定

## 组件组织
- **views/** — 页面级组件（一个路由对应一个 view）
- **子组件** — 与 view 同目录（如 `ProjectChangeTab.vue` 与 `ProjectEditor.vue` 同级）
- **共享组件** — 无独立组件目录，直接放 views 下

## 状态管理
- **Pinia Store** — 只有一个 `auth.ts` store
- 不创建多个 store — 认证状态集中管理
- 业务数据由各页面自行通过 API 获取，不存入全局状态

## 路由守卫
- `beforeEach` 中检查 token → 用户信息 → 权限列表 → 菜单过滤
- 菜单根据用户权限动态生成（不是静态全量菜单）
- 无权限页面跳转 `/403`

## 权限控制
- **路由级**：`meta.permission` 控制页面访问
- **DOM 级**：`v-permission` 指令控制按钮/元素显隐
- **API 级**：后端二次校验（前端权限控制仅优化体验）

## API 调用
- Axios 拦截器自动解包 `ApiResponse<T>` → 直接返回 `data`
- 401 自动跳转登录页
- 请求头自动附加 JWT

**Why:** 集中的 auth store 避免状态分散；每个页面独立请求数据保证数据新鲜度。参见 [[auth-system]]。
