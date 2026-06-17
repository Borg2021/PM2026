---
name: git-confirmation
description: Git 提交和推送需要用户确认
metadata:
  type: feedback
---

# Git 操作需用户确认

**规则**：任何 `git commit` 和 `git push` 操作前，必须先向用户展示将要提交的内容摘要，得到确认后再执行。

**Why:** 用户希望对版本控制有完全控制权，避免意外提交或推送不该提交的内容。

**How to apply:**
1. 代码变更完成后，先用 `git status` 或 `git diff --stat` 展示改动清单
2. 列出将要 commit 的信息（类型、描述、涉及文件）
3. 明确询问"是否确认提交/推送？"
4. 用户回复确认后再执行 `git commit` 和 `git push`
5. 用户说"好的"、"确认"、"可以" 等视为同意
6. 不允许擅自提交，即使改动很小

关联：[[coding-style]]
