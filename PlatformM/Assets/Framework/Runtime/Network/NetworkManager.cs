using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using NetworkFramework.Core.Model;
using Scommon;
using Astorise.Proto.Generated;
using UnityEngine;

using CoreNetworkManager = NetworkFramework.Core.Manager.NetworkManager;

namespace Astorise.Framework.Network
{
    /// <summary>
    /// 网络管理器：负责创建 WebSocketConnect 和 HttpClient 实例，以及提供服务器消息回调总入口。
    /// </summary>
    public class NetworkManager
    {
        #region 字段定义

        private const string TAG = "[NetworkManager]";

        // 对象池缓存
        private readonly Stack<Request> _requestPool = new Stack<Request>(10);
        private readonly Stack<RequestControl> _requestControlPool = new Stack<RequestControl>(10);
        private readonly Stack<NetworkCallback> _callbackPool = new Stack<NetworkCallback>(10);

        // AppNetConfig 缓存（配置不变时可复用）
        private AppNetConfig _cachedAppNetConfig;

        // URL 配置（供 WebSocketConnect 和 HttpClient 使用）
        private string _persistentConnectionRawUrl;
        private string _inconstantConnectionUrl;

        #endregion

        #region 公共 API

        /// <summary>
        /// 初始化网络管理器相关配置。
        /// </summary>
        /// <param name="persistentConnectionRawUrl">长连接网关基址（WebSocket）</param>
        /// <param name="inconstantConnectionUrl">短链接网关基址（HTTP）</param>
        /// <param name="publicKeyID">公钥 ID</param>
        /// <param name="publicKey">公钥</param>
        /// <param name="token">账号 Token（用于后续请求鉴权），默认为 null</param>
        public void Init(string persistentConnectionRawUrl, string inconstantConnectionUrl, string publicKeyID, string publicKey, string token = null)
        {
            _persistentConnectionRawUrl = persistentConnectionRawUrl;
            _inconstantConnectionUrl = inconstantConnectionUrl;
            _cachedAppNetConfig = new AppNetConfig(publicKeyID, publicKey);
            CoreNetworkManager.Instance.Init(inconstantConnectionUrl, persistentConnectionRawUrl, token, _cachedAppNetConfig);
#if UNITY_DEBUG
            Debug.Log($"{TAG} 初始化完成: persistentConnectionRawUrl={persistentConnectionRawUrl}, inconstantConnectionUrl={inconstantConnectionUrl}, publicKeyID={publicKeyID}");
#endif
        }

        /// <summary>
        /// 设置或更新账号 Token（用于后续请求鉴权）。
        /// </summary>
        /// <param name="token">账号 Token</param>
        public void SetToken(string token)
        {
            CoreNetworkManager.Instance.SetAccountToken(token);
#if UNITY_DEBUG
            Debug.Log($"{TAG} Token 已更新");
#endif
        }

        /// <summary>
        /// 创建 WebSocketConnect 实例。
        /// </summary>
        /// <param name="service">服务名</param>
        /// <returns>WebSocketConnect 实例</returns>
        public WebsocketConnect CreateWebSocketConnect(string service)
        {
            WebsocketConnect instance = new WebsocketConnect();
            instance.Init(service, _persistentConnectionRawUrl, this);
            return instance;
        }

        /// <summary>
        /// 创建 HttpClient 实例。
        /// </summary>
        /// <param name="service">服务名</param>
        /// <returns>HttpClient 实例</returns>
        public HttpClient CreateHttpClient(string service)
        {
            HttpClient instance = new HttpClient();
            instance.Init(service, this);
            return instance;
        }

