using System.Collections.Generic;
using System.Text;
using UnityEngine;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;
using System;
using NetworkFramework.Core.Model;
using NetworkFramework.Utils;
using NetworkFramework.Core;
using NetworkFramework.Core.Adapter;

namespace NetworkFramework.Interceptor
{
    /// <summary>
    /// 请求策略拦截器 - 与Android端保持一致
    /// </summary>
    public class RequestStrategyInterceptor : IInterceptor
    {
        private const string TAG = "RequestStrategyInterceptor";

        public async Task<Response> Intercept(Request request, IInterceptorChain chain, CancellationToken token)
        {
            LoggerUtil.Log($"{TAG}: Intercept Invoke");
            
            var originalRequest = request.Clone();

            // 根据连接类型选择处理策略
            switch (request.UseConnectionType)
            {
                // 长连接优先
                case ConnectionType.PERSISTENT_PRECEDE:
                    return await HandlePersistentPrecede(request, chain, token);

                // 短连接优先
                case ConnectionType.INCONSTANT_PRECEDE:
                    return await HandleInconstantPrecede(request, chain, token);

                // 其他连接类型（直接使用指定类型）
                default:
                    return await Process(request, chain, token);
            }
        }

        /// <summary>
        /// 处理长连接优先策略
        /// </summary>
        private async Task<Response> HandlePersistentPrecede(Request request, IInterceptorChain chain,
            CancellationToken token)
        {
            // 验证URL是否有效
            if (string.IsNullOrEmpty(request.PersistentConnectionUrl))
            {
                return new Response
                {
                    NetCode = ErrorCode.INVALID_URL,
                    NetMessage = "PERSISTENT_PRECEDE connection type, but persistent url is null or empty!",
                    SeqId = request.RequestControl?.SeqId ?? 0
                };
            }

            if (string.IsNullOrEmpty(request.InconstantConnectionUrl))
            {
                return new Response
                {
                    NetCode = ErrorCode.INVALID_URL,
                    NetMessage = "PERSISTENT_PRECEDE connection type, but inconstant url is null or empty!",
                    SeqId = request.RequestControl?.SeqId ?? 0
                };
            }

            // 获取长连接策略
            var persistentStrategy = PersistentStrategyHolder.GetStrategy(request.PersistentConnectionUrl);

            // 检查长连接是否可用
            if (await persistentStrategy.CanUse(request))
            {
                // 尝试使用长连接
                var persistentRequest = request.Clone(useConnectionType: ConnectionType.PERSISTENT);
                var response = await Process(persistentRequest, chain, token);

                // 如果长连接请求成功，直接返回
                if (response.NetCode == ErrorCode.HTTP_OK)
                {
                    return response;
                }
            }

            // 长连接不可用或请求失败，切换到短连接
            var inconstantRequest = request.Clone(useConnectionType: ConnectionType.INCONSTANT);
            return await Process(inconstantRequest, chain, token);
        }

        /// <summary>
        /// 处理短连接优先策略
        /// </summary>
        private async Task<Response> HandleInconstantPrecede(Request request, IInterceptorChain chain,
            CancellationToken token)
        {
            // 验证URL是否有效
            if (string.IsNullOrEmpty(request.PersistentConnectionUrl))
            {
                return new Response
                {
                    NetCode = ErrorCode.INVALID_URL,
                    NetMessage = "INCONSTANT_PRECEDE connection type, but persistent url is null or empty!",
                    SeqId = request.RequestControl?.SeqId ?? 0
                };
            }

            if (string.IsNullOrEmpty(request.InconstantConnectionUrl))
            {
                return new Response
                {
                    NetCode = ErrorCode.INVALID_URL,
                    NetMessage = "INCONSTANT_PRECEDE connection type, but inconstant url is null or empty!",
                    SeqId = request.RequestControl?.SeqId ?? 0
                };
            }

            // 先尝试短连接
            var inconstantRequest = request.Clone(useConnectionType: ConnectionType.INCONSTANT);
            var response = await Process(inconstantRequest, chain, token);

            // 如果短连接请求成功，直接返回
            if (response.NetCode == ErrorCode.HTTP_OK)
            {
                return response;
            }

            // 短连接请求失败，检查长连接是否可用
            var persistentStrategy = PersistentStrategyHolder.GetStrategy(request.PersistentConnectionUrl);
            if (!await persistentStrategy.CanUse(request))
            {
                return response;
            }

            // 长连接可用，切换到长连接
            var persistentRequest = request.Clone(useConnectionType: ConnectionType.PERSISTENT);
            return await Process(persistentRequest, chain, token);
        }

