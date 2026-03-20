# WindowMonitor 项目文档

**版本**：v1.0

**更新日期**：2026-03-20

## 1. 项目概述

**项目名称**：WindowMonitor（Windows桌面窗口监控软件）

**项目目标**：实时监控Windows操作系统中所有窗口的创建、销毁、状态变化等事件，并输出详细的执行日志，用于辅助AI进行窗口程序开发过程中的错误排查和问题修复。

**技术栈**：
- C# / .NET 8.0
- WPF (Windows Presentation Foundation)
- SQLite 3.40+（日志存储）
- Newtonsoft.Json 13.0+（JSON处理）
- Microsoft.Data.Sqlite 8.0+（数据库访问）

---

## 2. 项目结构

```
WindowMonitor/
├── WindowMonitor.slnx              # 解决方案文件
├── Architecture_Document.md         # 架构文档
├── WindowMonitor.Business/          # 业务逻辑层
├── WindowMonitor.Communication/      # 通信层
├── WindowMonitor.Data/              # 数据层
├── WindowMonitor.UI/                # UI层
└── WindowMonitor.Tests/             # 测试项目
```

### 2.1 解决方案项目

| 项目 | 层次 | 主要职责 |
|------|------|----------|
| WindowMonitor.Business | 业务逻辑层 | 监控引擎、事件处理、日志管理、配置管理 |
| WindowMonitor.Communication | 通信层 | Windows API封装、窗口钩子、进程间通信 |
| WindowMonitor.Data | 数据层 | SQLite日志存储引擎 |
| WindowMonitor.UI | UI层 | WPF主界面、ViewModel、MVVM架构 |
| WindowMonitor.Tests | 测试 | 单元测试与集成测试 |

---

## 3. 模块详细说明

### 3.1 业务逻辑层（WindowMonitor.Business）

#### 3.1.1 MonitorEngine（监控引擎）

**文件**：`MonitorEngine.cs`

**职责**：系统核心控制器，管理监控任务的生命周期，采用单例模式。

**核心功能**：
- `StartMonitoring()`：启动监控（加载配置、启动日志、设置窗口钩子）
- `StopMonitoring()`：停止监控
- `PauseMonitoring()` / `ResumeMonitoring()`：暂停/恢复监控
- `GetCurrentWindows()`：获取当前所有窗口列表
- `SetTargetWindow(hwnd)`：设置单个窗口监控目标
- `SetMonitorAllWindows()`：切换到监控所有窗口模式

**状态管理**：
```csharp
public enum MonitorState { Stopped, Running, Paused }
```

**事件发布**：
- `StateChanged`：监控状态变化
- `WindowEventOccurred`：窗口事件发生
- `LogEntryReceived`：日志条目接收
- `MonitorModeChanged`：监控模式变化
- `TargetWindowChanged`：目标窗口变化

---

#### 3.1.2 EventHandler（事件处理器）

**文件**：`EventHandler.cs`

**职责**：处理和分发监控事件，采用生产者-消费者模式。

**核心功能**：
- `RegisterHandler(eventType, handler)`：注册事件处理器
- `UnregisterHandler(eventType, handler)`：注销事件处理器
- `ProcessEvent(windowEvent)`：将事件加入处理队列
- `StartProcessing()` / `StopProcessing()`：启动/停止事件处理线程

**内部实现**：
- 使用 `BlockingCollection<WindowEvent>` 作为事件队列
- 后台线程 `EventProcessingThread` 异步处理事件
- 事件处理后创建 `LogEntry` 并触发 `LogEntryCreated` 事件

---

#### 3.1.3 LogManager（日志管理器）

**文件**：`LogManager.cs`

**职责**：生成、过滤、管理日志，采用单例模式。

**核心功能**：
- `WriteLog(logEntry)`：写入日志
- `SetLogLevel(level)`：设置日志级别
- `StartLogWriting()` / `StopLogWriting()`：启动/停止日志写入线程
- `QueryLogs(filter)`：查询内存中的日志
- `ClearInMemoryLogs()`：清空内存日志

**内存管理**：
- 使用 `ConcurrentQueue<LogEntry>` 和 `BlockingCollection<LogEntry>` 实现线程安全
- 内存中最多保留 `_maxInMemoryLogs`（默认1000条）条日志
- 超出上限时移除最旧的日志

**日志级别**：
```csharp
public enum LogLevel { Debug, Info, Warning, Error, Critical }
```

---

#### 3.1.4 ConfigManager（配置管理器）

**文件**：`ConfigManager.cs`

**职责**：管理系统配置，采用单例模式。

