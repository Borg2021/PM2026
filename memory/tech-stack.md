---
name: tech-stack
description: 项目技术栈选型与版本信息
metadata:
  type: project
---

# 技术栈

## 后端
- **.NET 8** — 长期支持版本 (LTS)，Clean Architecture + CQRS
- **MediatR** — 命令查询职责分离，减少 Controller 臃肿
- **EF Core 8** — ORM，使用 EnsureCreated + 原始 SQL（非 Migration）
- **SQL Server** — 主数据库，支持从 SQLite 迁移
- **JWT Bearer** — 无状态认证，24h 过期
- **BCrypt.Net-Next** — 密码哈希
- **FluentValidation** — 输入校验
- **AutoMapper** — DTO 映射

## 前端
- **Vue 3** (Composition API) + TypeScript
- **Element Plus** — UI 库，中文 locale
- **Pinia** — 状态管理
- **Vue Router 4** — 路由 + 权限守卫
- **Axios** — HTTP 客户端
- **Vite** — 构建工具
- **xlsx** — Excel 导出

**Why:** .NET 8 是当前 LTS 版本，Clean Architecture 保证项目可测试性和可维护性；Vue 3 + Element Plus 适合企业后台管理系统的快速开发。
