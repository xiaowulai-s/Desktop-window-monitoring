using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using WindowMonitor.Business;
using WindowMonitor.Business.Models;
using WindowMonitor.Communication;
using WindowMonitor.Communication.Models;

namespace WindowMonitor.UI.ViewModels
{
    public class MainWindowViewModel : ObservableObject, IDisposable
    {
        #region Private Fields

        private MonitorState _monitorState;
        private string _statusText;
        private LogEntry? _selectedLogEntry;
        private WindowInfo? _selectedWindow;
        private WindowInfo? _targetWindow;
        private MonitorMode _monitorMode;
        private bool _monitoringSingleWindow;
        private readonly Dispatcher _dispatcher;
        private bool _disposed;

        #endregion

        #region Public Properties

        public ObservableCollection<LogEntry> LogEntries { get; }
        public ObservableCollection<WindowInfo> CurrentWindows { get; }

        public MonitorState MonitorState
        {
            get => _monitorState;
            set => SetProperty(ref _monitorState, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public LogEntry? SelectedLogEntry
        {
            get => _selectedLogEntry;
            set => SetProperty(ref _selectedLogEntry, value);
        }

        public WindowInfo? SelectedWindow
        {
            get => _selectedWindow;
            set => SetProperty(ref _selectedWindow, value);
        }

        public WindowInfo? TargetWindow
        {
            get => _targetWindow;
            set => SetProperty(ref _targetWindow, value);
        }

        public MonitorMode MonitorMode
        {
            get => _monitorMode;
            set => SetProperty(ref _monitorMode, value);
        }

        public bool MonitoringSingleWindow
        {
            get => _monitoringSingleWindow;
            set => SetProperty(ref _monitoringSingleWindow, value);
        }

        public RelayCommand StartMonitoringCommand { get; }
        public RelayCommand StopMonitoringCommand { get; }
        public RelayCommand PauseMonitoringCommand { get; }
        public RelayCommand ClearLogsCommand { get; }
        public RelayCommand RefreshWindowsCommand { get; }
        public RelayCommand SelectTargetWindowCommand { get; }
        public RelayCommand MonitorAllWindowsCommand { get; }
        public RelayCommand ExportLogsCommand { get; }

        #endregion

        #region Constructor

        public MainWindowViewModel()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            LogEntries = new ObservableCollection<LogEntry>();
            CurrentWindows = new ObservableCollection<WindowInfo>();
            _monitorState = MonitorState.Stopped;
            _statusText = "就绪";
            _selectedLogEntry = null!;
            _selectedWindow = null!;
            _targetWindow = null!;
            _monitorMode = MonitorMode.AllWindows;
            _monitoringSingleWindow = false;

            // Initialize commands
            StartMonitoringCommand = new RelayCommand(
                _ => StartMonitoring(),
                _ => MonitorState == MonitorState.Stopped);
            StopMonitoringCommand = new RelayCommand(
                _ => StopMonitoring(),
                _ => MonitorState != MonitorState.Stopped);
            PauseMonitoringCommand = new RelayCommand(
                _ => PauseMonitoring(),
                _ => MonitorState == MonitorState.Running);
            ClearLogsCommand = new RelayCommand(
                _ => ClearLogs());
            RefreshWindowsCommand = new RelayCommand(
                _ => RefreshWindows());
            SelectTargetWindowCommand = new RelayCommand(
                _ => SelectTargetWindow(),
                _ => SelectedWindow != null);
            MonitorAllWindowsCommand = new RelayCommand(
                _ => MonitorAllWindows());
            ExportLogsCommand = new RelayCommand(
                _ => ExportLogs());

            // Wire up events
            MonitorEngine.Instance.StateChanged += OnMonitorStateChanged;
            MonitorEngine.Instance.LogEntryReceived += OnLogEntryReceived;
            MonitorEngine.Instance.MonitorModeChanged += OnMonitorModeChanged;
            MonitorEngine.Instance.TargetWindowChanged += OnTargetWindowChanged;
        }

        #endregion

        #region Public Methods

        public void StartMonitoring()
        {
            if (MonitorEngine.Instance.StartMonitoring())
            {
                StatusText = "监控中...";
                RefreshWindows();
            }
            else
            {
                StatusText = "启动失败";
                MessageBox.Show("无法启动监控，请确保应用程序以管理员身份运行。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void StopMonitoring()
        {
            if (MonitorEngine.Instance.StopMonitoring())
            {
                StatusText = "已停止";
            }
        }

        public void PauseMonitoring()
        {
            if (MonitorState == MonitorState.Running)
            {
                MonitorEngine.Instance.PauseMonitoring();
                StatusText = "已暂停";
            }
            else if (MonitorState == MonitorState.Paused)
            {
                MonitorEngine.Instance.ResumeMonitoring();
                StatusText = "监控中...";
            }
        }

        public void ClearLogs()
        {
            LogEntries.Clear();
            LogManager.Instance.ClearInMemoryLogs();
        }

        public void RefreshWindows()
        {
            CurrentWindows.Clear();
            var windows = MonitorEngine.Instance.GetCurrentWindows();
            foreach (var window in windows)
            {
                CurrentWindows.Add(window);
            }
            UpdateCommands();
        }

        public void SelectTargetWindow()
        {
            if (SelectedWindow == null)
                return;

            MonitorEngine.Instance.SetTargetWindow(SelectedWindow.Hwnd);
            StatusText = $"已选择监控窗口: {SelectedWindow.Title}";
        }

        public void MonitorAllWindows()
        {
            MonitorEngine.Instance.SetMonitorAllWindows();
            StatusText = "已切换到监控所有窗口模式";
        }

        public void ExportLogs()
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
                    DefaultExt = "txt",
                    FileName = $"WindowMonitor_Logs_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
                };

                if (dialog.ShowDialog() == true)
                {
                    var sb = new System.Text.StringBuilder();
                    foreach (var logEntry in LogEntries)
                    {
                        sb.Append(logEntry.ToAIAnalysisFormat());
                    }
                    System.IO.File.WriteAllText(dialog.FileName, sb.ToString());
                    StatusText = $"日志已导出到: {dialog.FileName}";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Private Methods

        private void OnMonitorStateChanged(object? sender, MonitorState newState)
        {
            _dispatcher.Invoke(() =>
            {
                MonitorState = newState;
                UpdateCommands();
            });
        }

        private void OnLogEntryReceived(object? sender, LogEntry logEntry)
        {
            Business.DebugLogger.Log($"[ViewModel] OnLogEntryReceived: {logEntry.EventType}");
            _dispatcher.Invoke(() =>
            {
                LogEntries.Insert(0, logEntry);
                Business.DebugLogger.Log($"[ViewModel] LogEntries.Count = {LogEntries.Count}");

                // Keep only the list manageable
                while (LogEntries.Count > 1000)
                {
                    LogEntries.RemoveAt(LogEntries.Count - 1);
                }

                // Also update window list on create/destroy events
                if (logEntry.EventType == "WindowCreate" || logEntry.EventType == "WindowDestroy")
                {
                    RefreshWindows();
                }
            });
        }

        private void OnMonitorModeChanged(object? sender, MonitorMode mode)
        {
            _dispatcher.Invoke(() =>
            {
                MonitorMode = mode;
                MonitoringSingleWindow = mode == MonitorMode.SingleWindow;
                UpdateCommands();
            });
        }

        private void OnTargetWindowChanged(object? sender, WindowInfo? windowInfo)
        {
            _dispatcher.Invoke(() =>
            {
                TargetWindow = windowInfo;
            });
        }

        private void UpdateCommands()
        {
            StartMonitoringCommand.RaiseCanExecuteChanged();
            StopMonitoringCommand.RaiseCanExecuteChanged();
            PauseMonitoringCommand.RaiseCanExecuteChanged();
            SelectTargetWindowCommand.RaiseCanExecuteChanged();
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
                MonitorEngine.Instance.StopMonitoring();
                MonitorEngine.Instance.StateChanged -= OnMonitorStateChanged;
                MonitorEngine.Instance.LogEntryReceived -= OnLogEntryReceived;
                MonitorEngine.Instance.MonitorModeChanged -= OnMonitorModeChanged;
                MonitorEngine.Instance.TargetWindowChanged -= OnTargetWindowChanged;
            }

            _disposed = true;
        }

        ~MainWindowViewModel()
        {
            Dispose(false);
        }

        #endregion
    }
}
