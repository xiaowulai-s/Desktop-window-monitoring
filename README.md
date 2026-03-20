# WindowMonitor

**版本**: v1.0 | **日期**: 2026-03-20

Windows 桌面窗口监控软件——实时监控窗口的创建、销毁、移动、大小变化、激活等事件，输出结构化日志，辅助 AI 进行窗口程序开发中的错误排查与问题修复。

---

## 功能特性

| 功能 | 说明 |
|------|------|
| 实时监控 | 监控 Windows 窗口的创建、销毁、移动、调整大小、激活、标题变化等事件 |
| AI 优化日志 | 每个事件包含 `ContextSummary`（摘要）和 `AnalysisHint`（开发建议），便于 AI 分析 |
| 监控模式 | 支持全局监控（所有窗口）和单窗口定向监控 |
| 日志持久化 | SQLite 数据库存储，支持按时间、类型、级别查询 |
| 日志导出 | 支持导出为文本格式，便于分享与复现 |
| MVVM 架构 | WPF + .NET 6.0，四层模块化设计 |

---

## 系统要求

- **操作系统**: Windows 7 / 8 / 10 / 11
- **运行时**: .NET 6.0 Runtime
- **权限**: 需要管理员权限（窗口钩子访问）

---

## 项目结构

```
WindowMonitor/
├── WindowMonitor.slnx                    # 解决方案文件
├── PROJECT_DOCUMENT.md                    # 项目详细文档
├── Architecture_Document.md               # 架构设计文档
├── .gitignore
│
├── WindowMonitor.Business/                # 业务逻辑层
│   ├── MonitorEngine.cs                  # 监控引擎（核心控制器）
│   ├── EventHandler.cs                   # 事件处理器
│   ├── LogManager.cs                     # 日志管理器
│   ├── ConfigManager.cs                  # 配置管理器
│   └── Models/
│       └── LogEntry.cs                   # 日志条目模型
│
├── WindowMonitor.Communication/           # 通信层
│   ├── WindowHook.cs                    # Windows 窗口钩子
│   ├── WindowsApi.cs                    # Win32 API 封装
│   └── Models/
│       ├── WindowInfo.cs                 # 窗口信息模型
│       └── WindowEvent.cs                # 窗口事件模型
│
├── WindowMonitor.Data/                   # 数据层
│   └── LogStorageEngine.cs               # SQLite 存储引擎
│
├── WindowMonitor.UI/                     # UI 层
│   ├── App.xaml / App.xaml.cs           # 应用程序入口
│   ├── MainWindow.xaml / .cs            # 主窗口
│   └── ViewModels/
│       ├── MainWindowViewModel.cs       # 主窗口 ViewModel
│       ├── ObservableObject.cs          # 可观察对象基类
│       └── RelayCommand.cs              # 命令基类
│
└── WindowMonitor.Tests/                  # 测试项目
```

---

## 四层架构

```
┌─────────────────────────────────────────────────────────────┐
│                     UI 层 (Presentation Layer)              │
│  WPF + MVVM — MainWindow、ViewModel、ObservableObject       │
├─────────────────────────────────────────────────────────────┤
│                  业务逻辑层 (Business Layer)                │
│  MonitorEngine、EventHandler、LogManager、ConfigManager      │
├─────────────────────────────────────────────────────────────┤
│                   通信层 (Communication Layer)             │
│  WindowHook (WinEvent Hook)、WindowsApi 封装                 │
├─────────────────────────────────────────────────────────────┤
│                     数据层 (Data Layer)                     │
│  LogStorageEngine — SQLite 数据库存储                        │
└─────────────────────────────────────────────────────────────┘
```

---

## 监控的事件类型

| 事件类型 | 说明 | AI 分析提示 |
|---------|------|------------|
| `WindowCreate` | 新窗口创建 | 检查窗口初始化代码、资源分配是否正常 |
| `WindowDestroy` | 窗口销毁 | 检查资源释放、清理代码是否完整 |
| `WindowActivate` | 窗口获得焦点 | 关注焦点切换逻辑、窗口状态管理 |
| `WindowDeactivate` | 窗口失去焦点 | 检查焦点丢失后的状态保存 |
| `WindowMove` | 窗口移动 | 验证布局代码、坐标计算 |
| `WindowResize` | 窗口调整大小 | 检查响应式布局、控件重绘 |
| `WindowTitleChange` | 窗口标题变化 | 关注状态更新逻辑 |
| `WindowShow` | 窗口显示 | 验证显示状态管理 |
| `WindowHide` | 窗口隐藏 | 检查隐藏状态下的行为 |

---

## 快速开始

### 编译

```bash
# 克隆仓库
git clone https://github.com/xiaowulai-s/Desktop-window-monitoring.git
cd Desktop-window-monitoring

# 编译（需要 .NET 6.0 SDK）
dotnet build WindowMonitor.slnx
```

### 运行

```bash
# 以管理员身份运行
dotnet run --project WindowMonitor.UI/WindowMonitor.UI.csproj
```

> **注意**: 窗口监控需要管理员权限。首次运行请右键选择"以管理员身份运行"。

### 使用

1. **启动监控**: 点击界面上的"启动监控"按钮
2. **选择目标窗口**: 从窗口列表中选择一个窗口，点击"监控选定窗口"
3. **全局监控**: 点击"监控所有窗口"切换回全局监控模式
4. **导出日志**: 点击"导出日志"保存日志文件

---

## 配置

配置文件位于: `%LOCALAPPDATA%/WindowMonitor/config.json`

```json
{
  "MonitorIntervalMs": 100,
  "LogLevel": "Info",
  "LogRetentionDays": 30,
  "DatabasePath": "C:\\Users\\xxx\\AppData\\Local\\WindowMonitor\\windowmonitor.db",
  "AutoStartMonitoring": false,
  "MinimizeToTray": true,
  "MaxLogEntriesInMemory": 1000
}
```

| 配置项 | 默认值 | 说明 |
|--------|--------|------|
| `MonitorIntervalMs` | 100 | 监控间隔（毫秒） |
| `LogLevel` | Info | 日志级别 (Debug/Info/Warning/Error/Critical) |
| `LogRetentionDays` | 30 | 日志保留天数 |
| `MaxLogEntriesInMemory` | 1000 | 内存中最大日志条数 |

---

## 日志数据库

- **路径**: `%LOCALAPPDATA%/WindowMonitor/windowmonitor.db`
- **表**: `LogEntries`
- **索引**: `idx_log_entries_timestamp`, `idx_log_entries_event_type`, `idx_log_entries_level`

---

## 技术栈

| 技术 | 版本 | 用途 |
|------|------|------|
| C# / .NET | 6.0 | 开发语言与运行时 |
| WPF | .NET 6.0 | UI 框架 |
| SQLite | 3.40+ | 日志持久化存储 |
| Newtonsoft.Json | 13.0+ | JSON 序列化 |
| Microsoft.Data.Sqlite | 6.0+ | SQLite 数据库访问 |

---

## 扩展

### 注册自定义事件处理器

```csharp
// 实现 IWindowEventHandler 接口
public class MyCustomHandler : IWindowEventHandler
{
    public void HandleEvent(WindowEvent windowEvent)
    {
        // 自定义处理逻辑
    }
}

// 注册到事件处理器
MonitorEngine.Instance.RegisterEventHandler(WindowEventType.WindowCreate, new MyCustomHandler());
```

---

## License

MIT License
