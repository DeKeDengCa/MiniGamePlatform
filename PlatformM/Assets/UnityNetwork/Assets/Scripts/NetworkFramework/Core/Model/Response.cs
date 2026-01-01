using System;
using System.Collections.Generic;
using Scommon;

namespace NetworkFramework.Core.Model
{
   

    /// <summary>
    /// 响应对象
    /// </summary>
    public class Response
    {
        /// <summary>
        /// 响应头集合
        /// 用于存储HTTP响应头信息，与HeaderInterceptor中使用的header常量对应
        /// </summary>
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();
        /// <summary>
        /// 系统错误码或网络状态码
        /// </summary>
        public int NetCode { get; set; }

        /// <summary>
        /// 系统或网络信息
        /// </summary>
        public string NetMessage { get; set; }

        /// <summary>
        /// 服务端code，-1表示服务器没有返回
        /// </summary>
        public long Code { get; set; } = ErrorCode.INVALID_CODE;

        /// <summary>
        /// 服务端message（一般用于调试）
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 提示语，key-value分别为语言和对应语言的提示语内容
        /// </summary>
        public Dictionary<string, string> Toast { get; set; }

        /// <summary>
        /// 响应体body序列化数据
        /// </summary>
        public byte[] Body { get; set; }

        /// <summary>
        /// 优化传输性能使用
        /// </summary>
        public string Encrypt { get; set; }

        /// <summary>
        /// body数据压缩类型
        /// </summary>
        public CompressType CompressType { get; set; } = CompressType.None;

        /// <summary>
        /// 服务器时间，0表示服务器没有返回
        /// </summary>
        public long ServerTime { get; set; }

        /// <summary>
        /// 请求ID，可用于DEBUG
        /// </summary>
        public long SeqId { get; set; }
        
        /// <summary>
        /// 响应体RspNotifyControl序列化数据
        /// </summary>
        public byte[] ResponseControl { get; set; }

        /// <summary>
        /// 克隆响应对象
        /// </summary>
        public Response Clone(
            int? netCode = null,
            string netMessage = null,
            byte[] body = null,
            string cryptoKey = null,
            CompressType? compressType = null
        )
        {
            return new Response
            {
                NetCode = netCode ?? this.NetCode,
                NetMessage = netMessage ?? this.NetMessage,
                Code = this.Code,
                Message = this.Message,
                Toast = this.Toast != null ? new Dictionary<string, string>(this.Toast) : null,
                Body = body ?? (this.Body != null ? (byte[])this.Body.Clone() : null),
                Encrypt = cryptoKey ?? this.Encrypt,
                CompressType = compressType ?? this.CompressType,
                ServerTime = this.ServerTime,
                SeqId = this.SeqId,
                Headers = CloneHeaders(this.Headers),
                ResponseControl = this.ResponseControl
            };
        }
        
        /// <summary>
        /// 克隆Headers集合
        /// </summary>
        /// <param name="headers">原始Headers集合</param>
        /// <returns>克隆后的Headers集合</returns>
        private Dictionary<string, string> CloneHeaders(Dictionary<string, string> headers)
        {
            if (headers == null || headers.Count == 0)
            {
                return new Dictionary<string, string>();
            }
            
            return new Dictionary<string, string>(headers);
        }

        public override string ToString()
        {
            var bodyStr = Body != null && Body.Length > 0
                ? Convert.ToBase64String(Body)
                : string.Empty;

            return $"Response(SeqId='{SeqId}' NetCode='{NetCode} NetMessage='{NetMessage}' Message='{Message}' Code='{Code}' Body='{bodyStr}') ";
        }
    }
}