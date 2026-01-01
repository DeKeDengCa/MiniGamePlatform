using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NetworkFramework.Core.Model;
using NetworkFramework.Runtime;
using UnityEngine;
using UnityEngine.Networking;

namespace NetworkFramework.Core.Adapter
{
    /// <summary>
    /// 商业化可用的 UnityWebRequest 桥接层，
    /// 将 UnityWebRequest 封装为 Task<byte[]>，
    /// 确保主线程安全、支持取消和异常处理。
    /// </summary>
    public static class UnityWebRequestBridge
    {
        /// <summary>
        /// 发起 GET 请求，返回响应字节数组。
        /// </summary>
        /// <param name="request">请求对象</param>
        /// <param name="token">取消令牌</param>
        /// <returns>响应字节数组</returns>
        public static Task<HttpResponseData> ExecuteRequestAsync(Request request, CancellationToken token)
        {
            var url = request.InconstantConnectionUrl;

            if (string.IsNullOrEmpty(url))
                throw new ArgumentException("URL cannot be null or empty", nameof(url));

            var tcs = new TaskCompletionSource<HttpResponseData>(TaskCreationOptions.RunContinuationsAsynchronously);

            // 确保在主线程发起 UnityWebRequest
            UnityMainThread.Post(async () =>
            {
                UnityWebRequest uwr = null;
                try
                {
                    switch (request.RequestControl.Method)
                    {
                        case "GET":
                            uwr = UnityWebRequest.Get(url);
                            break;

                        case "POST":
                            uwr = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
                            uwr.uploadHandler = new UploadHandlerRaw(request.Body ?? Array.Empty<byte>());
                            uwr.downloadHandler = new DownloadHandlerBuffer();
                            uwr.SetRequestHeader("Content-Type", "application/json");
                            break;

                        case "PUT":
                            uwr = UnityWebRequest.Put(url, request.Body ?? Array.Empty<byte>());
                            uwr.SetRequestHeader("Content-Type", "application/json");
                            break;

                        case "DELETE":
                            uwr = UnityWebRequest.Delete(url);
                            break;

                        default:
                            throw new NotSupportedException($"Unsupported method: {request.RequestControl.Method}");
                    }

                    var operation = uwr.SendWebRequest();

                    while (!operation.isDone)
                    {
                        if (token.IsCancellationRequested)
                        {
                            uwr.Abort();
                            tcs.TrySetException(new TaskCanceledException("Request canceled"));
                            return;
                        }

                        await Task.Yield();
                    }

                    var statusCode = (int)uwr.responseCode;

                    if (uwr.result == UnityWebRequest.Result.Success)
                    {
                        tcs.TrySetResult(new HttpResponseData
                        {
                            StatusCode = statusCode,
                            Body = uwr.downloadHandler.data
                        });
                    }
                    else
                    {
                        var errorMsg = $"UnityWebRequest failed: {uwr.error}, Code: {statusCode}";
                        Debug.LogError(errorMsg);
                        tcs.TrySetException(new Exception(errorMsg));
                    }
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
                finally
                {
                    uwr?.Dispose();
                }
            });

            return tcs.Task;
        }
    }

    // /// <summary>
    // /// 自定义 HttpRequestException，包含状态码。
    // /// </summary>
    // public class HttpRequestException : Exception
    // {
    //     public int StatusCode { get; }
    //
    //     public HttpRequestException(string message, int statusCode) : base(message)
    //     {
    //         StatusCode = statusCode;
    //     }
    // }
}