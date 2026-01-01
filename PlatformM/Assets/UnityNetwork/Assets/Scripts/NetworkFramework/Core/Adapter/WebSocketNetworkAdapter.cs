using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using BestHTTP.WebSocket;
using UnityEngine;
using NetworkFramework.Core.Interface;
using NetworkFramework.Core.Model;
using NetworkFramework.Runtime;
using NetworkFramework.Utils;

namespace NetworkFramework.Core.Adapter
{
    /**
     * 已废弃
     */
    public class WebSocketNetworkAdapter : INetworkAdapter
{
    private WebSocket _socket;
    private CancellationTokenSource _ctsHeartbeat;
    private string _currentUrl;
    
    // 存储待处理的请求，以SeqId为键
    private readonly ConcurrentDictionary<long, TaskCompletionSource<Response>> _pendingRequests = new ConcurrentDictionary<long, TaskCompletionSource<Response>>();

    public event Action<byte[]> OnMessageReceived;
    public event Action OnConnected;
    public event Action<string> OnError;
    public event Action<ushort, string> OnClosed;

    public string CurrentUrl => _currentUrl;
    public bool IsConnected => _socket != null && _socket.IsOpen;


    public async Task<Response> Request(Request request, CancellationToken token)
    {
        // 确保连接已建立
        if (_socket == null || !_socket.IsOpen)
        {
            await ConnectAsync(request.PersistentConnectionUrl, token);
        }

        // 获取SeqId
        long seqId = request.RequestControl?.SeqId ?? 0;
        if (seqId == 0)
        {
            throw new ArgumentException("Request must have a valid SeqId in RequestControl");
        }

        // 创建TaskCompletionSource来等待响应
        var tcs = new TaskCompletionSource<Response>();
        
        // 将请求添加到待处理字典中
        if (!_pendingRequests.TryAdd(seqId, tcs))
        {
            throw new InvalidOperationException($"Request with SeqId {seqId} is already pending");
        }

        try
        {
            // 发送请求
            if (request.Body != null)
            {
                _socket.Send(request.Body);
            }

            // 设置超时处理
            var timeoutMs = request.TimeoutMs > 0 ? request.TimeoutMs : 30000; // 默认30秒超时
            using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(timeoutMs));
                
                // 等待响应或超时
                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(-1, timeoutCts.Token));
                
