# WindowMonitor Project Memory

## 项目信息
- 项目名称: WindowMonitor
- 仓库: https://github.com/xiaowulai-s/Desktop-window-monitoring.git
- 版本: v1.0
- 技术栈: C#/.NET 8.0, WPF, SQLite, Newtonsoft.Json

## 2026-03-20
- 整理项目文档 `PROJECT_DOCUMENT.md`（基于实际代码编写）
- 添加版本号 v1.0
- 初始化Git仓库并推送到GitHub
- Git远程仓库已配置: origin https://github.com/xiaowulai-s/Desktop-window-monitoring.git
- 推送到main分支成功

### 重要Bug修复
- **StackOverflow Bug**: `MonitorEngine.OnWindowEventOccurred` 递归调用自己而非触发事件
  - 原因: private方法 `OnWindowEventOccurred(object, WindowEvent)` 调用了同名的 protected virtual 方法 `OnWindowEventOccurred(WindowEvent)`，导致无限递归
  - 后果: StackOverflowException 被 WindowHook.WinEventCallback 的 try-catch 静默吞掉，所有事件无法到达 ViewModel
  - 修复: 改为 `WindowEventOccurred?.Invoke(this, e)`

### 升级到 .NET 8.0
- 所有项目从 .NET 6.0 升级到 .NET 8.0
- 编译警告从 69 个减少到 0 个
- NuGet 包版本更新 (Microsoft.Data.Sqlite 8.0.0)

### 新增功能
- UAC 管理员权限检测（启动时检测，提供重启选项）

### 修改的文件
- WindowMonitor.Business/MonitorEngine.cs (修复递归调用Bug)
- 其他文件见 Git 提交记录

### 项目文档优化
- 分析现有实现与文档的差异
- 更新文档使其与实际实现一致：
  - .NET 6.0 -> 8.0
  - Microsoft.Data.Sqlite 版本更新
  - 数据库列名 RawWindowInfoJson 与文档对齐
  - 操作系统要求更新为 Windows 10/11
  - 添加 RawWindowInfoJson 设计说明