        /// <summary>
        /// 处理直接连接类型（非优先策略）
        /// </summary>
        private async Task<Response> HandleDirectConnection(Request request, IInterceptorChain chain,
            CancellationToken token)
        {
            // 验证URL是否有效
            if (request.UseConnectionType == ConnectionType.PERSISTENT &&
                string.IsNullOrEmpty(request.PersistentConnectionUrl))
            {
                return new Response
                {
                    NetCode = ErrorCode.INVALID_URL,
                    NetMessage = "PERSISTENT connection type, but persistent url is null or empty!",
                    SeqId = request.RequestControl?.SeqId ?? 0
                };
            }

            if (request.UseConnectionType == ConnectionType.INCONSTANT &&
                string.IsNullOrEmpty(request.InconstantConnectionUrl))
            {
                return new Response
                {
                    NetCode = ErrorCode.INVALID_URL,
                    NetMessage = "INCONSTANT connection type, but inconstant url is null or empty!",
                    SeqId = request.RequestControl?.SeqId ?? 0
                };
            }

            return await Process(request, chain, token);
        }

        /// <summary>
        /// 处理请求的核心方法
        /// </summary>
        private async Task<Response> Process(Request request, IInterceptorChain chain, CancellationToken token)
        {
            // 请求前处理
            BeforeProcess(request);

            // 记录开始时间
            var startTime = Time.realtimeSinceStartup * 1000f; // 转换为毫秒

            // 执行请求
            var response = await chain.Proceed(request, token);

            // 计算耗时
            var endTime = Time.realtimeSinceStartup * 1000f;
            var consumeTime = (long)(endTime - startTime);

            // 请求后处理
            AfterProcess(request, response, consumeTime);

            return response;
        }

        /// <summary>
        /// 请求前处理
        /// </summary>
        private void BeforeProcess(Request request)
        {
            if (!string.IsNullOrEmpty(request.PersistentConnectionUrl))
            {
                var persistentStrategy = PersistentStrategyHolder.GetStrategy(request.PersistentConnectionUrl);
                persistentStrategy.BeforeProcess(request);
            }
        }

        /// <summary>
        /// 请求后处理
        /// </summary>
        private void AfterProcess(Request request, Response response, long consumeTime)
        {
            if (!string.IsNullOrEmpty(request.PersistentConnectionUrl))
            {
                var persistentStrategy = PersistentStrategyHolder.GetStrategy(request.PersistentConnectionUrl);
                persistentStrategy.AfterProcess(request, response, consumeTime);
            }
        }

        /// <summary>
        /// 长连接策略管理器
        /// </summary>
        public class PersistentStrategyHolder
        {
            private static readonly Dictionary<string, PersistentStrategy> _persistentStrategyMap =
                new Dictionary<string, PersistentStrategy>();

            private static readonly object _lock = new object();

            /// <summary>
            /// 获取指定URL的长连接策略
            /// </summary>
            public static PersistentStrategy GetStrategy(string url)
            {
                lock (_lock)
                {
                    if (!_persistentStrategyMap.TryGetValue(url, out var strategy))
                    {
                        strategy = new PersistentStrategy(url);
                        _persistentStrategyMap[url] = strategy;
                    }

                    return strategy;
                }
            }
        }

        /// <summary>
        /// 长连接策略类
        /// </summary>
        public class PersistentStrategy
        {
            private const int CALCULATE_SIZE = 5;
            private const int PERSISTENT_TIMEOUT_COUNT_THRESHOLD = 5;
            private const int PERSISTENT_WAITING_COUNT_THRESHOLD = 5;
            private const long PERSISTENT_WAIT_RESET_TIME_MS = 60 * 1000L;

            private readonly string _url;
            private readonly List<long> _persistentConsumeTimeList = new List<long>();
            private readonly List<long> _inconstantConsumeTimeList = new List<long>();
            private readonly AtomicLong _persistentAvgConsumeTime = new AtomicLong(-1L);
            private readonly AtomicLong _inconstantAvgConsumeTime = new AtomicLong(-1L);

