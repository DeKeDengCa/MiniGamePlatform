using System;
using Cysharp.Threading.Tasks;
using Astorise.Framework.Core;
using Spine.Unity;
using UnityEngine;
using YooAsset;

namespace Astorise.Framework.Core
{
    public class SpineManager
    {
        /// <summary>
        /// 异步生成 Spine 动画实例
        /// </summary>
        /// <param name="packageName">资源包名称</param>
        /// <param name="spineLocation">Spine 资源地址（SkeletonDataAsset 的路径）</param>
        /// <returns>创建的 SkeletonAnimation 实例，如果加载失败则返回 null</returns>
        public static async UniTask<SkeletonAnimation> GenerateSpineAnimationAsync(string packageName, string spineLocation)
        {
            if (string.IsNullOrEmpty(packageName))
            {
#if UNITY_DEBUG
                Debug.LogError("[SpineManager] Package name is null or empty");
#endif
                return null;
            }

            if (string.IsNullOrEmpty(spineLocation))
            {
#if UNITY_DEBUG
                Debug.LogError("[SpineManager] Spine location is null or empty");
#endif
                return null;
            }

            // 加载 SkeletonDataAsset
            AssetHandle handle = await ResourceManager.LoadAssetAsync<SkeletonDataAsset>(
                packageName,
                spineLocation,
                0);

            if (handle == null || handle.Status != EOperationStatus.Succeed)
            {
#if UNITY_DEBUG
                Debug.LogError($"[SpineManager] Failed to load asset: {spineLocation}, Error: {handle?.LastError ?? "Unknown error"}");
#endif
                return null;
            }

            SkeletonDataAsset skeletonDataAsset = handle.AssetObject as SkeletonDataAsset;
            
            if (skeletonDataAsset == null)
            {
#if UNITY_DEBUG
                Debug.LogError($"[SpineManager] Asset is not a SkeletonDataAsset or load failed: {spineLocation}");
#endif
                return null;
            }

            // 使用 Spine 官方 API 创建 SkeletonAnimation 实例
            SkeletonAnimation skeletonAnimation = SkeletonAnimation.NewSkeletonAnimationGameObject(skeletonDataAsset);
            if (skeletonAnimation == null)
            {
#if UNITY_DEBUG
                Debug.LogError($"[SpineManager] Failed to create SkeletonAnimation: {spineLocation}");
#endif
                return null;
            }

            // 初始化 SkeletonAnimation
            skeletonAnimation.Initialize(false);

            return skeletonAnimation;
        }

        /// <summary>
        /// 同步生成 Spine 动画实例（假设资源已预加载）
        /// </summary>
        /// <param name="packageName">资源包名称</param>
        /// <param name="spineLocation">Spine 资源地址（SkeletonDataAsset 的路径）</param>
        /// <returns>创建的 SkeletonAnimation 实例</returns>
        /// <exception cref="InvalidOperationException">当资源未加载或加载失败时抛出</exception>
        /// <exception cref="InvalidCastException">当资源类型不是 SkeletonDataAsset 时抛出</exception>
        public static SkeletonAnimation GenerateSpineAnimation(string packageName, string spineLocation)
        {
            if (string.IsNullOrEmpty(packageName))
            {
                throw new InvalidOperationException("[SpineManager] Package name is null or empty");
            }

            if (string.IsNullOrEmpty(spineLocation))
            {
                throw new InvalidOperationException("[SpineManager] Spine location is null or empty");
            }

            // 获取指定 Package
            ResourcePackage package = YooAssets.GetPackage(packageName);
            if (package == null)
            {
                throw new InvalidOperationException($"[SpineManager] Package '{packageName}' not found");
            }

            // 尝试同步获取资源（需要资源已通过 ResourceManager 加载过）
            // ResourceManager 内部管理了已加载的资源，我们需要通过它来获取
            // 由于 ResourceManager 没有提供同步获取已加载资源的方法，我们使用 Package 直接加载
            // 注意：这要求资源已经通过 ResourceManager 加载过，否则会失败
            AssetHandle handle = package.LoadAssetSync<SkeletonDataAsset>(spineLocation);
            if (handle == null || !handle.IsValid)
            {
                throw new InvalidOperationException($"[SpineManager] Asset is not loaded yet: {spineLocation}. Please ensure the asset is preloaded via ResourceManager before calling this method.");
            }

            if (handle.Status != EOperationStatus.Succeed)
            {
                throw new InvalidOperationException($"[SpineManager] Failed to load asset: {spineLocation}, Error: {handle.LastError}");
            }

            SkeletonDataAsset skeletonDataAsset = handle.AssetObject as SkeletonDataAsset;
            if (skeletonDataAsset == null)
            {
                throw new InvalidCastException($"[SpineManager] Asset is not a SkeletonDataAsset: {spineLocation}. Actual type: {handle.AssetObject?.GetType()}");
            }

            // 使用 Spine 官方 API 创建 SkeletonAnimation 实例
            SkeletonAnimation skeletonAnimation = SkeletonAnimation.NewSkeletonAnimationGameObject(skeletonDataAsset);
            if (skeletonAnimation == null)
            {
                throw new InvalidOperationException($"[SpineManager] Failed to create SkeletonAnimation: {spineLocation}");
            }

            // 初始化 SkeletonAnimation
            skeletonAnimation.Initialize(false);

            return skeletonAnimation;
        }

