
using System;
using System.Collections.Concurrent;
using NetworkFramework.Core;
using NetworkFramework.Utils;
using UnityEngine;

namespace NetworkFramework.Runtime
{

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class UnityMainThread : MonoBehaviour
    {
        private static UnityMainThread _instance;
        private static readonly ConcurrentQueue<Action> _queue = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            LoggerUtil.Log("UnityMainThread init()");
            UnitySafe.Init();
            if (!UnitySafe.IsPlaying) return; // 非 Play 模式不安装
            EnsureInstance();
        }

        private static void EnsureInstance()
        {
            if (_instance != null) return;

            var existing = FindFirstObjectByType<UnityMainThread>();
            if (existing != null)
            {
                _instance = existing;
            }
            else
            {
                var go = new GameObject(nameof(UnityMainThread));
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<UnityMainThread>();
            }
        }

        private void Awake()
        {
            // 非 Play 模式下，自动自毁，避免编辑器误触发
            if (!UnitySafe.IsPlaying)
            {
                DestroyImmediate(gameObject);
                return;
            }

            // 防重复挂载：如果场景里已经有另一个实例，当前这个销毁
            if (_instance != null && _instance != this)
            {
                DestroyImmediate(gameObject);
                return;
            }

            _instance = this;
            
            // BestHTTP.HTTPManager.Setup();
        }

        private void Update()
        {
            // 非 Play 模式不处理
            if (!UnitySafe.IsPlaying) return;

            // 将所有待执行的动作出队执行，异常安全
            // todo 需要处理每帧的任务，不然所有任务都在一帧内执行的话，会造成卡顿
            while (_queue.TryDequeue(out var action))
            {
                try
                {
                    action?.Invoke();
                }
                catch (Exception ex)
                {
                    LoggerUtil.LogException(ex);
                }
            }
        }

        public static void Post(Action action)
        {
            // 非 Play 模式下直接忽略请求，避免卡死或误用
            if (!UnitySafe.IsPlaying) return;
            _queue.Enqueue(action);
        }

        public static void Run(Action action)
        {
            // 在主线程立即执行；否则入队等待主线程处理
            if (!UnitySafe.IsPlaying) return;

            if (_instance != null && _instance.gameObject.scene.IsValid() && IsOnUnityThread())
            {
                try
                {
                    action?.Invoke();
                }
                catch (Exception ex)
                {
                    LoggerUtil.LogException(ex);
                }
            }
            else
            {
                Post(action);
            }
        }

        // 简单判断：Update 驱动的主线程上下文
        private static bool IsOnUnityThread()
        {
            // 在 Unity 中无法通用判断当前是否主线程，这里用保守策略：
            // 如果此刻能直接访问 _instance 且处在有效场景，视为主线程。
            // 更严谨可引入 ThreadStatic 标记，在 Update 中设置主线程 ID。
            return true;
        }
    }
}