using System;
using System.Collections.Generic;
using System.Threading;
using YooAsset;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Astorise.Framework.AOT;

namespace Astorise.Framework.Core
{
    /// <summary>
    /// 资源管理器，负责 Asset 加载、释放、实例化和资源生命周期管理。
    /// 约束：
    /// - Framework 层：对外不暴露 async/await、IEnumerator、UniTask
    /// - Asset 默认延时释放（120秒），常驻资源需显式注册
    /// - Instantiate 必须走统一接口：InstantiateGameObject + ReleaseInstance
    /// </summary>
    public static class ResourceManager
    {
        /// <summary>
        /// 静态构造函数，用于订阅 Update 事件。
        /// </summary>
        static ResourceManager()
        {
            GameEvents.OnUpdate += TickDelayedReleases;
        }

        #region 字段定义

        /// <summary>
        /// 默认延时释放时间（秒），2分钟
        /// </summary>
        private const float DefaultDelayedReleaseSeconds = 6;

        /// <summary>
        /// 全局 Asset 管理：location -> AssetRecord
        /// </summary>
        private static readonly Dictionary<string, AssetRecord> GlobalAssetRecords = new Dictionary<string, AssetRecord>(256);

        /// <summary>
        /// 全局 Asset 列表：存储所有已加载的 Asset location
        /// </summary>
        private static readonly List<string> GlobalAssetList = new List<string>(256);

        /// <summary>
        /// 每次 TickDelayedReleases 处理的资源数量
        /// </summary>
        private const int DelayedReleaseBatchSize = 50;

        /// <summary>
        /// TickDelayedReleases 执行间隔（秒）
        /// </summary>
        private const float TickDelayedReleaseInterval = 1;

        /// <summary>
        /// 当前处理的索引位置
        /// </summary>
        private static int _delayedReleaseIndex = 0;

        /// <summary>
        /// 上次执行 TickDelayedReleases 的时间（秒）
        /// </summary>
        private static float _lastTickDelayedReleaseTime = 0f;

        /// <summary>
        /// 实例 ID 到 location 的映射
        /// </summary>
        private static readonly Dictionary<int, string> InstanceLocations = new Dictionary<int, string>(256);

        /// <summary>
        /// 常驻 Asset 注册表：location -> true
        /// </summary>
        private static readonly Dictionary<string, bool> PermanentAssets = new Dictionary<string, bool>(64);

        /// <summary>
        /// 自定义延时释放注册表：location -> delaySeconds
        /// </summary>
        private static readonly Dictionary<string, float> CustomDelayAssets = new Dictionary<string, float>(64);

        /// <summary>
        /// 资源映射表：id (int) -> ResourceInfo
        /// </summary>
        private static readonly Dictionary<int, ResourceInfo> ResourceMap = new Dictionary<int, ResourceInfo>(256);

        #endregion

        #region 内部类定义

        private sealed class AssetRecord
        {
            /// <summary>
            /// 资源位置标识
            /// </summary>
            public readonly string Location;

            /// <summary>
            /// 记录是从哪个包加载的
            /// </summary>
            public string PackageName;

            /// <summary>
            /// 缓存的预制体句柄
            /// </summary>
            public AssetHandle CachedPrefabHandle;

            /// <summary>
            /// 是否正在加载中
            /// </summary>
            public bool IsLoading;

            /// <summary>
            /// 实例引用计数
            /// </summary>
            public int InstanceRefCount;

            /// <summary>
            /// 释放时间（秒），0 表示未开启倒计时
            /// </summary>
            public float ReleaseAtTime;

            /// <summary>
            /// 在 AssetRecord 类中增加一个内部使用的 TaskSource
            /// </summary>
            internal UniTaskCompletionSource<AssetHandle> LoadingSource;

            /// <summary>
            /// 创建资源记录。
            /// </summary>
            public AssetRecord(string location)
            {
                Location = location;
            }
        }

