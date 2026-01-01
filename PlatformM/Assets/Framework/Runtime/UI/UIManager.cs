using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Astorise.Framework.Core;
using UnityEngine;
using UnityEngine.UI;
using YooAsset;

namespace Astorise.Framework.UI
{
    /// <summary>
    /// UI 管理器，负责 UI 视图的加载、实例化和分发。
    /// </summary>
    public static class UIManager
    {
        private const string DefaultPackageName = "BBQAsset";
        private const int ViewConfigCapacity = 64;
        private const int LayerCapacity = 4;
        private const int OpenViewCapacity = 32;

        private static readonly Dictionary<int, UIViewConfig> ViewConfigs = new Dictionary<int, UIViewConfig>(ViewConfigCapacity);
        private static readonly Dictionary<UILayer, Canvas> LayerCanvases = new Dictionary<UILayer, Canvas>(LayerCapacity);
        private static readonly Dictionary<int, UIView> OpenViews = new Dictionary<int, UIView>(OpenViewCapacity);

        private static GameObject _canvasRoot;

        /// <summary>
        /// 初始化 UIManager 并创建层级 Canvas。
        /// </summary>
        public static void Initialize()
        {
            if (_canvasRoot != null)
            {
#if UNITY_DEBUG && UI_MANAGER
                Debug.Log("[UIManager] Already initialized, skipping");
#endif
                return;
            }

            _canvasRoot = new GameObject("UIManagerRoot");
            UnityEngine.Object.DontDestroyOnLoad(_canvasRoot);

            CreateLayerCanvases();

#if UNITY_DEBUG && UI_MANAGER
            Debug.Log("[UIManager] Initialize completed");
#endif
        }

        /// <summary>
        /// 设置视图配置表。
        /// </summary>
        /// <param name="configs">视图配置列表</param>
        public static void SetViewConfigs(List<UIViewConfig> configs)
        {
            if (configs == null)
            {
#if UNITY_DEBUG
                Debug.LogError("[UIManager] SetViewConfigs failed: configs is null");
#endif
                return;
            }

            ViewConfigs.Clear();
            foreach (UIViewConfig config in configs)
            {
                if (config == null)
                {
                    continue;
                }

                if (ViewConfigs.ContainsKey(config.ID))
                {
#if UNITY_DEBUG
                    Debug.LogError($"[UIManager] SetViewConfigs warning: duplicate config ID {config.ID}, overwriting");
#endif
                }

                ViewConfigs[config.ID] = config;
            }

#if UNITY_DEBUG && UI_MANAGER
            Debug.Log($"[UIManager] View configs set, total {ViewConfigs.Count} configs");
#endif
        }

        /// <summary>
        /// 根据配置 ID 打开视图。
        /// </summary>
        /// <param name="configID">视图配置 ID</param>
        /// <returns>打开的 UIView 实例，失败时返回 null</returns>
        public static async UniTask<UIView> OpenView(int configID)
        {
            if (!ViewConfigs.TryGetValue(configID, out UIViewConfig config))
            {
#if UNITY_DEBUG
                Debug.LogError($"[UIManager] OpenView failed: config ID {configID} not found");
#endif
                return null;
            }

            if (config.ViewType == null)
            {
#if UNITY_DEBUG
                Debug.LogError($"[UIManager] OpenView failed: ViewType is null for config {configID}");
#endif
                return null;
            }

            if (!typeof(UIView).IsAssignableFrom(config.ViewType))
            {
#if UNITY_DEBUG
                Debug.LogError($"[UIManager] OpenView failed: ViewType {config.ViewType.Name} is not a subclass of UIView, configID={configID}");
#endif
                return null;
            }

            if (OpenViews.TryGetValue(configID, out UIView existingView))
            {
#if UNITY_DEBUG && UI_MANAGER
                Debug.Log($"[UIManager] OpenView: view {configID} already open, returning existing instance");
#endif
                return existingView;
            }

            string location = config.Location;
            if (string.IsNullOrEmpty(location))
            {
#if UNITY_DEBUG
                Debug.LogError($"[UIManager] OpenView failed: Location is empty for config {configID}");
#endif
                return null;
            }

            string packageName = config.PackageName;
            if (string.IsNullOrEmpty(packageName))
            {
#if UNITY_DEBUG
                Debug.LogError($"[UIManager] OpenView failed: PackageName is empty for config {configID}");
#endif
                return null;
            }

            GameObject viewRoot = await LoadAndInstantiateView(packageName, location, config.Layer, config.ZOrder);

            if (viewRoot == null)
            {
#if UNITY_DEBUG
                Debug.LogError($"[UIManager] OpenView failed: cannot load or instantiate view, configID={configID}");
#endif
                return null;
            }

            UIView viewInstance = (UIView)Activator.CreateInstance(config.ViewType);
            viewInstance.Initialize(viewRoot);
            viewInstance.BindButtons();

            OpenViews[configID] = viewInstance;

#if UNITY_DEBUG && UI_MANAGER
            Debug.Log($"[UIManager] OpenView success: configID={configID}, layer={config.Layer}, zorder={config.ZOrder}, type={config.ViewType.Name}");
#endif

            return viewInstance;
        }

