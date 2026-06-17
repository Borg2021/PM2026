---
name: coding-style
description: 项目代码风格偏好（补充 CLAUDE.md）
metadata:
  type: feedback
---

# 代码风格偏好

## 语言
- 始终使用**中文**与用户交流（回复、解释、注释、commit message）
- 代码标识符使用英文（变量名、函数名、类名）

## 命名
- **kebab-case**：文件和目录名（`project-controller.ts`、`create-project-command.cs`）
- **PascalCase**：C# 类名、方法名、属性名
- **camelCase**：TypeScript 变量名、函数名
- 完整单词，不缩写（`DepartmentManagement` 不是 `DeptMgmt`）

## 代码结构
- 函数 ≤ 20 行，文件 ≤ 200 行
- 不用接口+实现类+工厂的"重型脚手架"
- 优先使用函数而非类（前端）

## 变更原则
- 一次只改一个文件/模块
- 不改注释、空行、引号风格（除非明确要求）
- 不跨目录批量修改
- 重构单独提 PR

**Why:** 由 CLAUDE.md 定义，这里记录实际执行中的补充约定。
**How to apply:** 每次写代码前回顾 [[CLAUDE.md]] 和本文件。
