using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BestHTTP;
using NetworkFramework.Core.Interface;
using NetworkFramework.Core.Model;
using UnityEngine;

namespace NetworkFramework.Core.Adapter
{
    /// <summary>
    /// HTTP 网络适配器实现
    /// </summary>
    public class HttpNetworkAdapter : INetworkAdapter, IDisposable
    {
        // private string TAG = "HttpNetworkAdapter";
        private bool _isDisposed;

        public async Task<Response> Request(Request request, CancellationToken token = default)
        {
            // var thread = Thread.CurrentThread;
            // Debug.Log($"{TAG}: Intercept Invoke on Thread " +
            //           $"Name={thread.Name ?? "null"}, " +
            //           $"ID={thread.ManagedThreadId}, " +
            //           $"IsThreadPool={thread.IsThreadPoolThread}");

            // 使用BestHTTP发送请求
            return await SendRequestWithBestHTTP(request, token);
        }

        /// <summary>
        /// 使用BestHTTP发送网络请求
        /// </summary>
        private async Task<Response> SendRequestWithBestHTTP(Request request, CancellationToken token)
        {
            try
            {
                var tcs = new TaskCompletionSource<Response>();

                // 构造 BestHTTP 的请求
                var httpRequest = new HTTPRequest(new Uri(request.InconstantConnectionUrl), HTTPMethods.Post,
                    (req, resp) =>
                    {
                        if (resp == null)
                        {
                            tcs.TrySetException(new Exception("No response received"));
                            return;
                        }

                        var response = new Response
                        {
                            Headers = ConvertResponseHeaders(resp.Headers),
                            NetCode = resp.StatusCode,
                            NetMessage = resp.Message,
                            Body = resp.Data,
                            SeqId = request.RequestControl.SeqId
                        };

                        tcs.TrySetResult(response);
                    });
                httpRequest.Timeout = TimeSpan.FromSeconds(30); // 设置请求超时为30秒

                // 添加自定义请求头
                if (request.Headers != null)
                {
                    foreach (var header in request.Headers)
                    {
                        httpRequest.SetHeader(header.Key, header.Value);
                    }
                }

                // 设置请求体
                if (request.Body != null)
                {
                    httpRequest.RawData = request.Body;
                }

                // 设置请求方法 - 默认使用POST

                // 支持取消
                token.Register(() =>
                {
                    if (httpRequest.State < HTTPRequestStates.Finished)
                    {
                        httpRequest.Abort();
                        tcs.TrySetResult(new Response
                        {
                            NetCode = (int)ErrorCode.REQUEST_CANCELLED,
                            NetMessage = "Request cancelled",
                            Body = null,
                            SeqId = request.RequestControl.SeqId
                        });
                    }
                });


                // 发送请求
                httpRequest.Send();

                // 等待请求完成
                return await tcs.Task;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                throw;
            }
        }
        

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            
        }
        
        /// <summary>
        /// 将BestHTTP的Headers格式转换为业务Response所需的格式
        /// BestHTTP使用Dictionary<string, List<string>>，业务层使用Dictionary<string, string>
        /// 对于多值的Header，取第一个值
        /// </summary>
        private Dictionary<string, string> ConvertResponseHeaders(Dictionary<string, List<string>> bestHttpHeaders)
        {
            var headers = new Dictionary<string, string>();
    
            if (bestHttpHeaders == null)
                return headers;

            foreach (var kvp in bestHttpHeaders)
            {
                if (kvp.Value != null && kvp.Value.Count > 0)
                {
                    // 对于多值的Header，取第一个值
                    headers[kvp.Key] = kvp.Value[0];
                }
            }

            return headers;
        }
    }
    
}