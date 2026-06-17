---
name: clean-architecture-layers
description: 四层架构中各层的职责和约定
metadata:
  type: project
---

# Clean Architecture 分层约定

## Domain 层（ProjectManagement.Domain）
- **只含纯 C# 类型**：实体、枚举、仓储接口
- **不引用任何外部包**（无 EF Core、无 FluentValidation）
- 实体是 POCO（Plain Old CLR Object），不含业务逻辑方法
- 仓储接口只定义契约，不定义实现

## Application 层（ProjectManagement.Application）
- **CQRS 模式**：每个操作 = Command/Query + Handler + DTO
- 按功能模块分目录：`Projects/`、`Templates/`、`PlanBundles/`、`System/`、`Auth/`
- 每个模块下分：`Commands/`、`Queries/`、`DTOs/`
- Handler 中编排业务逻辑，通过仓储接口访问数据
- FluentValidation Validator 与 Command 同目录

## Infrastructure 层（ProjectManagement.Infrastructure）
- 实现 Domain 中定义的仓储接口
- **AppDbContext** — 唯一的数据上下文
- **DbInitializer** — 种子数据 + 运行时兼容性修补
- **SqlServerDataMigrator** — SQLite → SQL Server 迁移工具
- 数据范围过滤在 `ProjectDataScopeFilter` 中实现

## API 层（ProjectManagement.API）
- Controller 极简：只做参数绑定 + 调用 MediatR
- 自定义认证在 `Auth/` 目录
- 全局异常处理在 `Middleware/ExceptionMiddleware`
- 前端 SPA 构建产物在 `wwwroot/`

**Why:** 严格分层使每层可独立测试，Domain 不依赖框架使核心逻辑不随框架升级而变化。
