using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NetworkFramework.Core.Adapter;
using NetworkFramework.Core.Interface;
using NetworkFramework.Core.Model;
using NetworkFramework.Interceptor;
using NetworkFramework.Tasks;
using NetworkFramework.Utils;
using System.Collections.Concurrent;
using System.Text;
using Cproxy;
using Scommon;

namespace NetworkFramework.Core.Manager
{
    public class NetworkManager : INetwork
    {
        private static string TAG = "NetworkManager";
        private static readonly Lazy<NetworkManager> _instance = new(() => new NetworkManager());

        public static NetworkManager Instance => _instance.Value;

        private PriorityTaskScheduler _scheduler;
        public PriorityTaskScheduler Scheduler => _scheduler;

        private string _inconstantConnectionUrl;
        private string _persistentConnectionUrl;
        private string _accountToken;
        private AppNetConfig _appNetConfig;
        public AppNetConfig AppNetConfig => _appNetConfig;

        private readonly List<IInterceptor> _interceptors = new();
        private readonly NetworkRequestInterceptor _networkRequestInterceptor;

        // 初始化时配置两种适配器
        private HttpNetworkAdapter _httpAdapter;
        private WebSocketManager _wsAdapter;
        private DefaultNetworkAdapterSelector _selector;

        // 推送订阅管理：url -> handlers
        private readonly object _subscriberLock = new();
        private readonly Dictionary<string, List<Action<PushMessage>>> _pushSubscribers = new();

        // 推送解密key缓存：url -> notifyKey
        private readonly ConcurrentDictionary<string, byte[]> _notifyKeyMap = new();

        private NetworkManager()
        {
            _interceptors.Add(new LogInterceptor());
            _interceptors.Add(new CompressInterceptor());
            // _interceptors.Add(new RequestStrategyInterceptor());
            _interceptors.Add(new EncryptInterceptor());
            // _interceptors.Add(new BIReportInterceptor());
            _interceptors.Add(new HeaderInterceptor());

            _scheduler = new PriorityTaskScheduler();
            _httpAdapter = new HttpNetworkAdapter();
            _wsAdapter = WebSocketManager.Instance; // 基于 BestHTTP
            _wsAdapter.OnPushMessage += OnPushMessage;
            _wsAdapter.OnConnectionClosed += OnConnectionClosed;
            _wsAdapter.OnConnectionError += OnConnectionError;
            _selector = new DefaultNetworkAdapterSelector(_httpAdapter, _wsAdapter);
            _networkRequestInterceptor = new NetworkRequestInterceptor(_selector);
        }

        public void Init(string inconstantConnectionUrl, string persistentConnectionUrl, string accountToken, AppNetConfig config)
        {
            _inconstantConnectionUrl = inconstantConnectionUrl;
            _persistentConnectionUrl = persistentConnectionUrl;
            _accountToken = accountToken;
            _appNetConfig = config;
            LoggerUtil.Log($"{TAG}, _inconstantConnectionUrl : {_inconstantConnectionUrl}, _persistentConnectionUrl : {_persistentConnectionUrl}, _appNetConfig : {_appNetConfig}");
        }

        public void SetAccountToken(string accountToken)
        {
            _accountToken = accountToken;
        }

        public async Task<Response> Request(Request request, CancellationToken token)
        {
            CheckUrl(request);
            long seqId = TaskIdGenerator.Generate();
            LoggerUtil.Log($"{TAG} request seqId: {seqId}");
            request.RequestControl.SeqId = seqId;
            if (string.IsNullOrEmpty(request.Token))
            {
                request.Token = _accountToken;
            }
            var allInterceptors = new List<IInterceptor>(_interceptors) { _networkRequestInterceptor };
            var chain = new InterceptorChain(allInterceptors, request, seqId);
            var resp = await chain.Proceed(request, token).ConfigureAwait(false);
            return resp;
        }