        /// <summary>
        /// 根据资源 ID 列表预加载视图资源。
        /// </summary>
        /// <param name="resourceIDs">需要预加载的资源 ID 列表</param>
        public static async UniTask PreloadViews(List<int> resourceIDs)
        {
            if (resourceIDs == null || resourceIDs.Count == 0)
            {
#if UNITY_DEBUG && UI_MANAGER
                Debug.Log("[UIManager] PreloadViews: resource ID list is empty, skip");
#endif
                return;
            }

            Dictionary<string, List<(string location, ResourceInfo resourceInfo)>> resourcesByPackage =
                new Dictionary<string, List<(string location, ResourceInfo resourceInfo)>>();

            foreach (int resourceID in resourceIDs)
            {
                ResourceInfo resourceInfo = ResourceManager.GetResourceInfo(resourceID);

                if (resourceInfo == null)
                {
#if UNITY_DEBUG
                    Debug.LogError($"[UIManager] PreloadViews failed: resource ID {resourceID} not found in map");
#endif
                    continue;
                }

                string location = resourceInfo.Location;
                string packageName = resourceInfo.PackageName;
                if (string.IsNullOrEmpty(packageName))
                {
                    packageName = DefaultPackageName;
#if UNITY_DEBUG && UI_MANAGER
                    Debug.LogWarning($"[UIManager] PreloadViews: resource ID {resourceID} PackageName is empty, using default '{packageName}'");
#endif
                }

                if (!resourcesByPackage.TryGetValue(packageName, out List<(string location, ResourceInfo resourceInfo)> resources))
                {
                    resources = new List<(string location, ResourceInfo resourceInfo)>();
                    resourcesByPackage[packageName] = resources;
                }

                resources.Add((location, resourceInfo));
            }

            if (resourcesByPackage.Count == 0)
            {
#if UNITY_DEBUG && UI_MANAGER
                Debug.Log("[UIManager] PreloadViews: no valid resources to preload");
#endif
                return;
            }

            List<UniTask<AssetHandle>> loadTasks = new List<UniTask<AssetHandle>>();

            foreach (KeyValuePair<string, List<(string location, ResourceInfo resourceInfo)>> kvp in resourcesByPackage)
            {
                string packageName = kvp.Key;
                ResourcePackage package = YooAssets.TryGetPackage(packageName);
                if (package == null)
                {
#if UNITY_DEBUG
                    Debug.LogError($"[UIManager] PreloadViews failed: package '{packageName}' does not exist");
#endif
                    continue;
                }

                foreach ((string location, ResourceInfo resourceInfo) in kvp.Value)
                {
                    UniTask<AssetHandle> loadTask = LoadAssetHandleAsync(packageName, location);
                    loadTasks.Add(loadTask);

                    if (resourceInfo.ReleasePolicy == AssetLifePolicy.Permanent)
                    {
                        ResourceManager.RegisterPermanentAsset(location);
                    }
                    else
                    {
                        ResourceManager.RegisterDelayedReleaseAsset(location, resourceInfo.DelayedReleaseTime);
                    }
                }
            }

            await UniTask.WhenAll(loadTasks);

#if UNITY_DEBUG && UI_MANAGER
            Debug.Log($"[UIManager] PreloadViews completed: preloaded {loadTasks.Count} assets");
#endif
        }

