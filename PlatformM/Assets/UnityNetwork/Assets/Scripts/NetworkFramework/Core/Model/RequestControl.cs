using System;
using System.Text;
using NetworkFramework.Utils;
using Scommon;

namespace NetworkFramework.Core.Model
{
    /// <summary>
    /// 请求控制类
    /// </summary>
    public class RequestControl
    {
        public long SeqId { get; set; }

        public string Service { get; set; }
        public string RouteKey { get; set; }

        public long Timeout { get; set; }

        public string Method { get; set; }
        public string CryptoInfo { get; set; }
        public CompressType CompressType { get; set; }
        public bool IsAppBackground { get; set; }
        public RPCReason Reason { get; set; }

        /// <summary>
        /// 编码方法
        /// </summary>
        public byte[] Encode(ContentType contentType)
        {
            var reqControl = new ReqControl
            {
                Service = Service ?? string.Empty,
                Method = Method ?? string.Empty,
                Seqid = SeqId,
                RouteKey = RouteKey ?? string.Empty,
                Timeout = Timeout,
                Encrypt = CryptoInfo ?? string.Empty,
                Compress = CompressType,
                Background = IsAppBackground,
                Reason = Reason
            };

            return contentType == ContentType.Proto
                ? Serializer.SerializeToProtoBuf(reqControl)
                : Encoding.UTF8.GetBytes(Serializer.SerializeToJson(reqControl));
        }

        /// <summary>
        /// 克隆请求控制
        /// </summary>
        public RequestControl Clone()
        {
            return new RequestControl
            {
                Service = Service,
                Method = Method,
                SeqId = SeqId,
                RouteKey = RouteKey,
                Timeout = Timeout,
                CryptoInfo = CryptoInfo,
                CompressType = CompressType,
                IsAppBackground = IsAppBackground,
                Reason = Reason
            };
        }
    }
}