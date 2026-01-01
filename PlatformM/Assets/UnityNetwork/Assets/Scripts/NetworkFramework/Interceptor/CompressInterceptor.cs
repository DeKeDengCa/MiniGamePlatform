using System.IO;
using System.IO.Compression;
using UnityEngine;
using System.Threading.Tasks;
using System;
using System.Threading;
using NetworkFramework.Core.Model;
using NetworkFramework.Utils;
using Scommon;

namespace NetworkFramework.Interceptor
{
 
    /// <summary>
    /// 压缩拦截器，用于请求和响应数据的压缩和解压缩
    /// </summary>
    public class CompressInterceptor : IInterceptor
    {
        private const string TAG = "CompressInterceptor";
        private const int MIN_COMPRESS_SIZE = 1024; // 小于1KB不压缩

        public async Task<Response> Intercept(Request request, IInterceptorChain chain, CancellationToken token)
        {
            // // 打印当前线程信息
            // var thread = Thread.CurrentThread;
            // LoggerUtil.Log($"{TAG}: Intercept Invoke on Thread " +
            //           $"Name={thread.Name ?? "null"}, " +
            //           $"ID={thread.ManagedThreadId}, " +
            //           $"IsThreadPool={thread.IsThreadPoolThread}");
            
            Response newResponse;
            
            // 检查是否需要压缩
            bool useGzip = IsLargeEnoughToCompress(request.Body);
            
            // 设置压缩类型
            if (useGzip)
            {
                request.RequestControl.CompressType = CompressType.Gzip;
            }
            else
            {
                request.RequestControl.CompressType = CompressType.None;
            }
            
            LoggerUtil.Log($"{TAG}: request data gzip? {useGzip}");
            
            // 执行压缩
            var newReq = request.Clone();
            if (useGzip && request.Body != null)
            {
                try
                {
                    byte[] compressedData = GzipUtil.CompressGzip(request.Body);
                    newReq = request.Clone(body: compressedData);
                }
                catch (Exception e)
                {
                    // 压缩失败，记录日志并返回错误响应
                    string base64Data = Convert.ToBase64String(request.Body);
                    LoggerUtil.LogError($"{TAG}: request data compress fail! body:{base64Data}");
                    LoggerUtil.LogException(e);
                    return CompressFailResponse(request.RequestControl.SeqId);
                }
            }
            
            // 执行下一个拦截器
            var response = await chain.Proceed(newReq, token).ConfigureAwait(false);
            newResponse = response.Clone();
            
            // 解压响应数据
            if (response.CompressType == CompressType.Gzip && response.Body != null)
            {
                try
                {
                    byte[] decompressedData = GzipUtil.DecompressGzip(response.Body);
                    newResponse = response.Clone(
                        compressType: CompressType.None,
                        body: decompressedData
                    );
                }
                catch (Exception e)
                {
                    // 解压失败，记录日志并返回错误响应
                    string base64Data = Convert.ToBase64String(response.Body);
                    LoggerUtil.LogError($"{TAG}: response data decompress fail! body:{base64Data}");
                    LoggerUtil.LogException(e);
                    return DecompressFailResponse(request.RequestControl.SeqId);
                }
            }
            
            return newResponse;
        }

        private Response CompressFailResponse(long seqId)
        {
            return new Response()
            {
                NetCode = ErrorCode.DATA_COMPRESS_FAIL,
                Message = "request data compress fail!",
                SeqId = seqId
            };
        }

        private Response DecompressFailResponse(long seqId)
        {
            return new Response()
            {
                NetCode = ErrorCode.DATA_DECOMPRESS_FAIL,
                Message = "response data decompress fail!",
                SeqId = seqId
            };
        }

        private bool IsLargeEnoughToCompress(byte[] data)
        {
            if (data == null)
            {
                return false;
            }
            return data.Length > MIN_COMPRESS_SIZE;
        }

    }
}