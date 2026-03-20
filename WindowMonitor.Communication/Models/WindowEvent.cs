using System;
using Newtonsoft.Json;

namespace WindowMonitor.Communication.Models
{
    public enum WindowEventType
    {
        Unknown,
        WindowCreate,
        WindowDestroy,
        WindowMove,
        WindowResize,
        WindowActivate,
        WindowDeactivate,
        WindowTitleChange,
        WindowShow,
        WindowHide
    }

    public class WindowEvent
    {
        public string EventId { get; set; } = string.Empty;
        public WindowEventType EventType { get; set; }
        public WindowInfo WindowInfo { get; set; } = new WindowInfo();
        public DateTime Timestamp { get; set; }
        public string Details { get; set; } = string.Empty;

        public WindowEvent()
        {
            EventId = Guid.NewGuid().ToString();
            Timestamp = DateTime.Now;
        }

        public WindowEvent(WindowEventType eventType, WindowInfo windowInfo) : this()
        {
            EventType = eventType;
            WindowInfo = windowInfo;
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        public static WindowEvent? FromJson(string json)
        {
            return JsonConvert.DeserializeObject<WindowEvent>(json);
        }

        public string GetEventTypeName()
        {
            return EventType.ToString();
        }
    }
}
