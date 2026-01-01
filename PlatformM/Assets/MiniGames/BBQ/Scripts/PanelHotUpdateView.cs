using Astorise.Framework.UI;
using UnityEngine;

namespace Astorise.MiniGames.BBQ
{
    /// <summary>
    /// PanelHotUpdate 视图类，用于管理热更新面板 UI。
    /// </summary>
    public class PanelHotUpdateView : UIView
    {
        /// <summary>
        /// TestButton 按钮点击回调。
        /// </summary>
        private void OnTestButtonClick()
        {
            Debug.Log("[PanelHotUpdateView] TestButton 被点击");
        }

        /// <summary>
        /// TestButtonA 按钮点击回调。
        /// </summary>
        private void OnTestButtonAClick()
        {
            Debug.Log("[PanelHotUpdateView] TestButtonA 被点击");
        }
    }
}

