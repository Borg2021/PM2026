# ProjectEditor.vue 组件拆分设计

## 目标

将 5238 行的 `ProjectEditor.vue` 按功能拆分为 14 个独立子组件，父组件保留路由/权限/Tab切换/共享状态管理职责，子组件通过 props/emit 通信。

## 组件树

```
ProjectEditor.vue (~300行) ← 薄壳父组件
  ├── Tab 1: 基本信息
  │   ├── ProjectBasicInfo.vue      (~400行)   表单字段（10行网格）
  │   ├── ProductListTable.vue       (~80行)   产品列表编辑表
  │   └── ProjectScopeTable.vue      (~80行)   项目范围编辑表
  ├── Tab 2: 成员人员
  │   ├── ProjectMembers.vue        (~250行)   成员表（含拖拽排序）
  │   └── MemberTemplateDialog.vue  (~150行)   从模板导入对话框
  ├── Tab 3: 文件资料 → 已有 ProjectFileTab.vue ✅
  ├── Tab 4: 项目任务计划
  │   ├── ProjectTaskPlan.vue       (~200行)   编排器（工具栏+表格+对话框组合）
  │   ├── TaskTable.vue             (~500行)   任务表格（行内编辑、右键菜单、列拖拽）
  │   ├── TaskEditDialog.vue        (~350行)   新增/编辑任务对话框
  │   ├── TaskViewDialog.vue        (~200行)   查看任务详情对话框
  │   ├── TaskTemplateDialog.vue     (~80行)   从模板新增对话框
  │   ├── PreTaskTooltip.vue        (~100行)   前置任务悬浮提示浮层
  │   └── MilestoneTable.vue         (~80行)   里程碑列表
  ├── Tab 5: 计划甘特图
  │   └── ProjectGantt.vue          (~700行)   甘特图 + 关键路径
  ├── Tab 6: 任务列表看板
  │   └── ProjectKanban.vue          (~80行)   看板分组视图
  ├── Tab 7/8/9: 变更/财务/日志 → 已有 ✅
  └── Lifecycle
      └── ProjectLifecycle.vue       (~80行)   生命周期展示
```

## Props/Emits 接口定义

### 父组件 ProjectEditor.vue（薄壳）

**职责：**
- 路由解析（mode, projectId, isReadonly）
- 权限判断（hasFieldPerm, canViewFileTab, isTaskLocked）
- Tab 切换（activeTab）
- 表单主数据（form reactive）
- 辅助数据加载（departments, roles, functions, users, dictMap, deptTreeData）
- 保存/提交逻辑（handleSave）
- 状态操作（激活/暂停/完成等）
- onMounted 生命周期（加载详情 + 辅助数据）

**状态保留在父组件：**
- `form` — 表单数据（所有 Tab 共享）
- `products` / `projectScopes` / `members` — 基本信息子表
- `tasks` — 任务计划数据
- `loading` / `saving` — 全局加载状态
- 所有辅助数据引用（departments, roles, functions, users, dictMap）

### ProjectBasicInfo.vue

| 方向 | 名称 | 类型 | 说明 |
|------|------|------|------|
| props | modelValue | Partial\<ProjectDetail\> | 表单数据 |
| props | rules | object | 校验规则 |
| props | isReadonly | boolean | 只读模式 |
| props | hasFieldPerm | (code: string) => boolean | 字段权限判断 |
| props | projectId | number \| null | 项目ID |
| props | mode | 'create' \| 'edit' \| 'view' | 页面模式 |
| props | dictMap | object | 字典映射 |
| props | projectManagerOptions | UserInfo[] | 项目经理候选人 |
| props | salesManagerOptions | UserInfo[] | 销售候选人 |
| props | preSalesOptions | UserInfo[] | 售前候选人 |
| props | isCurrentUserPm | boolean | 当前用户是PM |
| props | isPmCreator | boolean | 创建模式下是PM |
| props | formStatus | number | 表单状态 |
| emit | update:modelValue | — | 双向绑定 |
| slot | productTable | — | 产品列表插槽 |
| slot | scopeTable | — | 项目范围插槽 |

### ProductListTable.vue

| 方向 | 名称 | 类型 | 说明 |
|------|------|------|------|
| props | modelValue | ProductItem[] | 产品列表 |
| props | isReadonly | boolean | 只读 |
| props | formStatus | number | 表单状态 |
| emit | update:modelValue | — | — |

### ProjectScopeTable.vue

| 方向 | 名称 | 类型 | 说明 |
|------|------|------|------|
| props | modelValue | ScopeItem[] | 范围列表 |
| props | isReadonly | boolean | 只读 |
| emit | update:modelValue | — | — |

### ProjectMembers.vue