        #endregion

        #region 公共 API - 异步加载资源

        /// <summary>
        /// 异步加载 Asset 资源（通过 packageName 获取包并内部解析 ResourcePackage，UniTask 版本）。
        /// </summary>
        /// <param name="packageName">包名称</param>
        /// <param name="location">资源位置标识</param>
        /// <param name="priority">加载优先级，数值越大优先级越高</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>AssetHandle，失败时返回 null</returns>
        public static async UniTask<AssetHandle> LoadAssetAsync<T>(
            string packageName,
            string location,
            uint priority,
            CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            if (!TryGetPackageByName(packageName, out ResourcePackage package))
            {
#if UNITY_DEBUG
                Debug.LogError($"[ResourceManager] LoadAssetAsync 失败: package 不存在, package='{packageName}', location='{location}'");
#endif
                return null;
            }

            return await LoadAssetAsync<T>(package, location, priority, cancellationToken);
        }

        #endregion

        #region 公共 API - Asset 策略注册（常驻 / 自定义延时）

        /// <summary>
        /// 注册常驻资源（永不自动释放）。
        /// </summary>
        public static void RegisterPermanentAsset(string location)
        {
            if (string.IsNullOrEmpty(location))
                return;
            PermanentAssets[location] = true;
            CustomDelayAssets.Remove(location);
        }

        /// <summary>
        /// 注册自定义延时释放资源（覆盖默认的 120 秒）。
        /// </summary>
        /// <param name="delaySeconds">延时释放时间（秒），如果小于等于 0 则使用默认值 120 秒</param>
        public static void RegisterDelayedReleaseAsset(string location, float delaySeconds)
        {
            if (string.IsNullOrEmpty(location))
                return;
            PermanentAssets.Remove(location);
            CustomDelayAssets[location] = delaySeconds > 0f ? delaySeconds : DefaultDelayedReleaseSeconds;
        }

        #endregion

        #region 公共 API - Instantiate / RefCount（统一实例化与回收）

        /// <summary>
        /// 通用实例化接口：根据 location 实例化预制体。
        /// 前提：该 location 必须已通过 LoadAssetAsync 加载过。
        /// </summary>
        /// <param name="location">资源位置标识</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>实例化的游戏对象，如果未加载或加载失败则返回 null</returns>
        public static async UniTask<GameObject> InstantiateGameObjectAsync(string location, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(location))
                return null;

            if (!GlobalAssetRecords.TryGetValue(location, out AssetRecord record))
            {
                // 未加载过，返回 null
#if UNITY_DEBUG
                Debug.LogError($"[ResourceManager] InstantiateGameObjectAsync failed: location '{location}' not loaded. Call LoadAssetAsync first.");
#endif
                return null;
            }

            // 如果正在加载中，等待加载完成
            if (record.IsLoading)
            {
                AssetHandle handle = await record.LoadingSource.Task.AttachExternalCancellation(cancellationToken);
                if (handle == null || !handle.IsValid)
                {
#if UNITY_DEBUG
                    Debug.LogError($"[ResourceManager] InstantiateGameObjectAsync failed: loading failed for location '{location}'");
#endif
                    return null;
                }
            }

            // 检查 handle 是否有效
            if (record.CachedPrefabHandle == null || !record.CachedPrefabHandle.IsValid)
            {
#if UNITY_DEBUG
                Debug.LogError($"[ResourceManager] InstantiateGameObjectAsync failed: handle is invalid for location '{location}'");
#endif
                return null;
            }

            // 实例化
            return InstantiateFromRecord(record);
        }

        /// <summary>
        /// 释放一个通过 InstantiateGameObjectAsync 创建的实例。
        /// </summary>
        public static void ReleaseInstance(GameObject instance)
        {
            if (instance == null)
                return;

            int id = instance.GetInstanceID();
            if (!InstanceLocations.TryGetValue(id, out string location))
            {
                UnityEngine.Object.Destroy(instance);
                return;
            }

            InstanceLocations.Remove(id);
            UnityEngine.Object.Destroy(instance);

            if (!GlobalAssetRecords.TryGetValue(location, out AssetRecord record))
                return;

            if (record.InstanceRefCount > 0)
                record.InstanceRefCount--;
        }