        /// <summary>
        /// 向所有已打开的视图分发消息。
        /// </summary>
        /// <param name="messageID">消息 ID</param>
        /// <param name="data">消息数据</param>
        public static void OnMessage(int messageID, object data)
        {
            foreach (UIView view in OpenViews.Values)
            {
                if (view != null)
                {
                    view.OnMessage(messageID, data);
                }
            }
        }

        private static void CreateLayerCanvases()
        {
            UILayer[] layers = new[] { UILayer.Background, UILayer.GamePlay, UILayer.UI, UILayer.Top };

            foreach (UILayer layer in layers)
            {
                GameObject canvasObj = new GameObject($"Canvas_{layer}");
                canvasObj.transform.SetParent(_canvasRoot.transform, false);

                Canvas canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = (int)layer;

                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();

                LayerCanvases[layer] = canvas;

#if UNITY_DEBUG && UI_MANAGER
                Debug.Log($"[UIManager] Created Canvas layer: {layer}, sortingOrder={(int)layer}");
#endif
            }
        }

        private static async UniTask<GameObject> LoadAndInstantiateView(string packageName, string location, UILayer layer, int zorder)
        {
            if (string.IsNullOrEmpty(packageName) || string.IsNullOrEmpty(location))
            {
#if UNITY_DEBUG
                Debug.LogError("[UIManager] LoadAndInstantiateView failed: packageName or location is invalid");
#endif
                return null;
            }

            AssetHandle handle = await LoadAssetHandleAsync(packageName, location);
            if (handle == null || handle.Status != EOperationStatus.Succeed)
            {
                return null;
            }

            GameObject prefab = handle.AssetObject as GameObject;
            if (prefab == null)
            {
#if UNITY_DEBUG
                Debug.LogError($"[UIManager] LoadAndInstantiateView failed: asset is not GameObject, location={location}");
#endif
                return null;
            }

            if (!LayerCanvases.TryGetValue(layer, out Canvas canvas))
            {
#if UNITY_DEBUG
                Debug.LogError($"[UIManager] LoadAndInstantiateView failed: Canvas layer not found, layer={layer}");
#endif
                return null;
            }

            GameObject instance = UnityEngine.Object.Instantiate(prefab, canvas.transform);

            int siblingIndex = GetSiblingIndexForZOrder(canvas.transform, zorder);
            instance.transform.SetSiblingIndex(siblingIndex);

            return instance;
        }

        private static async UniTask<AssetHandle> LoadAssetHandleAsync(string packageName, string location)
        {
            return await ResourceManager.LoadAssetAsync<GameObject>(packageName, location, 0);
        }

        private static int GetSiblingIndexForZOrder(Transform parent, int zorder)
        {
            int childCount = parent.childCount;
            if (childCount == 0)
            {
                return 0;
            }

            for (int i = 0; i < childCount; i++)
            {
                Transform child = parent.GetChild(i);

                int childZOrder = 0;
                foreach (KeyValuePair<int, UIView> kvp in OpenViews)
                {
                    UIView view = kvp.Value;
                    if (view != null && view.RootGameObject == child.gameObject)
                    {
                        if (ViewConfigs.TryGetValue(kvp.Key, out UIViewConfig childConfig))
                        {
                            childZOrder = childConfig.ZOrder;
                        }
                        break;
                    }
                }

                if (childZOrder > zorder)
                {
                    return i;
                }
            }

            return childCount;
        }
    }
}
