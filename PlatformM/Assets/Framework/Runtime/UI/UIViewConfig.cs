using System;

namespace Astorise.Framework.UI
{
    /// <summary>
    /// UI 视图配置信息。
    /// </summary>
    [Serializable]
    public class UIViewConfig
    {
        /// <summary>
        /// 配置 ID（全局唯一）
        /// </summary>
        public int ID;

        /// <summary>
        /// 资源位置标识（UGUI asset 路径）
        /// </summary>
        public string Location;

        /// <summary>
        /// Canvas 层级（background, gamePlay, UI, top）
        /// </summary>
        public UILayer Layer;

        /// <summary>
        /// Z-Order（数字越大越在上层）
        /// </summary>
        public int ZOrder;

        /// <summary>
        /// 资源包名称（可选，如果为空则使用默认包名）
        /// </summary>
        public string PackageName;

        /// <summary>
        /// UIView 派生类的类型信息。
        /// </summary>
        [NonSerialized]
        public Type ViewType;
    }

    /// <summary>
    /// UI 层级枚举。
    /// </summary>
    public enum UILayer
    {
        /// <summary>
        /// 背景层（最底层）
        /// </summary>
        Background = 0,

        /// <summary>
        /// 游戏玩法层
        /// </summary>
        GamePlay = 1,

        /// <summary>
        /// UI 层
        /// </summary>
        UI = 2,

        /// <summary>
        /// 顶层（最上层）
        /// </summary>
        Top = 3
    }
}

