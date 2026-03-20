using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.Data.Sqlite;
using WindowMonitor.Business;
using WindowMonitor.Business.Models;

namespace WindowMonitor.Data
{
    public class LogStorageEngine
    {
        #region Singleton

        private static readonly Lazy<LogStorageEngine> _instance = new Lazy<LogStorageEngine>(() => new LogStorageEngine());
        public static LogStorageEngine Instance => _instance.Value;

        #endregion

        #region Private Fields

        private string _databasePath;
        private readonly object _lockObject = new object();
        private bool _initialized;

        #endregion

        #region Constructor

        private LogStorageEngine()
        {
            _databasePath = ConfigManager.Instance.DatabasePath;
            _initialized = false;
        }

        #endregion

        #region Public Properties

        public string DatabasePath
        {
            get => _databasePath;
            set
            {
                _databasePath = value;
                _initialized = false;
            }
        }

        #endregion

        #region Public Methods

        public bool Initialize()
        {
            lock (_lockObject)
            {
                if (_initialized)
                    return true;

                try
                {
                    // Ensure directory exists
                    string directory = Path.GetDirectoryName(_databasePath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    // Create database and tables
                    using (var connection = GetConnection())
                    {
                        connection.Open();
                        CreateTables(connection);
                    }

                    _initialized = true;
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        public bool StoreLog(LogEntry logEntry)
        {
            if (!_initialized && !Initialize())
                return false;

            lock (_lockObject)
            {
                try
                {
                    using (var connection = GetConnection())
                    {
                        connection.Open();
                        InsertLogEntry(connection, logEntry);
                    }
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        public List<LogEntry> QueryLogs(int limit = 1000, int offset = 0)
        {
            if (!_initialized && !Initialize())
                return new List<LogEntry>();

            lock (_lockObject)
            {
                var logs = new List<LogEntry>();
                try
                {
                    using (var connection = GetConnection())
                    {
                        connection.Open();
                        logs = SelectLogEntries(connection, limit, offset);
                    }
                }
                catch
                {
                    // Return empty list on error
                }
                return logs;
            }
        }

        public List<LogEntry> QueryLogsByEventType(string eventType, int limit = 1000)
        {
            if (!_initialized && !Initialize())
                return new List<LogEntry>();

            lock (_lockObject)
            {
                var logs = new List<LogEntry>();
                try
                {
                    using (var connection = GetConnection())
                    {
                        connection.Open();
                        logs = SelectLogEntriesByEventType(connection, eventType, limit);
                    }
                }
                catch
                {
                    // Return empty list on error
                }
                return logs;
            }
        }

        public List<LogEntry> QueryLogsByDateRange(DateTime startDate, DateTime endDate, int limit = 1000)
        {
            if (!_initialized && !Initialize())
                return new List<LogEntry>();

            lock (_lockObject)
            {
                var logs = new List<LogEntry>();
                try
                {
                    using (var connection = GetConnection())
                    {
                        connection.Open();
                        logs = SelectLogEntriesByDateRange(connection, startDate, endDate, limit);
                    }
                }
                catch
                {
                    // Return empty list on error
                }
                return logs;
            }
        }

        public bool CleanupOldLogs(int retentionDays)
        {
            if (!_initialized && !Initialize())
                return false;

            lock (_lockObject)
            {
                try
                {
                    var cutoffDate = DateTime.Now.AddDays(-retentionDays);
                    using (var connection = GetConnection())
                    {
                        connection.Open();
                        DeleteOldLogEntries(connection, cutoffDate);
                    }
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        public bool ClearAllLogs()
        {
            if (!_initialized && !Initialize())
                return false;

            lock (_lockObject)
            {
                try
                {
                    using (var connection = GetConnection())
                    {
                        connection.Open();
                        DeleteAllLogEntries(connection);
                    }
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        #endregion

        #region Private Methods

        private SqliteConnection GetConnection()
        {
            return new SqliteConnection($"Data Source={_databasePath}");
        }

        private void CreateTables(SqliteConnection connection)
        {
            string createTableSql = @"
                CREATE TABLE IF NOT EXISTS LogEntries (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Timestamp TEXT NOT NULL,
                    EventId TEXT NOT NULL,
                    EventType TEXT NOT NULL,
                    Level INTEGER NOT NULL,
                    WindowTitle TEXT,
                    ProcessName TEXT,
                    ProcessId INTEGER,
                    Hwnd TEXT,
                    WindowInfoJson TEXT,
                    Details TEXT,
                    SystemInfoJson TEXT
                );

                CREATE INDEX IF NOT EXISTS idx_log_entries_timestamp ON LogEntries(Timestamp);
                CREATE INDEX IF NOT EXISTS idx_log_entries_event_type ON LogEntries(EventType);
                CREATE INDEX IF NOT EXISTS idx_log_entries_level ON LogEntries(Level);
            ";

            using (var command = new SqliteCommand(createTableSql, connection))
            {
                command.ExecuteNonQuery();
            }
        }

        private void InsertLogEntry(SqliteConnection connection, LogEntry logEntry)
        {
            string insertSql = @"
                INSERT INTO LogEntries (
                    Timestamp, EventId, EventType, Level, WindowTitle, 
                    ProcessName, ProcessId, Hwnd, WindowInfoJson, Details, SystemInfoJson
                ) VALUES (
                    @Timestamp, @EventId, @EventType, @Level, @WindowTitle,
                    @ProcessName, @ProcessId, @Hwnd, @WindowInfoJson, @Details, @SystemInfoJson
                );
            ";

            using (var command = new SqliteCommand(insertSql, connection))
            {
                command.Parameters.AddWithValue("@Timestamp", logEntry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                command.Parameters.AddWithValue("@EventId", logEntry.EventId ?? string.Empty);
                command.Parameters.AddWithValue("@EventType", logEntry.EventType ?? string.Empty);
                command.Parameters.AddWithValue("@Level", (int)logEntry.Level);
                command.Parameters.AddWithValue("@WindowTitle", logEntry.WindowTitle ?? string.Empty);
                command.Parameters.AddWithValue("@ProcessName", logEntry.ProcessName ?? string.Empty);
                command.Parameters.AddWithValue("@ProcessId", logEntry.ProcessId);
                command.Parameters.AddWithValue("@Hwnd", logEntry.Hwnd ?? string.Empty);
                command.Parameters.AddWithValue("@WindowInfoJson", logEntry.RawWindowInfoJson ?? string.Empty);
                command.Parameters.AddWithValue("@Details", logEntry.Details ?? string.Empty);
                command.Parameters.AddWithValue("@SystemInfoJson", string.Empty);

                command.ExecuteNonQuery();
            }
        }

        private List<LogEntry> SelectLogEntries(SqliteConnection connection, int limit, int offset)
        {
            var logs = new List<LogEntry>();
            string selectSql = @"
                SELECT Id, Timestamp, EventId, EventType, Level, WindowTitle,
                       ProcessName, ProcessId, Hwnd, WindowInfoJson, Details, SystemInfoJson
                FROM LogEntries
                ORDER BY Timestamp DESC
                LIMIT @Limit OFFSET @Offset;
            ";

            using (var command = new SqliteCommand(selectSql, connection))
            {
                command.Parameters.AddWithValue("@Limit", limit);
                command.Parameters.AddWithValue("@Offset", offset);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        logs.Add(ReadLogEntry(reader));
                    }
                }
            }

            return logs;
        }

        private List<LogEntry> SelectLogEntriesByEventType(SqliteConnection connection, string eventType, int limit)
        {
            var logs = new List<LogEntry>();
            string selectSql = @"
                SELECT Id, Timestamp, EventId, EventType, Level, WindowTitle,
                       ProcessName, ProcessId, Hwnd, WindowInfoJson, Details, SystemInfoJson
                FROM LogEntries
                WHERE EventType = @EventType
                ORDER BY Timestamp DESC
                LIMIT @Limit;
            ";

            using (var command = new SqliteCommand(selectSql, connection))
            {
                command.Parameters.AddWithValue("@EventType", eventType);
                command.Parameters.AddWithValue("@Limit", limit);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        logs.Add(ReadLogEntry(reader));
                    }
                }
            }

            return logs;
        }

        private List<LogEntry> SelectLogEntriesByDateRange(SqliteConnection connection, DateTime startDate, DateTime endDate, int limit)
        {
            var logs = new List<LogEntry>();
            string selectSql = @"
                SELECT Id, Timestamp, EventId, EventType, Level, WindowTitle,
                       ProcessName, ProcessId, Hwnd, WindowInfoJson, Details, SystemInfoJson
                FROM LogEntries
                WHERE Timestamp BETWEEN @StartDate AND @EndDate
                ORDER BY Timestamp DESC
                LIMIT @Limit;
            ";

            using (var command = new SqliteCommand(selectSql, connection))
            {
                command.Parameters.AddWithValue("@StartDate", startDate.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                command.Parameters.AddWithValue("@EndDate", endDate.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                command.Parameters.AddWithValue("@Limit", limit);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        logs.Add(ReadLogEntry(reader));
                    }
                }
            }

            return logs;
        }

        private void DeleteOldLogEntries(SqliteConnection connection, DateTime cutoffDate)
        {
            string deleteSql = @"
                DELETE FROM LogEntries
                WHERE Timestamp < @CutoffDate;
            ";

            using (var command = new SqliteCommand(deleteSql, connection))
            {
                command.Parameters.AddWithValue("@CutoffDate", cutoffDate.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                command.ExecuteNonQuery();
            }
        }

        private void DeleteAllLogEntries(SqliteConnection connection)
        {
            string deleteSql = "DELETE FROM LogEntries;";

            using (var command = new SqliteCommand(deleteSql, connection))
            {
                command.ExecuteNonQuery();
            }
        }

        private LogEntry ReadLogEntry(SqliteDataReader reader)
        {
            var logEntry = new LogEntry
            {
                Id = reader.GetInt64(0),
                Timestamp = DateTime.Parse(reader.GetString(1)),
                EventId = reader.GetString(2),
                EventType = reader.GetString(3),
                Level = (LogLevel)reader.GetInt32(4),
                WindowTitle = reader.GetString(5),
                ProcessName = reader.GetString(6),
                ProcessId = (uint)reader.GetInt32(7),
                Hwnd = reader.GetString(8),
                RawWindowInfoJson = reader.GetString(9),
                Details = reader.GetString(10)
            };

            return logEntry;
        }

        #endregion
    }
}
