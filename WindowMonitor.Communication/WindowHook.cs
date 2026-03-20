using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using WindowMonitor.Communication.Models;

namespace WindowMonitor.Communication
{
    public enum MonitorMode
    {
        AllWindows,
        SingleWindow
    }

    public class WindowHook : IDisposable
    {
        #region Events

        public event EventHandler<WindowEvent>? WindowEventOccurred;

        #endregion

        #region Private Fields

        private IntPtr _winEventHook;
        private WindowsApi.WinEventDelegate _winEventDelegate;
        private bool _isHooked;
        private readonly object _lockObject = new object();
        private readonly Dictionary<IntPtr, WindowInfo> _windowCache;
        private IntPtr _lastForegroundWindow;
        
        private MonitorMode _monitorMode;
        private IntPtr _targetWindowHandle;
        private string _targetWindowTitle;

        #endregion

        #region Public Properties

        public MonitorMode Mode
        {
            get => _monitorMode;
            set
            {
                lock (_lockObject)
                {
                    _monitorMode = value;
                }
            }
        }

        public IntPtr TargetWindowHandle
        {
            get => _targetWindowHandle;
            set
            {
                lock (_lockObject)
                {
                    _targetWindowHandle = value;
                }
            }
        }

        public string TargetWindowTitle
        {
            get => _targetWindowTitle;
            set
            {
                lock (_lockObject)
                {
                    _targetWindowTitle = value;
                }
            }
        }

        #endregion

        #region Constructor

        public WindowHook()
        {
            _windowCache = new Dictionary<IntPtr, WindowInfo>();
            _winEventDelegate = WinEventCallback;
            _isHooked = false;
            _lastForegroundWindow = IntPtr.Zero;
            _monitorMode = MonitorMode.AllWindows;
            _targetWindowHandle = IntPtr.Zero;
            _targetWindowTitle = string.Empty;
        }

        #endregion

        #region Public Methods