**默认配置项**：
| 配置项 | 默认值 | 说明 |
|--------|--------|------|
| MonitorIntervalMs | 100 | 监控间隔（毫秒） |
| LogLevel | "Info" | 日志级别 |
| LogRetentionDays | 30 | 日志保留天数 |
| DatabasePath | LocalAppData/WindowMonitor/windowmonitor.db | 数据库路径 |
| AutoStartMonitoring | false | 自动启动监控 |
| MinimizeToTray | true | 最小化到托盘 |
| MaxLogEntriesInMemory | 1000 | 内存中最大日志数 |

**存储格式**：JSON

---

#### 3.1.5 LogEntry（日志条目模型）

**文件**：`Models/LogEntry.cs`

**职责**：日志数据模型，包含AI辅助分析上下文。

**主要属性**：
- 基本信息：`Timestamp`, `EventId`, `EventType`, `Level`
- 窗口信息：`WindowTitle`, `ClassName`, `Hwnd`, `ParentHwnd`, `Left`, `Top`, `Right`, `Bottom`, `Width`, `Height`, `Visible`, `Active`
- 进程信息：`ProcessName`, `ProcessId`, `ThreadId`
- AI上下文：`ContextSummary`（摘要）, `AnalysisHint`（分析提示）, `EventMetadata`（元数据字典）

**AI分析支持**：
- `ToAIAnalysisFormat()`：生成适合AI分析的日志格式
- 每个事件类型都有预设的 `ContextSummary` 和 `AnalysisHint`

---

### 3.2 通信层（WindowMonitor.Communication）

#### 3.2.1 WindowHook（窗口钩子）

**文件**：`WindowHook.cs`

**职责**：与Windows API交互，捕获窗口事件，采用WinEvent Hook机制。

**监控模式**：
```csharp
public enum MonitorMode { AllWindows, SingleWindow }
```

**核心功能**：
- `SetHook()` / `UnsetHook()`：设置/移除系统钩子
- `GetWindowInfo(hwnd)`：获取指定窗口详细信息
- `GetCurrentWindows()`：获取当前所有窗口列表
- `SetTargetWindow(hwnd)` / `SetMonitorAllWindows()`：切换监控模式

**支持的窗口事件类型**：
```csharp
public enum WindowEventType
{
    Unknown,
    WindowCreate,       // 窗口创建
    WindowDestroy,      // 窗口销毁
    WindowMove,         // 窗口移动
    WindowResize,       // 窗口调整大小
    WindowActivate,     // 窗口激活
    WindowDeactivate,   // 窗口失去激活
    WindowTitleChange,  // 窗口标题变化
    WindowShow,         // 窗口显示
    WindowHide          // 窗口隐藏
}
```

**内部机制**：
- 使用 `SetWinEventHook` 设置WinEvent钩子
- 监控事件范围：`EVENT_OBJECT_CREATE` ~ `EVENT_OBJECT_NAMECHANGE`
- 维护 `_windowCache` 字典缓存窗口信息
- 过滤规则：跳过不可见窗口、工具窗口、无标题窗口（除非有WS_EX_APPWINDOW）

---

#### 3.2.2 WindowsApi（Windows API封装）

**文件**：`WindowsApi.cs`

**职责**：封装Windows Native API，提供跨进程窗口信息获取能力。

**核心数据结构**：
- `RECT`：窗口矩形区域
- `POINT`：坐标点
- `MSG`：消息结构
- `CWPSTRUCT`：调用窗口过程结构

**核心API函数**：
| 函数 | 来源 | 功能 |
|------|------|------|
| SetWinEventHook | user32.dll | 设置WinEvent钩子 |
| UnhookWinEvent | user32.dll | 移除WinEvent钩子 |
| GetWindowRect | user32.dll | 获取窗口矩形 |
| GetWindowText | user32.dll | 获取窗口标题 |
| GetClassName | user32.dll | 获取窗口类名 |
| GetForegroundWindow | user32.dll | 获取前台窗口 |
| GetWindowThreadProcessId | user32.dll | 获取窗口所属进程ID |
| GetModuleBaseName | psapi.dll | 获取进程模块名 |
| EnumWindows | user32.dll | 枚举所有窗口 |

**辅助方法**：
- `GetWindowTitle(hWnd)`：获取窗口标题
- `GetWindowClassName(hWnd)`：获取窗口类名
- `GetWindowRectangle(hWnd)`：获取窗口矩形
- `GetWindowProcessId(hWnd)`：获取进程ID
- `GetProcessName(processId)`：获取进程名

---

#### 3.2.3 WindowInfo（窗口信息模型）

