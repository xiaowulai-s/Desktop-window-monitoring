using NUnit.Framework;
using WindowMonitor.Communication.Models;
using WindowMonitor.Business.Models;
using WindowMonitor.Business;

namespace WindowMonitor.Tests
{
    public class WindowMonitorTests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void TestWindowInfoCreation()
        {
            var windowInfo = new WindowInfo
            {
                Title = "Test Window",
                ClassName = "TestClass",
                ProcessName = "test.exe",
                ProcessId = 1234
            };

            Assert.IsNotNull(windowInfo);
            Assert.AreEqual("Test Window", windowInfo.Title);
            Assert.AreEqual("TestClass", windowInfo.ClassName);
            Assert.AreEqual("test.exe", windowInfo.ProcessName);
            Assert.AreEqual(1234u, windowInfo.ProcessId);
        }

        [Test]
        public void TestWindowEventCreation()
        {
            var windowInfo = new WindowInfo { Title = "Test Window" };
            var windowEvent = new WindowEvent(WindowEventType.WindowCreate, windowInfo);

            Assert.IsNotNull(windowEvent);
            Assert.AreEqual(WindowEventType.WindowCreate, windowEvent.EventType);
            Assert.IsNotNull(windowEvent.WindowInfo);
            Assert.AreEqual("Test Window", windowEvent.WindowInfo.Title);
            Assert.IsFalse(string.IsNullOrEmpty(windowEvent.EventId));
        }

        [Test]
        public void TestLogEntryFromWindowEvent()
        {
            var windowInfo = new WindowInfo 
            { 
                Title = "Test Window", 
                ProcessName = "test.exe",
                ProcessId = 1234
            };
            var windowEvent = new WindowEvent(WindowEventType.WindowCreate, windowInfo);
            var logEntry = LogEntry.FromWindowEvent(windowEvent);

            Assert.IsNotNull(logEntry);
            Assert.AreEqual("WindowCreate", logEntry.EventType);
            Assert.AreEqual("Test Window", logEntry.WindowTitle);
            Assert.AreEqual("test.exe", logEntry.ProcessName);
            Assert.AreEqual(1234u, logEntry.ProcessId);
            Assert.AreEqual(LogLevel.Info, logEntry.Level);
        }

        [Test]
        public void TestConfigManagerDefaultValues()
        {
            var config = ConfigManager.Instance;

            Assert.IsNotNull(config);
            Assert.AreEqual(100, config.MonitorIntervalMs);
            Assert.AreEqual("Info", config.LogLevel);
            Assert.AreEqual(30, config.LogRetentionDays);
            Assert.IsFalse(config.AutoStartMonitoring);
            Assert.IsTrue(config.MinimizeToTray);
        }

        [Test]
        public void TestConfigManagerSetAndGet()
        {
            var config = ConfigManager.Instance;
            
            config.SetConfig("TestKey", "TestValue");
            var value = config.GetConfig<string>("TestKey");
            
            Assert.AreEqual("TestValue", value);
        }

        [Test]
        public void TestLogManagerSingleton()
        {
            var instance1 = LogManager.Instance;
            var instance2 = LogManager.Instance;

            Assert.IsNotNull(instance1);
            Assert.IsNotNull(instance2);
            Assert.AreSame(instance1, instance2);
        }

        [Test]
        public void TestMonitorEngineSingleton()
        {
            var instance1 = MonitorEngine.Instance;
            var instance2 = MonitorEngine.Instance;

            Assert.IsNotNull(instance1);
            Assert.IsNotNull(instance2);
            Assert.AreSame(instance1, instance2);
            Assert.AreEqual(MonitorState.Stopped, instance1.State);
        }

        [Test]
        public void TestWindowInfoJsonSerialization()
        {
            var windowInfo = new WindowInfo
            {
                Title = "Test Window",
                ClassName = "TestClass",
                ProcessName = "test.exe",
                ProcessId = 1234
            };

            string json = windowInfo.ToJson();
            Assert.IsFalse(string.IsNullOrEmpty(json));

            var deserialized = WindowInfo.FromJson(json);
            Assert.IsNotNull(deserialized);
            Assert.AreEqual(windowInfo.Title, deserialized.Title);
            Assert.AreEqual(windowInfo.ClassName, deserialized.ClassName);
        }

        [Test]
        public void TestWindowEventJsonSerialization()
        {
            var windowInfo = new WindowInfo { Title = "Test Window" };
            var windowEvent = new WindowEvent(WindowEventType.WindowCreate, windowInfo);

            string json = windowEvent.ToJson();
            Assert.IsFalse(string.IsNullOrEmpty(json));

            var deserialized = WindowEvent.FromJson(json);
            Assert.IsNotNull(deserialized);
            Assert.AreEqual(windowEvent.EventType, deserialized.EventType);
            Assert.AreEqual(windowEvent.WindowInfo.Title, deserialized.WindowInfo.Title);
        }

        [Test]
        public void TestLogEntryInitialState()
        {
            var logEntry = new LogEntry();

            Assert.IsNotNull(logEntry);
            Assert.AreEqual(LogLevel.Info, logEntry.Level);
            Assert.IsTrue(logEntry.Timestamp <= DateTime.Now);
            Assert.IsTrue(logEntry.Timestamp >= DateTime.Now.AddSeconds(-1));
        }
    }
}
