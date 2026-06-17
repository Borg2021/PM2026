# 📋 项目管理系统 (Project Management System)

> 面向中大企业的工程/销售类项目全生命周期管理平台，覆盖招投标、交付、财务、文档四大核心流程。

---

## 🧭 功能地图

```
┌──────────────────────────────────────────────────────────────┐
│                       🔐 登录 / JWT 认证                      │
├────────────┬────────────┬────────────┬──────────────────────┤
│  📊 工作台  │  📁 项目管理 │  📐 模板管理 │  ⚙️ 系统管理         │
│  待办任务   │  项目CRUD  │  计划模板  │  用户 / 部门 / 岗位   │
│  待传文件   │  WBS 任务  │  里程碑模板 │  角色 / 权限 / RBAC  │
│  统计看板   │  里程碑    │  成员模板  │  字典 / 系统参数     │
│            │  文件版本化  │  文件模板  │  操作日志审计        │
│            │  财务收支   │  模板套装  │                    │
│            │  变更管理   │           │                    │
│            │  甘特图     │           │                    │
└────────────┴────────────┴────────────┴──────────────────────┘
```

---

## 🏗 技术栈

| 层 | 技术选型 |
|---|---------|
| **后端框架** | .NET 8 · ASP.NET Core Web API |
| **架构模式** | Clean Architecture + CQRS (MediatR) |
| **数据库** | SQL Server · EF Core 8 |
| **认证授权** | JWT Bearer · 自定义 RBAC 权限策略 |
| **前端框架** | Vue 3 + TypeScript |
| **UI 组件库** | Element Plus (中文 locale) |
| **状态管理** | Pinia |
| **构建工具** | Vite |

---

## 📂 项目结构

```
项目管理系统V1/
├── README.md                  ← 📖 项目入口（你在这里）
├── CLAUDE.md                  ← 🤖 AI 协作守则
│
├── docs/                      ← 📝 设计文档
│   ├── architecture.md        ← 系统架构
│   ├── api-overview.md        ← API 设计概览
│   ├── database-schema.md     ← 数据库模型
│   ├── auth-and-permissions.md← 认证与权限体系
│   └── frontend-architecture.md← 前端架构
│
├── codes/                     ← 📦 源码
│   ├── backend/
│   │   ├── ProjectManagement.Domain        ← 领域层：实体、枚举、仓储接口
│   │   ├── ProjectManagement.Application   ← 应用层：CQRS 命令/查询、DTO
│   │   ├── ProjectManagement.Infrastructure← 基础设施层：EF Core、仓储实现
│   │   └── ProjectManagement.API           ← 表现层：控制器、中间件、认证
│   └── frontend/              ← Vue 3 SPA
│
├── tests/                     ← 🧪 测试
│   ├── backend/
│   │   ├── ProjectManagement.UnitTests
│   │   └── ProjectManagement.IntegrationTests
│   └── frontend/
│
└── memory/                    ← 🧠 AI 持久化记忆
    └── MEMORY.md              ← 记忆索引
```

---

## 🚀 快速启动

### 环境要求

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 18+](https://nodejs.org/)
- SQL Server (LocalDB 或完整实例)

### 后端启动

```bash
cd codes/backend/ProjectManagement.API
# 修改 appsettings.json 中的连接字符串
dotnet run
# API → http://localhost:9090 · Swagger → http://localhost:9090/swagger
```

### 前端启动（开发模式）

```bash
cd codes/frontend
npm install
npm run dev
# 开发服务器 → http://localhost:5173
```

### 默认账户

| 角色 | 用户名 | 说明 |
|-----|-------|------|
| 系统管理员 | `admin` | 初始密码见部署文档 |

> 首次启动会自动创建数据库、生成自签名 HTTPS 证书、并初始化种子数据（部门/角色/权限/字典）。

---

## 🔐 权限体系速览

系统实现了 **自定义 RBAC + 数据范围** 的复合权限模型：

```
JWT Token → User → Roles → Permissions → 菜单 + 按钮 + 字段级控制
                                    └→ DataScope → 行级数据过滤
```

- **6 个预置角色**：系统管理员 / 模板管理员 / 项目管理员 / 项目经理 / 任务管理 / 普通用户
- **60+ 粒度权限**：按树形组织（菜单→页面→操作）
- **5 级数据范围**：全部 / 本部门 / 部门及子级 / 仅本人 / 项目成员
- **19 个字段级脱敏**：合同编号、客户名称、财务数据等可按角色控制可见性

详见 [`docs/auth-and-permissions.md`](docs/auth-and-permissions.md)

---

## 🧩 核心概念

| 概念 | 说明 |
|-----|------|
| **Project** | 项目主体，含基本信息、成员、产品、任务、里程碑、财务、变更 |
| **Template** | 可复用模板（计划节点树 / 里程碑 / 成员角色 / 文件要求），共 4 种类型 |
| **PlanBundle** | 模板套装，将多个模板打包，一键装配到项目中生成 WBS 任务树 |
| **ProjectTask** | 项目级 WBS 任务，支持前置依赖、进度跟踪、甘特图展示 |
| **ProjectFileItem** | 从文件模板生成的版本化文件节点，按角色控制读写权限 |

---

## 📄 许可

内部项目，未开源。

---

> 📌 新成员请先阅读 [`CLAUDE.md`](CLAUDE.md)（AI 协作规则）和 [`docs/architecture.md`](docs/architecture.md)（系统架构）。