**文件**：`Models/WindowInfo.cs`

**职责**：窗口详细信息数据模型。

**属性**：
```csharp
public class WindowInfo
{
    public IntPtr Hwnd { get; set; }           // 窗口句柄
    public string Title { get; set; }           // 窗口标题
    public string ClassName { get; set; }       // 窗口类名
    public uint ProcessId { get; set; }         // 进程ID
    public string ProcessName { get; set; }     // 进程名
    public Rectangle Rect { get; set; }         // 窗口矩形
    public bool Visible { get; set; }          // 是否可见
    public bool Active { get; set; }            // 是否激活
    public IntPtr ParentHwnd { get; set; }      // 父窗口句柄
    public uint ThreadId { get; set; }         // 线程ID
    public DateTime CreationTime { get; set; } // 创建时间
}
```

---

#### 3.2.4 WindowEvent（窗口事件模型）

**文件**：`Models/WindowEvent.cs`

**职责**：窗口事件数据模型。

**属性**：
```csharp
public class WindowEvent
{
    public string EventId { get; set; }        // 事件ID（GUID）
    public WindowEventType EventType { get; set; }  // 事件类型
    public WindowInfo WindowInfo { get; set; }  // 窗口信息
    public DateTime Timestamp { get; set; }    // 时间戳
    public string Details { get; set; }        // 详细信息
}
```

---

### 3.3 数据层（WindowMonitor.Data）

#### 3.3.1 LogStorageEngine（日志存储引擎）

**文件**：`LogStorageEngine.cs`

**职责**：日志数据的持久化存储，采用单例模式，使用SQLite数据库。

**核心功能**：
- `Initialize()`：初始化数据库，创建表和索引
- `StoreLog(logEntry)`：存储单条日志
- `QueryLogs(limit, offset)`：分页查询日志
- `QueryLogsByEventType(eventType, limit)`：按事件类型查询
- `QueryLogsByDateRange(startDate, endDate, limit)`：按日期范围查询
- `CleanupOldLogs(retentionDays)`：清理过期日志
- `ClearAllLogs()`：清空所有日志

**数据库schema**：
```sql
CREATE TABLE LogEntries (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Timestamp TEXT NOT NULL,
    EventId TEXT NOT NULL,
    EventType TEXT NOT NULL,
    Level INTEGER NOT NULL,
    WindowTitle TEXT,
    ProcessName TEXT,
    ProcessId INTEGER,
    Hwnd TEXT,
    RawWindowInfoJson TEXT,
    Details TEXT,
    SystemInfoJson TEXT
);

CREATE INDEX idx_log_entries_timestamp ON LogEntries(Timestamp);
CREATE INDEX idx_log_entries_event_type ON LogEntries(EventType);
CREATE INDEX idx_log_entries_level ON LogEntries(Level);
```

> **注**：`RawWindowInfoJson` 字段存储完整的窗口信息JSON，包含所有窗口属性（类名、位置、尺寸、可见状态、激活状态等），由 `WindowInfo.ToJson()` 生成。

---

### 3.4 UI层（WindowMonitor.UI）

#### 3.4.1 MainWindowViewModel（主窗口视图模型）

**文件**：`ViewModels/MainWindowViewModel.cs`

**职责**：主窗口的MVVM ViewModel，协调UI与业务逻辑。

**核心属性**：
- `LogEntries`：日志列表（ObservableCollection）
- `CurrentWindows`：当前窗口列表（ObservableCollection）
- `MonitorState`：监控状态
- `StatusText`：状态文本
- `TargetWindow`：监控目标窗口
- `MonitorMode`：监控模式

**核心命令**：
| 命令 | 说明 |
|------|------|
| StartMonitoringCommand | 启动监控 |
| StopMonitoringCommand | 停止监控 |
| PauseMonitoringCommand | 暂停/恢复监控 |
| ClearLogsCommand | 清空日志 |
| RefreshWindowsCommand | 刷新窗口列表 |
| SelectTargetWindowCommand | 选择目标窗口 |
| MonitorAllWindowsCommand | 监控所有窗口 |
| ExportLogsCommand | 导出日志 |

**事件订阅**：
- 订阅 `MonitorEngine` 的状态变化、事件、日志等事件
- 使用 `Dispatcher.Invoke` 确保UI线程安全更新

---

#### 3.4.2 ObservableObject（可观察对象基类）

**文件**：`ViewModels/ObservableObject.cs`

**职责**：实现 `INotifyPropertyChanged` 接口的基类。

---

#### 3.4.3 RelayCommand（命令基类）

**文件**：`ViewModels/RelayCommand.cs`