        #endregion

        #region 公共 API - 释放加载的 bundle

        /// <summary>
        /// 释放资源句柄。
        /// </summary>
        public static void Release(AssetHandle handle)
        {
            if (handle == null)
                return;
            handle.Release();
        }

        /// <summary>
        /// 手动触发延时释放检查。
        /// 需要在外部 MonoBehaviour 的 Update 中定期调用。
        /// 内部会控制执行间隔，每30秒真正执行一次。
        /// </summary>
        public static void TickDelayedReleases()
        {
            float now = Time.realtimeSinceStartup;
            
            // 检查是否已经过了30秒
            if (now - _lastTickDelayedReleaseTime < TickDelayedReleaseInterval)
            {
                return;
            }
            
            // 更新上次执行时间
            _lastTickDelayedReleaseTime = now;
            
            if (GlobalAssetList.Count == 0)
                return;

            int processedCount = 0;
            int startIndex = _delayedReleaseIndex;

            while (processedCount < DelayedReleaseBatchSize && processedCount < GlobalAssetList.Count)
            {
                // 防止索引越界（使用当前列表大小，因为列表可能在循环中被修改）
                if (_delayedReleaseIndex >= GlobalAssetList.Count)
                    _delayedReleaseIndex = 0;

                // 防止无限循环
                if (_delayedReleaseIndex == startIndex && processedCount > 0)
                    break;

                string location = GlobalAssetList[_delayedReleaseIndex];
                processedCount++;
                _delayedReleaseIndex++;

                // 获取记录
                if (!GlobalAssetRecords.TryGetValue(location, out AssetRecord record))
                {
                    // 数据不同步：记录不存在，报错并清理
#if UNITY_DEBUG
                    Debug.LogError($"[ResourceManager] 数据不同步: location '{location}' 在 GlobalAssetList 中但不在 GlobalAssetRecords 中，已从列表中移除");
#endif
                    GlobalAssetList.RemoveAt(_delayedReleaseIndex);
                    // 注意：索引调整在下次循环开始时会自动处理
                    continue;
                }

                // 检查是否为常驻资源
                AssetLifePolicy policy = GetPolicyForLocation(location);
                if (policy == AssetLifePolicy.Permanent)
                {
                    // 常驻资源，清除倒计时
                    record.ReleaseAtTime = 0f;
                    // 注意：索引调整在下次循环开始时会自动处理
                    continue;
                }

                // 检测引用计数
                if (record.InstanceRefCount == 0)
                {
                    // 引用计数为 0，开启倒计时
                    if (record.ReleaseAtTime == 0f)
                    {
                        float delaySeconds = GetDelaySecondsForLocation(location);
                        record.ReleaseAtTime = now + delaySeconds;
                    }

                    // 检查是否到达释放时间
                    if (now >= record.ReleaseAtTime)
                    {
                        ReleaseAsset(location, _delayedReleaseIndex);
                    }
                }
                else
                {
                    // 引用计数 > 0，清除倒计时
                    record.ReleaseAtTime = 0f;
                }
            }
        }

        #endregion

        #region 公共 API - 资源映射表管理

