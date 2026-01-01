using Google.Protobuf;
using NetworkFramework.Core.Model;
using UnityEngine;

namespace Astorise.Framework.Network
{
    /// <summary>
    /// 短链接（HTTP）管理器。
    /// </summary>
    public class HttpClient
    {
        #region 字段定义

        private const string TAG = "[HttpClient]";

        private string _service;
        private NetworkManager _networkManager;

        #endregion

        #region 公共 API

        /// <summary>
        /// 初始化短链接相关配置。
        /// </summary>
        /// <param name="service">服务名</param>
        /// <param name="inconstantConnectionUrl">短链接网关基址（HTTP）</param>
        /// <param name="networkManager">NetworkManager 实例（用于调用 SendRequest）</param>
        public void Init(string service, NetworkManager networkManager)
        {
            _service = service;
            _networkManager = networkManager;
#if UNITY_DEBUG
            Debug.Log($"{TAG} 初始化完成: service={service}");
#endif
        }

        /// <summary>
        /// 发送短链接请求（HTTP）。
        /// </summary>
        /// <param name="messageID">消息ID（即 method）</param>
        /// <param name="message">请求消息（Protobuf）</param>
        /// <param name="callback">回调结果对象（状态 + 返回数据）</param>
        public void SendRequest(string messageID, IMessage message, NetworkCallback callback = null)
        {
            // 直接调用 NetworkManager.SendRequest()，使用 ConnectionType.INCONSTANT，传入 service
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
            _networkManager.SendRequest(_service, messageID, message, ConnectionType.INCONSTANT, callback);
        }

        #endregion
    }
}
