using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using WindowMonitor.Communication.Models;

namespace WindowMonitor.Business.Models
{
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
        Critical
    }

    public class LogEntry
    {
        public long Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string EventId { get; set; }
        public string EventType { get; set; }
        public LogLevel Level { get; set; }
        
        public string WindowTitle { get; set; }
        public string ProcessName { get; set; }
        public uint ProcessId { get; set; }
        public string Hwnd { get; set; }
        
        public string ClassName { get; set; }
        public int Left { get; set; }
        public int Top { get; set; }
        public int Right { get; set; }
        public int Bottom { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool Visible { get; set; }
        public bool Active { get; set; }
        public uint ThreadId { get; set; }
        public string ParentHwnd { get; set; }
        
        public string ContextSummary { get; set; }
        public string AnalysisHint { get; set; }
        public Dictionary<string, string> EventMetadata { get; set; }
        
        public string RawWindowInfoJson { get; set; }
        public string Details { get; set; }

        public LogEntry()
        {
            Timestamp = DateTime.Now;
            Level = LogLevel.Info;
            EventId = string.Empty;
            EventType = string.Empty;
            WindowTitle = string.Empty;
            ProcessName = string.Empty;
            Hwnd = string.Empty;
            ClassName = string.Empty;
            ParentHwnd = string.Empty;
            ContextSummary = string.Empty;
            AnalysisHint = string.Empty;
            RawWindowInfoJson = string.Empty;
            Details = string.Empty;
            EventMetadata = new Dictionary<string, string>();
        }

        public static LogEntry FromWindowEvent(WindowEvent windowEvent)
        {
            var logEntry = new LogEntry
            {
                EventId = windowEvent.EventId,
                Timestamp = windowEvent.Timestamp,
                EventType = windowEvent.GetEventTypeName(),
                WindowTitle = windowEvent.WindowInfo?.Title ?? string.Empty,
                ProcessName = windowEvent.WindowInfo?.ProcessName ?? string.Empty,
                ProcessId = windowEvent.WindowInfo?.ProcessId ?? 0,
                Hwnd = windowEvent.WindowInfo != null ? "0x" + windowEvent.WindowInfo.Hwnd.ToInt64().ToString("X8") : string.Empty,
                ClassName = windowEvent.WindowInfo?.ClassName ?? string.Empty,
                Left = windowEvent.WindowInfo?.Rect?.Left ?? 0,
                Top = windowEvent.WindowInfo?.Rect?.Top ?? 0,
                Right = windowEvent.WindowInfo?.Rect?.Right ?? 0,
                Bottom = windowEvent.WindowInfo?.Rect?.Bottom ?? 0,
                Width = windowEvent.WindowInfo?.Rect != null ? (windowEvent.WindowInfo.Rect.Right - windowEvent.WindowInfo.Rect.Left) : 0,
                Height = windowEvent.WindowInfo?.Rect != null ? (windowEvent.WindowInfo.Rect.Bottom - windowEvent.WindowInfo.Rect.Top) : 0,
                Visible = windowEvent.WindowInfo?.Visible ?? false,
                Active = windowEvent.WindowInfo?.Active ?? false,
                ThreadId = windowEvent.WindowInfo?.ThreadId ?? 0,
                ParentHwnd = windowEvent.WindowInfo != null && windowEvent.WindowInfo.ParentHwnd != IntPtr.Zero 
                    ? "0x" + windowEvent.WindowInfo.ParentHwnd.ToInt64().ToString("X8") 
                    : string.Empty,
                RawWindowInfoJson = windowEvent.WindowInfo?.ToJson(),
                Details = windowEvent.Details
            };

            logEntry.SetLogLevelAndContext(windowEvent.EventType);
            logEntry.AddAIContext(windowEvent);

            return logEntry;
        }

        private void SetLogLevelAndContext(WindowEventType eventType)
        {
            switch (eventType)
            {
                case WindowEventType.WindowCreate:
                    Level = LogLevel.Info;
                    ContextSummary = "新窗口创建";
                    AnalysisHint = "检查窗口初始化代码、资源分配是否正常";
                    break;
                case WindowEventType.WindowDestroy:
                    Level = LogLevel.Info;
                    ContextSummary = "窗口销毁";
                    AnalysisHint = "检查资源释放、清理代码是否完整";
                    break;
                case WindowEventType.WindowActivate:
                    Level = LogLevel.Debug;
                    ContextSummary = "窗口获得焦点";
                    AnalysisHint = "关注焦点切换逻辑、窗口状态管理";
                    break;
                case WindowEventType.WindowDeactivate:
                    Level = LogLevel.Debug;
                    ContextSummary = "窗口失去焦点";
                    AnalysisHint = "检查焦点丢失后的状态保存";
                    break;
                case WindowEventType.WindowMove:
                    Level = LogLevel.Debug;
                    ContextSummary = "窗口位置改变";
                    AnalysisHint = "验证布局代码、坐标计算";
                    break;
                case WindowEventType.WindowResize:
                    Level = LogLevel.Debug;
                    ContextSummary = "窗口大小改变";
                    AnalysisHint = "检查响应式布局、控件重绘";
                    break;
                case WindowEventType.WindowTitleChange:
                    Level = LogLevel.Debug;
                    ContextSummary = "窗口标题改变";
                    AnalysisHint = "关注状态更新逻辑";
                    break;
                case WindowEventType.WindowShow:
                    Level = LogLevel.Debug;
                    ContextSummary = "窗口显示";
                    AnalysisHint = "验证显示状态管理";
                    break;
                case WindowEventType.WindowHide:
                    Level = LogLevel.Debug;
                    ContextSummary = "窗口隐藏";
                    AnalysisHint = "检查隐藏状态下的行为";
                    break;
                default:
                    Level = LogLevel.Debug;
                    ContextSummary = "未知事件";
                    AnalysisHint = "进一步分析事件类型";
                    break;
            }
        }

        private void AddAIContext(WindowEvent windowEvent)
        {
            if (windowEvent.WindowInfo != null)
            {
                EventMetadata["is_visible"] = windowEvent.WindowInfo.Visible.ToString();
                EventMetadata["is_active"] = windowEvent.WindowInfo.Active.ToString();
                EventMetadata["has_parent"] = (windowEvent.WindowInfo.ParentHwnd != IntPtr.Zero).ToString();
                EventMetadata["window_size"] = $"{Width}x{Height}";
                EventMetadata["window_position"] = $"({Left}, {Top})";
            }
            
            EventMetadata["event_sequence"] = DateTime.Now.Ticks.ToString();
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        public static LogEntry FromJson(string json)
        {
            return JsonConvert.DeserializeObject<LogEntry>(json);
        }

        public string ToAIAnalysisFormat()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== 窗口事件日志 [{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] ===");
            sb.AppendLine($"事件ID: {EventId}");
            sb.AppendLine($"事件类型: {EventType}");
            sb.AppendLine($"日志级别: {Level}");
            sb.AppendLine();
            sb.AppendLine($"--- 窗口信息 ---");
            sb.AppendLine($"窗口标题: {WindowTitle}");
            sb.AppendLine($"窗口类名: {ClassName}");
            sb.AppendLine($"窗口句柄: {Hwnd}");
            sb.AppendLine($"父窗口句柄: {ParentHwnd}");
            sb.AppendLine();
            sb.AppendLine($"--- 进程信息 ---");
            sb.AppendLine($"进程名: {ProcessName}");
            sb.AppendLine($"进程ID: {ProcessId}");
            sb.AppendLine($"线程ID: {ThreadId}");
            sb.AppendLine();
            sb.AppendLine($"--- 位置和尺寸 ---");
            sb.AppendLine($"位置: (X:{Left}, Y:{Top})");
            sb.AppendLine($"大小: {Width}x{Height}");
            sb.AppendLine($"边界: [L:{Left}, T:{Top}, R:{Right}, B:{Bottom}]");
            sb.AppendLine();
            sb.AppendLine($"--- 状态 ---");
            sb.AppendLine($"可见: {(Visible ? "是" : "否")}");
            sb.AppendLine($"活跃: {(Active ? "是" : "否")}");
            sb.AppendLine();
            sb.AppendLine($"--- AI分析上下文 ---");
            sb.AppendLine($"摘要: {ContextSummary}");
            sb.AppendLine($"分析提示: {AnalysisHint}");
            sb.AppendLine();
            if (!string.IsNullOrEmpty(Details))
            {
                sb.AppendLine($"--- 详细信息 ---");
                sb.AppendLine(Details);
                sb.AppendLine();
            }
            sb.AppendLine($"--- 元数据 ---");
            foreach (var kvp in EventMetadata)
            {
                sb.AppendLine($"{kvp.Key}: {kvp.Value}");
            }
            sb.AppendLine("====================================");
            sb.AppendLine();
            return sb.ToString();
        }
    }
}