            private readonly AtomicLong _persistentTimeoutCount = new AtomicLong(0); // 长连接请求连续超时次数
            private readonly AtomicLong _inconstantTimeoutCount = new AtomicLong(0); // 短连接请求连续超时次数
            private readonly AtomicLong _persistentWaitingResponseCount = new AtomicLong(0); // 队列中长连接请求等待响应的数量
            private readonly AtomicLong _persistentErrorCount = new AtomicLong(0); // 长连接请求失败次数
            private readonly AtomicLong _requestStartTime = new AtomicLong(0); // 请求开始时间

            private bool _isWaitToReusePersistentConnection = false;
            private readonly object _resetLock = new object();

            public PersistentStrategy(string url)
            {
                _url = url;
                // 初始化协程上下文，与Android端的单线程协程上下文对应
                // 在Unity中，我们通过主线程执行协程来保证线程安全
            }

            /// <summary>
            /// 检查长连接是否可用
            /// </summary>
            public Task<bool> CanUse(Request request)
            {
                // 检查长连接是否打开
                if (!IsPersistentConnectionOpen(request))
                {
                    LoggerUtil.Log(
                        $"{TAG}: Persistent connection not available for url: {_url}, reason: NOT_CONNECTED");
                    return Task.FromResult(false);
                }

                // 检查连续超时次数
                if (_persistentTimeoutCount.Get() > PERSISTENT_TIMEOUT_COUNT_THRESHOLD)
                {
                    lock (_resetLock)
                    {
                        if (!_isWaitToReusePersistentConnection)
                        {
                            _isWaitToReusePersistentConnection = true;
                            // 直接重置超时计数，不使用异步方法避免线程问题
                            // 在下次请求时重置统计数据
                            _persistentTimeoutCount.Set(0);
                            _isWaitToReusePersistentConnection = false;
                            ResetStatistics();
                            LoggerUtil.Log($"{TAG}: Reset persistent connection statistics for url: {_url}");
                        }
                    }

                    LoggerUtil.Log(
                        $"{TAG}: Persistent connection not available for url: {_url}, reason: TIMEOUT_CONTINUOUS");
                    return Task.FromResult(false);
                }

                // 检查等待响应的请求数量
                if (_persistentWaitingResponseCount.Get() > PERSISTENT_WAITING_COUNT_THRESHOLD)
                {
                    LoggerUtil.Log(
                        $"{TAG}: Persistent connection not available for url: {_url}, reason: TOO_MANY_REQUEST_NO_RESPONSE");
                    return Task.FromResult(false);
                }

                // 检查平均耗时
                if (_persistentAvgConsumeTime.Get() > 0 &&
                    _inconstantAvgConsumeTime.Get() > 0 &&
                    _persistentAvgConsumeTime.Get() > (_inconstantAvgConsumeTime.Get() * 2))
                {
                    LoggerUtil.Log(
                        $"{TAG}: Persistent connection not available for url: {_url}, reason: CONSUME_TIME_MORE_THAN_INCONSTANT_CONNECTION");
                    return Task.FromResult(false);
                }

                return Task.FromResult(true);
            }

