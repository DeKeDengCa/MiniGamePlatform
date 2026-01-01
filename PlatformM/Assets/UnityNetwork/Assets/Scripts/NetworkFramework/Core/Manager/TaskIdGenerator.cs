using System;

namespace NetworkFramework.Core.Manager
{
    /// <summary>
    /// 与Android端TaskIdGenerator一致的序列ID生成器。
    /// 保证ID随时间单调递增，毫秒级冲突时自增。
    /// </summary>
    public static class TaskIdGenerator
    {
        private static readonly object _lock = new object();
        private static long _previousNum = long.MinValue;

        public static long Generate()
        {
            lock (_lock)
            {
                long cur = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (cur > _previousNum)
                {
                    _previousNum = cur;
                    return cur;
                }
                else
                {
                    _previousNum += 1;
                    return _previousNum;
                }
            }
        }
    }
}