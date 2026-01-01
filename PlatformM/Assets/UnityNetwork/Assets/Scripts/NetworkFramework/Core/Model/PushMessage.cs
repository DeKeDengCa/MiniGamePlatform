namespace NetworkFramework.Core.Model
{
    using System;
    using System.Linq;
    using Scommon;

    /// <summary>
    /// 推送消息
    /// </summary>
    public class PushMessage
    {
        /// <summary>
        /// 语音房填roomid（可空）
        /// </summary>
        public long? RoomId { get; set; }

        /// <summary>
        /// 音视频通话的session id（可空）
        /// </summary>
        public string CallId { get; set; }

        /// <summary>
        /// 唯一ID，用于去重（服务器生成）
        /// </summary>
        public long SeqId { get; set; }

        /// <summary>
        /// proto的package名称，用于区分Notify所属的proto
        /// </summary>
        public string NotifyPkg { get; set; }

        /// <summary>
        /// 响应体序列化后的二进制
        /// </summary>
        public byte[] Body { get; set; }

        /// <summary>
        /// body数据压缩类型
        /// </summary>
        public CompressType CompressType { get; set; } = CompressType.None;

        /// <summary>
        /// 推送通道（URL）
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// body 类型（序列化方式）
        /// </summary>
        public ContentType ContentType { get; set; }

        /// <summary>
        /// 服务器时间（毫秒）
        /// </summary>
        public long ServerTimeMs { get; set; }

        public PushMessage() { }

        public PushMessage(
            long? roomId,
            string callId,
            long seqId,
            string notifyPkg,
            byte[] body,
            CompressType compressType,
            string url,
            ContentType contentType,
            long serverTimeMs
        )
        {
            RoomId = roomId;
            CallId = callId;
            SeqId = seqId;
            NotifyPkg = notifyPkg;
            Body = body;
            CompressType = compressType;
            Url = url;
            ContentType = contentType;
            ServerTimeMs = serverTimeMs;
        }

        /// <summary>
        /// 克隆并可选择性替换部分字段
        /// </summary>
        public PushMessage Clone(
            string notifyPkg = null,
            byte[] body = null,
            CompressType? compressType = null,
            string url = null,
            ContentType? contentType = null
        )
        {
            return new PushMessage(
                roomId: RoomId,
                callId: CallId,
                seqId: SeqId,
                notifyPkg: notifyPkg ?? this.NotifyPkg,
                body: body ?? (this.Body != null ? (byte[])this.Body.Clone() : null),
                compressType: compressType ?? this.CompressType,
                url: url ?? this.Url,
                contentType: contentType ?? this.ContentType,
                serverTimeMs: ServerTimeMs
            );
        }

        public override string ToString()
        {
            var bodyStr = Body != null && Body.Length > 0
                ? Convert.ToBase64String(Body)
                : string.Empty;

            return $"PushMessage(roomId='{RoomId}', callId='{CallId}', seqId='{SeqId}', notifyPkg='{NotifyPkg}', body={bodyStr}, compressType={CompressType}, url='{Url}', contentType={ContentType}, serverTimeMs={ServerTimeMs})";
        }

    }
}