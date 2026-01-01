using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BestHTTP;
using NetworkFramework.Core.Interface;
using NetworkFramework.Core.Manager;
using NetworkFramework.Core.Model;
using NetworkFramework.Utils;
using UnityEngine;

namespace NetworkFramework.Interceptor
{
    public class NetworkRequestInterceptor : IInterceptor
    {
        private const string TAG = "NetworkRequestInterceptor";

        private readonly INetworkAdapterSelector _selector;

        public NetworkRequestInterceptor(INetworkAdapterSelector selector)
        {
            _selector = selector;
        }

        public async Task<Response> Intercept(Request request, IInterceptorChain chain, CancellationToken token)
        {
            // var thread = Thread.CurrentThread;
            // LoggerUtil.Log($"{TAG}: Intercept Invoke on Thread " +
            //                $"Name={thread.Name ?? "null"}, " +
            //                $"ID={thread.ManagedThreadId}, " +
            //                $"IsThreadPool={thread.IsThreadPoolThread}");

            LoggerUtil.Log(
                $"{TAG}: Start request InconstantConnectionUrl : {request.InconstantConnectionUrl}, PersistentConnectionUrl : {request.PersistentConnectionUrl}");
            var adapter = _selector.SelectAdapter(request);
            return await adapter.Request(request, token);
        }
    }
}