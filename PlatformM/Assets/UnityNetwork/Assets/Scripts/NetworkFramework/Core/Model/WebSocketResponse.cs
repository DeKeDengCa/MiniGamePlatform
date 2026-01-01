using Scommon;

namespace NetworkFramework.Core.Model
{
    public class WebSocketResponse
    {
        public long SeqId { get; set; }
        
        public RspNotifyType MsgType { get; set; }
        
        public Response HttpResponse { get; set; }
        
        public PushMessage PushMessage { get; set; }
    }
}