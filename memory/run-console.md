---
name: run-console
description: 运行系统时启动可见控制台窗口
metadata:
  type: feedback
---

# 运行系统必须启动可见控制台

**规则**：启动后端 API 服务时，必须在一个可见的控制台窗口中运行，让用户能看到后端的实时日志输出（请求、异常、数据库操作等）。

**Why:** 用户在后台看不到 Kestrel 的请求日志、异常堆栈、数据库初始化等信息。通过可见控制台窗口可以实时观察系统运行状态。

**How to apply:**
1. 使用 PowerShell `Start-Process` 打开新的 cmd 窗口运行 `dotnet run`
2. 命令模板：
   ```
   powershell -Command "Start-Process cmd.exe -ArgumentList '/k cd /d <API项目路径> && dotnet run'"
   ```
3. 具体路径：`C:\Users\h\Desktop 2\Desktop\红黑榜\项目管理系统V1\codes\backend\ProjectManagement.API`
4. 不要用 `run_in_background` — 那样用户看不到输出
5. 启动后等待约 12 秒验证服务就绪
6. 同时用 `start https://localhost:9091` 打开前端页面

关联：[[git-confirmation]]
