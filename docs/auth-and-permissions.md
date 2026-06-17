# 🔐 认证与权限体系

## 概述

系统实现了 **JWT Bearer 认证 + 自定义 RBAC 权限策略 + 数据范围过滤 + 字段级脱敏** 的四层安全模型。

---

## 第一层：JWT 认证

### Token 签发 (登录)

```
POST /api/v1/auth/login
Body: { "username": "admin", "password": "xxx" }

→ BCrypt 密码验证
→ 生成 JWT Token (24h 过期)
→ Claims: { sub: userId, name: username, role: "admin" }
→ 返回 { token, user }
```

### Token 验证

- 密钥来自 `appsettings.json` 的 `Jwt:Key`
- ASP.NET Core JWT Bearer 中间件自动校验签名和过期时间
- 文件下载额外支持 `?access_token=<token>` 查询参数

---

## 第二层：自定义权限策略

### 架构

```
[RequirePermission("project:create")]
        │
        ▼
PermissionAuthorizationPolicyProvider
  动态生成 Policy "permission:project:create"
        │
        ▼
PermissionAuthorizationHandler
  查询当前用户的 RBAC 权限集合
  判断是否包含 "project:create"
        │
   ┌────┴────┐
   ▼         ▼
 通过       403
```

### RequirePermissionAttribute

```csharp
// 标记方式，类似 [Authorize(Roles = "...")]
[RequirePermission("project:create")]
public async Task<ApiResponse<int>> Create(CreateProjectCommand command)
```

- Admin 角色**自动跳过**所有权限检查
- 权限码以 `:` 分隔，按功能模块层级命名

### 权限树（60+ 节点）

```
📁 dashboard          ← 工作台
📁 project            ← 项目管理
  ├─ project:list     ← 项目列表
  ├─ project:create   ← 创建项目
  ├─ project:edit     ← 编辑项目
  ├─ project:delete   ← 删除项目
  ├─ project:view:contractCode    ← 查看合同编号（字段级）
  ├─ project:view:customerName    ← 查看客户名称（字段级）
  ├─ project:view:finance         ← 查看财务数据（字段级）
  ├─ ... (更多字段级权限)
  ├─ project:task:*    ← 任务管理
  ├─ project:file:*    ← 文件管理
  ├─ project:change:*  ← 变更管理
  └─ project:finance:* ← 财务管理
📁 template            ← 模板管理
📁 system              ← 系统管理
  ├─ system:user:*
  ├─ system:role:*
  └─ ...
```

---

## 第三层：数据范围过滤

### 6 级数据范围

| 级别 | 枚举值 | 含义 | 查询逻辑 |
|-----|--------|------|---------|
| **全部** | `All = 1` | 所有数据 | 不过滤 |
| **本部门** | `Dept = 2` | 仅本部门用户的数据 | `WHERE deptId = user.deptId` |
| **部门及子级** | `DeptAndChildren = 3` | 本部门 + 所有下级部门 | `WHERE deptId IN (deptId, childIds...)` |
| **仅本人** | `Self = 4` | 仅自己的数据 | `WHERE userId = currentUserId` |
| **仅项目成员** | `MemberOnly = 5` | 仅参与的项目 | `WHERE projectId IN (memberProjectIds)` |
| **PM 自有项目** | `ProjectManagerOwn = 6` | 仅自己管理的项目 | `WHERE projectManagerId = currentUserId` |

### 数据范围来源

```
角色 DataScope (默认) ──→ 用户 DataScopeOverride (可覆盖) ──→ 有效 DataScope
```

- 用户可配置 `DataScope` 覆盖角色的默认数据范围
- 反映在 `ProjectDataScopeFilter` 的 EF Core 查询过滤中

---

## 第四层：字段级脱敏

项目详情接口返回时，按用户权限动态过滤 19 个敏感字段：

| 字段 | 权限码 |
|-----|--------|
| 合同编号 | `project:view:contractCode` |
| 客户名称 | `project:view:customerName` |
| 联系人信息 | `project:view:contact` |
| 销售金额 | `project:view:salesAmount` |
| 财务数据 | `project:view:finance` |
| ... (更多) | |

> 无对应权限时，字段值返回 `"***"` 而非真实数据。

---

## 预置 RBAC 角色

| 角色 | DataScope | 典型权限 |
|-----|-----------|---------|
| **系统管理员** | All | 全部权限 (Admin 标记) |
| **模板管理员** | All | 模板 CRUD + 项目查看 |
| **项目管理员** | DeptAndChildren | 本部门项目全量管理 |
| **项目经理** | ProjectManagerOwn | 自有项目的任务/文件/变更管理 |
| **任务管理** | MemberOnly | 参与项目的任务查看和更新 |
| **普通用户** | Self | 个人工作台、文件上传 |

---

## 前端权限集成

```
路由守卫 (beforeEach)
  └→ 无 token → 跳转登录
  └→ 无页面权限 → 跳转 403
  └→ 动态生成侧边栏菜单（根据权限过滤）

v-permission 指令
  └→ 无按钮权限 → 隐藏 DOM 元素

API 拦截器
  └→ 401 → 跳转登录
  └→ 自动附加 Authorization header
```
