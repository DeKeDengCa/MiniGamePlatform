using System;
using System.Collections.Generic;
using UnityEngine;

namespace Astorise.Framework.SDK
{
    /// <summary>
    /// Editor 平台 UnityBridge（用于本地调试/编辑器运行）。
    /// 模拟原生桥接行为：把自己当成一个 native 的处理类，把所有数据都封装成 json 后，去调用 UnityBridge.Instance.OnBridgeMessage 模拟原生主动调用。
    /// </summary>
    public sealed class UnityBridgeEditor : IUnityBridgePlatform
    {
        private const string LogTag = "[UnityBridgeEditor]";

        // 方法名到回调函数的映射表
        private readonly Dictionary<string, Action<BridgeMessage>> _methodHandlers = new Dictionary<string, Action<BridgeMessage>>();

        /// <summary>
        /// 初始化 Editor 桥接。
        /// </summary>
        public void Initialize()
        {
            InitializeMethodHandlers();
#if UNITY_DEBUG
            Debug.Log($"{LogTag} Editor Bridge 初始化完成");
            Debug.Log($"{LogTag} 通知原生端 Unity 已准备好");
#endif
        }

        /// <summary>
        /// 初始化方法映射表。
        /// </summary>
        private void InitializeMethodHandlers()
        {
            _methodHandlers[BridgeMethodName.IsSupportMethod] = OnIsSupportMethod;
            _methodHandlers[BridgeMethodName.Report] = OnReport;
            _methodHandlers[BridgeMethodName.GetDeviceInfo] = OnGetDeviceInfo;
            _methodHandlers[BridgeMethodName.GetPublicParam] = OnGetPublicParam;
            _methodHandlers[BridgeMethodName.Log] = OnLog;
            _methodHandlers[BridgeMethodName.GetConfigInfo] = OnGetConfigInfo;
            _methodHandlers[BridgeMethodName.CallNative] = OnCallNative;
        }

        /// <summary>
        /// Unity→Native 发送消息（Editor 版本：序列化为 JSON 并打印日志）。
        /// </summary>
        public void SendRequestMessage(BridgeMessage message)
        {
            if (message == null)
            {
#if UNITY_DEBUG
                Debug.LogError($"{LogTag} SendRequestMessage：message 为 null");
#endif
                return;
            }

            try
            {
                string jsonString = UnityBridgeJsonSerializer.Serialize(message);
#if UNITY_DEBUG
                Debug.Log($"{LogTag} Unity→Native 发送消息:\n{jsonString}");
#endif
            }
            catch (Exception exception)
            {
#if UNITY_DEBUG
                Debug.LogError($"{LogTag} SendRequestMessage 序列化失败：{exception.Message}");
#endif
                return;
            }

            if (string.Equals(message.type, BridgeMessageType.Request, StringComparison.Ordinal))
            {
                RouteRequestMessage(message);
            }
        }

        /// <summary>
        /// 处理请求消息，根据方法名路由到对应的回调函数。
        /// </summary>
        private void RouteRequestMessage(BridgeMessage message)
        {
            if (message == null || string.IsNullOrEmpty(message.name))
            {
                return;
            }

            Action<BridgeMessage> handler = _methodHandlers[message.name];
            handler(message);
        }

        #region 方法回调函数

        /// <summary>
        /// isSupportMethod 回调。
        /// </summary>
        private void OnIsSupportMethod(BridgeMessage request)
        {
            Dictionary<string, object> responseData = new Dictionary<string, object>()
            {
                ["status"] = 200,
                ["message"] = "Success",
                ["data"] = new Dictionary<string, object>()
                {
                    ["supported"] = true
                }
            };
            SendResponse(request, 200, "Success", responseData);
        }

        /// <summary>
        /// report 回调。
        /// </summary>
        private void OnReport(BridgeMessage request)
        {
            Dictionary<string, object> responseData = new Dictionary<string, object>()
            {
                ["status"] = 200,
                ["message"] = "Success",
                ["data"] = new Dictionary<string, object>()
            };
            SendResponse(request, 200, "Success", responseData);
        }

        /// <summary>
        /// getDeviceInfo 回调。
        /// </summary>
        private void OnGetDeviceInfo(BridgeMessage request)
        {
            Dictionary<string, object> deviceInfo = new Dictionary<string, object>()
            {
                ["deviceId"] = SystemInfo.deviceUniqueIdentifier,
                ["osVersion"] = SystemInfo.operatingSystem,
                ["deviceModel"] = SystemInfo.deviceModel,
                ["platform"] = Application.platform.ToString()
            };

            Dictionary<string, object> responseData = new Dictionary<string, object>()
            {
                ["status"] = 200,
                ["message"] = "Success",
                ["data"] = deviceInfo
            };
            SendResponse(request, 200, "Success", responseData);
        }

