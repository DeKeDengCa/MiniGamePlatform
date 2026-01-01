namespace NetworkFramework.Core.Model
{
    /// <summary>
    /// Bridge 层返回的原始 HTTP 响应数据
    /// </summary>
    public class HttpResponseData
    {
        public int StatusCode { get; set; }
        public byte[] Body { get; set; }
    }
}