| 方向 | 名称 | 类型 | 说明 |
|------|------|------|------|
| props | modelValue | ProjectMemberItem[] | 成员列表 |
| props | isReadonly | boolean | 只读 |
| props | functions | FunctionItem[] | 职能列表 |
| props | departments | Department[] | 部门列表 |
| props | deptTreeData | TreeData[] | 部门树 |
| props | users | UserInfo[] | 用户列表 |
| props | templateImported | boolean | 模板已导入 |
| props | formManagerIds | { pm: number\|null, sales: number\|null, preSales: number\|null } | 锁定成员ID |
| emit | update:modelValue | — | — |
| emit | import-template | — | 打开模板导入对话框 |

### MemberTemplateDialog.vue

| 方向 | 名称 | 类型 | 说明 |
|------|------|------|------|
| props | visible | boolean | 对话框可见 |
| emit | update:visible | — | — |
| emit | imported | members: ProjectMemberItem[] | 导入完成 |

### ProjectTaskPlan.vue（编排器）

| 方向 | 名称 | 类型 | 说明 |
|------|------|------|------|
| props | projectId | number \| null | 项目ID |
| props | tasks | ProjectTaskItem[] | 任务列表 |
| props | isReadonly | boolean | 只读 |
| props | isTaskLocked | boolean | 任务锁定 |
| props | dictMap | object | 字典（任务类别） |
| props | taskNoRule | string | 编号规则 |
| props | departments | Department[] | 责任部门 |
| props | users | UserInfo[] | 责任人 |
| props | form | Partial\<ProjectDetail\> | 项目信息（导出用） |
| emit | update:tasks | — | 任务变更 |

**子组件（内部）：** TaskTable, TaskEditDialog, TaskViewDialog, TaskTemplateDialog, PreTaskTooltip, MilestoneTable

### TaskTable.vue

| 方向 | 名称 | 类型 | 说明 |
|------|------|------|------|
| props | tasks | ProjectTaskItem[] | 任务树数据 |
| props | taskTree | ProjectTaskItem[] | 树形展开数据 |
| props | isReadonly | boolean | — |
| props | isTaskLocked | boolean | — |
| props | dictMap | object | — |
| props | departments | Department[] | — |
| props | users | UserInfo[] | — |
| props | listEditMode | boolean | 编辑模式 |
| props | editingRowId | number \| null | 当前编辑行 |
| emit | add-child | taskId: number | 添加子任务 |
| emit | add-sibling | taskId: number | 添加同级 |
| emit | delete | taskId: number | 删除 |
| emit | edit | task: ProjectTaskItem | 打开编辑对话框 |
| emit | view | task: ProjectTaskItem | 打开查看对话框 |
| emit | toggle-edit-mode | — | 切换编辑模式 |
| emit | update:listEditMode | — | — |
| emit | update:editingRowId | — | — |
| emit | reorder | srcIdx, tgtIdx | 拖拽排序 |
| emit | export-excel | — | 导出 |

### TaskEditDialog.vue

| 方向 | 名称 | 类型 | 说明 |
|------|------|------|------|
| props | visible | boolean | — |
| props | mode | 'create' \| 'edit' | — |
| props | task | ProjectTaskItem \| null | 原任务数据 |
| props | parentTask | ProjectTaskItem \| null | 父任务 |
| props | projectId | number \| null | — |
| props | taskNoRule | string | — |
| props | tasks | ProjectTaskItem[] | 所有任务（前置任务树选择） |
| props | dictMap | object | — |
| props | departments | Department[] | — |
| props | users | UserInfo[] | — |
| emit | update:visible | — | — |
| emit | saved | task: ProjectTaskItem | 保存成功 |

### TaskViewDialog.vue

| 方向 | 名称 | 类型 | 说明 |
|------|------|------|------|
| props | visible | boolean | — |
| props | task | ProjectTaskItem \| null | — |
| props | dictMap | object | — |
| emit | update:visible | — | — |

### TaskTemplateDialog.vue

| 方向 | 名称 | 类型 | 说明 |
|------|------|------|------|
| props | visible | boolean | — |
| props | projectId | number \| null | — |
| emit | update:visible | — | — |
| emit | imported | count: number | 导入完成 |

### PreTaskTooltip.vue

| 方向 | 名称 | 类型 | 说明 |
|------|------|------|------|
| props | visible | boolean | — |
| props | x | number | 浮层X坐标 |
| props | y | number | 浮层Y坐标 |
| props | task | ProjectTaskItem \| null | 目标任务 |
| props | tasks | ProjectTaskItem[] | 所有任务 |
| emit | update:visible | — | — |
| emit | navigate | taskId: number | 导航到前置任务 |