        public bool SetHook()
        {
            lock (_lockObject)
            {
                if (_isHooked)
                    return true;

                try
                {
                    // Set up WinEvent hook for various window events
                    uint eventMin = WindowsApi.EVENT_OBJECT_CREATE;
                    uint eventMax = WindowsApi.EVENT_OBJECT_NAMECHANGE;

                    _winEventHook = WindowsApi.SetWinEventHook(
                        eventMin,
                        eventMax,
                        IntPtr.Zero,
                        _winEventDelegate,
                        0,
                        0,
                        WindowsApi.WINEVENT_OUTOFCONTEXT);

                    if (_winEventHook == IntPtr.Zero)
                    {
                        throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                    }

                    // Enumerate existing windows to build initial cache
                    EnumerateExistingWindows();

                    _isHooked = true;
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        public bool UnsetHook()
        {
            lock (_lockObject)
            {
                if (!_isHooked)
                    return true;

                try
                {
                    if (_winEventHook != IntPtr.Zero)
                    {
                        WindowsApi.UnhookWinEvent(_winEventHook);
                        _winEventHook = IntPtr.Zero;
                    }

                    _windowCache.Clear();
                    _isHooked = false;
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        public WindowInfo GetWindowInfo(IntPtr hwnd)
        {
            if (!WindowsApi.IsWindow(hwnd))
                return null;

            var windowInfo = new WindowInfo
            {
                Hwnd = hwnd,
                Title = WindowsApi.GetWindowTitle(hwnd),
                ClassName = WindowsApi.GetWindowClassName(hwnd),
                ProcessId = WindowsApi.GetWindowProcessId(hwnd),
                Visible = WindowsApi.IsWindowVisible(hwnd),
                ParentHwnd = WindowsApi.GetParent(hwnd)
            };

            windowInfo.ProcessName = WindowsApi.GetProcessName(windowInfo.ProcessId);

            var rect = WindowsApi.GetWindowRectangle(hwnd);
            windowInfo.Rect = new Rectangle(rect.Left, rect.Top, rect.Right, rect.Bottom);

            windowInfo.ThreadId = (uint)WindowsApi.GetWindowThreadProcessId(hwnd, out _).ToInt32();
            windowInfo.Active = (hwnd == WindowsApi.GetForegroundWindow());

            return windowInfo;
        }

        public List<WindowInfo> GetCurrentWindows()
        {
            var windows = new List<WindowInfo>();

            WindowsApi.EnumWindows((hwnd, lParam) =>
            {
                if (IsValidWindow(hwnd))
                {
                    var windowInfo = GetWindowInfo(hwnd);
                    if (windowInfo != null)
                    {
                        windows.Add(windowInfo);
                    }
                }
                return true;
            }, IntPtr.Zero);

            return windows;
        }

        public void SetTargetWindow(IntPtr hwnd)
        {
            lock (_lockObject)
            {
                _targetWindowHandle = hwnd;
                if (hwnd != IntPtr.Zero)
                {
                    _monitorMode = MonitorMode.SingleWindow;
                    var windowInfo = GetWindowInfo(hwnd);
                    if (windowInfo != null)
                    {
                        _targetWindowTitle = windowInfo.Title;
                    }
                }
                else
                {
                    _monitorMode = MonitorMode.AllWindows;
                    _targetWindowTitle = string.Empty;
                }
            }
        }

        public void SetMonitorAllWindows()
        {
            lock (_lockObject)
            {
                _monitorMode = MonitorMode.AllWindows;
                _targetWindowHandle = IntPtr.Zero;
                _targetWindowTitle = string.Empty;
            }
        }

        #endregion

        #region Private Methods

        private void WinEventCallback(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (idObject != 0 || idChild != 0)
                return;

            if (!IsValidWindow(hwnd))
                return;

            try
            {
                ProcessWinEvent(eventType, hwnd);
            }
            catch
            {
                // Ignore errors in callback
            }
        }

        private void ProcessWinEvent(uint eventType, IntPtr hwnd)
        {
            WindowEvent windowEvent = null;

            switch (eventType)
            {
                case WindowsApi.EVENT_OBJECT_CREATE:
                    windowEvent = CreateWindowEvent(WindowEventType.WindowCreate, hwnd);
                    break;

                case WindowsApi.EVENT_OBJECT_DESTROY:
                    windowEvent = CreateWindowEvent(WindowEventType.WindowDestroy, hwnd);
                    // Remove from cache
                    if (_windowCache.ContainsKey(hwnd))
                    {
                        _windowCache.Remove(hwnd);
                    }
                    break;

                case WindowsApi.EVENT_OBJECT_SHOW:
                    windowEvent = CreateWindowEvent(WindowEventType.WindowShow, hwnd);
                    break;

                case WindowsApi.EVENT_OBJECT_HIDE:
                    windowEvent = CreateWindowEvent(WindowEventType.WindowHide, hwnd);
                    break;

                case WindowsApi.EVENT_OBJECT_LOCATIONCHANGE:
                    windowEvent = HandleLocationChange(hwnd);
                    break;

                case WindowsApi.EVENT_OBJECT_NAMECHANGE:
                    windowEvent = CreateWindowEvent(WindowEventType.WindowTitleChange, hwnd);
                    break;

                case WindowsApi.EVENT_SYSTEM_FOREGROUND:
                    windowEvent = HandleForegroundChange(hwnd);
                    break;
            }

            if (windowEvent != null && ShouldProcessWindowEvent(windowEvent))
            {
                OnWindowEventOccurred(windowEvent);
            }
        }

        private bool ShouldProcessWindowEvent(WindowEvent windowEvent)
        {
            lock (_lockObject)
            {
                if (_monitorMode == MonitorMode.AllWindows)
                {
                    return true;
                }

                if (_targetWindowHandle == IntPtr.Zero && string.IsNullOrEmpty(_targetWindowTitle))
                {
                    return true;
                }

                if (windowEvent.WindowInfo == null)
                {
                    return false;
                }

                if (_targetWindowHandle != IntPtr.Zero)
                {
                    return windowEvent.WindowInfo.Hwnd == _targetWindowHandle;
                }

                if (!string.IsNullOrEmpty(_targetWindowTitle))
                {
                    return windowEvent.WindowInfo.Title == _targetWindowTitle;
                }

                return true;
            }
        }

        private WindowEvent CreateWindowEvent(WindowEventType eventType, IntPtr hwnd)
        {
            var windowInfo = GetWindowInfo(hwnd);
            if (windowInfo == null)
                return null;

            // Update cache
            _windowCache[hwnd] = windowInfo;

            return new WindowEvent(eventType, windowInfo);
        }

        private WindowEvent HandleLocationChange(IntPtr hwnd)
        {
            var currentInfo = GetWindowInfo(hwnd);
            if (currentInfo == null)
                return null;

            WindowEventType eventType;
            if (_windowCache.TryGetValue(hwnd, out var cachedInfo))
            {
                bool moved = cachedInfo.Rect.Left != currentInfo.Rect.Left || cachedInfo.Rect.Top != currentInfo.Rect.Top;
                bool resized = cachedInfo.Rect.Width != currentInfo.Rect.Width || cachedInfo.Rect.Height != currentInfo.Rect.Height;

                if (resized)
                {
                    eventType = WindowEventType.WindowResize;
                }
                else if (moved)
                {
                    eventType = WindowEventType.WindowMove;
                }
                else
                {
                    return null;
                }
            }
            else
            {
                eventType = WindowEventType.WindowMove;
            }

            _windowCache[hwnd] = currentInfo;
            return new WindowEvent(eventType, currentInfo);
        }

        private WindowEvent HandleForegroundChange(IntPtr hwnd)
        {
            if (hwnd == _lastForegroundWindow)
                return null;

            WindowEvent deactivateEvent = null;
            if (_lastForegroundWindow != IntPtr.Zero && _windowCache.ContainsKey(_lastForegroundWindow))
            {
                var deactivateInfo = _windowCache[_lastForegroundWindow];
                deactivateInfo.Active = false;
                deactivateEvent = new WindowEvent(WindowEventType.WindowDeactivate, deactivateInfo);
            }

            _lastForegroundWindow = hwnd;
            var activateEvent = CreateWindowEvent(WindowEventType.WindowActivate, hwnd);

            if (deactivateEvent != null)
            {
                OnWindowEventOccurred(deactivateEvent);
            }

            return activateEvent;
        }

        private void EnumerateExistingWindows()
        {
            _windowCache.Clear();

            WindowsApi.EnumWindows((hwnd, lParam) =>
            {
                if (IsValidWindow(hwnd))
                {
                    var windowInfo = GetWindowInfo(hwnd);
                    if (windowInfo != null)
                    {
                        _windowCache[hwnd] = windowInfo;
                    }
                }
                return true;
            }, IntPtr.Zero);

            _lastForegroundWindow = WindowsApi.GetForegroundWindow();
        }

        private bool IsValidWindow(IntPtr hwnd)
        {
            if (!WindowsApi.IsWindow(hwnd))
                return false;

            // Skip invisible windows unless they have WS_EX_APPWINDOW
            long exStyle = WindowsApi.GetWindowLong(hwnd, WindowsApi.GWL_EXSTYLE);
            if (!WindowsApi.IsWindowVisible(hwnd) && (exStyle & WindowsApi.WS_EX_APPWINDOW) == 0)
                return false;

            // Skip tool windows
            if ((exStyle & WindowsApi.WS_EX_TOOLWINDOW) != 0)
                return false;

            // Skip windows without title unless they have WS_EX_APPWINDOW
            string title = WindowsApi.GetWindowTitle(hwnd);
            if (string.IsNullOrEmpty(title) && (exStyle & WindowsApi.WS_EX_APPWINDOW) == 0)
                return false;

            // Skip owned windows (dialogs, etc.) unless they are app windows
            if (WindowsApi.GetWindow(hwnd, WindowsApi.GW_OWNER) != IntPtr.Zero && (exStyle & WindowsApi.WS_EX_APPWINDOW) == 0)
                return false;

            return true;
        }

        protected virtual void OnWindowEventOccurred(WindowEvent e)
        {
            WindowEventOccurred?.Invoke(this, e);
        }

        #endregion

        #region IDisposable

        private bool _disposed = false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                // Dispose managed resources
            }

            UnsetHook();
            _disposed = true;
        }

        ~WindowHook()
        {
            Dispose(false);
        }

        #endregion
    }
}
