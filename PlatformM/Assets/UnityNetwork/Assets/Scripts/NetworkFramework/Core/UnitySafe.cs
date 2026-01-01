using System.Threading;
using NetworkFramework.Utils;
using UnityEngine;

namespace NetworkFramework.Core
{
    public static class UnitySafe
    {
        private static int _mainThreadId;
        private static bool _cachedIsPlaying;

        public static void Init()
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            _cachedIsPlaying = Application.isPlaying;
            LoggerUtil.Log($"UnitySafe Initialized _mainThreadId {_mainThreadId}, _cachedIsPlaying : {_cachedIsPlaying}");
        }

        public static bool IsPlaying
        {
            get
            {
                if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
                {
                    // 子线程访问时返回缓存值
                    // LoggerUtil.Log($"UnitySafe _cachedIsPlaying : {_cachedIsPlaying}");
                    return _cachedIsPlaying;
                }

                // LoggerUtil.Log($"UnitySafe IsPlaying : {Application.isPlaying}");
                return Application.isPlaying;
            }
        }
    }
}