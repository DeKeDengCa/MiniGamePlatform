using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BestHTTP;
using BestHTTP.WebSocket;
using NetworkFramework.Core.Interface;
using NetworkFramework.Core.Model;
using NetworkFramework.Utils;
using Scommon;
using WebSocketResponse = NetworkFramework.Core.Model.WebSocketResponse;

namespace NetworkFramework.Core.Adapter
{
    /// <summary>
    /// 单条 WebSocket 长链适配器
    /// </summary>
    public sealed class BestHttpWebSocketAdapter : INetworkAdapter, IDisposable
    {
        private const string TAG = "BestHttpWebSocketAdapter";

        // 心跳相关常量，避免出现魔法值
        private const int HEARTBEAT_INTERVAL_MS = 15 * 1000; // 心跳间隔（毫秒）
        private const int HEARTBEAT_CLOSE_AFTER_NO_MSG_SECONDS = 30; // 无消息关闭的超时时间（秒）

        private WebSocket _webSocket;
        private readonly ConcurrentDictionary<long, TaskCompletionSource<Response>> _pending = new();

        public bool IsConnected => _webSocket != null && _webSocket.IsOpen;

        public event Action<PushMessage> OnPushMessage;
        public event Action<ushort, string> OnConnectionClosed;
        public event Action<string> OnConnectionError;

        public async Task<Response> Request(Request request, CancellationToken token)
        {
            if (!IsConnected)
            {
                try
                {
                    await ConnectAsync(request, token);
                }
                catch (Exception ex)
                {
                    return new Response
                    {
                        NetCode = 500,
                        NetMessage = ex.Message,
                        Code = ErrorCode.CONNECTION_FAILED,
                        Message = ex.Message,
                        SeqId = request.RequestControl?.SeqId ?? 0
                    };
                }
            }

            if (!IsConnected)
            {
                return new Response
                {
                    NetCode = 500,
                    NetMessage = "WebSocket not connected",
                    Code = ErrorCode.CONNECTION_FAILED,
                    Message = "WebSocket not connected",
                    SeqId = request.RequestControl?.SeqId ?? 0
                };
            }

            // 如果只是建立连接，则直接建连成功后返回数据
            if (request.OnlyConnected)
            {
                return new Response
                {
                    NetCode = 200,
                    NetMessage = "WebSocket connected successfully.",
                    Code = ErrorCode.SUCCESS,
                    Message = "WebSocket connected successfully.",
                    SeqId = request.RequestControl?.SeqId ?? 0
                };
            }

            var tcs = new TaskCompletionSource<Response>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!_pending.TryAdd(request.RequestControl.SeqId, tcs))
                throw new InvalidOperationException($"Duplicate SeqId: {request.RequestControl.SeqId}");

            token.Register(() =>
            {
                if (_pending.TryRemove(request.RequestControl.SeqId, out var pendingTcs))
                    pendingTcs.TrySetCanceled();
            });

            // 判断是否在主线程后才调用？

            // —— 按Android持久化请求编码规则打包消息 ——
            // 1. ContentType(1字节) 标记：JSON=0x1, PROTOBUF=0x0
            byte contentTypeFlag = request.ContentType == ContentType.Proto ? (byte)0x0 : (byte)0x1;

            // 2. 控制字段序列化（依赖ContentType）
            byte[] controlBytes = request.RequestControl != null
                ? request.RequestControl.Encode(request.ContentType)
                : Array.Empty<byte>();

            // 3. 控制字段长度（4字节, 小端序）
            byte[] controlLenBytes = BitConverter.GetBytes(controlBytes.Length);
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(controlLenBytes);

            // 4. Body（可选）
            byte[] bodyBytes = request.Body ?? Array.Empty<byte>();

            // 5. 拼装最终发送数据
            var payload = new byte[1 + 4 + controlBytes.Length + bodyBytes.Length];
            payload[0] = contentTypeFlag;
            Buffer.BlockCopy(controlLenBytes, 0, payload, 1, 4);
            Buffer.BlockCopy(controlBytes, 0, payload, 5, controlBytes.Length);
            if (bodyBytes.Length > 0)
                Buffer.BlockCopy(bodyBytes, 0, payload, 5 + controlBytes.Length, bodyBytes.Length);

            try
            {
                _webSocket.Send(payload);
            }
            catch (Exception ex)
            {
                _pending.TryRemove(request.RequestControl.SeqId, out _);
                tcs.TrySetResult(new Response
                {
                    NetCode = 500,
                    NetMessage = ex.Message,
                    Code = ErrorCode.SEND_FAILED,
                    Message = ex.Message,
                    SeqId = request.RequestControl?.SeqId ?? 0
                });
            }
            return await tcs.Task.ConfigureAwait(false);
        }


