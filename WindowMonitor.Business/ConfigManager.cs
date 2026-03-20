using System;
using System.Collections.Concurrent;
using System.IO;
using Newtonsoft.Json;

namespace WindowMonitor.Business
{
    public class ConfigManager
    {
        #region Singleton

        private static readonly Lazy<ConfigManager> _instance = new Lazy<ConfigManager>(() => new ConfigManager());
        public static ConfigManager Instance => _instance.Value;

        #endregion

        #region Private Fields

        private readonly ConcurrentDictionary<string, object> _configValues;
        private readonly object _lockObject = new object();
        private string _configFilePath;

        #endregion

        #region Constructor

        private ConfigManager()
        {
            _configValues = new ConcurrentDictionary<string, object>();
            InitializeDefaultConfig();
        }

        #endregion

        #region Default Configuration

        private void InitializeDefaultConfig()
        {
            _configValues["MonitorIntervalMs"] = 100;
            _configValues["LogLevel"] = "Info";
            _configValues["LogRetentionDays"] = 30;
            _configValues["DatabasePath"] = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WindowMonitor",
                "windowmonitor.db");
            _configValues["AutoStartMonitoring"] = false;
            _configValues["MinimizeToTray"] = true;
            _configValues["MaxLogEntriesInMemory"] = 1000;
        }

        #endregion

        #region Public Properties

        public string ConfigFilePath
        {
            get
            {
                if (string.IsNullOrEmpty(_configFilePath))
                {
                    _configFilePath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "WindowMonitor",
                        "config.json");
                }
                return _configFilePath;
            }
            set => _configFilePath = value;
        }

        #endregion

        #region Public Methods

        public bool LoadConfig()
        {
            lock (_lockObject)
            {
                try
                {
                    if (!File.Exists(ConfigFilePath))
                        return false;

                    string json = File.ReadAllText(ConfigFilePath);
                    var loadedConfig = JsonConvert.DeserializeObject<ConcurrentDictionary<string, object>>(json);

                    if (loadedConfig != null)
                    {
                        foreach (var kvp in loadedConfig)
                        {
                            _configValues[kvp.Key] = kvp.Value;
                        }
                    }

                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        public bool SaveConfig()
        {
            lock (_lockObject)
            {
                try
                {
                    string directory = Path.GetDirectoryName(ConfigFilePath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    string json = JsonConvert.SerializeObject(_configValues, Formatting.Indented);
                    File.WriteAllText(ConfigFilePath, json);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        public T GetConfig<T>(string key, T defaultValue = default(T))
        {
            if (_configValues.TryGetValue(key, out object value))
            {
                try
                {
                    if (value is T typedValue)
                    {
                        return typedValue;
                    }

                    // Try to convert from string or other types
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    return defaultValue;
                }
            }
            return defaultValue;
        }

        public void SetConfig<T>(string key, T value)
        {
            _configValues[key] = value;
        }

        public int MonitorIntervalMs
        {
            get => GetConfig<int>("MonitorIntervalMs", 100);
            set => SetConfig("MonitorIntervalMs", value);
        }

        public string LogLevel
        {
            get => GetConfig<string>("LogLevel", "Info");
            set => SetConfig("LogLevel", value);
        }

        public int LogRetentionDays
        {
            get => GetConfig<int>("LogRetentionDays", 30);
            set => SetConfig("LogRetentionDays", value);
        }

        public string DatabasePath
        {
            get => GetConfig<string>("DatabasePath", string.Empty);
            set => SetConfig("DatabasePath", value);
        }

        public bool AutoStartMonitoring
        {
            get => GetConfig<bool>("AutoStartMonitoring", false);
            set => SetConfig("AutoStartMonitoring", value);
        }

        public bool MinimizeToTray
        {
            get => GetConfig<bool>("MinimizeToTray", true);
            set => SetConfig("MinimizeToTray", value);
        }

        public int MaxLogEntriesInMemory
        {
            get => GetConfig<int>("MaxLogEntriesInMemory", 1000);
            set => SetConfig("MaxLogEntriesInMemory", value);
        }

        #endregion
    }
}