        /// <summary>
        /// 异步加载并播放指定的 Spine 动画
        /// </summary>
        /// <param name="packageName">资源包名称</param>
        /// <param name="location">Spine 资源地址（SkeletonDataAsset 的路径）</param>
        /// <param name="animationName">要播放的动画名称</param>
        /// <param name="parent">父对象 Transform，如果为 null 则不设置父对象</param>
        /// <param name="position">本地位置，默认为 Vector3.zero</param>
        /// <returns>创建的 SkeletonAnimation 实例，如果加载失败或动画不存在则返回 null</returns>
        public static async UniTask<SkeletonAnimation> PlayAnimation(string packageName, string location, string animationName, Transform parent = null, Vector3 position = default(Vector3))
        {
            if (string.IsNullOrEmpty(animationName))
            {
#if UNITY_DEBUG
                Debug.LogError("[SpineManager] Animation name is null or empty");
#endif
                return null;
            }

            // 调用 GenerateSpineAnimationAsync 加载并创建实例
            SkeletonAnimation skeletonAnimation = await GenerateSpineAnimationAsync(packageName, location);
            if (skeletonAnimation == null)
            {
                return null;
            }

            // 查找指定的动画
            if (skeletonAnimation.Skeleton == null || skeletonAnimation.Skeleton.Data == null)
            {
#if UNITY_DEBUG
                Debug.LogError($"[SpineManager] Skeleton or Skeleton.Data is null: {location}");
#endif
                return null;
            }

            Spine.Animation animation = skeletonAnimation.Skeleton.Data.FindAnimation(animationName);
            if (animation == null)
            {
#if UNITY_DEBUG
                Debug.LogError($"[SpineManager] Animation '{animationName}' not found in: {location}");
#endif
                return null;
            }

            // 播放动画（循环播放）
            skeletonAnimation.AnimationState.SetAnimation(0, animation, true);

            // 设置父对象和位置
            if (parent != null)
            {
                skeletonAnimation.transform.SetParent(parent, false);
            }
            skeletonAnimation.transform.localPosition = position;

            return skeletonAnimation;
        }

        /// <summary>
        /// 异步加载并播放第0个 Spine 动画（默认）
        /// </summary>
        /// <param name="packageName">资源包名称</param>
        /// <param name="location">Spine 资源地址（SkeletonDataAsset 的路径）</param>
        /// <param name="parent">父对象 Transform，如果为 null 则不设置父对象</param>
        /// <param name="position">本地位置，默认为 Vector3.zero</param>
        /// <returns>创建的 SkeletonAnimation 实例，如果加载失败或没有动画则返回 null</returns>
        public static async UniTask<SkeletonAnimation> PlayAnimation(string packageName, string location, Transform parent = null, Vector3 position = default(Vector3))
        {
            // 调用 GenerateSpineAnimationAsync 加载并创建实例
            SkeletonAnimation skeletonAnimation = await GenerateSpineAnimationAsync(packageName, location);
            if (skeletonAnimation == null)
            {
                return null;
            }

            // 获取动画列表
            if (skeletonAnimation.Skeleton == null || skeletonAnimation.Skeleton.Data == null)
            {
#if UNITY_DEBUG
                Debug.LogError($"[SpineManager] Skeleton or Skeleton.Data is null: {location}");
#endif
                return null;
            }

            Spine.ExposedList<Spine.Animation> animations = skeletonAnimation.Skeleton.Data.Animations;
            if (animations == null || animations.Count == 0)
            {
#if UNITY_DEBUG
                Debug.LogError($"[SpineManager] No animations found in: {location}");
#endif
                return null;
            }

            // 获取第0个动画
            Spine.Animation firstAnimation = animations.Items[0];

            // 播放动画（循环播放）
            skeletonAnimation.AnimationState.SetAnimation(0, firstAnimation, true);

            // 设置父对象和位置
            if (parent != null)
            {
                skeletonAnimation.transform.SetParent(parent, false);
            }
            skeletonAnimation.transform.localPosition = position;

            return skeletonAnimation;
        }