        public async Task ConnectAsync(Request request, CancellationToken token)
        {
            if (IsConnected) return;

            var url = request.PersistentConnectionUrl;
            var tcs = new TaskCompletionSource<bool>();
            _webSocket = new WebSocket(new Uri(url));
            HTTPManager.RequestTimeout = TimeSpan.FromSeconds(30); // 握手请求超时
            HTTPManager.ConnectTimeout = TimeSpan.FromSeconds(30); // 建立连接超时

            // 配置心跳逻辑：启用内置的 Ping 线程，并设置频率与无消息关闭的阈值
            // WebGL 平台下 BestHTTP 的 Ping 线程不可用，编译器指令会在该平台自动剔除相关属性
#if (!UNITY_WEBGL || UNITY_EDITOR)
            _webSocket.StartPingThread = true; // 开启 Ping 心跳线程
            _webSocket.PingFrequency = HEARTBEAT_INTERVAL_MS; // 设置心跳频率
            _webSocket.CloseAfterNoMesssage = TimeSpan.FromSeconds(HEARTBEAT_CLOSE_AFTER_NO_MSG_SECONDS); // 设置无消息关闭阈值
            // _webSocket.CloseAfterNoMesssage = TimeSpan.Zero; // 不因无消息自动断开
#endif

            // 添加自定义请求头
            var websocketRequest = _webSocket.InternalRequest;
            if (request.Headers != null && websocketRequest != null)
            {
                foreach (var header in request.Headers)
                {
                    websocketRequest.SetHeader(header.Key, header.Value);
                }
            }

            // 注册事件
            _webSocket.OnOpen += (ws) => tcs.TrySetResult(true);
            _webSocket.OnError += (ws, ex) =>
            {
                var exception = ex ?? new Exception("WebSocket connection error occurred");
                var resp = ws.InternalRequest?.Response;
                if (resp != null)
                {
                    LoggerUtil.LogError($"Handshake failed: status={resp.StatusCode}, msg={resp.Message}");
                }

                tcs.TrySetException(exception);
                ClearPendingRequests($"WebSocket error: {exception.Message}");
                OnConnectionError?.Invoke(exception.Message);
            };
            _webSocket.OnClosed += (ws, code, msg) =>
            {
                LoggerUtil.Log($"[WebSocket] OnClosed: Code={code}, Message={msg}");
                ClearPendingRequests($"WebSocket closed: {code} - {msg}");
                OnConnectionClosed?.Invoke(code, msg);
            };
            // _webSocket.OnMessage += (ws, msg) => HandleIncoming(msg, false);
            _webSocket.OnMessage += (ws, msg) => { LoggerUtil.Log($"[WebSocket] OnMessage: Message={msg}"); };
            _webSocket.OnBinary += (ws, data) => HandleIncoming(data, true);

            // 判断是否在主线程后才调用？
            _webSocket.Open();

            token.Register(() =>
            {
                tcs.TrySetCanceled();
                Disconnect();
            });

            await tcs.Task.ConfigureAwait(false);
        }