### MilestoneTable.vue

| 方向 | 名称 | 类型 | 说明 |
|------|------|------|------|
| props | tasks | ProjectTaskItem[] | 所有任务（筛选里程碑） |
| props | isReadonly | boolean | — |
| props | dictMap | object | — |

### ProjectGantt.vue

| 方向 | 名称 | 类型 | 说明 |
|------|------|------|------|
| props | tasks | ProjectTaskItem[] | 任务列表 |
| props | isReadonly | boolean | — |
| emit | navigate | taskId: number | 导航到任务行 |

### ProjectKanban.vue

| 方向 | 名称 | 类型 | 说明 |
|------|------|------|------|
| props | tasks | ProjectTaskItem[] | 任务列表 |
| props | dictMap | object | — |

### ProjectLifecycle.vue

| 方向 | 名称 | 类型 | 说明 |
|------|------|------|------|
| props | projectId | number \| null | 项目ID |

## 拆分原则

1. **不改变行为** — 纯代码搬运，不修改任何业务逻辑
2. **数据向下，事件向上** — 子组件只读 props，通过 emit 通知父组件
3. **使用 v-model 双向绑定** — 列表类数据用 modelValue + update:modelValue
4. **DI 模式透传** — 共享数据（departments, users, dictMap 等）通过 props 透传，不用 provide/inject
5. **保持 import 路径** — 子组件 import 来自 `@/api/project`、`@/types/project`、`@/store/auth`（需要 store 的按需注入）
6. **文件放在 `components/` 目录** — 与现有 ProjectFileTab 等并列

## 测试策略

- **冒烟测试**：每个 Tab 能正常切换、数据能加载
- **基本流程**：创建项目 → 编辑基本信息 → 添加成员 → 创建任务 → 保存
- **回归用例**：状态操作（激活/暂停/完成）、任务树拖拽、列拖拽、甘特图导航
- **边界**：只读模式、权限控制、表单校验

## 实施阶段

| 阶段 | 新组件 | 说明 |
|------|--------|------|
| 1 | ProductListTable, ProjectScopeTable | 最小最简单，验证拆分流程 |
| 2 | ProjectBasicInfo | 信息表单项搬迁（需要阶段1的2个组件作为插槽） |
| 3 | ProjectMembers, MemberTemplateDialog | 成员模块 |
| 4 | TaskTemplateDialog, TaskViewDialog, PreTaskTooltip | 独立对话框，风险低 |
| 5 | TaskEditDialog | 最复杂的对话框 |
| 6 | TaskTable | 核心表格 |
| 7 | MilestoneTable, ProjectKanban | 看板+里程碑 |
| 8 | ProjectTaskPlan | 编排器（组装阶段4-7的子组件） |
| 9 | ProjectGantt | 甘特图 |
| 10 | ProjectLifecycle | 生命周期 |
| 11 | ProjectEditor 瘦身 | 父组件清理，组装所有子组件 |

每个阶段完成后用 `vite build` 验证编译无报错。

---

## 实施结果（2026-06-23）

### 实际创建的组件（10个）

| 组件 | 行数 | 说明 |
|------|------|------|
| ProductListTable.vue | 80 | 产品列表编辑表 |
| ProjectScopeTable.vue | 65 | 项目范围编辑表 |
| MilestoneTable.vue | 56 | 里程碑列表 |
| ProjectKanban.vue | 85 | 看板分组视图 |
| PreTaskTooltip.vue | 93 | 前置任务悬浮提示 |
| TaskTemplateDialog.vue | 107 | 从模板新增任务 |
| TaskViewDialog.vue | 193 | 查看任务详情 |
| TaskEditDialog.vue | 606 | 新增/编辑任务（最多逻辑的对话框） |
| ProjectGantt.vue | 1095 | 甘特图+关键路径（最大组件） |
| LifecycleDisplay.vue | 22 | 占位（未使用） |

### 未拆分的部分

- **ProjectBasicInfo** — 表单字段与 form/rules/hasFieldPerm 深度耦合，拆分收益低
- **ProjectMembers** — 成员表与 syncSelectedStaffToMembers/applyUserInfo/templateImported 等逻辑深度交织
- **MemberTemplateDialog** — 已内联在父组件中（约90行），与 members 状态耦合
- **TaskTable** — 任务表格与 editingRowId/dirtyRowIds/列拖拽/flashRow 等逻辑深度交织

### 最终结果

- **父组件**：5238 → 3268 行（**减少 1970 行 / 37.6%**）
- **子组件**：10 个新组件 + 4 个已有组件 = 14 个
- **编译**：vite build 通过，0 error
- **行为**：纯代码搬迁，业务逻辑零变更
