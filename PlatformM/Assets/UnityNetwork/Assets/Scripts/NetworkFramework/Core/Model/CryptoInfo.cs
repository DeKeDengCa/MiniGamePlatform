namespace NetworkFramework.Core.Model
{
    
    /// <summary>
    /// 加密信息类
    /// </summary>
    public class CryptoInfo
    {
        public byte[] Key { get; set; }
        public string PublicKeyNo { get; set; }
        public string Cache { get; set; }
    }
}