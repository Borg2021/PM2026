# 🌐 API 设计概览

## 基础信息

| 项目 | 内容 |
|-----|------|
| **Base URL** | `http://localhost:9090/api/v1` |
| **认证方式** | JWT Bearer Token (`Authorization: Bearer <token>`) |
| **请求格式** | JSON (`application/json`) |
| **响应格式** | `ApiResponse<T>` 统一封装 |

## 统一响应格式

```json
{
  "code": 200,
  "message": "操作成功",
  "data": { },
  "success": true
}
```

| 字段 | 类型 | 说明 |
|-----|------|------|
| `code` | int | HTTP 状态码或业务码 |
| `message` | string | 提示信息（中文） |
| `data` | T | 业务数据，可能为 null |
| `success` | bool | 是否成功 |

分页接口的 `data` 为 `PagedResult<T>`：
```json
{
  "items": [],
  "totalCount": 100,
  "pageIndex": 1,
  "pageSize": 20
}
```

## 控制器路由表

### Auth — `/api/v1/auth`

| 方法 | 路由 | 认证 | 说明 |
|-----|------|------|------|
| `POST` | `/login` | 无 | 用户登录，返回 JWT |
| `POST` | `/register` | Admin | 管理员注册新用户 |

### Project — `/api/v1/projects`

| 方法 | 路由 | 说明 |
|-----|------|------|
| `GET` | `/projects` | 项目列表（分页 + 筛选 + 数据范围过滤） |
| `GET` | `/projects/{id}` | 项目详情（含字段级脱敏） |
| `POST` | `/projects` | 创建项目 |
| `PUT` | `/projects/{id}` | 更新项目 |
| `PUT` | `/projects/{id}/status` | 变更项目状态 |
| `POST` | `/projects/{id}/copy` | 复制项目 |
| `DELETE` | `/projects/{id}` | 删除项目（Admin 或 PM） |
| | | |
| `GET` | `/projects/{id}/tasks` | 获取项目任务树 |
| `POST` | `/projects/{id}/tasks` | 创建任务 |
| `PUT` | `/projects/{id}/tasks/{taskId}` | 更新任务 |
| `DELETE` | `/projects/{id}/tasks/{taskId}` | 删除任务 |
| `POST` | `/projects/{id}/tasks/from-template` | 从模板批量创建任务 |
| | | |
| `GET` | `/projects/{id}/members` | 获取项目成员 |
| `POST` | `/projects/{id}/members` | 保存项目成员 |
| | | |
| `GET` | `/projects/{id}/milestones` | 获取项目里程碑 |
| `PUT` | `/projects/{id}/milestones` | 保存项目里程碑 |
| | | |
| `GET` | `/projects/{id}/changes` | 获取变更记录 |
| `POST` | `/projects/{id}/changes` | 创建变更记录 |
| `PUT` | `/projects/{id}/changes/{changeId}` | 更新变更记录 |
| `DELETE` | `/projects/{id}/changes/{changeId}` | 删除变更记录 |
| | | |
| `GET` | `/projects/{id}/finance` | 获取财务信息 |
| `PUT` | `/projects/{id}/finance` | 保存财务信息 |
| | | |
| `GET` | `/projects/{id}/files` | 获取文件清单 |
| `POST` | `/projects/{id}/files` | 上传文件（multipart） |
| `DELETE` | `/projects/{id}/files/{fileId}` | 删除文件 |
| `GET` | `/projects/{id}/files/{fileId}/download` | 下载文件 |

### Template — `/api/v1/templates`

| 方法 | 路由 | 说明 |
|-----|------|------|
| `GET` | `/templates` | 模板列表 |
| `GET` | `/templates/{id}` | 模板详情 |
| `POST` | `/templates` | 创建模板 |
| `PUT` | `/templates/{id}` | 更新模板 |
| `DELETE` | `/templates/{id}` | 删除模板 |
| `GET` | `/templates/{id}/plan-nodes` | 获取计划节点树 |
| `PUT` | `/templates/{id}/plan-nodes` | 保存计划节点树 |
| `GET` | `/templates/{id}/milestones` | 获取里程碑列表 |
| `PUT` | `/templates/{id}/milestones` | 保存里程碑列表 |
| `GET` | `/templates/{id}/members` | 获取成员模板 |
| `PUT` | `/templates/{id}/members` | 保存成员模板 |
| `GET` | `/templates/{id}/files` | 获取文件模板 |
| `PUT` | `/templates/{id}/files` | 保存文件模板 |

### PlanBundle — `/api/v1/plan-bundles`

| 方法 | 路由 | 说明 |
|-----|------|------|
| `GET` | `/plan-bundles` | 模板套装列表 |
| `GET` | `/plan-bundles/{id}` | 套装详情 |
| `POST` | `/plan-bundles` | 创建套装 |
| `PUT` | `/plan-bundles/{id}` | 更新套装 |
| `DELETE` | `/plan-bundles/{id}` | 删除套装 |
| `POST` | `/plan-bundles/{id}/assemble/{projectId}` | 装配到项目 |

### System — `/api/v1/system`

> 全部需要 Admin 角色

| 方法 | 路由 | 说明 |
|-----|------|------|
| `GET` | `/system/users` | 用户列表 |
| `POST` | `/system/users` | 创建用户 |
| `PUT` | `/system/users/{id}` | 更新用户 |
| `PUT` | `/system/users/{id}/reset-password` | 重置密码 |
| `DELETE` | `/system/users/{id}` | 删除用户 |
| | | |
| `GET` | `/system/departments` | 部门树 |
| `POST` | `/system/departments` | 创建部门 |
| | | |
| `GET` | `/system/roles` | 角色列表 |
| | | |
| `GET` | `/system/permissions` | 权限树 |
| `PUT` | `/system/roles/{id}/permissions` | 分配角色权限 |
| | | |
| `GET` | `/system/dict-types` | 字典类型列表 |
| `GET` | `/system/dicts/{typeCode}` | 字典项列表 |
| | | |
| `GET` | `/system/sys-params` | 系统参数列表 |
| `PUT` | `/system/sys-params` | 更新系统参数 |

### Common — `/api/v1`

| 方法 | 路由 | 说明 |
|-----|------|------|
| `GET` | `/users/current` | 获取当前登录用户信息 |
| `GET` | `/users/current/permissions` | 获取当前用户权限码集合 |
| `GET` | `/departments` | 部门列表 |
| `GET` | `/functions` | 岗位列表 |
| `GET` | `/dicts/{typeCode}` | 按类型获取字典 |
| `GET` | `/role-dicts` | 角色字典 |

## 错误处理

全局异常中间件统一处理：

| 异常类型 | HTTP 状态码 | 说明 |
|---------|-----------|------|
| `InvalidOperationException` | 400 | 业务逻辑错误（中文提示） |
| 未认证 | 401 | Token 无效或过期 |
| 权限不足 | 403 | 无对应角色/权限 |
| 其他未处理异常 | 500 | 返回通用错误信息 |

## 认证约定

- 登录成功返回的 JWT 有效期 24 小时
- 前端存储于 Pinia store（内存），刷新后需重新登录
- 文件下载接口支持 `?access_token=<token>` 查询参数（绕过 Authorization header 限制）
- 前端 Axios 拦截器自动附加 `Authorization: Bearer` 头