                if (completedTask == tcs.Task)
                {
                    return await tcs.Task;
                }
                else
                {
                    // 超时处理
                    _pendingRequests.TryRemove(seqId, out _);
                    throw new TimeoutException($"Request with SeqId {seqId} timed out after {timeoutMs}ms");
                }
            }
        }
        catch (Exception)
        {
            // 发生异常时清理待处理请求
            _pendingRequests.TryRemove(seqId, out _);
            throw;
        }
    }

    public async Task ConnectAsync(string url, CancellationToken token)
    {
        _currentUrl = url;
        _socket = new WebSocket(new Uri(url));

        _socket.OnOpen += (ws) =>
        {
            LoggerUtil.Log("WebSocket connected!");
            UnityMainThread.Post(() => OnConnected?.Invoke());
            StartHeartbeat();
        };

        _socket.OnError += (ws, ex) =>
        {
            string errorMsg = ex != null ? ex.Message : "Unknown error";
            LoggerUtil.LogError("WebSocket error: " + errorMsg);
            ClearPendingRequests($"WebSocket error: {errorMsg}");
            UnityMainThread.Post(() => OnError?.Invoke(errorMsg));
        };

        _socket.OnClosed += (ws, code, message) =>
        {
            LoggerUtil.Log($"WebSocket closed: {code} - {message}");
            StopHeartbeat();
            ClearPendingRequests($"WebSocket closed: {code} - {message}");
            UnityMainThread.Post(() => OnClosed?.Invoke(code, message));
        };

        _socket.OnMessage += (ws, message) =>
        {
            LoggerUtil.Log("WebSocket received text message: " + message);
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(message);
            UnityMainThread.Post(() => 
            {
                ProcessReceivedMessage(bytes);
                OnMessageReceived?.Invoke(bytes);
            });
        };

        _socket.OnBinary += (ws, data) =>
        {
            LoggerUtil.Log("WebSocket received binary data: " + data.Length + " bytes");
            UnityMainThread.Post(() => 
            {
                ProcessReceivedMessage(data);
                OnMessageReceived?.Invoke(data);
            });
        };

        // 开始连接
        _socket.Open();
        
        // 等待连接建立
        var tcs = new TaskCompletionSource<bool>();
        
        void onOpen(WebSocket ws) 
        {
            _socket.OnOpen -= onOpen;
            tcs.TrySetResult(true);
        }
        
        void onError(WebSocket ws, System.Exception ex)
        {
            _socket.OnError -= onError;
            tcs.TrySetException(ex ?? new Exception("WebSocket connection failed"));
        }
        
        _socket.OnOpen += onOpen;
        _socket.OnError += onError;
        
        await tcs.Task;
    }

    public async Task CloseAsync()
    {
        if (_socket != null)
        {
            StopHeartbeat();
            _socket.Close(1000, "Normal closure");
            _socket = null;
            _currentUrl = null;
        }
        
        // 清理所有待处理的请求
        ClearPendingRequests("Connection closed");
        
        await Task.CompletedTask;
    }

    private void StartHeartbeat()
    {
        _ctsHeartbeat = new CancellationTokenSource();
        var token = _ctsHeartbeat.Token;

        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (_socket != null && _socket.IsOpen)
                    {
                        _socket.Send("ping");
                    }
                }
                catch (Exception ex)
                {
                    LoggerUtil.LogError("Heartbeat error: " + ex.Message);
                }

                await Task.Delay(5000, token); // 每 5 秒发一次心跳
            }
        }, token);
    }

    private void StopHeartbeat()
    {
        _ctsHeartbeat?.Cancel();
        _ctsHeartbeat = null;
    }

    /// <summary>
    /// 处理接收到的消息，尝试根据SeqId匹配待处理的请求
    /// </summary>
    /// <param name="data">接收到的消息数据</param>
    private void ProcessReceivedMessage(byte[] data)
    {
        try
        {
            // 尝试解析消息中的SeqId
            long seqId = ExtractSeqIdFromMessage(data);
            
            if (seqId > 0 && _pendingRequests.TryRemove(seqId, out var tcs))
            {
                // 找到对应的请求，创建响应对象
                var response = new Response
                {
                    NetCode = 200,
                    Body = data,
                    // 可以根据需要添加更多字段的解析
                };
                
                // 完成对应的请求
                tcs.TrySetResult(response);
                LoggerUtil.Log($"Completed request with SeqId: {seqId}");
            }
            else
            {
                // 没有找到对应的请求，可能是服务器推送消息
                LoggerUtil.Log($"Received message without matching request, SeqId: {seqId}");
            }
        }
        catch (Exception ex)
        {
            LoggerUtil.LogError($"Error processing received message: {ex.Message}");
        }
    }

    /// <summary>
    /// 从消息数据中提取SeqId
    /// 这里需要根据实际的消息格式来实现
    /// </summary>
    /// <param name="data">消息数据</param>
    /// <returns>SeqId，如果无法提取则返回0</returns>
    private long ExtractSeqIdFromMessage(byte[] data)
    {
        try
        {
            // 这里需要根据实际的消息格式来解析SeqId
            // 示例：假设消息是JSON格式，包含seqId字段
            string jsonString = System.Text.Encoding.UTF8.GetString(data);
            
            // 简单的JSON解析，查找seqId字段
            // 注意：这是一个简化的实现，实际项目中应该使用更健壮的JSON解析
            if (jsonString.Contains("\"seqId\"") || jsonString.Contains("\"seqid\""))
            {
                // 使用正则表达式或JSON解析库来提取seqId
                // 这里使用简单的字符串查找方式
                var patterns = new[] { "\"seqId\":", "\"seqid\":" };
                
                foreach (var pattern in patterns)
                {
                    int index = jsonString.IndexOf(pattern);
                    if (index >= 0)
                    {
                        int start = index + pattern.Length;
                        int end = jsonString.IndexOfAny(new char[] { ',', '}', ' ', '\n', '\r' }, start);
                        if (end > start)
                        {
                            string seqIdStr = jsonString.Substring(start, end - start).Trim();
                            if (long.TryParse(seqIdStr, out long seqId))
                            {
                                return seqId;
                            }
                        }
                    }
                }
            }
            
            // 如果是ProtoBuf格式，需要根据具体的proto定义来解析
            // 这里暂时返回0，表示无法提取SeqId
            return 0;
        }
        catch (Exception ex)
        {
            LoggerUtil.LogError($"Error extracting SeqId from message: {ex.Message}");
            return 0;
         }
     }

    /// <summary>
    /// 清理所有待处理的请求
    /// </summary>
    /// <param name="reason">清理原因</param>
    private void ClearPendingRequests(string reason)
    {
        foreach (var kvp in _pendingRequests)
        {
            var tcs = kvp.Value;
            var response = new Response
            {
                NetCode = 500,
                NetMessage = reason,
                Body = System.Text.Encoding.UTF8.GetBytes(reason)
            };
            
            tcs.TrySetResult(response);
        }
        
        _pendingRequests.Clear();
        LoggerUtil.Log($"Cleared {_pendingRequests.Count} pending requests due to: {reason}");
    }
}
}