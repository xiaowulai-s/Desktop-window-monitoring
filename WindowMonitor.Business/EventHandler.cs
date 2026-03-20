using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using WindowMonitor.Business.Models;
using WindowMonitor.Communication.Models;

namespace WindowMonitor.Business
{
    public interface IWindowEventHandler
    {
        void HandleEvent(WindowEvent windowEvent);
    }

    public class EventHandler
    {
        #region Events

        public event EventHandler<LogEntry>? LogEntryCreated;

        #endregion

        #region Private Fields

        private readonly ConcurrentDictionary<WindowEventType, List<IWindowEventHandler>> _handlers;
        private readonly object _lockObject = new object();
        private readonly BlockingCollection<WindowEvent> _eventQueue;
        private Thread _processingThread;
        private volatile bool _isProcessing;
        private readonly CancellationTokenSource _cancellationTokenSource;

        #endregion

        #region Constructor

        public EventHandler()
        {
            _handlers = new ConcurrentDictionary<WindowEventType, List<IWindowEventHandler>>();
            _eventQueue = new BlockingCollection<WindowEvent>();
            _cancellationTokenSource = new CancellationTokenSource();
            _isProcessing = false;
        }

        #endregion

        #region Public Methods

        public void RegisterHandler(WindowEventType eventType, IWindowEventHandler handler)
        {
            lock (_lockObject)
            {
                if (!_handlers.TryGetValue(eventType, out var handlers))
                {
                    handlers = new List<IWindowEventHandler>();
                    _handlers[eventType] = handlers;
                }
                if (!handlers.Contains(handler))
                {
                    handlers.Add(handler);
                }
            }
        }

        public void UnregisterHandler(WindowEventType eventType, IWindowEventHandler handler)
        {
            lock (_lockObject)
            {
                if (_handlers.TryGetValue(eventType, out var handlers))
                {
                    handlers.Remove(handler);
                }
            }
        }

        public void ProcessEvent(WindowEvent windowEvent)
        {
            if (windowEvent == null)
                return;

            DebugLogger.Log($"[EventHandler] ProcessEvent: {windowEvent.EventType}");
            _eventQueue.Add(windowEvent);
        }

        public void StartProcessing()
        {
            if (_isProcessing)
                return;

            _isProcessing = true;
            _processingThread = new Thread(ProcessEventQueue)
            {
                IsBackground = true,
                Name = "EventProcessingThread"
            };
            _processingThread.Start();
        }

        public void StopProcessing()
        {
            if (!_isProcessing)
                return;

            _isProcessing = false;
            _cancellationTokenSource.Cancel();
            _eventQueue.CompleteAdding();

            _processingThread?.Join(TimeSpan.FromSeconds(5));
        }

        #endregion

        #region Private Methods

        private void ProcessEventQueue()
        {
            try
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    if (_eventQueue.TryTake(out var windowEvent, TimeSpan.FromMilliseconds(100)))
                    {
                        ProcessEventInternal(windowEvent);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
        }

        private void ProcessEventInternal(WindowEvent windowEvent)
        {
            DebugLogger.Log($"[EventHandler] ProcessEventInternal: {windowEvent.EventType}");
            // Create log entry
            var logEntry = LogEntry.FromWindowEvent(windowEvent);
            DebugLogger.Log($"[EventHandler] LogEntry created: {logEntry.EventType}, Level: {logEntry.Level}");
            OnLogEntryCreated(logEntry);

            // Notify registered handlers
            if (_handlers.TryGetValue(windowEvent.EventType, out var handlers))
            {
                foreach (var handler in handlers.ToArray())
                {
                    try
                    {
                        handler.HandleEvent(windowEvent);
                    }
                    catch
                    {
                        // Ignore handler errors
                    }
                }
            }

            // Also notify handlers registered for all events
            if (_handlers.TryGetValue(WindowEventType.Unknown, out var allEventHandlers))
            {
                foreach (var handler in allEventHandlers.ToArray())
                {
                    try
                    {
                        handler.HandleEvent(windowEvent);
                    }
                    catch
                    {
                        // Ignore handler errors
                    }
                }
            }
        }

        protected virtual void OnLogEntryCreated(LogEntry e)
        {
            LogEntryCreated?.Invoke(this, e);
        }

        #endregion
    }
}
