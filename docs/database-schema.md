# 🗄 数据库模型

## 技术信息

| 项目 | 内容 |
|-----|------|
| **数据库** | SQL Server |
| **ORM** | Entity Framework Core 8 |
| **初始化方式** | `EnsureCreated()` + 原始 SQL（非 Migration） |
| **查询策略** | 默认 Split Query（避免笛卡尔积） |
| **连接字符串键** | `DefaultConnection` |

> 系统支持从 SQLite 迁移到 SQL Server（通过 `SqlServerDataMigrator`）。

---

## 实体关系图（核心部分）

```
┌──────────┐     ┌──────────────┐     ┌──────────────┐
│   User   │────→│   UserRole   │←────│    Role      │
└──────────┘     └──────────────┘     └──────────────┘
      │                                      │
      │                              ┌───────┴────────┐
      │                              │  RolePermission │
      │                              └───────┬────────┘
      │                                      │
      ▼                                  ┌───┴──────┐
┌──────────┐                             │Permission │
│  Project │←── ProjectMember            └──────────┘
└──────────┘
      │
      ├── ProjectTask (WBS 任务树)
      ├── ProjectMilestone
      ├── ProjectFileItem → ProjectFileVersion
      ├── ProjectChange
      ├── ProjectFinance → PlanReceipt / Receipt / Invoice
      ├── ProjectProduct
      └── OperationLog
```

---

## 核心实体概览（30 个）

### 👤 用户与组织

| 实体 | 关键字段 | 说明 |
|-----|---------|------|
| `User` | Username, Password(BCrypt), RealName, Role, DepartmentId | 系统用户 |
| `Department` | Name, ParentId, Code, SortOrder | 组织架构树 |
| `Function` | Name, Code | 岗位/职能 |
| `UserFunction` | UserId, FunctionId | 用户-职能多对多 |
| `UserRole` | UserId, RoleId | 用户-RBAC角色多对多 |
| `RoleDict` | Name, Code | 项目成员角色字典 |

### 🔐 RBAC 权限

| 实体 | 关键字段 | 说明 |
|-----|---------|------|
| `Permission` | Code, Name, ParentId, Type(Menu/Button), SortOrder | 权限树 |
| `Role` | Name, Code, DataScope, IsAdmin | RBAC 角色 |
| `RolePermission` | RoleId, PermissionId | 角色-权限多对多 |

### 📁 项目核心

| 实体 | 关键字段 | 说明 |
|-----|---------|------|
| `Project` | Code, Name, Type, Status(0-3), CustomerName, SalesManagerId, ProjectManagerId | 项目主表 |
| `ProjectMember` | ProjectId, UserId, Role, FunctionName, DepartmentName | 项目成员 |
| `ProjectProduct` | ProjectId, Name, Model, Quantity, UnitPrice | 产品清单 |
| `OperationLog` | ProjectId, UserId, Operation, BeforeData, AfterData(JSON) | 操作审计日志 |

### 📋 WBS 任务

| 实体 | 关键字段 | 说明 |
|-----|---------|------|
| `ProjectTask` | ProjectId, WbsCode, Name, Status, Priority, StartDate, EndDate, Duration, Progress, ParentId, PreconditionCodes | WBS 任务节点 |

### 🏷 里程碑

| 实体 | 关键字段 | 说明 |
|-----|---------|------|
| `ProjectMilestone` | ProjectId, Name, Date, Status, CompletedDate | 项目里程碑 |

### 📄 文件管理

| 实体 | 关键字段 | 说明 |
|-----|---------|------|
| `ProjectFile` | ProjectId, FileName, FilePath | 简易文件（旧版） |
| `ProjectFileItem` | ProjectId, TemplateItemId, Name, IsPublic, Required, Status | 结构化文件节点 |
| `ProjectFileVersion` | FileItemId, FileName, FileSize, Extension, VersionNumber, UploaderId | 文件版本 |

### 🔄 变更管理

| 实体 | 关键字段 | 说明 |
|-----|---------|------|
| `ProjectChange` | ProjectId, ChangeType, Title, Status, RequestDate | 变更请求/通知 |

### 💰 财务管理

| 实体 | 关键字段 | 说明 |
|-----|---------|------|
| `ProjectFinance` | ProjectId, ContractAmount, TaxRate, Currency, PaymentMethod | 财务主信息 |
| `ProjectPlanReceipt` | FinanceId, Amount, PlannedDate | 计划收款 |
| `ProjectReceipt` | FinanceId, Amount, ReceiptDate | 实际收款 |
| `ProjectInvoice` | FinanceId, InvoiceNo, Amount, InvoiceDate | 发票记录 |

### 📐 模板系统

| 实体 | 关键字段 | 说明 |
|-----|---------|------|
| `Template` | Name, Code, TemplateType(1-4), Description | 模板主表 |
| `PlanNode` | TemplateId, Name, ParentId, Duration, SortOrder | 计划节点树 |
| `PlanNodeDependency` | PredecessorId, SuccessorId | 节点依赖 |
| `Milestone` | TemplateId, Name, DurationDays, SortOrder | 里程碑模板 |
| `TemplateMember` | TemplateId, RoleName, FunctionName | 成员角色模板 |
| `FileTemplateItem` | TemplateId, Name, IsPublic, Required | 文件要求模板 |
| `PlanBundle` | Name, Description | 模板套装 |
| `PlanBundleItem` | BundleId, TemplateId, SortOrder | 套装-模板关联 |

### 📚 系统配置

| 实体 | 关键字段 | 说明 |
|-----|---------|------|
| `DictType` | Code, Name | 字典类型 |
| `DictItem` | TypeCode, Key, Value, SortOrder | 字典条目 |
| `SysParam` | Key, Value, Description | 系统参数 KV |

---

## 枚举值速查

| 枚举 | 值 | 含义 |
|-----|---|------|
| `ProjectStatus` | 0/1/2/3 | 未激活/进行中/已完成/已暂停 |
| `UserRole` (Enum) | 1/2/3 | Admin/TemplateAdmin/User |
| `TemplateType` | 1/2/3/4 | 计划/里程碑/成员/文件 |
| `NodeType` | 1/2 | 计划节点/任务节点 |
| `DataScopeType` | 1-6 | 全部/本部门/部门及子级/仅本人/仅成员/PM自有 |