        /// <summary>
        /// 发送请求（统一基础函数）。
        /// </summary>
        /// <param name="service">服务名</param>
        /// <param name="messageID">消息ID（即 method）</param>
        /// <param name="message">请求消息（Protobuf）</param>
        /// <param name="connectionType">连接类型（PERSISTENT 长连接 或 INCONSTANT 短链接）</param>
        /// <param name="callback">回调结果对象（状态 + 返回数据）</param>
        public void SendRequest(string service, string messageID, IMessage message, ConnectionType connectionType, NetworkCallback callback = null)
        {
            if (string.IsNullOrEmpty(service) || string.IsNullOrEmpty(messageID) || message == null)
            {
#if UNITY_DEBUG
                Debug.LogError($"{TAG} 发送请求参数无效: service={service}, messageID={messageID}, message={message?.GetType().Name ?? "null"}");
#endif
                if (callback != null)
                {
                    NetworkCallback errorCallback = GetCallbackFromPool();
                    errorCallback.Status = NetworkCallbackState.Failed;
                    errorCallback.Data = null;
                    callback.Status = errorCallback.Status;
                    callback.Data = errorCallback.Data;
                    ReturnCallbackToPool(errorCallback);
                }
                return;
            }

            // messageID 就是 method
            string method = messageID;

            // 2. 将 IMessage 序列化为 byte[]
            byte[] bodyBytes = message.ToByteArray();
            if (bodyBytes == null || bodyBytes.Length == 0)
            {
#if UNITY_DEBUG
                Debug.LogError($"{TAG} 序列化消息失败: service={service}, method={method}");
#endif
                if (callback != null)
                {
                    NetworkCallback errorCallback = GetCallbackFromPool();
                    errorCallback.Status = NetworkCallbackState.Failed;
                    errorCallback.Data = null;
                    callback.Status = errorCallback.Status;
                    callback.Data = errorCallback.Data;
                    ReturnCallbackToPool(errorCallback);
                }
                return;
            }

#if UNITY_DEBUG
            Debug.Log($"{TAG} 发送请求: service={service}, method={method}, connectionType={connectionType}, bodySize={bodyBytes.Length}");
#endif

            // 3. 从对象池获取 Request 和 RequestControl 对象
            Request request = GetRequestFromPool();
            RequestControl requestControl = GetRequestControlFromPool();

            // 4. 设置 Request 对象字段
            requestControl.Service = service;
            requestControl.Method = method;
            requestControl.Reason = RPCReason.UserAction;
            requestControl.CompressType = Scommon.CompressType.None;

            request.RequestControl = requestControl;
            request.Body = bodyBytes;
            request.UseConnectionType = connectionType;
            request.ContentType = Scommon.ContentType.Proto;

            // 5. 调用底层 CoreNetworkManager.Instance.Request()（使用回调方式处理 Task 结果）
            CancellationTokenSource cts = new CancellationTokenSource();
            Task<Response> task = CoreNetworkManager.Instance.Request(request, cts.Token);

            task.ContinueWith(t =>
            {
                Response response = null;
                Exception exception = null;

                if (t.IsFaulted)
                {
                    exception = t.Exception?.GetBaseException();
#if UNITY_DEBUG
                    Debug.LogError($"{TAG} 发送请求失败: service={service}, method={method}, exception={exception?.Message}");
                    if (exception != null)
                    {
                        Debug.LogError($"{TAG} 异常堆栈: {exception}");
                    }
#endif
                }
                else if (t.IsCanceled)
                {
                    exception = new OperationCanceledException();
#if UNITY_DEBUG
                    Debug.LogError($"{TAG} 发送请求已取消: service={service}, method={method}");
#endif
                }
                else
                {
                    response = t.Result;
                }

                // 6. 从 Response 反序列化为 IMessage（使用 RpcMethodRegistry 获取响应类型）
                IMessage responseMessage = null;
                NetworkCallbackState status = NetworkCallbackState.Failed;

                if (response != null)
                {
                    responseMessage = DeserializeResponse(method, response);
                    status = ConvertResponseToNetworkCallbackStatus(response);
                    
                    if (responseMessage == null)
                    {
#if UNITY_DEBUG
                        Debug.LogError($"{TAG} 反序列化响应失败: service={service}, method={method}, netCode={response.NetCode}, code={response.Code}");
#endif
                    }
                    else
                    {
#if UNITY_DEBUG
                        Debug.Log($"{TAG} 发送请求成功: service={service}, method={method}, status={status}");
#endif
                    }
                }

                // 7. 从对象池获取 NetworkCallback 对象，设置字段并调用回调
                if (callback != null)
                {
                    callback.Status = status;
                    callback.Data = responseMessage;
                }

                // 8. 归还 Request、RequestControl、NetworkCallback 对象到池中
                ReturnRequestToPool(request);
                ReturnRequestControlToPool(requestControl);
            }, TaskScheduler.Default);
        }

        /// <summary>
        /// 服务器消息回调总入口。
        /// </summary>
        /// <param name="messageID">消息 ID</param>
        /// <param name="message">Protobuf 消息对象</param>
        public void OnServerMessage(string messageID, IMessage message)
        {
            // 当前可为空实现，供业务层 override 或订阅
        }

        #endregion

        #region 对象池方法

        /// <summary>
        /// 从对象池获取 Request 对象。
        /// </summary>
        /// <returns>Request 对象</returns>
        private Request GetRequestFromPool()
        {
            if (_requestPool.Count <= 0)
            {
                return new Request();
            }
            Request request = _requestPool.Pop();
            // 清理字段
            request.InconstantConnectionUrl = null;
            request.PersistentConnectionUrl = null;
            request.Token = null;
            request.Body = null;
            request.RequestControl = null;
            request.PublicParams = null;
            request.Headers?.Clear();
            request.OnlyConnected = false;
            return request;
        }

        /// <summary>
        /// 归还 Request 对象到池中。
        /// </summary>
        /// <param name="request">Request 对象</param>
        private void ReturnRequestToPool(Request request)
        {
            if (_requestPool.Count >= 100)
            {
                return;
            }
            _requestPool.Push(request);
        }

