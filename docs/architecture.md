# 🏗 系统架构

## 架构风格：Clean Architecture + CQRS

```
┌─────────────────────────────────────────────────────────────┐
│                    ProjectManagement.API                      │
│   Controllers · Middleware · Auth · wwwroot (SPA)            │
│   [表现层：HTTP 请求/响应、路由、认证、SPA 静态托管]              │
├─────────────────────────────────────────────────────────────┤
│                 ProjectManagement.Application                  │
│   CQRS Handlers · DTOs · Validators · AutoMapper Profiles    │
│   [应用层：业务编排、命令分发、DTO 映射、参数校验]               │
├─────────────────────────────────────────────────────────────┤
│                   ProjectManagement.Domain                     │
│   Entities · Enums · Repository Interfaces · DataScope       │
│   [领域层：核心业务实体、枚举、仓储契约、数据范围定义]            │
├─────────────────────────────────────────────────────────────┤
│               ProjectManagement.Infrastructure                 │
│   AppDbContext · Repository Impl · DbInitializer · Migrator  │
│   [基础设施层：EF Core、数据库初始化、仓储实现、SQL 迁移]        │
└─────────────────────────────────────────────────────────────┘
```

## 依赖方向

```
API → Application → Domain ← Infrastructure
                  └──────────────────↑ (DI 注入时反转)
```

- **Domain** 不依赖任何外部项目，只含纯 C# 类型
- **Application** 只依赖 Domain，通过接口定义所需服务
- **Infrastructure** 实现 Domain 中定义的仓储接口
- **API** 组合所有层，通过依赖注入连接接口与实现

## 请求处理流程

```
HTTP Request
  │
  ├─ ExceptionMiddleware      ← 全局异常捕获
  │
  ├─ JWT Authentication       ← Token 校验
  │
  ├─ Permission AuthZ         ← 自定义权限策略评估
  │     └─ PermissionAuthorizationHandler → DB查询角色权限
  │
  ├─ Controller               ← 参数绑定、路由匹配
  │
  ├─ MediatR Dispatcher       ← 命令/查询分发
  │     └─ IRequestHandler<T>  ← 业务逻辑执行
  │           └─ Repository   ← 数据访问
  │
  └─ ApiResponse<T>           ← 统一响应格式
```

## 后端项目引用关系

| 项目 | 引用 |
|-----|------|
| `ProjectManagement.Domain` | 无（纯类库） |
| `ProjectManagement.Application` | Domain · AutoMapper · BCrypt.Net-Next · FluentValidation · MediatR |
| `ProjectManagement.Infrastructure` | Domain · EF Core · SqlServer · Sqlite |
| `ProjectManagement.API` | Application · Infrastructure · JwtBearer · Swashbuckle |

## 前端架构

```
Vue 3 SPA (Vite 构建)
  │
  ├─ Pinia Store             ← 全局状态（auth、permissions、pending counts）
  │
  ├─ Vue Router              ← 路由 + 权限导航守卫
  │
  ├─ Axios (request.ts)      ← HTTP 客户端 + JWT 拦截器 + 错误处理
  │
  ├─ Element Plus            ← UI 组件库（中文 locale）
  │
  └─ Directives              ← v-permission 自定义指令（DOM 级权限控制）
```

构建产物输出到 `backend/ProjectManagement.API/wwwroot/`，前后端统一部署。

## 部署架构

```
┌──────────────────────────────────┐
│          Kestrel (单进程)          │
│  HTTP :9090  ·  HTTPS :9091      │
│  ┌────────────────────────────┐  │
│  │   ASP.NET Core Web API     │  │
│  │   + SPA 静态文件服务        │  │
│  └────────────────────────────┘  │
│              │                    │
│         SQL Server                │
└──────────────────────────────────┘
```

- 启动时自动生成自签名 HTTPS 证书
- 上传文件大小限制由 `SysParams` 动态配置（30 秒缓存）
- 文件下载支持 `?access_token=` 查询参数传递 JWT
