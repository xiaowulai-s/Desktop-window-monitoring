using System;
using Newtonsoft.Json;

namespace WindowMonitor.Communication.Models
{
    public class WindowInfo
    {
        public IntPtr Hwnd { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public uint ProcessId { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public Rectangle Rect { get; set; } = new Rectangle();
        public bool Visible { get; set; }
        public bool Active { get; set; }
        public IntPtr ParentHwnd { get; set; }
        public uint ThreadId { get; set; }
        public DateTime CreationTime { get; set; }

        public WindowInfo()
        {
            CreationTime = DateTime.Now;
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        public static WindowInfo? FromJson(string json)
        {
            return JsonConvert.DeserializeObject<WindowInfo>(json);
        }
    }

    public class Rectangle
    {
        public int Left { get; set; }
        public int Top { get; set; }
        public int Right { get; set; }
        public int Bottom { get; set; }

        public int Width => Right - Left;
        public int Height => Bottom - Top;

        public Rectangle() { }

        public Rectangle(int left, int top, int right, int bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }
    }
}
