using System;
using System.IO;
using System.Threading.Tasks;
using NetworkFramework.Utils;
using UnityEngine;

namespace NetworkFramework.Global
{
    public static class GlobalExceptionHandler
    {
        private static string TAG = "[GlobalExceptionHandler]";
        private static bool _enabled;
        private static string _logFilePath;

        public static bool Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled == value) return;
                _enabled = value;

                if (_enabled) Register();
                else Unregister();
            }
        }

        private static void Register()
        {
            LoggerUtil.Log("GlobalExceptionHandler Register start");
            _logFilePath = Path.Combine(Application.persistentDataPath, "unity_exceptions.log");
            // 捕获未处理异常
            AppDomain.CurrentDomain.UnhandledException += OnUnhandled;
            // 捕获 Task 异常
            TaskScheduler.UnobservedTaskException += OnTaskException;
            // 捕获 Unity 内部日志
            Application.logMessageReceived += OnLogMessage;

            LoggerUtil.Log($"Initialized. Log file: {_logFilePath}");
        }

        private static void Unregister()
        {
            AppDomain.CurrentDomain.UnhandledException -= OnUnhandled;
            TaskScheduler.UnobservedTaskException -= OnTaskException;
            Application.logMessageReceived -= OnLogMessage;

            LoggerUtil.Log("Disabled");
        }


        private static void OnUnhandled(object sender, UnhandledExceptionEventArgs e)
        {
            LogException("[UnhandledException]", e.ExceptionObject as Exception);
        }

        private static void OnTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            LogException("[UnobservedTaskException]", e.Exception);
            e.SetObserved();
        }

        private static void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (type is LogType.Exception or LogType.Error)
            {
                File.AppendAllText(_logFilePath, $"{DateTime.Now:u} {type}: {condition}\n{stackTrace}\n\n");
            }
        }

        private static void LogException(string tag, Exception ex)
        {
            if (ex == null) return;
            var msg = $"{DateTime.Now:u} {TAG}{tag}: {ex}\n";
            File.AppendAllText(_logFilePath, msg + "\n");
        }
    }
}