        private void HandleIncoming(byte[] payload, bool isBinary)
        {
            try
            {
                WebSocketResponse webRsp = OnRawMessageReceived(payload);
                long seqId = webRsp.SeqId;
                LoggerUtil.Log($"BestHttpWebSocketAdapter: seqId={seqId}, msgType={webRsp.MsgType}, " +
                               $"webSocketResponse={webRsp.PushMessage} " +
                               $"response={webRsp.HttpResponse} ");
                if (_pending.TryRemove(seqId, out var tcs))
                    tcs.TrySetResult(webRsp.HttpResponse);
                else
                    OnPushMessage?.Invoke(webRsp.PushMessage);
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"[WebSocket] HandleIncoming error: {ex}");
            }
        }

        public void Disconnect()
        {
            if (_webSocket != null)
            {
                _webSocket.OnOpen = null;
                _webSocket.OnError = null;
                _webSocket.OnClosed = null;
                _webSocket.OnMessage = null;
                _webSocket.OnBinary = null;

                _webSocket.Close();
                _webSocket = null;
            }
            ClearPendingRequests("WebSocket disconnected");
        }


        /// <summary>
        /// 处理接收到的原始消息
        /// </summary>
        /// <param name="data">原始消息数据</param>
        private WebSocketResponse OnRawMessageReceived(byte[] data)
        {
            var webResponse = new WebSocketResponse();
            webResponse.MsgType = RspNotifyType.MsgTypeResponse;

            try
            {
                // 解析消息格式，与Android端保持一致：
                // ContentType(1字节) + 控制数据长度(4字节，小端序) + 控制数据 + 可选的body数据
                if (data == null || data.Length < 5)
                {
                    LoggerUtil.LogError($"{TAG}: Invalid message format, length too short");
                    webResponse.HttpResponse = new Response
                    {
                        NetCode = 200,
                        Code = ErrorCode.INVALID_CODE,
                        Message = "Invalid message format"
                    };

                    return webResponse;
                }


                int index = 0;

                // 1. 解析ContentType
                byte contentTypeByte = data[index];
                ContentType contentType;
                switch (contentTypeByte)
                {
                    case 0x0:
                        contentType = ContentType.Proto;
                        break;
                    case 0x1:
                        contentType = ContentType.Json;
                        break;
                    default:
                        LoggerUtil.LogError($"{TAG}: Unknown content type: {contentTypeByte}");
                        return new WebSocketResponse
                        {
                            HttpResponse = new Response
                            {
                                NetCode = 200,
                                Code = ErrorCode.INVALID_CODE,
                                Message = $"Unknown content type: {contentTypeByte}"
                            }
                        };
                }

                index += 1;

                // 2. 解析控制数据长度 (4字节，小端序，与Android保持一致)
                byte[] ctrlLenBytes = new byte[4];
                Array.Copy(data, index, ctrlLenBytes, 0, 4);

                // 确保使用小端序读取，与Android的ByteOrder.LITTLE_ENDIAN保持一致
                if (!BitConverter.IsLittleEndian)
                {
                    Array.Reverse(ctrlLenBytes);
                }

                int ctrlLen = BitConverter.ToInt32(ctrlLenBytes, 0);
                index += 4;

                // 3. 验证控制数据长度
                if (ctrlLen <= 0 || data.Length < (index + ctrlLen))
                {
                    LoggerUtil.LogError($"{TAG}: Invalid control length: {ctrlLen}, msg size: {data.Length}");
                    webResponse.HttpResponse = new Response
                    {
                        NetCode = 200,
                        Code = ErrorCode.INVALID_CODE,
                        Message = $"Invalid control length: {ctrlLen}"
                    };

                    return webResponse;
                }

                // 4. 提取控制数据
                byte[] ctrlData = new byte[ctrlLen];
                Array.Copy(data, index, ctrlData, 0, ctrlLen);
                index += ctrlLen;

                // 5. 提取body数据（如果有剩余数据）
                byte[] bodyData = null;
                if (data.Length > index)
                {
                    int bodyLen = data.Length - index;
                    bodyData = new byte[bodyLen];
                    Array.Copy(data, index, bodyData, 0, bodyLen);
                }

                // 6. 解析控制数据
                RspNotifyControl ctrl;
                try
                {
                    if (contentType == ContentType.Proto)
                    {
                        ctrl = Serializer.DeserializeFromProtoBuf<RspNotifyControl>(ctrlData);
                    }
                    else
                    {
                        ctrl = Serializer.DeserializeFromJson<RspNotifyControl>(Encoding.UTF8.GetString(ctrlData));
                    }
                }
                catch (Exception ex)
                {
                    LoggerUtil.LogError($"{TAG}: Control data parse error: {ex.Message}");
                    webResponse.HttpResponse = new Response
                    {
                        NetCode = 200,
                        Code = ErrorCode.INVALID_CODE,
                        Message = "Control data parse error"
                    };
                    return webResponse;
                }

                // 7. 构建响应对象，与Android逻辑保持一致

                if (ctrl.MsgType == RspNotifyType.MsgTypeNotify)
                {
                    webResponse.MsgType = ctrl.MsgType;
                    webResponse.SeqId = ctrl.Seqid;
                    PushMessage pushMessage = new PushMessage
                    {
                        RoomId = ctrl.RoomId,
                        CallId = ctrl.CallId,
                        SeqId = ctrl.Seqid,
                        NotifyPkg = ctrl.NotifyPkg,
                        Body = bodyData ?? Array.Empty<byte>(),
                        CompressType = ctrl.Compress,
                        ServerTimeMs = ctrl.TsMs
                    };
                    webResponse.PushMessage = pushMessage;
                    return webResponse;
                }
                else
                {
                    Response rsp = new Response
                    {
                        NetCode = 200, // HTTP_OK
                        Code = ctrl.Result?.Code ?? -1,
                        Message = ctrl.Result?.Message,
                        // Toast = ctrl.Toast?.Msg, // 如果需要的话可以启用
                        Body = bodyData,
                        Encrypt = ctrl.Encrypt,
                        ServerTime = ctrl.TsMs,
                        SeqId = ctrl.Seqid
                    };
                    webResponse.SeqId = ctrl.Seqid;
                    webResponse.HttpResponse = rsp;
                    return webResponse;
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"{TAG}: Parse message error: {ex.Message}");
                webResponse.HttpResponse = new Response
                {
                    NetCode = 200,
                    Code = ErrorCode.INVALID_CODE,
                    Message = "Parse message error"
                };

                return webResponse;
            }
        }

        private void ClearPendingRequests(string reason)
        {
            foreach (var kvp in _pending)
            {
                if (_pending.TryRemove(kvp.Key, out var tcs))
                {
                    var response = new Response
                    {
                        NetCode = 500,
                        NetMessage = reason,
                        Code = ErrorCode.INVALID_CODE,
                        Message = reason
                    };
                    tcs.TrySetResult(response);
                }
            }
        }

        public void Dispose() => Disconnect();

    }
}