        /// <summary>
        /// 设置资源映射表，将资源 ID 映射到资源信息。
        /// </summary>
        /// <param name="resourceMap">资源映射表，key 为资源 ID (int)，value 为资源信息</param>
        public static void SetResourceMap(Dictionary<int, ResourceInfo> resourceMap)
        {
            if (resourceMap == null)
            {
#if UNITY_DEBUG
                Debug.LogError("[ResourceManager] SetResourceMap 失败: resourceMap 为空");
#endif
                return;
            }

            ResourceMap.Clear();
            foreach (KeyValuePair<int, ResourceInfo> kvp in resourceMap)
            {
                if (kvp.Value == null)
                {
#if UNITY_DEBUG
                    Debug.LogError($"[ResourceManager] SetResourceMap 跳过无效条目: key={kvp.Key}, value=null");
#endif
                    continue;
                }
                ResourceMap[kvp.Key] = kvp.Value;
            }

#if UNITY_DEBUG && YOO_ASSET
            Debug.Log($"[ResourceManager] 资源映射表已设置，共 {ResourceMap.Count} 个条目");
#endif
        }

        /// <summary>
        /// 根据资源 ID 获取资源信息。
        /// </summary>
        /// <param name="resourceID">资源 ID</param>
        /// <returns>资源信息，如果不存在则返回 null</returns>
        public static ResourceInfo GetResourceInfo(int resourceID)
        {
            if (ResourceMap.TryGetValue(resourceID, out ResourceInfo resourceInfo))
            {
                return resourceInfo;
            }

#if UNITY_DEBUG
            Debug.LogError($"[ResourceManager] GetResourceInfo 失败: 资源 ID {resourceID} 不存在于映射表中");
#endif
            return null;
        }

        #endregion

        #region 私有方法 - 异步加载资源

        /// <summary>
        /// 异步加载 Asset 资源（UniTask 版本，内部使用）。
        /// </summary>
        /// <param name="package">资源包</param>
        /// <param name="location">资源位置标识</param>
        /// <param name="priority">加载优先级，数值越大优先级越高</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>AssetHandle，失败时返回 null</returns>
        private static async UniTask<AssetHandle> LoadAssetAsync<T>(
            ResourcePackage package,
            string location,
            uint priority,
            CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            // 1. 获取或创建记录
            if (!GlobalAssetRecords.TryGetValue(location, out AssetRecord record))
            {
                record = new AssetRecord(location) { PackageName = package.PackageName };
                GlobalAssetRecords[location] = record;
                GlobalAssetList.Add(location);
            }
            else
            {
                // 记录已存在，检查列表同步性
                if (!GlobalAssetList.Contains(location))
                {
#if UNITY_DEBUG
                    Debug.LogError($"[ResourceManager] 数据不同步: location '{location}' 在 GlobalAssetRecords 中但不在 GlobalAssetList 中，已修复");
#endif
                    GlobalAssetList.Add(location);
                }
            }

            // 3. 命中缓存：如果已经加载好了，直接返回
            if (record.CachedPrefabHandle != null && record.CachedPrefabHandle.IsValid)
            {
                return record.CachedPrefabHandle;
            }

            // 4. 处理并发加载：如果正在加载中，则等待
            if (record.IsLoading)
            {
                // 这里使用 AttachExternalCancellation 确保外部取消时能立刻中断等待
                return await record.LoadingSource.Task.AttachExternalCancellation(cancellationToken);
            }

            // 5. 启动新加载
            record.IsLoading = true;
            record.LoadingSource = new UniTaskCompletionSource<AssetHandle>();

            AssetHandle handle = package.LoadAssetAsync<T>(location, priority);

            handle.Completed += h =>
            {
                record.IsLoading = false;
                if (h.Status == EOperationStatus.Succeed)
                {
                    record.CachedPrefabHandle = h;
                    record.LoadingSource.TrySetResult(h); // 成功：通知所有等待者
                }
                else
                {
                    h.Release(); // 失败也要释放
                    record.LoadingSource.TrySetResult(null);
#if UNITY_DEBUG
                    Debug.LogError($"[ResourceManager] 加载失败: {location}");
#endif
                }
            };

            return await record.LoadingSource.Task.AttachExternalCancellation(cancellationToken);
        }

        #endregion

        #region 私有方法 - Asset 策略辅助