**职责**：实现 `ICommand` 接口，支持命令参数和条件执行。

---

## 4. 数据流架构

```
┌─────────────────────────────────────────────────────────────────────┐
│                         Windows 系统                                 │
│   (窗口创建/销毁/移动/大小改变/激活/标题变化等事件)                      │
└────────────────────────────┬────────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    WindowHook（窗口钩子）                             │
│   - SetWinEventHook 捕获系统级窗口事件                                 │
│   - 过滤无效窗口（工具窗口、隐藏窗口等）                               │
│   - 维护窗口信息缓存                                                  │
└────────────────────────────┬────────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    MonitorEngine（监控引擎）                          │
│   - 管理监控生命周期（启动/停止/暂停/恢复）                             │
│   - 协调各组件工作                                                    │
│   - 发布状态/事件变化                                                 │
└─────────────┬───────────────────────────────┬─────────────────────┘
              │                               │
              ▼                               ▼
┌─────────────────────────────┐   ┌───────────────────────────────────┐
│   EventHandler（事件处理器）  │   │   LogManager（日志管理器）         │
│   - 事件入队                  │   │   - 日志写入队列                   │
│   - 后台线程处理              │   │   - 内存缓存管理                   │
│   - 创建LogEntry             │   │   - 日志级别过滤                   │
└─────────────┬───────────────┘   └───────────────┬───────────────────┘
              │                                   │
              │            ┌──────────────────────┘
              │            │
              ▼            ▼
┌─────────────────────────────────────────┐
│           LogEntry（日志条目）            │
│   - 从WindowEvent转换而来                │
│   - 包含AI分析上下文                      │
└─────────────────┬───────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────┐
│      LogStorageEngine（存储引擎）         │
│      SQLite数据库持久化                   │
└─────────────────────────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────┐
│      MainWindowViewModel（ViewModel）     │
│      - ObservableCollection 绑定UI         │
│      - Dispatcher 确保线程安全            │
└─────────────────────────────────────────┘
```

---

## 5. 线程模型

| 线程 | 名称 | 职责 |
|------|------|------|
| UI线程 | Main Thread | 界面渲染、用户交互响应 |
| 监控线程 | WinEvent Callback | Windows事件回调（系统调用） |
| 事件处理线程 | EventProcessingThread | 事件队列处理、LogEntry创建 |
| 日志写入线程 | LogWritingThread | 日志持久化、内存管理 |

**线程间通信**：
- `BlockingCollection<WindowEvent>`：事件队列
- `BlockingCollection<LogEntry>`：日志队列
- `ConcurrentQueue<LogEntry>`：内存缓存
- 事件/委托：组件间通知

---

## 6. 配置文件

**路径**：`%LOCALAPPDATA%/WindowMonitor/config.json`

**示例内容**：
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

---

## 7. 数据库

**路径**：`%LOCALAPPDATA%/WindowMonitor/windowmonitor.db`

**主表**：`LogEntries`

**索引**：
- `idx_log_entries_timestamp`：按时间戳排序
- `idx_log_entries_event_type`：按事件类型查询
- `idx_log_entries_level`：按日志级别查询

---

## 8. AI辅助分析

本系统生成的日志专为AI分析优化，包含：

### 8.1 ContextSummary（上下文摘要）
每个事件类型都有预设的摘要描述，如：
- `WindowCreate` → "新窗口创建"
- `WindowDestroy` → "窗口销毁"
- `WindowActivate` → "窗口获得焦点"

### 8.2 AnalysisHint（分析提示）
每个事件类型都有对应的开发建议，如：
- `WindowCreate` → "检查窗口初始化代码、资源分配是否正常"
- `WindowDestroy` → "检查资源释放、清理代码是否完整"

### 8.3 ToAIAnalysisFormat()
生成格式化的文本输出，包含：
- 时间戳、事件ID、事件类型、日志级别
- 窗口信息（标题、类名、句柄）
- 进程信息（名称、ID、线程ID）
- 位置和尺寸
- 状态（可见、激活）
- AI分析上下文

---

## 9. 扩展点

### 9.1 插件化架构
- 定义 `IWindowEventHandler` 接口支持自定义事件处理器
- 通过 `EventHandler.RegisterHandler()` 注册

### 9.2 监控模式
- 全局监控（AllWindows）
- 单窗口监控（SingleWindow）

### 9.3 日志级别过滤
- Debug、Info、Warning、Error、Critical
- 可运行时调整

---

## 10. 运行要求

- **操作系统**：Windows 10/11
- **.NET**：.NET 8.0 Runtime
- **权限**：需要管理员权限（访问窗口钩子）
