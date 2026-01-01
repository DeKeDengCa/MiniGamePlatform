using System;
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
    /// 长连接（WebSocket）管理器。
    /// </summary>
    public class WebsocketConnect
    {
        #region 字段定义

        private const string TAG = "[WebSocketConnect]";

        private string _service;
        private string _persistentConnectionUrl;
        private Action<IMessage> _onMessageHandler;
        private string _messageID;
        private NetworkManager _networkManager;

        #endregion

        #region 公共 API

        /// <summary>
        /// 初始化长连接相关配置。
        /// </summary>
        /// <param name="service">服务名</param>
        /// <param name="persistentConnectionUrl">长链接网关基址（WebSocket）</param>
        /// <param name="networkManager">NetworkManager 实例（用于调用 SendRequest）</param>
        public void Init(string service, string persistentConnectionUrl, NetworkManager networkManager)
        {
            _service = service;
            _persistentConnectionUrl = persistentConnectionUrl;
            _networkManager = networkManager;
#if UNITY_DEBUG
            Debug.Log($"{TAG} 初始化完成: service={service}, persistentConnectionUrl={persistentConnectionUrl}");
#endif
        }

        /// <summary>
        /// 建立长连接（只负责建连，不负责发送数据）。
        /// </summary>
        public void Connect()
        {
            if (string.IsNullOrEmpty(_persistentConnectionUrl))
            {
#if UNITY_DEBUG
                Debug.LogError($"{TAG} 连接URL为空");
#endif
                return;
            }

#if UNITY_DEBUG
            Debug.Log($"{TAG} 开始连接: service={_service}, url={_persistentConnectionUrl}");
#endif
            CancellationTokenSource cts = new CancellationTokenSource();
            Task<Response> task = CoreNetworkManager.Instance.Connect(_persistentConnectionUrl, cts.Token);

            task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
#if UNITY_DEBUG
                    Exception exception = t.Exception?.GetBaseException();
                    Debug.LogError($"{TAG} 连接失败: service={_service}, url={_persistentConnectionUrl}, exception={exception?.Message}");
                    if (exception != null)
                    {
                        Debug.LogError($"{TAG} 异常堆栈: {exception}");
                    }
#endif
                }
                else if (t.IsCanceled)
                {
#if UNITY_DEBUG
                    Debug.LogError($"{TAG} 连接已取消: service={_service}, url={_persistentConnectionUrl}");
#endif
                }
                else
                {
#if UNITY_DEBUG
                    Debug.Log($"{TAG} 连接成功: service={_service}, url={_persistentConnectionUrl}");
#endif
                }
            }, TaskScheduler.Default);
        }

        /// <summary>
        /// 发送长连接请求（WebSocket）。
        /// </summary>
        /// <param name="messageID">消息ID（即 method）</param>
        /// <param name="message">请求消息（Protobuf）</param>
        /// <param name="callback">回调结果对象（状态 + 返回数据）</param>
        public void SendRequest(string messageID, IMessage message, NetworkCallback callback = null)
        {
            // 直接调用 NetworkManager.SendRequest()，使用 ConnectionType.PERSISTENT，传入 service
            if (_networkManager == null)
            {
#if UNITY_DEBUG
                Debug.LogError($"{TAG} NetworkManager 为空: service={_service}, messageID={messageID}");
#endif
                return;
            }
#if UNITY_DEBUG
            Debug.Log($"{TAG} 发送请求: service={_service}, messageID={messageID}");
#endif
            _networkManager.SendRequest(_service, messageID, message, ConnectionType.PERSISTENT, callback);
        }

        /// <summary>
        /// 设置 Push 消息回调。
        /// </summary>
        /// <param name="messageID">消息ID</param>
        /// <param name="onMessage">消息回调，参数为 IMessage</param>
        public void SetOnMessage(string messageID, Action<IMessage> onMessage)
        {
            _messageID = messageID;
            _onMessageHandler = onMessage;

            if (string.IsNullOrEmpty(_persistentConnectionUrl))
            {
#if UNITY_DEBUG
                Debug.LogError($"{TAG} 连接URL为空: service={_service}, messageID={messageID}");
#endif
                return;
            }

#if UNITY_DEBUG
            Debug.Log($"{TAG} 设置消息回调: service={_service}, messageID={messageID}, url={_persistentConnectionUrl}");
#endif

            // 向底层注册 Push 消息处理器
            CoreNetworkManager.Instance.RegisterPushHandler(_persistentConnectionUrl, (PushMessage pushMessage) =>
            {
                if (pushMessage == null || pushMessage.Body == null || pushMessage.Body.Length == 0)
                {
#if UNITY_DEBUG
                    Debug.LogError($"{TAG} Push消息无效: service={_service}, messageID={messageID}");
#endif
                    return;
                }

                // 从 PushMessage.Body 反序列化为 IMessage
                // 构建完整的 method key: Service.Method
                string methodKey = $"{_service}.{messageID}";
                IMessage deserializedMessage = DeserializePushMessage(methodKey, pushMessage.Body);
                if (deserializedMessage == null)
                {
#if UNITY_DEBUG
                    Debug.LogError($"{TAG} 反序列化Push消息失败: service={_service}, messageID={messageID}, methodKey={methodKey}");
#endif
                    return;
                }

#if UNITY_DEBUG
                Debug.Log($"{TAG} 收到Push消息: service={_service}, messageID={messageID}, methodKey={methodKey}");
#endif

                // 调用 onMessage 回调
                if (_onMessageHandler != null)
                {
                    _onMessageHandler(deserializedMessage);
                }

                // 调用 NetworkManager.OnServerMessage()
                if (_networkManager == null)
                {
#if UNITY_DEBUG
                    Debug.LogError($"{TAG} NetworkManager 为空: service={_service}, messageID={messageID}");
#endif
                    return;
                }
                _networkManager.OnServerMessage(methodKey, deserializedMessage);
            });
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 从 PushMessage.Body 反序列化为 IMessage。
        /// </summary>
        /// <param name="messageID">消息ID（用于获取消息类型）</param>
        /// <param name="body">PushMessage.Body</param>
        /// <returns>反序列化后的 IMessage，失败返回 null</returns>
        private IMessage DeserializePushMessage(string messageID, byte[] body)
        {
            if (string.IsNullOrEmpty(messageID) || body == null || body.Length == 0)
            {
#if UNITY_DEBUG
                Debug.LogError($"{TAG} 反序列化参数无效: messageID={messageID}, bodyLength={body?.Length ?? 0}");
#endif
                return null;
            }

            // 使用 RpcMethodRegistry 获取消息类型（Push 消息通常是响应类型）
            if (!RpcMethodRegistry.TryGet(messageID, out RpcMethodRegistry.RpcTypePair pair))
            {
#if UNITY_DEBUG
                Debug.LogError($"{TAG} RpcMethodRegistry 未找到: messageID={messageID}");
#endif
                return null;
            }

            Type messageType = pair.GetResponseType();
            if (messageType == null)
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
                messageInstance = Activator.CreateInstance(messageType) as IMessage;
                if (messageInstance == null)
                {
#if UNITY_DEBUG
                    Debug.LogError($"{TAG} 创建实例失败: messageID={messageID}, messageType={messageType.Name}");
#endif
                    return null;
                }
                messageInstance.MergeFrom(body);
            }
            catch (Exception ex)
            {
#if UNITY_DEBUG
                Debug.LogError($"{TAG} 反序列化失败: messageID={messageID}, messageType={messageType.Name}, exception={ex.Message}");
                Debug.LogError($"{TAG} 异常堆栈: {ex}");
#endif
                return null;
            }

            return messageInstance;
        }

        #endregion
    }
}