        /// <summary>
        /// 从对象池获取 RequestControl 对象。
        /// </summary>
        /// <returns>RequestControl 对象</returns>
        private RequestControl GetRequestControlFromPool()
        {
            if (_requestControlPool.Count <= 0)
            {
                return new RequestControl();
            }
            RequestControl requestControl = _requestControlPool.Pop();
            // 清理字段
            requestControl.Service = null;
            requestControl.Method = null;
            requestControl.SeqId = 0;
            requestControl.RouteKey = null;
            requestControl.Timeout = 0;
            requestControl.CryptoInfo = null;
            requestControl.CompressType = Scommon.CompressType.None;
            requestControl.IsAppBackground = false;
            requestControl.Reason = RPCReason.None;
            return requestControl;
        }

        /// <summary>
        /// 归还 RequestControl 对象到池中。
        /// </summary>
        /// <param name="requestControl">RequestControl 对象</param>
        private void ReturnRequestControlToPool(RequestControl requestControl)
        {
            if (_requestControlPool.Count >= 100)
            {
                return;
            }
            _requestControlPool.Push(requestControl);
        }

        /// <summary>
        /// 从对象池获取 NetworkCallback 对象。
        /// </summary>
        /// <returns>NetworkCallback 对象</returns>
        private NetworkCallback GetCallbackFromPool()
        {
            if (_callbackPool.Count <= 0)
            {
                return new NetworkCallback();
            }
            NetworkCallback callback = _callbackPool.Pop();
            // 清理字段
            callback.Status = NetworkCallbackState.Success;
            callback.Data = null;
            return callback;
        }

        /// <summary>
        /// 归还 NetworkCallback 对象到池中。
        /// </summary>
        /// <param name="callback">NetworkCallback 对象</param>
        private void ReturnCallbackToPool(NetworkCallback callback)
        {
            if (_callbackPool.Count >= 100)
            {
                return;
            }
            _callbackPool.Push(callback);
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 从 Response 反序列化为 IMessage。
        /// </summary>
        /// <param name="messageID">消息ID（用于获取响应类型）</param>
        /// <param name="response">Response 对象</param>
        /// <returns>反序列化后的 IMessage，失败返回 null</returns>
        private IMessage DeserializeResponse(string messageID, Response response)
        {
            if (response == null || response.Body == null || response.Body.Length == 0)
            {
#if UNITY_DEBUG
                Debug.LogError($"{TAG} 响应体为空: messageID={messageID}");
#endif
                return null;
            }

            // 使用 RpcMethodRegistry 获取响应类型
            if (!RpcMethodRegistry.TryGet(messageID, out RpcMethodRegistry.RpcTypePair pair))
            {
#if UNITY_DEBUG
                Debug.LogError($"{TAG} RpcMethodRegistry 未找到: messageID={messageID}");
#endif
                return null;
            }

            Type responseType = pair.GetResponseType();
            if (responseType == null)
            {
#if UNITY_DEBUG
                Debug.LogError($"{TAG} 响应类型为空: messageID={messageID}");
#endif
                return null;
            }

            // 使用 IMessage.MergeFrom 直接反序列化，无需反射调用泛型方法
            IMessage messageInstance = null;
            try
            {
                messageInstance = Activator.CreateInstance(responseType) as IMessage;
                if (messageInstance == null)
                {
#if UNITY_DEBUG
                    Debug.LogError($"{TAG} 创建响应实例失败: messageID={messageID}, responseType={responseType.Name}");
#endif
                    return null;
                }
                messageInstance.MergeFrom(response.Body);
            }
            catch (Exception ex)
            {
#if UNITY_DEBUG
                Debug.LogError($"{TAG} 反序列化失败: messageID={messageID}, responseType={responseType.Name}, exception={ex.Message}");
                Debug.LogError($"{TAG} 异常堆栈: {ex}");
#endif
                return null;
            }

            return messageInstance;
        }

        /// <summary>
        /// 将 Response 转换为 NetworkCallback 状态。
        /// </summary>
        /// <param name="response">Response 对象</param>
        /// <returns>NetworkCallbackState</returns>
        private NetworkCallbackState ConvertResponseToNetworkCallbackStatus(Response response)
        {
            if (response == null)
            {
                return NetworkCallbackState.Failed;
            }

            // 网络错误
            if (response.NetCode != 200 && response.NetCode != 0)
            {
                return NetworkCallbackState.Failed;
            }

            // 业务错误：Code == 0 表示成功
            if (response.Code == ErrorCode.SUCCESS)
            {
                return NetworkCallbackState.Success;
            }

            // 其他情况视为失败
            return NetworkCallbackState.Failed;
        }

        #endregion
    }
}
