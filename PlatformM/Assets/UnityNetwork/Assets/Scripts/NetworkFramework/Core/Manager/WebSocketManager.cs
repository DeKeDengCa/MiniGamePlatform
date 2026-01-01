using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using NetworkFramework.Core.Adapter;
using NetworkFramework.Core.Interface;
using NetworkFramework.Core.Model;

namespace NetworkFramework.Core.Manager
{
    /// <summary>
    /// 全局多连接管理器
    /// </summary>
    public sealed class WebSocketManager : INetworkAdapter
    {
        private static readonly Lazy<WebSocketManager> _instance = new(() => new WebSocketManager());

        public static WebSocketManager Instance => _instance.Value;

        private readonly ConcurrentDictionary<string, BestHttpWebSocketAdapter> _connections = new();


        private WebSocketManager()
        {
        }

        public async Task Connect(Request request, CancellationToken token)
        {
            var url = request.PersistentConnectionUrl;

            if (!_connections.TryGetValue(url, out var adapter))
            {
                adapter = new BestHttpWebSocketAdapter();
                adapter.OnPushMessage += (resp) => OnPushMessage?.Invoke(url, resp);
                adapter.OnConnectionClosed += (code, msg) => OnConnectionClosed?.Invoke(url, code, msg);
                adapter.OnConnectionError += (msg) => OnConnectionError?.Invoke(url, msg);

                _connections[url] = adapter;
            }

            if (!adapter.IsConnected)
            {
                await adapter.ConnectAsync(request, token);
            }
        }

        public async Task<Response> Request(Request request, CancellationToken token)
        {
            // 直接复用 Connect 逻辑
            var url = request.PersistentConnectionUrl;
            await Connect(request, token);
            var adapter = _connections[url];
            return await adapter.Request(request, token);
        }

        public void Disconnect(string url)
        {
            if (_connections.TryRemove(url, out var adapter))
                adapter.Disconnect();
        }

        public void DisconnectAll()
        {
            foreach (var kv in _connections)
                kv.Value.Disconnect();
            _connections.Clear();
        }

        public bool IsConnected(string url) =>
            _connections.TryGetValue(url, out var adapter) && adapter.IsConnected;

        public event Action<string, PushMessage> OnPushMessage;
        public event Action<string, ushort, string> OnConnectionClosed;
        public event Action<string, string> OnConnectionError;
    }
}