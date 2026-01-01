using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Astorise.Framework.SDK;
using Astorise.Framework.Network;
using Astorise.Framework.UI;

namespace Astorise.Framework.Core
{
    /// <summary>
    /// Framework 层主入口，负责初始化 Framework 层各个模块并启动登录流程。
    /// </summary>
    public static class Main
    {
        /// <summary>
        /// 启动 Framework 层，初始化各个模块并执行登录流程。
        /// </summary>
        /// <returns>UniTask</returns>
        public static async UniTask Start()
        {
#if UNITY_DEBUG && GAME_BOOTSTRAP
            Debug.Log("[Framework.Main] 开始启动 Framework 层");
#endif

            // 1. 初始化 UnityBridge
            InitializeUnityBridge();

            // 2. 初始化 UIManager
            InitializeUIManager();

            // 3. 初始化 NetworkManager（需要配置参数，暂时使用默认值）
            InitializeNetworkManager();

            // 4. 执行登录流程
            await ExecuteLogin();
        }

        /// <summary>
        /// 初始化 UnityBridge。
        /// </summary>
        private static void InitializeUnityBridge()
        {
#if UNITY_DEBUG && GAME_BOOTSTRAP
            Debug.Log("[Framework.Main] 初始化 UnityBridge");
#endif

            // 确保 UnityBridge 实例存在
            if (UnityBridge.Instance == null)
            {
                GameObject bridgeGameObject = new GameObject("UnityBridge");
                bridgeGameObject.AddComponent<UnityBridge>();
            }

            // 检测平台并初始化 UnityBridge
            UnityBridgePlatform platform = DetectPlatform();
            UnityBridge.Instance.Initialize(platform);

#if UNITY_DEBUG && GAME_BOOTSTRAP
            Debug.Log($"[Framework.Main] UnityBridge 初始化完成，平台：{platform}");
#endif
        }

        /// <summary>
        /// 初始化 UIManager。
        /// </summary>
        private static void InitializeUIManager()
        {
#if UNITY_DEBUG && GAME_BOOTSTRAP
            Debug.Log("[Framework.Main] 初始化 UIManager");
#endif

            UIManager.Initialize();

#if UNITY_DEBUG && GAME_BOOTSTRAP
            Debug.Log("[Framework.Main] UIManager 初始化完成");
#endif
        }

        /// <summary>
        /// 初始化 NetworkManager。
        /// 注意：这里使用默认配置，实际项目中应该从配置文件中读取。
        /// </summary>
        private static void InitializeNetworkManager()
        {
#if UNITY_DEBUG && GAME_BOOTSTRAP
            Debug.Log("[Framework.Main] 初始化 NetworkManager");
#endif

            // TODO: 从配置文件读取网络配置
            // 当前使用默认值，实际项目中应该从配置文件中读取
            string persistentConnectionRawUrl = "ws://127.0.0.1:8080";
            string inconstantConnectionUrl = "http://127.0.0.1:8080";
            string publicKeyID = "default";
            string publicKey = "default";

            NetworkManager networkManager = new NetworkManager();
            networkManager.Init(persistentConnectionRawUrl, inconstantConnectionUrl, publicKeyID, publicKey);

#if UNITY_DEBUG && GAME_BOOTSTRAP
            Debug.Log("[Framework.Main] NetworkManager 初始化完成");
#endif
        }

        /// <summary>
        /// 执行登录流程。
        /// </summary>
        /// <returns>UniTask</returns>
        private static async UniTask ExecuteLogin()
        {
#if UNITY_DEBUG && GAME_BOOTSTRAP
            Debug.Log("[Framework.Main] 开始执行登录流程");
#endif

            // 执行登录（使用缓存结果优先）
            UniTaskCompletionSource<LoginResult> loginCompletionSource = new UniTaskCompletionSource<LoginResult>();

            LoginManager.Instance.Login("did", "DefaultLogin", tryUseCachedResult: true, (loginResult, exception) =>
            {
                if (exception != null)
                {
#if UNITY_DEBUG
                    Debug.LogError($"[Framework.Main] 登录失败：{exception.Message}");
#endif
                    loginCompletionSource.TrySetException(exception);
                }
                else if (loginResult != null)
                {
#if UNITY_DEBUG && GAME_BOOTSTRAP
                    Debug.Log($"[Framework.Main] 登录成功，uid={loginResult.Uid}, type={loginResult.Type}");
#endif
                    loginCompletionSource.TrySetResult(loginResult);
                }
                else
                {
                    Exception error = new InvalidOperationException("登录结果为空");
#if UNITY_DEBUG
                    Debug.LogError($"[Framework.Main] 登录失败：{error.Message}");
#endif
                    loginCompletionSource.TrySetException(error);
                }
            });

            await loginCompletionSource.Task;
        }

        /// <summary>
        /// 检测当前运行平台。
        /// </summary>
        /// <returns>UnityBridge 平台类型</returns>
        private static UnityBridgePlatform DetectPlatform()
        {
#if UNITY_EDITOR
            return UnityBridgePlatform.Editor;
#elif UNITY_ANDROID
            return UnityBridgePlatform.Android;
#elif UNITY_IOS
            return UnityBridgePlatform.IOS;
#else
            return UnityBridgePlatform.Editor;
#endif
        }
    }
}