        /// <summary>
        /// getPublicParam 回调。
        /// </summary>
        private void OnGetPublicParam(BridgeMessage request)
        {
            Dictionary<string, object> publicParam = new Dictionary<string, object>()
            {
                ["appVersion"] = Application.version,
                ["platform"] = Application.platform.ToString()
            };

            Dictionary<string, object> responseData = new Dictionary<string, object>()
            {
                ["status"] = 200,
                ["message"] = "Success",
                ["data"] = publicParam
            };
            SendResponse(request, 200, "Success", responseData);
        }

        /// <summary>
        /// log 回调。
        /// </summary>
        private void OnLog(BridgeMessage request)
        {
            Dictionary<string, object> responseData = new Dictionary<string, object>()
            {
                ["status"] = 200,
                ["message"] = "Success",
                ["data"] = new Dictionary<string, object>()
            };
            SendResponse(request, 200, "Success", responseData);
        }

        /// <summary>
        /// getConfigInfo 回调。
        /// </summary>
        private void OnGetConfigInfo(BridgeMessage request)
        {
            Dictionary<string, object> configData = new Dictionary<string, object>()
            {
                ["publicKey"] = "MIGJAoGBAMemgJuzFBXPZCmYRWR1k9iFHMfOcorItJJ0d7AWnUW88cjJwOjN4Y/uxiu6UU7i5J5or7jACY7yHIwVEdUC2PcxDyFoaN6UoZyydhaC3Sx10Ltkr6yuquZopNQy1/rzfdYAlU2STyhHFMFuuHOdDsViTDqgDYKWdzANH3ebqoCZAgMBAAE=",
                ["publicKeyId"] = "0",
                ["inconstantUrl"] = "http://dev-api.ruok.live/sgw/api?app=astrorise",
                ["persistentUrl"] = "ws://dev-api.ruok.live/sgw/ws?app=astrorise",
            };

            Dictionary<string, object> responseData = new Dictionary<string, object>()
            {
                ["status"] = 200,
                ["message"] = "Success",
                ["data"] = configData
            };
            SendResponse(request, 200, "Success", responseData);
        }

        /// <summary>
        /// callNative 回调。
        /// </summary>
        private void OnCallNative(BridgeMessage request)
        {
            Dictionary<string, object> requestData = (Dictionary<string, object>)request.data;
            string command = requestData["command"] as string;

            // 根据 command 分发到对应的处理函数
            switch (command)
            {
                case BridgeCommandName.Login:
                    OnCallNativeLogin(request, requestData);
                    break;
                case BridgeCommandName.Logout:
                    OnCallNativeLogout(request, requestData);
                    break;
                case BridgeCommandName.DeleteAccount:
                    OnCallNativeDeleteAccount(request, requestData);
                    break;
                case BridgeCommandName.RefreshToken:
                    OnCallNativeRefreshToken(request, requestData);
                    break;
                default:
#if UNITY_DEBUG
                    Debug.LogError($"{LogTag} callNative: 未支持的 command: {command}");
#endif
                    SendResponse(request, 20000, "MethodNotImplemented", null);
                    break;
            }
        }

        /// <summary>
        /// callNative.login 回调。
        /// </summary>
        private void OnCallNativeLogin(BridgeMessage request, Dictionary<string, object> requestData)
        {
            Dictionary<string, object> extensionParameters = (Dictionary<string, object>)requestData["ext"];
            string loginType = extensionParameters["type"] as string;

            long userId = GeneratePositiveUserId();

            Dictionary<string, object> loginData = new Dictionary<string, object>()
            {
                ["type"] = loginType,
                ["uid"] = userId,
                ["isFirstRegister"] = true,
                ["access_token"] = $"access_token_{userId}",
                ["refresh_token"] = $"refresh_token{userId}",
                ["avatarUrl"] = null,
                ["username"] = "MockUser",
            };

            Dictionary<string, object> content = new Dictionary<string, object>()
            {
                ["code"] = 1L,
                ["msg"] = "success",
                ["timestamp"] = BridgeMessage.GetCurrentTimestampMicroseconds(),
                ["data"] = loginData,
            };

            Dictionary<string, object> callbackData = new Dictionary<string, object>()
            {
                ["command"] = BridgeCallUnityCommandName.LoginCompletion,
                ["_requestId"] = Guid.NewGuid().ToString(),
                ["ext"] = content,
            };

            BridgeMessage callbackMessage = new BridgeMessage();
            callbackMessage.type = BridgeMessageType.Request;
            callbackMessage.id = Guid.NewGuid().ToString();
            callbackMessage.name = BridgeMethodName.CallUnity;
            callbackMessage.ts = BridgeMessage.GetCurrentTimestampMicroseconds();
            callbackMessage.data = callbackData;

            SendToUnity(callbackMessage);
        }