        /// <summary>
        /// 只用于建立长连接，不用于数据发送
        /// </summary>
        /// <param name="wsUrl"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<Response> Connect(string wsUrl, CancellationToken token)
        {
            var seqId = TaskIdGenerator.Generate();
            LoggerUtil.Log($"{TAG} connect seqId: {seqId}");
            
            var request = new Request
            {
                PersistentConnectionUrl = wsUrl,
                UseConnectionType = ConnectionType.PERSISTENT,
                RequestControl = new RequestControl
                {
                    SeqId = seqId,
                    Service = "",
                    Method = "",
                    Reason = RPCReason.UserAction
                },
                OnlyConnected = true,
                Token = _accountToken,
                Body = Array.Empty<byte>()
            };
            CheckUrl(request);
            var allInterceptors = new List<IInterceptor>(_interceptors) { _networkRequestInterceptor };
            var chain = new InterceptorChain(allInterceptors, request, seqId);
            var resp = await chain.Proceed(request, token).ConfigureAwait(false);
            return resp;
        }

        public void Disconnect(string wsUrl)
        {
            if (string.IsNullOrEmpty(wsUrl))
            {
                wsUrl = _persistentConnectionUrl;
            }
            _wsAdapter.Disconnect(wsUrl);
        }

        private void CheckUrl(Request request)
        {
            if (string.IsNullOrEmpty(request.InconstantConnectionUrl))
            {
                request.InconstantConnectionUrl = _inconstantConnectionUrl;
            }

            if (string.IsNullOrEmpty(request.PersistentConnectionUrl))
            {
                request.PersistentConnectionUrl = _persistentConnectionUrl;
            }
        }

        // 注册/注销推送处理器（按URL）
        public void RegisterPushHandler(string url, Action<PushMessage> handler)
        {
            if (string.IsNullOrEmpty(url) || handler == null) return;
            var key = CleanUrl(url);
            lock (_subscriberLock)
            {
                if (!_pushSubscribers.TryGetValue(key, out var handlers))
                {
                    handlers = new List<Action<PushMessage>>();
                    _pushSubscribers[key] = handlers;
                }
                handlers.Add(handler);
            }
        }

        public void UnregisterPushHandler(string url, Action<PushMessage> handler)
        {
            if (string.IsNullOrEmpty(url) || handler == null) return;
            var key = CleanUrl(url);
            lock (_subscriberLock)
            {
                if (_pushSubscribers.TryGetValue(key, out var handlers))
                {
                    handlers.Remove(handler);
                    if (handlers.Count == 0)
                    {
                        _pushSubscribers.Remove(key);
                    }
                }
            }
        }

        private void OnPushMessage(string url, PushMessage resp)
        {
            if (resp == null)
            {
                LoggerUtil.LogError($"{TAG} OnPushMessage: resp is null for url: {url}");
                return;
            }

            try
            {
                resp.Url = url;
                if (resp.NotifyPkg == "cproxy.Notify")
                {
                    var cryptoInfo = CryptoManager.GetPersistentConnectionCryptoInfo(resp.Url);
                    if (cryptoInfo == null || cryptoInfo.Key == null || cryptoInfo.Key.Length == 0)
                    {
                        LoggerUtil.LogError($"{TAG} persistent crypto key missing for url: {resp.Url}");
                        return;
                    }

                    var decryptMsg = DecryptBody(resp, cryptoInfo.Key);
                    if (decryptMsg == null) return;

                    var finalMsg = DecompressBody(decryptMsg);
                    if (finalMsg == null) return;

                    Notify keyNotify;
                    if (finalMsg.ContentType == ContentType.Proto)
                    {
                        keyNotify = Serializer.DeserializeFromProtoBuf<Notify>(finalMsg.Body);
                    }
                    else
                    {
                        var json = Encoding.UTF8.GetString(finalMsg.Body);
                        keyNotify = Serializer.DeserializeFromJson<Notify>(json);
                    }

                    if (keyNotify == null || string.IsNullOrEmpty(keyNotify.NotifyEntKey))
                    {
                        LoggerUtil.LogError($"{TAG} failed to deserialize notify key from push, contentType: {finalMsg.ContentType}");
                        return;
                    }

                    LoggerUtil.Log($"{TAG} receive notify key {keyNotify.NotifyEntKey}");
                    var keyBytes = Encoding.UTF8.GetBytes(keyNotify.NotifyEntKey);
                    _notifyKeyMap[resp.Url] = keyBytes;
                }
                else
                {
                    if (!_notifyKeyMap.TryGetValue(resp.Url, out var notifyKey) || notifyKey == null || notifyKey.Length == 0)
                    {
                        LoggerUtil.LogError($"{TAG} {resp.Url} notifyKey was null!");
                        return;
                    }

                    var decryptMsg = DecryptBody(resp, notifyKey);
                    if (decryptMsg == null) return;

                    var finalMsg = DecompressBody(decryptMsg);
                    if (finalMsg == null) return;

                    var cleanedUrl = CleanUrl(resp.Url);
                    List<Action<PushMessage>> handlers;
                    lock (_subscriberLock)
                    {
                        _pushSubscribers.TryGetValue(cleanedUrl, out handlers);
                    }

                    if (handlers != null && handlers.Count > 0)
                    {
                        foreach (var h in handlers)
                        {
                            try
                            {
                                h(finalMsg);
                            }
                            catch (Exception e)
                            {
                                LoggerUtil.LogWarning($"{TAG} push handler exception: {e.Message}");
                            }
                        }
                    }
                    else
                    {
                        LoggerUtil.LogWarning($"{TAG} no subscriber for {cleanedUrl}");
                    }
                }
            }
            catch (Exception e)
            {
                LoggerUtil.LogError($"{TAG} OnPushMessage exception: {e.Message}");
            }
        }

