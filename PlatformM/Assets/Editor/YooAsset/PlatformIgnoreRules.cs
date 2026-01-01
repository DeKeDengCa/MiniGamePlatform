using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using YooAsset.Editor;

namespace AstroRise.Edtor.YooAssetRules
{
    public sealed class PlatformIgnoreRules : NormalIgnoreRule, IIgnoreRule
    {
        /// <summary>
        /// 需要忽略打包的目录前缀列表（命中任意前缀则忽略）。
        /// </summary>
        private static readonly string[] IgnoreFolderPathList =
        {
            "Assets/TextMesh Pro",
        };

        /// <summary>
        /// 判断路径是否命中“目录前缀忽略列表”。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsInIgnoreFolderPathList(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return false;

            for (int index = 0; index < IgnoreFolderPathList.Length; index++)
            {
                string ignoreFolderPath = IgnoreFolderPathList[index];

                if (assetPath == ignoreFolderPath)
                    return true;

                if (assetPath.StartsWith(ignoreFolderPath + "/", StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// IIgnoreRule 显式实现：合并“项目自定义忽略”和“Normal 默认忽略”。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool IIgnoreRule.IsIgnore(AssetInfo assetInfo)
        {
            if (assetInfo == null || string.IsNullOrEmpty(assetInfo.AssetPath))
                return true;

            // 1) 先走项目自定义忽略（强制忽略）
            if (IsInIgnoreFolderPathList(assetInfo.AssetPath))
                return true;

            // 2) 再走 YooAsset 默认 Normal 忽略规则
            return base.IsIgnore(assetInfo);
        }
    }
}

