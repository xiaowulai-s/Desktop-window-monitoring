using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using WindowMonitor.Business.Models;

namespace WindowMonitor.Business
{
    public class LogManager
    {
        #region Singleton

        private static readonly Lazy<LogManager> _instance = new Lazy<LogManager>(() => new LogManager());
        public static LogManager Instance => _instance.Value;

        #endregion

        #region Events

        public event EventHandler<LogEntry> LogEntryAdded;

        #endregion

        #region Private Fields

        private readonly ConcurrentQueue<LogEntry> _logQueue;
        private readonly BlockingCollection<LogEntry> _logBuffer;
        private readonly List<LogEntry> _inMemoryLogs;
        private readonly object _lockObject = new object();
        private Thread _writeThread;
        private volatile bool _isWriting;
        private LogLevel _currentLogLevel;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private int _maxInMemoryLogs;

        #endregion

        #region Constructor

        private LogManager()
        {
            _logQueue = new ConcurrentQueue<LogEntry>();
            _logBuffer = new BlockingCollection<LogEntry>();
            _inMemoryLogs = new List<LogEntry>();
            _cancellationTokenSource = new CancellationTokenSource();
            _isWriting = false;
            _maxInMemoryLogs = ConfigManager.Instance.MaxLogEntriesInMemory;

            SetLogLevel(ParseLogLevel(ConfigManager.Instance.LogLevel));
        }

        #endregion

        #region Public Properties

        public LogLevel CurrentLogLevel => _currentLogLevel;

        public IReadOnlyList<LogEntry> InMemoryLogs
        {
            get
            {
                lock (_lockObject)
                {
                    return _inMemoryLogs.ToList().AsReadOnly();
                }
            }
        }

        #endregion

        #region Public Methods

        public void WriteLog(LogEntry logEntry)
        {
            if (logEntry == null)
                return;

            DebugLogger.Log($"[LogManager] WriteLog called: {logEntry.EventType}, Level={logEntry.Level}, CurrentLevel={_currentLogLevel}");

            if (!ShouldLog(logEntry.Level))
            {
                DebugLogger.Log($"[LogManager] Log filtered by level");
                return;
            }

            _logBuffer.Add(logEntry);
            DebugLogger.Log($"[LogManager] Log added to buffer");
        }

        public void SetLogLevel(LogLevel level)
        {
            _currentLogLevel = level;
            ConfigManager.Instance.LogLevel = level.ToString();
        }

        public void StartLogWriting()
        {
            if (_isWriting)
                return;

            _isWriting = true;
            _writeThread = new Thread(WriteLogThread)
            {
                IsBackground = true,
                Name = "LogWritingThread"
            };
            _writeThread.Start();
        }

        public void StopLogWriting()
        {
            if (!_isWriting)
                return;

            _isWriting = false;
            _cancellationTokenSource.Cancel();
            _logBuffer.CompleteAdding();

            _writeThread?.Join(TimeSpan.FromSeconds(5));
        }

        public List<LogEntry> QueryLogs(Func<LogEntry, bool>? filter = null)
        {
            lock (_lockObject)
            {
                if (filter == null)
                    return _inMemoryLogs.ToList();

                return _inMemoryLogs.Where(filter).ToList();
            }
        }

        public void ClearInMemoryLogs()
        {
            lock (_lockObject)
            {
                _inMemoryLogs.Clear();
            }
        }

        #endregion

        #region Private Methods

        private bool ShouldLog(LogLevel level)
        {
            return level >= _currentLogLevel;
        }

        private LogLevel ParseLogLevel(string levelString)
        {
            if (Enum.TryParse<LogLevel>(levelString, true, out var level))
            {
                return level;
            }
            return LogLevel.Info;
        }

        private void WriteLogThread()
        {
            try
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    if (_logBuffer.TryTake(out var logEntry, TimeSpan.FromMilliseconds(100)))
                    {
                        ProcessLogEntry(logEntry);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }

            // Process remaining logs
            while (_logBuffer.TryTake(out var logEntry))
            {
                ProcessLogEntry(logEntry);
            }
        }

        private void ProcessLogEntry(LogEntry logEntry)
        {
            // Add to in-memory collection
            lock (_lockObject)
            {
                _inMemoryLogs.Add(logEntry);

                // Trim if exceeded max count
                while (_inMemoryLogs.Count > _maxInMemoryLogs)
                {
                    _inMemoryLogs.RemoveAt(0);
                }
            }

            // Add to queue for persistence
            _logQueue.Enqueue(logEntry);

            // Notify listeners
            OnLogEntryAdded(logEntry);
        }

        protected virtual void OnLogEntryAdded(LogEntry e)
        {
            LogEntryAdded?.Invoke(this, e);
        }

        #endregion
    }
}