        private PushMessage DecryptBody(PushMessage message, byte[] key)
        {
            try
            {
                var decryptedBody = CryptoHelper.NormalAESDecrypt(message.Body, key);
                if (decryptedBody == null)
                {
                    var base64data = message.Body != null ? Convert.ToBase64String(message.Body) : "";
                    LoggerUtil.LogError($"{TAG} decrypted body == null, base64data:{base64data}, key:{Encoding.UTF8.GetString(key ?? Array.Empty<byte>())}");
                    return null;
                }
                return message.Clone(body: decryptedBody);
            }
            catch (Exception e)
            {
                var base64data = message.Body != null ? Convert.ToBase64String(message.Body) : "";
                LoggerUtil.LogError($"{TAG} decode push message fail! {e.Message}, base64data:{base64data}, key:{Encoding.UTF8.GetString(key ?? Array.Empty<byte>())}");
                return null;
            }
        }

        private PushMessage DecompressBody(PushMessage message)
        {
            if (message.CompressType == CompressType.Gzip)
            {
                try
                {
                    var decompressed = GzipUtil.DecompressGzip(message.Body);
                    return message.Clone(compressType: CompressType.None, body: decompressed);
                }
                catch (Exception)
                {
                    var base64data = message.Body != null ? Convert.ToBase64String(message.Body) : "";
                    LoggerUtil.LogError($"{TAG} decompress push message fail! body:{base64data}");
                    return null;
                }
            }
            return message;
        }

        private string CleanUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return url;
            try
            {
                var uri = new Uri(url);
                var query = uri.Query;
                if (string.IsNullOrEmpty(query) || !query.Contains("uid="))
                {
                    return url;
                }

                var pairs = query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
                var kept = new List<string>();
                foreach (var p in pairs)
                {
                    if (!p.StartsWith("uid=", StringComparison.OrdinalIgnoreCase))
                    {
                        kept.Add(p);
                    }
                }

                var qb = string.Join("&", kept);
                var ub = new UriBuilder(uri) { Query = qb };
                return ub.Uri.ToString();
            }
            catch
            {
                return url;
            }
        }

        private void OnConnectionClosed(string url, ushort code, string reason)
        {
            var cleanedUrl = CleanUrl(url);
            LoggerUtil.Log($"OnConnectionClosed url:{cleanedUrl}, code:{code}, reason:{reason}");
        }
        
        private void OnConnectionError(string url, string error)
        {
            var cleanedUrl = CleanUrl(url);
            LoggerUtil.LogWarning($"OnConnectionError url:{cleanedUrl}, error:{error}");
        }
        
    }
}