        private static AssetLifePolicy GetPolicyForLocation(string location)
        {
            if (PermanentAssets.ContainsKey(location))
                return AssetLifePolicy.Permanent;
            return AssetLifePolicy.DelayedRelease;
        }

        private static float GetDelaySecondsForLocation(string location)
        {
            if (CustomDelayAssets.TryGetValue(location, out float delay))
                return delay;
            return DefaultDelayedReleaseSeconds;
        }

        #endregion

        #region 私有方法 - 内部辅助方法

        /// <summary>
        /// 释放资源：释放句柄并从两个数据结构中同步移除。
        /// </summary>
        /// <param name="location">资源位置标识</param>
        /// <param name="currentIndex">当前在 GlobalAssetList 中的索引，用于优化移除操作</param>
        private static void ReleaseAsset(string location, int currentIndex = -1)
        {
            if (string.IsNullOrEmpty(location))
                return;

            if (!GlobalAssetRecords.TryGetValue(location, out AssetRecord record))
            {
#if UNITY_DEBUG
                Debug.LogError($"[ResourceManager] ReleaseAsset 失败: location '{location}' 不在 GlobalAssetRecords 中");
#endif
                return;
            }

#if UNITY_DEBUG && YOO_ASSET
            Debug.Log($"[ResourceManager] Asset 已自动释放: {location}");

            // 保存 location 用于验证
            string locationToVerify = location;
            AssetHandle handleToVerify = record.CachedPrefabHandle;
#endif

            // 执行释放
            record.CachedPrefabHandle?.Release();
            record.CachedPrefabHandle = null;
            record.ReleaseAtTime = 0f;

            // 同步移除：确保两个数据结构同步
            bool removedFromDict = GlobalAssetRecords.Remove(location);
            bool removedFromList = false;
            
            if (currentIndex >= 0 && currentIndex < GlobalAssetList.Count && GlobalAssetList[currentIndex] == location)
            {
                // 使用提供的索引直接移除
                GlobalAssetList.RemoveAt(currentIndex);
                removedFromList = true;
            }
            else
            {
                // 数据不同步：尝试从列表中查找并移除
                int index = GlobalAssetList.IndexOf(location);
                if (index >= 0)
                {
                    GlobalAssetList.RemoveAt(index);
                    removedFromList = true;
                }
            }

#if UNITY_DEBUG
            if (!removedFromDict || !removedFromList)
            {
                Debug.LogError($"[ResourceManager] 数据不同步: 释放资源时，从 GlobalAssetRecords 移除={removedFromDict}, 从 GlobalAssetList 移除={removedFromList}, location='{location}'");
            }

            // 验证 1: Handle 应该无效
            if (handleToVerify != null && handleToVerify.IsValid)
            {
                Debug.LogError($"[ResourceManager] Asset 释放验证失败: handle 仍然有效, location='{locationToVerify}'");
            }

            // 验证 2: 记录应该从字典中移除
            if (GlobalAssetRecords.ContainsKey(locationToVerify))
            {
                Debug.LogError($"[ResourceManager] Asset 释放验证失败: 记录未移除, location='{locationToVerify}'");
            }
#endif
        }

        private static bool TryGetPackageByName(string packageName, out ResourcePackage package)
        {
            package = null;
            if (string.IsNullOrEmpty(packageName))
                return false;

            package = YooAssets.TryGetPackage(packageName);
            return package != null;
        }


        private static GameObject InstantiateFromRecord(AssetRecord record)
        {
            GameObject prefab = record.CachedPrefabHandle?.AssetObject as GameObject;
            if (prefab == null)
            {
#if UNITY_DEBUG
                Debug.LogError($"[ResourceManager] InstantiateFromRecord failed: prefab is null for location '{record.Location}'");
#endif
                return null;
            }

            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            record.InstanceRefCount++;
            InstanceLocations[instance.GetInstanceID()] = record.Location;
            return instance;
        }

        #endregion
    }
}