        //string spineLocation = "Assets/Test/Example/Spine/cloud.prefab";

        // 测试 1: PlayAnimation(location) - 播放第0个动画
        //SpineManager.PlayAnimationUI(spineLocation, _googleLoginBtn.transform, Vector3.zero).Forget();

        // 测试 PlayAnimation 方法
        //SpineManager.PlayAnimationUI(spineLocation, "playing-in-the-rain", _googleLoginBtn.transform, Vector3.zero).Forget();

        /// <summary>
        /// 异步加载并播放指定的 Spine 动画（UI 版本，使用 SkeletonGraphic）
        /// </summary>
        /// <param name="packageName">资源包名称</param>
        /// <param name="location">GameObject Prefab 路径（包含 SkeletonGraphic 组件）</param>
        /// <param name="animationName">要播放的动画名称</param>
        /// <param name="parent">父对象 Transform（必须提供，UI 元素需要挂载在 Canvas 下）</param>
        /// <param name="position">本地位置，默认为 Vector3.zero</param>
        /// <returns>创建的 GameObject 实例，如果加载失败或动画不存在则返回 null</returns>
        public static async UniTask<GameObject> PlayAnimationUI(string packageName, string location, string animationName, Transform parent, Vector3 position = default(Vector3))
        {
            if (string.IsNullOrEmpty(animationName))
            {
#if UNITY_DEBUG
                Debug.LogError("[SpineManager] Animation name is null or empty");
#endif
                return null;
            }

            if (parent == null)
            {
#if UNITY_DEBUG
                Debug.LogError("[SpineManager] Parent is required for PlayAnimationUI");
#endif
                return null;
            }

            // 加载并实例化 GameObject Prefab
            GameObject prefabInstance = await InstantiateSkeletonGraphicFromPrefab(packageName, location, parent, position);
            if (prefabInstance == null)
            {
                return null;
            }

            // 获取 SkeletonGraphic 组件
            SkeletonGraphic skeletonGraphic = prefabInstance.GetComponent<SkeletonGraphic>();
            if (skeletonGraphic == null)
            {
#if UNITY_DEBUG
                Debug.LogError($"[SpineManager] Prefab does not contain SkeletonGraphic component: {location}");
#endif
                return null;
            }

            // 查找指定的动画
            if (skeletonGraphic.Skeleton == null || skeletonGraphic.Skeleton.Data == null)
            {
#if UNITY_DEBUG
                Debug.LogError($"[SpineManager] Skeleton or Skeleton.Data is null: {location}");
#endif
                return null;
            }

            Spine.Animation animation = skeletonGraphic.Skeleton.Data.FindAnimation(animationName);
            if (animation == null)
            {
#if UNITY_DEBUG
                Debug.LogError($"[SpineManager] Animation '{animationName}' not found in: {location}");
#endif
                return null;
            }

            // 播放动画（循环播放）
            skeletonGraphic.AnimationState.SetAnimation(0, animation, true);

            return prefabInstance;
        }

