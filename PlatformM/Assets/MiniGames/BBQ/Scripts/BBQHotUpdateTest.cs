using UnityEngine;
using Astorise.Framework.Core;
using Astorise.Framework.AOT;
using Cysharp.Threading.Tasks;
using YooAsset;

namespace Astorise.MiniGames.BBQ
{
    /// <summary>
    /// BBQ 热更新测试类。
    /// 用于验证 HybridCLR 热更新功能。
    /// </summary>
    public static class BBQHotUpdateTest
    {
        private const string GameNameValue = "BBQ";

        private const string PackageName = "BBQAsset";

        private const string PanelHotUpdateLocation = "Assets/MiniGames/BBQ/UI/PanelHotUpdate.prefab";

        private const uint PanelLoadPriority = 0;

        private static GameObject _panelInstance;

        /// <summary>
        /// 运行热更新测试，打印测试日志。
        /// </summary>
        public static void Run()
        {
            Debug.Log("[BBQHotUpdateTest] BBQ HotUpdate Test: Hello from HotUpdate!");
            RunPanelHotUpdateTestAsync().Forget();
        }

        private static async UniTask RunPanelHotUpdateTestAsync()
        {
            if (_panelInstance != null)
            {
                ResourceManager.ReleaseInstance(_panelInstance);
                _panelInstance = null;
            }

            var handle = await LoadPanelHandleAsync(PackageName);
            if (handle == null || handle.Status != EOperationStatus.Succeed)
            {
#if UNITY_DEBUG
                Debug.LogError("[BBQHotUpdateTest] PanelHotUpdate load failed: handle invalid.");
#endif
                return;
            }

            var instance = await InstantiatePanelAsync();
            if (instance == null)
            {
#if UNITY_DEBUG
                Debug.LogError("[BBQHotUpdateTest] PanelHotUpdate instantiate failed.");
#endif
                return;
            }

            // 将 UI 面板添加到 Canvas
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                // 如果场景中没有 Canvas，创建一个
                GameObject canvasObj = new GameObject("Canvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
                canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
#if UNITY_DEBUG
                Debug.Log("[BBQHotUpdateTest] Created Canvas for PanelHotUpdate");
#endif
            }

            // 将面板设置为 Canvas 的子对象
            instance.transform.SetParent(canvas.transform, false);

            _panelInstance = instance;
            _panelInstance.SetActive(true);

            // 3 秒后删除资源
            await UniTask.Delay(3000); // 3000 毫秒 = 3 秒
            
            if (_panelInstance != null)
            {
                ResourceManager.ReleaseInstance(_panelInstance);
                _panelInstance = null;
#if UNITY_DEBUG
                Debug.Log("[BBQHotUpdateTest] PanelHotUpdate instance released after 3 seconds.");
#endif
            }
        }

        private static async UniTask<AssetHandle> LoadPanelHandleAsync(string packageName)
        {
            return await ResourceManager.LoadAssetAsync<GameObject>(packageName, PanelHotUpdateLocation, PanelLoadPriority);
        }

        private static UniTask<GameObject> InstantiatePanelAsync()
        {
            return ResourceManager.InstantiateGameObjectAsync(PanelHotUpdateLocation);
        }
    }
}
