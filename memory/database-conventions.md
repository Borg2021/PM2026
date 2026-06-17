---
name: database-conventions
description: 数据库和 EF Core 的使用约定
metadata:
  type: project
---

# 数据库约定

## EF Core 配置
- **不使用 Migration**：使用 `EnsureCreated()` 创建数据库 + 原始 SQL 修补
- **Split Query 默认开启**：避免多表 JOIN 造成笛卡尔积
- **SQLite → SQL Server 迁移**：`SqlServerDataMigrator` 实现数据迁移

## 连接字符串
- 配置键：`DefaultConnection`
- 格式：`Server=localhost;Database=ProjectManagement;Trusted_Connection=true;TrustServerCertificate=true`

## 种子数据 (`DbInitializer.Seed()`)
启动时自动初始化：
- 部门树（6 个部门）
- 角色字典（10 个）
- 字典类型（3 个）+ 条目
- 权限树（60+ 节点）
- RBAC 角色（6 个）+ 角色-权限映射

## 兼容性修补 (`EnsureSeedAsync()`)
运行时自动处理：
- 添加缺失的数据库列
- 修复 Function Code（使用迁移代码将旧的 Function 引用更新）
- 确保管理员用户存在
- 补充缺失的角色分配

**Why:** Migration 在多人协作中容易产生冲突；对于内部项目，EnsureCreated + 运行时修补更简单直接。参见 [[tech-stack]]。
