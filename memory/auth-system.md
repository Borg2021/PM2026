---
name: auth-system
description: 认证授权系统的设计决策和实现要点
metadata:
  type: project
---

# 认证授权系统

## 设计决策

### 为什么不用 ASP.NET Core 原生 Policy？
原生 `[Authorize(Policy = "...")]` 需要提前注册所有 Policy 名称。本系统有 60+ 动态权限码，通过自定义 `PermissionAuthorizationPolicyProvider` 实现运行时动态解析。

### 四层权限模型
1. **JWT 认证** — 确认身份
2. **权限策略** — `[RequirePermission("code")]` → 动态评估 RBAC 权限
3. **数据范围** — `DataScopeType` 控制行级可见性
4. **字段脱敏** — 19 个敏感字段按权限码返回 `"***"`

### Admin 绕过
`PermissionAuthorizationHandler` 中 Admin 角色直接通过，不查询权限表。

### 文件下载 Token 传递
文件下载时浏览器不会自动带 `Authorization` header，所以支持 `?access_token=xxx` 查询参数。

## 权限命名约定
- 格式：`{module}:{action}` 或 `{module}:{sub}:{action}`
- 例：`project:create`、`project:view:finance`、`system:user:delete`
- 字段级权限以 `view:` 开头

## 角色数据范围
| 角色 | DataScope | 典型场景 |
|-----|-----------|---------|
| 系统管理员 | All | 全部数据 |
| 模板管理员 | All | 管理模板，查看项目 |
| 项目经理 | ProjectManagerOwn | 只看自己管理的项目 |

**Why:** 通用 RBAC 库无法同时满足动态权限码 + 数据范围 + 字段脱敏的复合需求，自定义实现更灵活。参见 [[clean-architecture-layers]]。
