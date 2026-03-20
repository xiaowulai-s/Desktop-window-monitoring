using System;
using System.Collections.Generic;
using System.Threading;
using WindowMonitor.Business.Models;
using WindowMonitor.Communication;
using WindowMonitor.Communication.Models;

namespace WindowMonitor.Business
{
    public enum MonitorState
    {
        Stopped,
        Running,
        Paused
    }

    public class MonitorEngine : IDisposable
    {
        #region Singleton

        private static readonly Lazy<MonitorEngine> _instance = new Lazy<MonitorEngine>(() => new MonitorEngine());
        public static MonitorEngine Instance => _instance.Value;

        #endregion

        #region Events

        public event EventHandler<MonitorState>? StateChanged;
        public event EventHandler<WindowEvent>? WindowEventOccurred;
        public event EventHandler<LogEntry>? LogEntryReceived;
        public event EventHandler<MonitorMode>? MonitorModeChanged;
        public event EventHandler<WindowInfo?>? TargetWindowChanged;

        #endregion

        #region Private Fields

        private readonly WindowHook _windowHook;
        private readonly EventHandler _eventHandler;
        private MonitorState _state;
        private readonly object _lockObject = new object();
        private bool _disposed;

        #endregion

        #region Constructor

        private MonitorEngine()
        {
            _windowHook = new WindowHook();
            _eventHandler = new EventHandler();
            _state = MonitorState.Stopped;

            // Wire up events
            _windowHook.WindowEventOccurred += OnWindowEventOccurred;
            _eventHandler.LogEntryCreated += OnLogEntryCreated;
        }

        #endregion

        #region Public Properties

        public MonitorState State
        {
            get => _state;
            private set
            {
                if (_state != value)
                {
                    _state = value;
                    OnStateChanged(_state);
                }
            }
        }

        public WindowHook WindowHook => _windowHook;

        public MonitorMode CurrentMonitorMode => _windowHook.Mode;

        public WindowInfo? CurrentTargetWindow { get; private set; }

        #endregion

        #region Public Methods

        public bool StartMonitoring()
        {
            lock (_lockObject)
            {
                if (State == MonitorState.Running)
                    return true;

                try
                {
                    // Load config if not already loaded
                    ConfigManager.Instance.LoadConfig();

                    // Start log manager
                    LogManager.Instance.StartLogWriting();

                    // Start event handler
                    _eventHandler.StartProcessing();

                    // Set up window hook
                    if (!_windowHook.SetHook())
                    {
                        throw new InvalidOperationException("Failed to set up window hook");
                    }

                    State = MonitorState.Running;
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        public bool StopMonitoring()
        {
            lock (_lockObject)
            {
                if (State == MonitorState.Stopped)
                    return true;

                try
                {
                    // Unset window hook
                    _windowHook.UnsetHook();

                    // Stop event handler
                    _eventHandler.StopProcessing();

                    // Stop log manager
                    LogManager.Instance.StopLogWriting();

                    // Save config
                    ConfigManager.Instance.SaveConfig();

                    State = MonitorState.Stopped;
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        public bool PauseMonitoring()
        {
            lock (_lockObject)
            {
                if (State != MonitorState.Running)
                    return false;

                State = MonitorState.Paused;
                return true;
            }
        }

        public bool ResumeMonitoring()
        {
            lock (_lockObject)
            {
                if (State != MonitorState.Paused)
                    return false;

                State = MonitorState.Running;
                return true;
            }
        }

        public List<WindowInfo> GetCurrentWindows()
        {
            return _windowHook.GetCurrentWindows();
        }

        public void SetTargetWindow(IntPtr hwnd)
        {
            _windowHook.SetTargetWindow(hwnd);
            if (hwnd != IntPtr.Zero)
            {
                CurrentTargetWindow = _windowHook.GetWindowInfo(hwnd);
            }
            else
            {
                CurrentTargetWindow = null;
            }
            OnMonitorModeChanged(_windowHook.Mode);
            OnTargetWindowChanged(CurrentTargetWindow);
        }

        public void SetMonitorAllWindows()
        {
            _windowHook.SetMonitorAllWindows();
            CurrentTargetWindow = null;
            OnMonitorModeChanged(_windowHook.Mode);
            OnTargetWindowChanged(null);
        }

        public void RegisterEventHandler(WindowEventType eventType, IWindowEventHandler handler)
        {
            _eventHandler.RegisterHandler(eventType, handler);
        }

        public void UnregisterEventHandler(WindowEventType eventType, IWindowEventHandler handler)
        {
            _eventHandler.UnregisterHandler(eventType, handler);
        }

        #endregion

        #region Private Methods

        private void OnWindowEventOccurred(object sender, WindowEvent e)
        {
            if (State == MonitorState.Paused)
                return;

            // Forward to event handler
            _eventHandler.ProcessEvent(e);

            // Notify listeners
            OnWindowEventOccurred(e);
        }

        private void OnLogEntryCreated(object sender, LogEntry e)
        {
            // Forward to log manager
            LogManager.Instance.WriteLog(e);

            // Notify listeners
            OnLogEntryReceived(e);
        }

        protected virtual void OnStateChanged(MonitorState newState)
        {
            StateChanged?.Invoke(this, newState);
        }

        protected virtual void OnWindowEventOccurred(WindowEvent e)
        {
            WindowEventOccurred?.Invoke(this, e);
        }

        protected virtual void OnLogEntryReceived(LogEntry e)
        {
            LogEntryReceived?.Invoke(this, e);
        }

        protected virtual void OnMonitorModeChanged(MonitorMode mode)
        {
            MonitorModeChanged?.Invoke(this, mode);
        }

        protected virtual void OnTargetWindowChanged(WindowInfo? windowInfo)
        {
            TargetWindowChanged?.Invoke(this, windowInfo);
        }

        #endregion

        #region IDisposable

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
                StopMonitoring();
                _windowHook.Dispose();
            }

            _disposed = true;
        }

        ~MonitorEngine()
        {
            Dispose(false);
        }

        #endregion
    }
}