            /// <summary>
            /// 请求处理前的逻辑
            /// </summary>
            public void BeforeProcess(Request request)
            {
                if (request.UseConnectionType == ConnectionType.PERSISTENT)
                {
                    _persistentWaitingResponseCount.IncrementAndGet();
                    _requestStartTime.Set(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                }
            }

            /// <summary>
            /// 请求处理后的逻辑
            /// </summary>
            public void AfterProcess(Request request, Response response, long consumeTime)
            {
                if (request.UseConnectionType == ConnectionType.PERSISTENT)
                {
                    // 更新平均耗时
                    lock (_persistentConsumeTimeList)
                    {
                        if (_persistentConsumeTimeList.Count >= CALCULATE_SIZE)
                        {
                            _persistentConsumeTimeList.RemoveAt(0);
                        }

                        _persistentConsumeTimeList.Add(consumeTime);

                        if (_persistentConsumeTimeList.Count == CALCULATE_SIZE)
                        {
                            long sum = _persistentConsumeTimeList.Sum();
                            _persistentAvgConsumeTime.Set(sum / CALCULATE_SIZE);
                        }
                    }

                    // 更新连续超时计数
                    if (response.NetCode == ErrorCode.REQUEST_TIMEOUT)
                    {
                        _persistentTimeoutCount.IncrementAndGet();
                        _persistentErrorCount.IncrementAndGet();
                    }
                    else
                    {
                        _persistentTimeoutCount.Set(0);
                        // 成功请求后重置错误计数
                        if (response.NetCode == ErrorCode.HTTP_OK)
                        {
                            _persistentErrorCount.Set(0);
                        }
                    }

                    // 减少等待响应计数
                    _persistentWaitingResponseCount.DecrementAndGet();

                    // 重置请求开始时间
                    _requestStartTime.Set(0);
                }
                else if (request.UseConnectionType == ConnectionType.INCONSTANT)
                {
                    // 更新平均耗时
                    lock (_inconstantConsumeTimeList)
                    {
                        if (_inconstantConsumeTimeList.Count >= CALCULATE_SIZE)
                        {
                            _inconstantConsumeTimeList.RemoveAt(0);
                        }

                        _inconstantConsumeTimeList.Add(consumeTime);

                        if (_inconstantConsumeTimeList.Count == CALCULATE_SIZE)
                        {
                            long sum = _inconstantConsumeTimeList.Sum();
                            _inconstantAvgConsumeTime.Set(sum / CALCULATE_SIZE);
                        }
                    }

                    // 更新连续超时计数
                    if (response.NetCode == ErrorCode.REQUEST_TIMEOUT)
                    {
                        _inconstantTimeoutCount.IncrementAndGet();
                    }
                    else
                    {
                        _inconstantTimeoutCount.Set(0);
                    }
                }
            }

            /// <summary>
            /// 检查长连接是否打开 - 使用同步方法避免线程问题
            /// </summary>
            private bool IsPersistentConnectionOpen(Request request)
            {
                // try
                // {
                //     // 获取NetworkManager实例并检查WebSocket连接状态
                //     var networkManager = NetworkManager.Instance;
                //     if (networkManager == null)
                //     {
                //         LoggerUtil.Log($"{TAG}: NetworkManager instance not found");
                //         return false;
                //     }
                //
                //     var webSocketAdapter = networkManager.GetWebSocketAdapter();
                //     if (webSocketAdapter == null)
                //     {
                //         LoggerUtil.Log($"{TAG}: WebSocket adapter not initialized");
                //         return false;
                //     }
                //
                //     // 检查连接是否有效且URL匹配
                //     bool isConnected = webSocketAdapter.IsConnected;
                //     string currentUrl = (string)typeof(WebSocketNetworkAdapter).GetProperty("CurrentUrl")
                //         .GetValue(webSocketAdapter);
                //     bool urlMatch = currentUrl == _url;
                //
                //     LoggerUtil.Log(
                //         $"{TAG}: WebSocket connection check for url: {_url}, connected: {isConnected}, urlMatch: {urlMatch}");
                //     return isConnected && urlMatch;
                // }
                // catch (System.Exception ex)
                // {
                //     LoggerUtil.Log($"{TAG}: Error checking WebSocket connection: {ex.Message}");
                return false;
                // }
            }

            // 移除异步重置方法，使用同步方式处理

            /// <summary>
            /// 重置统计数据
            /// </summary>
            private void ResetStatistics()
            {
                lock (_persistentConsumeTimeList)
                {
                    _persistentConsumeTimeList.Clear();
                }

                lock (_inconstantConsumeTimeList)
                {
                    _inconstantConsumeTimeList.Clear();
                }

                _persistentAvgConsumeTime.Set(-1L);
                _inconstantAvgConsumeTime.Set(-1L);
                _persistentErrorCount.Set(0);
            }
        }

        /// <summary>
        /// 原子长整型，用于线程安全计数
        /// </summary>
        private class AtomicLong
        {
            private long _value;
            private readonly object _lock = new object();

            public AtomicLong(long initialValue)
            {
                _value = initialValue;
            }

            public long Get()
            {
                lock (_lock)
                {
                    return _value;
                }
            }

            public void Set(long newValue)
            {
                lock (_lock)
                {
                    _value = newValue;
                }
            }

            public long IncrementAndGet()
            {
                lock (_lock)
                {
                    return ++_value;
                }
            }

            public long DecrementAndGet()
            {
                lock (_lock)
                {
                    return --_value;
                }
            }
        }
    }
}