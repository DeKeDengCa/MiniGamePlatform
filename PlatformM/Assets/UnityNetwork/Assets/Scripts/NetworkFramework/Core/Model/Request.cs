using System.Collections.Generic;
using Scommon;

namespace NetworkFramework.Core.Model
{
    
    /// <summary>
    /// 请求对象
    /// </summary>
    public class Request
    {
        public string InconstantConnectionUrl { get; set; }
        public string PersistentConnectionUrl { get; set; }
        public ConnectionType UseConnectionType { get; set; }
        public string Token { get; set; }
        public PublicParams PublicParams { get; set; }
        public RequestControl RequestControl { get; set; }
        public byte[] Body { get; set; }
        public ContentType ContentType { get; set; }
        public long TimeoutMs { get; set; }
        
        /// <summary>
        /// 请求头集合
        /// 用于存储HTTP请求头信息，与HeaderInterceptor中使用的header常量对应
        /// </summary>
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();
        
        // 是否只建立长连接
        public bool OnlyConnected { get; set; }
        
        /// <summary>
        /// 克隆Headers集合
        /// 确保在克隆请求对象时正确复制所有的请求头信息
        /// </summary>
        /// <param name="sourceHeaders">源Headers集合</param>
        /// <returns>克隆后的Headers集合</returns>
        private static Dictionary<string, string> CloneHeaders(Dictionary<string, string> sourceHeaders)
        {
            if (sourceHeaders == null)
                return new Dictionary<string, string>();
            
            // 创建一个新的字典并复制所有键值对
            var clonedHeaders = new Dictionary<string, string>();
            foreach (var header in sourceHeaders)
            {
                clonedHeaders[header.Key] = header.Value;
            }
            
            return clonedHeaders;
        }

        /// <summary>
        /// 克隆请求对象
        /// </summary>
        public Request Clone(
            string inconstantConnectionUrl = null,
            string persistentConnectionUrl = null,
            ConnectionType? useConnectionType = null,
            string token = null,
            PublicParams publicParams = null,
            RequestControl requestControl = null,
            byte[] body = null,
            ContentType? contentType = null,
            long? timeoutMs = null
        )
        {
            return new Request
            {
                InconstantConnectionUrl = inconstantConnectionUrl ?? this.InconstantConnectionUrl,
                PersistentConnectionUrl = persistentConnectionUrl ?? this.PersistentConnectionUrl,
                UseConnectionType = useConnectionType ?? this.UseConnectionType,
                Token = token ?? this.Token,
                PublicParams = publicParams ?? this.PublicParams?.Clone(),
                RequestControl = requestControl ?? this.RequestControl?.Clone(),
                Body = body ?? (this.Body != null ? (byte[])this.Body.Clone() : null),
                ContentType = contentType ?? this.ContentType,
                TimeoutMs = timeoutMs ?? this.TimeoutMs,
                Headers = CloneHeaders(this.Headers),
                OnlyConnected = this.OnlyConnected
            };
        }
    }
}