        /// <summary>
        /// callNative.logout 回调。
        /// </summary>
        private void OnCallNativeLogout(BridgeMessage request, Dictionary<string, object> requestData)
        {
            Dictionary<string, object> content = new Dictionary<string, object>()
            {
                ["code"] = 1L,
                ["msg"] = "success",
                ["timestamp"] = BridgeMessage.GetCurrentTimestampMicroseconds(),
            };

            Dictionary<string, object> callbackData = new Dictionary<string, object>()
            {
                ["command"] = BridgeCallUnityCommandName.LogoutCompletion,
                ["_requestId"] = Guid.NewGuid().ToString(),
                ["ext"] = content,
            };

            BridgeMessage callbackMessage = new BridgeMessage();
            callbackMessage.type = BridgeMessageType.Request;
            callbackMessage.id = Guid.NewGuid().ToString();
            callbackMessage.name = BridgeMethodName.CallUnity;
            callbackMessage.ts = BridgeMessage.GetCurrentTimestampMicroseconds();
            callbackMessage.data = callbackData;

            SendToUnity(callbackMessage);
        }

        /// <summary>
        /// callNative.deleteAccount 回调。
        /// </summary>
        private void OnCallNativeDeleteAccount(BridgeMessage request, Dictionary<string, object> requestData)
        {
            Dictionary<string, object> responseData = new Dictionary<string, object>()
            {
                ["status"] = 200,
                ["message"] = "Success",
                ["data"] = new Dictionary<string, object>()
            };
            SendResponse(request, 200, "Success", responseData);
        }

        /// <summary>
        /// callNative.refreshToken 回调。
        /// </summary>
        private void OnCallNativeRefreshToken(BridgeMessage request, Dictionary<string, object> requestData)
        {
            Dictionary<string, object> extensionParameters = (Dictionary<string, object>)requestData["ext"];
            string newAccessToken = extensionParameters["access_token"] as string;
            string newRefreshToken = extensionParameters["refresh_token"] as string;

            Dictionary<string, object> tokenData = new Dictionary<string, object>()
            {
                ["access_token"] = newAccessToken,
                ["refresh_token"] = newRefreshToken,
            };

            Dictionary<string, object> responseData = new Dictionary<string, object>()
            {
                ["status"] = 200,
                ["message"] = "Success",
                ["data"] = tokenData
            };
            SendResponse(request, 200, "Success", responseData);
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 发送响应消息到 Unity。
        /// </summary>
        private void SendResponse(BridgeMessage request, int status, string message, Dictionary<string, object> data)
        {
            Dictionary<string, object> responseData = new Dictionary<string, object>()
            {
                ["status"] = status,
                ["message"] = message
            };

            if (data != null)
            {
                responseData["data"] = data;
            }
            else
            {
                responseData["data"] = new Dictionary<string, object>();
            }

            BridgeMessage response = new BridgeMessage();
            response.type = BridgeMessageType.Response;
            response.id = request.id;
            response.name = request.name;
            response.ts = request.ts; // 复用请求的时间戳
            response.data = responseData;

            SendToUnity(response);
        }

        /// <summary>
        /// 发送消息到 Unity（通过 UnityBridge.Instance.OnBridgeMessage）。
        /// </summary>
        private void SendToUnity(BridgeMessage message)
        {
            if (UnityBridge.Instance == null)
            {
#if UNITY_DEBUG
                Debug.LogError($"{LogTag} UnityBridge.Instance 为 null，无法发送消息");
#endif
                return;
            }

            string jsonString = UnityBridgeJsonSerializer.Serialize(message);

#if UNITY_DEBUG
            Debug.Log($"{LogTag} Native→Unity 模拟回调:\n{jsonString}");
#endif

            // 模拟原生主动调用 UnityBridge.Instance.OnBridgeMessage
            UnityBridge.Instance.OnBridgeMessage(jsonString);
        }

        /// <summary>
        /// 生成正数用户 ID。
        /// </summary>
        private long GeneratePositiveUserId()
        {
            byte[] guidBytes = Guid.NewGuid().ToByteArray();
            long userId = BitConverter.ToInt64(guidBytes, 8);
            return 0x7fffffffffffffff & userId;
        }

        #endregion
    }
}
