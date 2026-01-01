using System.IO;
using YooAsset.Editor;   // 确保有这个，用到 DisplayNameAttribute 和 IPackRule


namespace AstroRise.Edtor.YooAssetRules
{
    [DisplayName("打包原生文件(保留后缀)")]
    public class PackRawFileWithExtension : IPackRule
    {
        public PackRuleResult GetPackRuleResult(PackRuleData data)
        {
            // 资源包名：你这里用 AssetPath，OK
            string bundleName = data.AssetPath;

            // 取源文件扩展名
            string fileExtension = Path.GetExtension(data.AssetPath); // ".mp3" / ".dll" / ".json"

            // 去掉开头的点，因为 PackRuleResult 要的是不带点的后缀
            if (!string.IsNullOrEmpty(fileExtension) && fileExtension.StartsWith("."))
                fileExtension = fileExtension.Substring(1);

            // 第二个参数 = “原生文件后缀”，会传给 RawFileBuildPipeline 用来当物理文件后缀
            PackRuleResult result = new PackRuleResult(bundleName, fileExtension);
            return result;
        }

        // 标记这是原生文件规则
        public bool IsRawFilePackRule()
        {
            return true;
        }
    }
}
