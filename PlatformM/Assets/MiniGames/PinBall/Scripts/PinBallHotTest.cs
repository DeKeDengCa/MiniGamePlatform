using UnityEngine;

namespace Astorise.MiniGames.PinBall
{
    /// <summary>
    /// BBQ 热更新测试类。
    /// 用于验证 HybridCLR 热更新功能。
    /// </summary>
    public static class PinBallHotTest
    {
        /// <summary>
        /// 运行热更新测试，打印测试日志。
        /// </summary>
        public static void Run()
        {
            Debug.Log("[PinBallHotTest] PinBall HotUpdate Test: Hello from HotUpdate!");
        }
    }
}