        /// <summary>
        /// 异步加载并播放第0个 Spine 动画（UI 版本，使用 SkeletonGraphic）
        /// </summary>
        /// <param name="packageName">资源包名称</param>
        /// <param name="location">GameObject Prefab 路径（包含 SkeletonGraphic 组件）</param>
        /// <param name="parent">父对象 Transform（必须提供，UI 元素需要挂载在 Canvas 下）</param>
        /// <param name="position">本地位置，默认为 Vector3.zero</param>
        /// <returns>创建的 GameObject 实例，如果加载失败或没有动画则返回 null</returns>
        public static async UniTask<GameObject> PlayAnimationUI(string packageName, string location, Transform parent, Vector3 position = default(Vector3))
        {
            if (parent == null)
            {
#if UNITY_DEBUG
                Debug.LogError("[SpineManager] Parent is required for PlayAnimationUI");
#endif
                return null;
            }

            // 加载并实例化 GameObject Prefab
            GameObject prefabInstance = await InstantiateSkeletonGraphicFromPrefab(packageName, location, parent, position);
            if (prefabInstance == null)
            {
                return null;
            }

            // 获取 SkeletonGraphic 组件
            SkeletonGraphic skeletonGraphic = prefabInstance.GetComponent<SkeletonGraphic>();
            if (skeletonGraphic == null)
            {
#if UNITY_DEBUG
                Debug.LogError($"[SpineManager] Prefab does not contain SkeletonGraphic component: {location}");
#endif
                return null;
            }

            // 获取动画列表
            if (skeletonGraphic.Skeleton == null || skeletonGraphic.Skeleton.Data == null)
            {
#if UNITY_DEBUG
                Debug.LogError($"[SpineManager] Skeleton or Skeleton.Data is null: {location}");
#endif
                return null;
            }

            Spine.ExposedList<Spine.Animation> animations = skeletonGraphic.Skeleton.Data.Animations;
            if (animations == null || animations.Count == 0)
            {
#if UNITY_DEBUG
                Debug.LogError($"[SpineManager] No animations found in: {location}");
#endif
                return null;
            }

            // 获取第0个动画
            Spine.Animation firstAnimation = animations.Items[0];

            // 播放动画（循环播放）
            skeletonGraphic.AnimationState.SetAnimation(0, firstAnimation, true);

            return prefabInstance;
        }

        /// <summary>
        /// 从 Prefab 实例化 SkeletonGraphic（内部辅助方法）
        /// </summary>
        /// <param name="packageName">资源包名称</param>
        /// <param name="location">GameObject Prefab 路径（包含 SkeletonGraphic 组件）</param>
        /// <param name="parent">父对象 Transform</param>
        /// <param name="position">本地位置</param>
        /// <returns>GameObject 实例，如果加载失败则返回 null</returns>
        private static async UniTask<GameObject> InstantiateSkeletonGraphicFromPrefab(string packageName, string location, Transform parent, Vector3 position)
        {
            if (string.IsNullOrEmpty(packageName))
            {
#if UNITY_DEBUG
                Debug.LogError("[SpineManager] Package name is null or empty");
#endif
                return null;
            }

            // 加载 Prefab
            AssetHandle prefabHandle = await ResourceManager.LoadAssetAsync<GameObject>(
                packageName,
                location,
                0);
            if (prefabHandle == null || prefabHandle.Status != EOperationStatus.Succeed)
            {
#if UNITY_DEBUG
                Debug.LogError($"[SpineManager] Failed to load Prefab: {location}, Error: {prefabHandle?.LastError ?? "Unknown error"}");
#endif
                return null;
            }

            GameObject prefab = prefabHandle.AssetObject as GameObject;
            if (prefab == null)
            {
#if UNITY_DEBUG
                Debug.LogError($"[SpineManager] Prefab is not a GameObject: {location}");
#endif
                return null;
            }

            // 实例化 Prefab
            GameObject prefabInstance = await ResourceManager.InstantiateGameObjectAsync(location);
            if (prefabInstance == null)
            {
#if UNITY_DEBUG
                Debug.LogError($"[SpineManager] Failed to instantiate Prefab: {location}");
#endif
                return null;
            }

            // 获取 SkeletonGraphic 组件
            SkeletonGraphic skeletonGraphic = prefabInstance.GetComponent<SkeletonGraphic>();
            if (skeletonGraphic == null)
            {
#if UNITY_DEBUG
                Debug.LogError($"[SpineManager] Prefab does not contain SkeletonGraphic component: {location}");
#endif
                UnityEngine.Object.Destroy(prefabInstance);
                return null;
            }

            // 确保 SkeletonGraphic 已初始化（如果 Prefab 中已配置好 skeletonDataAsset）
            if (skeletonGraphic.skeletonDataAsset != null && !skeletonGraphic.IsValid)
            {
                skeletonGraphic.Initialize(false);
            }

            // 设置父对象和位置
            prefabInstance.transform.SetParent(parent, false);
            prefabInstance.transform.localPosition = position;

            return prefabInstance;
        }
    }
}

