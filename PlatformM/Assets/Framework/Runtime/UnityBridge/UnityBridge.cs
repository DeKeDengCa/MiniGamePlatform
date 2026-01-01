using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using UnityEngine;
using Astorise.Framework.Network;

namespace Astorise.Framework.SDK
{
    /// <summary>
    /// UnityBridge 总入口（路由器）：外部只依赖此类型，不直接依赖各平台实现类。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class UnityBridge : MonoBehaviour
    {
        #region 单例模式

        /// <summary>
        /// UnityBridge 单例实例。
        /// </summary>
        public static UnityBridge Instance;

        #endregion

        #region 字段定义

        /// <summary>
        /// 当前已初始化的平台类型（用于调试/上层判断）。
        /// </summary>
        public UnityBridgePlatform CurrentPlatform = UnityBridgePlatform.Editor;

        private IUnityBridgePlatform _implementation;
        private readonly ConcurrentDictionary<string, Action<BridgeResult>> _pendingRequests = new ConcurrentDictionary<string, Action<BridgeResult>>();

        /// <summary>
        /// Native -> Unity 请求消息
        /// </summary>
        private readonly Dictionary<string, Func<BridgeMessage, BridgeResult>> _registeredNativeRequestMethods = new Dictionary<string, Func<BridgeMessage, BridgeResult>>();

        /// <summary>
        /// 空的扩展参数字典，用于复用（避免频繁创建新对象）。
        /// </summary>
        private static readonly Dictionary<string, object> EmptyExtensionParametersDictionary = new Dictionary<string, object>();

        /// <summary>
        /// Login 扩展参数字典，用于复用（避免频繁创建新对象）。
        /// </summary>
        private static readonly Dictionary<string, object> LoginExtensionParametersDictionary = new Dictionary<string, object>();

        /// <summary>
        /// RefreshToken 扩展参数字典，用于复用（避免频繁创建新对象）。
        /// </summary>
        private static readonly Dictionary<string, object> RefreshTokenExtensionParametersDictionary = new Dictionary<string, object>();

        /// <summary>
        /// Login 参数字典，用于复用（避免频繁创建新对象）。
        /// </summary>
        private static readonly Dictionary<string, object> LoginRequestParametersDictionary = new Dictionary<string, object>();

        /// <summary>
        /// Logout 参数字典，用于复用（避免频繁创建新对象）。
        /// </summary>
        private static readonly Dictionary<string, object> LogoutRequestParametersDictionary = new Dictionary<string, object>();

        /// <summary>
        /// DeleteAccount 参数字典，用于复用（避免频繁创建新对象）。
        /// </summary>
        private static readonly Dictionary<string, object> DeleteAccountRequestParametersDictionary = new Dictionary<string, object>();

        /// <summary>
        /// RefreshToken 参数字典，用于复用（避免频繁创建新对象）。
        /// </summary>
        private static readonly Dictionary<string, object> RefreshTokenRequestParametersDictionary = new Dictionary<string, object>();

        /// <summary>
        /// Log 参数字典，用于复用（避免频繁创建新对象）。
        /// </summary>
        private static readonly Dictionary<string, object> LogRequestParametersDictionary = new Dictionary<string, object>();

        /// <summary>
        /// Report 参数字典，用于复用（避免频繁创建新对象）。
        /// </summary>
        private static readonly Dictionary<string, object> ReportRequestParametersDictionary = new Dictionary<string, object>();

        /// <summary>
        /// IsSupportMethod 参数字典，用于复用（避免频繁创建新对象）。
        /// </summary>
        private static readonly Dictionary<string, object> IsSupportMethodRequestParametersDictionary = new Dictionary<string, object>();

        #endregion

        #region 生命周期

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        #endregion

        #region 初始化

        /// <summary>
        /// 初始化 UnityBridge，并选择平台实现。
        /// 注意：不使用编译宏隔离平台类；由 platform 参数决定路由目标。
        /// </summary>
        public void Initialize(UnityBridgePlatform platform)
        {
            CurrentPlatform = platform;
            _implementation = CreateImplementation(platform);

            // 平台实现可选择保存该回调；当前框架层仅搭骨架。
            // Initialize 中会处理通知原生端 Unity 已准备好的逻辑
            _implementation?.Initialize();

            // 注册内置方法
            RegisterBuiltinMethods();
        }

        private void RegisterBuiltinMethods()
        {
            _registeredNativeRequestMethods[BridgeMethodName.GetUnityInfo] = CreateUnityInfoResult;
            _registeredNativeRequestMethods[BridgeMethodName.IsSupportMethod] = CreateSupportMethodResult;
            _registeredNativeRequestMethods[BridgeMethodName.CallUnity] = ExecuteCallUnityRequest;
        }

        private IUnityBridgePlatform CreateImplementation(UnityBridgePlatform platform)
        {
            switch (platform)
            {
                case UnityBridgePlatform.Editor:
                    return new UnityBridgeEditor();
                case UnityBridgePlatform.Android:
                    return new UnityBridgeAndroid();
                case UnityBridgePlatform.IOS:
                    return new UnityBridgeIOS();
                default:
                    return new UnityBridgeEditor();
            }
        }

        /// <summary>
        /// Unity→Native 发送消息（参数参考现有 bridge：BridgeMessage）。
        /// </summary>
        public void SendRequestMessage(BridgeMessage message)
        {
            _implementation?.SendRequestMessage(message);
        }

        /// <summary>
        /// Unity→Native 发送请求消息（带回调）。
        /// </summary>
        public void SendRequestMessage(string methodName, Dictionary<string, object> parameters, Action<BridgeResult> callback = null)
        {
            BridgeMessage message = new BridgeMessage();
            message.type = BridgeMessageType.Request;
            message.id = System.Guid.NewGuid().ToString();
            message.name = methodName;
            message.ts = BridgeMessage.GetCurrentTimestampMicroseconds();
            message.data = parameters ?? EmptyExtensionParametersDictionary;

            if (callback != null)
            {
                _pendingRequests[message.id] = callback;
            }

            SendRequestMessage(message);
        }

        private void SendResponseMessage(string requestId, string methodName, BridgeResult result, long requestTs)
        {
            Dictionary<string, object> responseData = new Dictionary<string, object>
            {
                ["status"] = result.status,
                ["message"] = result.message ?? "success",
                ["data"] = result.data ?? EmptyExtensionParametersDictionary
            };

            BridgeMessage response = new BridgeMessage();
            response.type = BridgeMessageType.Response;
            response.id = requestId;
            response.name = methodName;
            response.ts = requestTs;
            response.data = responseData;

            SendRequestMessage(response);
        }

        #endregion

        #region Native -> Unity 请求消息

        /// <summary>
        /// Native→Unity 消息入口（通常由 UnitySendMessage / 原生回调调用）。
        /// 此方法供原生代码通过 UnitySendMessage 调用。
        /// </summary>
        public void OnBridgeMessage(string jsonString)
        {
            if (string.IsNullOrEmpty(jsonString))
            {
                return;
            }

            BridgeMessage message = null;
            try
            {
                message = UnityBridgeJsonSerializer.Deserialize(jsonString);
                if (message == null)
                {
#if UNITY_DEBUG
                    Debug.LogError($"[UnityBridge] 反序列化消息失败：{jsonString}");
#endif
                    return;
                }

                if (string.Equals(message.type, BridgeMessageType.Response, StringComparison.Ordinal))
                {
                    ResolveNativeResponseMessage(message);
                }
                else if (string.Equals(message.type, BridgeMessageType.Request, StringComparison.Ordinal))
                {
                    RouteNativeRequestMessage(message);
                }
            }
            catch (Exception ex)
            {
#if UNITY_DEBUG
                Debug.LogError($"[UnityBridge] 处理消息异常：{ex.Message}\n{jsonString}");
#endif
                if (message != null &&
                    string.Equals(message.type, BridgeMessageType.Request, StringComparison.Ordinal) &&
                    !string.IsNullOrEmpty(message.id) &&
                    !string.IsNullOrEmpty(message.name))
                {
                    if (string.Equals(message.name, BridgeMethodName.CallUnity, StringComparison.Ordinal))
                    {
                        Dictionary<string, object> data = (Dictionary<string, object>)message.data;
                        string command = data["command"].ToString();
                        SendResponseMessage(message.id, message.name, BridgeResult.MethodNotImplemented(command), message.ts);
                        return;
                    }

                    SendResponseMessage(message.id, message.name, BridgeResult.MethodNotImplemented(message.name), message.ts);
                }
            }
        }

        /// <summary>
        /// 处理Native -> Unity 的Request消息
        /// </summary>
        private void RouteNativeRequestMessage(BridgeMessage message)
        {
            if (string.IsNullOrEmpty(message.name))
            {
                return;
            }

            long requestTs = message.ts;

            Func<BridgeMessage, BridgeResult> handler;
            handler = _registeredNativeRequestMethods[message.name];
            BridgeResult result = handler(message);
            if (result != null)
            {
                SendResponseMessage(message.id, message.name, result, requestTs);
            }
        }

        private BridgeResult CreateUnityInfoResult(BridgeMessage message)
        {
            Dictionary<string, object> unityInfo = new Dictionary<string, object>
            {
                ["unityVersion"] = Application.unityVersion,
                ["platform"] = Application.platform.ToString(),
                ["isEditor"] = Application.isEditor,
                ["isPlaying"] = Application.isPlaying,
                ["targetFrameRate"] = Application.targetFrameRate,
                ["graphicsDeviceName"] = SystemInfo.graphicsDeviceName,
                ["graphicsMemorySize"] = SystemInfo.graphicsMemorySize,
                ["graphicsDeviceType"] = SystemInfo.graphicsDeviceType.ToString(),
                ["graphicsDeviceVersion"] = SystemInfo.graphicsDeviceVersion,
                ["graphicsShaderLevel"] = SystemInfo.graphicsShaderLevel,
                ["productName"] = Application.productName,
                ["companyName"] = Application.companyName,
                ["version"] = Application.version,
                ["bundleIdentifier"] = Application.identifier,
                ["maxTextureSize"] = SystemInfo.maxTextureSize,
                ["supportsVibration"] = SystemInfo.supportsVibration,
                ["supportsAccelerometer"] = SystemInfo.supportsAccelerometer,
                ["supportsGyroscope"] = SystemInfo.supportsGyroscope,
                ["supportsLocationService"] = SystemInfo.supportsLocationService
            };

            return BridgeResult.SuccessWithData("success", unityInfo);
        }

        private BridgeResult CreateSupportMethodResult(BridgeMessage message)
        {
            Dictionary<string, object> data = (Dictionary<string, object>)message.data;
            List<object> methodsList = (List<object>)data["methods"];

            Dictionary<string, int> supportMap = new Dictionary<string, int>();
            foreach (object methodObj in methodsList)
            {
                if (methodObj is string methodName)
                {
                    bool isSupported = _registeredNativeRequestMethods.ContainsKey(methodName);
                    supportMap[methodName] = isSupported ? 1 : 0;
                }
            }

            return BridgeResult.SuccessWithData("success", new Dictionary<string, object>
            {
                ["supportMap"] = supportMap
            });
        }

        private BridgeResult ExecuteCallUnityRequest(BridgeMessage message)
        {
            Dictionary<string, object> data = (Dictionary<string, object>)message.data;
            string command = data["command"].ToString();
            Dictionary<string, object> extensionParameters = (Dictionary<string, object>)data["ext"];

            // callUnity 是异步的，需要通过回调返回结果
            bool completionInvoked = false;
            Action<BridgeResult> completion = (BridgeResult result) =>
            {
                if (completionInvoked)
                {
#if UNITY_DEBUG
                    Debug.LogError("[UnityBridge] callUnity 回调 completion 被重复调用，已忽略后续调用");
#endif
                    return;
                }
                completionInvoked = true;
                BridgeResult finalResult = result ?? BridgeResult.MethodNotImplemented(command);
                SendResponseMessage(message.id, BridgeMethodName.CallUnity, finalResult, message.ts);
            };

            ResponseNativeToUnity(command, extensionParameters, completion);

            // 返回 null 表示由回调异步返回结果
            return null;
        }

        /// <summary>
        /// 原生→Unity 的回调事件入口（callUnity）：由业务层在此处处理 command，并通过 completion 返回 BridgeResult。
        /// </summary>
        /// <param name="command">命令名称</param>
        /// <param name="ext">扩展参数</param>
        /// <param name="completion">完成回调，业务层处理完命令后必须调用此回调返回结果</param>
        private void ResponseNativeToUnity(string command, Dictionary<string, object> extensionParameters, Action<BridgeResult> completion)
        {
            switch (command)
            {
                case BridgeCallUnityCommandName.LoginCompletion:
                    ResponseLoginCompletion(extensionParameters, completion);
                    return;

                case BridgeCallUnityCommandName.LogoutCompletion:
                    ResponseLogoutCompletion(extensionParameters, completion);
                    return;

                case BridgeCallUnityCommandName.DeleteAccountCompletion:
                    ResponseDeleteAccountCompletion(extensionParameters, completion);
                    return;

                case BridgeCallUnityCommandName.RefreshTokenCompletion:
                    ResponseRefreshTokenCompletion(extensionParameters, completion);
                    return;

                default:
                    completion?.Invoke(BridgeResult.MethodNotImplemented(command));
                    return;
            }
        }

        /// <summary>
        /// 处理登录完成回调：由业务层重写此方法来实现具体的登录完成逻辑。
        /// </summary>
        /// <param name="ext">扩展参数</param>
        /// <param name="completion">完成回调，业务层处理完后必须调用此回调返回结果</param>
        protected virtual void ResponseLoginCompletion(Dictionary<string, object> ext, Action<BridgeResult> completion)
        {
            LoginManager.Instance.OnLoginCompletion(ext);
            completion?.Invoke(BridgeResult.Success());
        }

        /// <summary>
        /// 处理登出完成回调：由业务层重写此方法来实现具体的登出完成逻辑。
        /// </summary>
        /// <param name="ext">扩展参数</param>
        /// <param name="completion">完成回调，业务层处理完后必须调用此回调返回结果</param>
        protected virtual void ResponseLogoutCompletion(Dictionary<string, object> ext, Action<BridgeResult> completion)
        {
            LoginManager.Instance.OnLogoutCompletion(ext);
            completion?.Invoke(BridgeResult.Success());
        }

        /// <summary>
        /// 处理删除账号完成回调：由业务层重写此方法来实现具体的删除账号完成逻辑。
        /// </summary>
        /// <param name="ext">扩展参数</param>
        /// <param name="completion">完成回调，业务层处理完后必须调用此回调返回结果</param>
        protected virtual void ResponseDeleteAccountCompletion(Dictionary<string, object> ext, Action<BridgeResult> completion)
        {
            completion?.Invoke(BridgeResult.MethodNotImplemented(BridgeCallUnityCommandName.DeleteAccountCompletion));
        }

        /// <summary>
        /// 处理刷新 Token 完成回调：由业务层重写此方法来实现具体的刷新 Token 完成逻辑。
        /// </summary>
        /// <param name="ext">扩展参数</param>
        /// <param name="completion">完成回调，业务层处理完后必须调用此回调返回结果</param>
        protected virtual void ResponseRefreshTokenCompletion(Dictionary<string, object> ext, Action<BridgeResult> completion)
        {
            completion?.Invoke(BridgeResult.MethodNotImplemented(BridgeCallUnityCommandName.RefreshTokenCompletion));
        }

        /// <summary>
        /// 处理Native -> Unity 的Response消息
        /// </summary>
        private void ResolveNativeResponseMessage(BridgeMessage message)
        {
            if (string.IsNullOrEmpty(message.id))
            {
                return;
            }

            if (_pendingRequests.TryRemove(message.id, out Action<BridgeResult> callback))
            {
                BridgeResult result = new BridgeResult();
                Dictionary<string, object> dataDict = (Dictionary<string, object>)message.data;
                result.status = Convert.ToInt32(dataDict["status"]);
                result.message = dataDict["message"]?.ToString();
                result.data = (Dictionary<string, object>)dataDict["data"];

                callback?.Invoke(result);
            }
        }

        #endregion

        #region Unity → Native 请求消息

        /// <summary>
        /// 检查方法是否支持。
        /// </summary>
        public void IsSupportMethod(string[] methods, Action<Dictionary<string, int>> callback)
        {
            List<object> methodsList = new List<object>(methods);
            IsSupportMethodRequestParametersDictionary.Clear();
            IsSupportMethodRequestParametersDictionary["methods"] = methodsList;

            SendRequestMessage(BridgeMethodName.IsSupportMethod, IsSupportMethodRequestParametersDictionary, result =>
            {
                Dictionary<string, int> supportMap = new Dictionary<string, int>();
                if (result.status == StatusCode.Success)
                {
                    Dictionary<string, object> map = (Dictionary<string, object>)result.data["supportMap"];
                    foreach (KeyValuePair<string, object> keyValuePair in map)
                    {
                        supportMap[keyValuePair.Key] = Convert.ToInt32(keyValuePair.Value);
                    }
                }

                callback?.Invoke(supportMap);
            });
        }

        /// <summary>
        /// 上报事件。
        /// </summary>
        public void Report(string action, Dictionary<string, string> param, Action<bool> callback)
        {
            ReportRequestParametersDictionary.Clear();
            ReportRequestParametersDictionary["action"] = action;

            if (param != null && param.Count > 0)
            {
                Dictionary<string, object> paramDict = new Dictionary<string, object>();
                foreach (KeyValuePair<string, string> keyValuePair in param)
                {
                    paramDict[keyValuePair.Key] = keyValuePair.Value;
                }
                ReportRequestParametersDictionary["param"] = paramDict;
            }

            SendRequestMessage(BridgeMethodName.Report, ReportRequestParametersDictionary, result =>
            {
                callback?.Invoke(result.status == StatusCode.Success);
            });
        }

        /// <summary>
        /// 获取设备信息（旧版网络框架）。
        /// </summary>
        public void GetDeviceInfo(Action<Dictionary<string, object>> callback)
        {
            SendRequestMessage(BridgeMethodName.GetDeviceInfo, null, result =>
            {
                Dictionary<string, object> data = result.status == StatusCode.Success && result.data != null
                    ? result.data
                    : new Dictionary<string, object>();
                callback?.Invoke(data);
            });
        }

        /// <summary>
        /// 获取公共参数信息（新版网络框架）。
        /// </summary>
        public void GetPublicParam(Action<Dictionary<string, object>> callback)
        {
            SendRequestMessage(BridgeMethodName.GetPublicParam, null, result =>
            {
                Dictionary<string, object> data = result.status == StatusCode.Success && result.data != null
                    ? result.data
                    : new Dictionary<string, object>();
                callback?.Invoke(data);
            });
        }

        /// <summary>
        /// 输出日志。
        /// </summary>
        public void Log(string level, string message, string tag, Action<bool> callback)
        {
            LogRequestParametersDictionary["level"] = level;
            LogRequestParametersDictionary["log"] = message;

            if (!string.IsNullOrEmpty(tag))
            {
                LogRequestParametersDictionary["tag"] = tag;
            }

            SendRequestMessage(BridgeMethodName.Log, LogRequestParametersDictionary, result =>
            {
                callback?.Invoke(result.status == StatusCode.Success);
            });
        }

        /// <summary>
        /// 获取配置信息。
        /// </summary>
        public void GetConfigInfo(Action<Dictionary<string, object>> callback)
        {
            SendRequestMessage(BridgeMethodName.GetConfigInfo, null, result =>
            {
                Dictionary<string, object> data = result.status == StatusCode.Success && result.data != null
                    ? result.data
                    : new Dictionary<string, object>();
                callback?.Invoke(data);
            });
        }

        /// <summary>
        /// 登录。
        /// </summary>
        public void Login(string type, Action<Dictionary<string, object>> callback = null)
        {
            LoginExtensionParametersDictionary["type"] = type;

            LoginRequestParametersDictionary["command"] = BridgeCommandName.Login;
            LoginRequestParametersDictionary["ext"] = LoginExtensionParametersDictionary;

            SendRequestMessage(BridgeMethodName.CallNative, LoginRequestParametersDictionary, result =>
            {
                Dictionary<string, object> data = result.status == StatusCode.Success && result.data != null
                    ? result.data
                    : new Dictionary<string, object>();
                callback?.Invoke(data);
            });
        }

        /// <summary>
        /// 登出。
        /// </summary>
        public void Logout(Action<Dictionary<string, object>> callback = null)
        {
            LogoutRequestParametersDictionary["command"] = BridgeCommandName.Logout;
            LogoutRequestParametersDictionary["ext"] = EmptyExtensionParametersDictionary;

            SendRequestMessage(BridgeMethodName.CallNative, LogoutRequestParametersDictionary, result =>
            {
                Dictionary<string, object> data = result.status == StatusCode.Success && result.data != null
                    ? result.data
                    : new Dictionary<string, object>();
                callback?.Invoke(data);
            });
        }

        /// <summary>
        /// 删除账号。
        /// </summary>
        public void DeleteAccount(Action<Dictionary<string, object>> callback = null)
        {
            DeleteAccountRequestParametersDictionary["command"] = BridgeCommandName.DeleteAccount;
            DeleteAccountRequestParametersDictionary["ext"] = EmptyExtensionParametersDictionary;

            SendRequestMessage(BridgeMethodName.CallNative, DeleteAccountRequestParametersDictionary, result =>
            {
                Dictionary<string, object> data = result.status == StatusCode.Success && result.data != null
                    ? result.data
                    : new Dictionary<string, object>();
                callback?.Invoke(data);
            });
        }

        /// <summary>
        /// 刷新 Token。
        /// </summary>
        public void RefreshToken(string accessToken, string refreshToken, Action<Dictionary<string, object>> callback = null)
        {
            RefreshTokenExtensionParametersDictionary["access_token"] = accessToken;
            RefreshTokenExtensionParametersDictionary["refresh_token"] = refreshToken;

            RefreshTokenRequestParametersDictionary["command"] = BridgeCommandName.RefreshToken;
            RefreshTokenRequestParametersDictionary["ext"] = RefreshTokenExtensionParametersDictionary;

            SendRequestMessage(BridgeMethodName.CallNative, RefreshTokenRequestParametersDictionary, result =>
            {
                Dictionary<string, object> data = result.status == StatusCode.Success && result.data != null
                    ? result.data
                    : new Dictionary<string, object>();
                callback?.Invoke(data);
            });
        }

        #endregion
    }
}


