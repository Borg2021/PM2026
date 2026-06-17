# 🧪 测试

## 目录结构

```
tests/
├── README.md                                      ← 本文件
│
├── backend/
│   ├── ProjectManagement.UnitTests/               ← 单元测试
│   │   ├── ProjectManagement.UnitTests.csproj
│   │   ├── Domain/                                ← 领域逻辑测试
│   │   │   ├── ProjectTests.cs                    ← 项目实体行为
│   │   │   └── DataScopeTests.cs                  ← 数据范围逻辑
│   │   ├── Application/                           ← 应用层测试
│   │   │   ├── Projects/                          ← 项目 CQRS 测试
│   │   │   └── Templates/                         ← 模板 CQRS 测试
│   │   └── GlobalUsings.cs
│   │
│   └── ProjectManagement.IntegrationTests/        ← 集成测试
│       ├── ProjectManagement.IntegrationTests.csproj
│       └── Controllers/                           ← API 控制器集成测试
│
└── frontend/                                      ← 前端测试
    ├── unit/                                      ← Vitest 组件/工具测试
    └── e2e/                                       ← Playwright E2E 测试
```

## 测试原则

遵循 [`CLAUDE.md`](../CLAUDE.md) 第 9 条：

1. **测试核心行为**，不测试实现细节
2. **断言必须能失败**，不写永远 true 的判断
3. **不 mock 一切**，仅 mock 外部依赖（数据库、文件系统、HTTP）
4. **不跳过关键用例**：正常路径、边界值、异常路径、权限边界

## 运行

### 后端单元测试

```bash
cd tests/backend/ProjectManagement.UnitTests
dotnet test
```

### 后端集成测试

```bash
cd tests/backend/ProjectManagement.IntegrationTests
dotnet test
```

### 前端测试

```bash
cd codes/frontend
npx vitest run          # 单元测试
npx playwright test     # E2E 测试
```

## 命名约定

- 测试文件：`{被测类名}Tests.cs`
- 测试方法：`{方法名}_{场景}_{预期结果}()`
  - 例：`CreateProject_ValidInput_ReturnsProjectId()`
  - 例：`GetProjectList_UserHasDeptScope_ReturnsFilteredList()`

## xUnit 约定

- `[Fact]` — 无参数的独立测试
- `[Theory]` + `[InlineData]` — 参数化测试
- 使用 AAA 模式：Arrange → Act → Assert
