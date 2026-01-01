using UnityEngine;

namespace Astorise.Framework.AOT
{
    public class Main : MonoBehaviour
    {
        async void Start()
        {
            try
            {
                // 设置是否在 Editor 环境
#if UNITY_EDITOR
                HotUpdateManager.SetIsEditorEnvironment(true);
                HotUpdateManager.SetUseEditorMode(false);
#else
                HotUpdateManager.SetIsEditorEnvironment(false);
#endif

                await HotUpdateLauncher.Start();
#if UNITY_DEBUG && GAME_BOOTSTRAP
                Debug.Log("[Main] ========== 游戏引导流程全部完成 ==========");
#endif
                // 执行 BBQ 测试
                await GameBootstrap.StartFramework();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Main] Start 方法执行失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        void Update()
        {
            GameEvents.InvokeUpdate();
        }
    }
}
