# Git 最佳实践

> 适用于 ROBO 项目管理系统的协作开发规范。

## 1. 远程连接

### 推荐：SSH（当前项目已采用）

```bash
git remote set-url origin git@github.com:Borg2021/PM2026.git
```

**Why:** HTTPS 在国内网络环境经常被阻断（`Connection was reset`），SSH 走 22 端口更稳定。

验证 SSH 是否通：

```bash
ssh -T git@github.com
# 预期输出: Hi Borg2021! You've successfully authenticated...
```

---

## 2. 分支策略

### ✅ 推荐模式：一功能一分支

```
master          ← 稳定主线，只通过 PR 合入
  ├── feature/xxx-xxx    ← 每个功能/修复一个独立分支
  ├── fix/xxx-xxx        ← 小修复也可以单独分支
  └── chore/xxx-xxx      ← 构建/依赖/文档类变更
```

| 分支类型 | 命名示例 | 用途 |
|---------|---------|------|
| `feature/` | `feature/scroll-behavior-fix` | 新功能或 UI 改进 |
| `fix/` | `fix/login-error` | 紧急 bug 修复 |
| `chore/` | `chore/update-deps` | 构建、依赖、文档 |

### ❌ 反模式：多功能混一个分支

不要把无关的功能堆在同一个 feature 分支上。比如当前 `feature/multi-file-upload` 实际包含了：

- 🔴 文件资料多文件上传（原始目的）
- 🔴 部门增加负责人（多负责人重构）
- 🔴 人员/部门/职能管理界面滚动修复

**后果：** PR 边界模糊，Code Review 困难，一旦需要回滚会牵连无关功能。

### 当前补救建议

```
feature/multi-file-upload  (已含 3 个功能，本次先合入)
  ↓ 合入 master 后
master
  ├── feature/multi-file-upload-2   ← 如果后续还有文件上传改动
  ├── feature/xxx                   ← 新功能从 master 最新 commit 拉
```

**新功能起步：**

```bash
git checkout master
git pull origin master
git checkout -b feature/新功能名
```

---

## 3. 提交规范

### 提交信息格式（Conventional Commits）

本项目已在使用，继续保持：

```
<type>: <简短描述>
```

| Type | 用途 | 示例 |
|------|------|------|
| `feat` | 新增功能 | `feat: add department leader setting UI` |
| `fix` | 修复 bug | `fix: constrain layout height to prevent whole-page scroll` |
| `refactor` | 重构（不改功能） | `refactor: replace single LeaderId with junction table` |
| `chore` | 构建/工具/部署 | `chore: rebuild frontend with scroll behavior fixes` |
| `docs` | 文档 | `docs: add git best practice guide` |

### 提交粒度

- **一个 commit 做一件事**，不要堆 10 个文件改 3 个功能
- 写完一个 task → commit 一次 → 再写下一个
- Commit 时带上：`Co-Authored-By: Claude <noreply@anthropic.com>`

---

## 4. PR 工作流

### 流程

```
1. feature/xxx 分支开发
2. 推送: git push origin feature/xxx
3. GitHub 创建 PR: feature/xxx → master
4. Review → Merge → 删除 feature 分支
```

### 当前 PR

```
#1  feat: 文件资料多文件上传支持  (OPEN)
    feature/multi-file-upload → master
```

⚠️ 注意：这个分支因为混了多个功能，PR 描述需要补全实际包含的内容。

---

## 5. 什么该提交，什么不该

### ✅ 应该提交

| 文件 | 说明 |
|------|------|
| `codes/**` | 源代码 |
| `docs/specs/*.md` | 需求文档（功能做完后更新） |
| `docs/plans/*-Plan.md` | 实施计划 |
| `codes/backend/.../wwwroot/` | 前端构建产物（给后端提供静态文件服务） |

### ❌ 不应该提交

| 文件 | 说明 |
|------|------|
| `appsettings.json` 中的本地数据库密码 | 用 `.gitignore` 排除或保持本地修改不提交 |
| `*.db`, `*.db-shm`, `*.db-wal` | SQLite 本地数据库文件 |
| `node_modules/` | npm 依赖 |
| `bin/`, `obj/` | .NET 编译产物 |

### 当前待清理

```bash
# 这些本地文件不应该随意提交：
codes/backend/ProjectManagement.API/appsettings.json  ← 有本地 DB 密码修改
_query.csx                                              ← 临时脚本

# 这些应该提交（当前未跟踪）：
docs/plans/20260617-优化人员部门管理界面-Plan.md
docs/specs/20260617-优化人员部门管理界面.md
```

---

## 6. 发布流程

```bash
# 1. 构建前端
cd codes/frontend && npm run build

# 2. 自包含发布（不依赖目标机器装 .NET）
cd codes/backend/ProjectManagement.API
dotnet publish -c Release -r win-x64 --self-contained true -o 目标目录

# 3. 启动：双击 ProjectManagement.API.exe
```

---

## 7. 常用命令速查

```bash
git remote -v                              # 查看远程仓库
git branch -a                              # 查看所有分支
git log --oneline -10                      # 最近 10 条提交
git status                                 # 工作区状态
git checkout -b feature/新功能名           # 从当前位置开新分支
git checkout master && git pull            # 切回主线并拉最新
git push origin 分支名                     # 推送
```

---

## 8. 改进建议（总结）

| 优先级 | 建议 | 原因 |
|--------|------|------|
| 🔴 高 | 新功能从 master 拉独立分支 | 避免多功能混在一个分支 |
| 🟡 中 | `appsettings.json` 加入 `.gitignore` | 避免本地密码泄露到仓库 |
| 🟡 中 | 当前未跟踪的 `docs/` 文件补 commit | 文档和代码同步版本管理 |
| 🟢 低 | 给 `_query.csx` 加 `.gitignore` | 临时脚本不该出现在 `git status` 里 |
