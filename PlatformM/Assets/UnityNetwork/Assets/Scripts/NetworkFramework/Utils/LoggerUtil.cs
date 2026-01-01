using System;
using UnityEngine;

namespace NetworkFramework.Utils
{
    public static class LoggerUtil
    {
        public static bool Enabled
        {
            get;
            set;
        }
        public static void Log(string message)
        {
            if (!Enabled) return;
            Debug.Log($"{message}");
        }

        public static void LogWarning(string message)
        {
            if (!Enabled) return;
            Debug.LogWarning($"{message}");
        }

        public static void LogError(string message)
        {
            if (!Enabled) return;
            Debug.LogError($"{message}");
        }

        public static void LogVerbose(string message)
        {
#if DEBUG
            if (!Enabled) return;
            Debug.Log($"[Verbose] {message}");
#endif
        }

        public static void LogException(Exception exception)
        {
            if (!Enabled) return;
            Debug.LogException(exception);
        }
    }
}