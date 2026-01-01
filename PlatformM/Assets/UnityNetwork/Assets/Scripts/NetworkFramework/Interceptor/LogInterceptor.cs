using UnityEngine;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetworkFramework.Core.Model;
using NetworkFramework.Utils; // 添加NetworkTestUI所在命名空间

namespace NetworkFramework.Interceptor
{
    /// <summary>
    /// 日志拦截器，用于记录网络请求和响应的详细信息
    /// </summary>
    public class LogInterceptor : IInterceptor
    {
        private const string TAG = "LogInterceptor";
        
        public async Task<Response> Intercept(Request request, IInterceptorChain chain, CancellationToken token)
        {
            // 打印当前线程信息
            // var thread = Thread.CurrentThread;
            // Debug.Log($"{TAG}: Intercept Invoke on Thread " +
            //           $"Name={thread.Name ?? "null"}, " +
            //           $"ID={thread.ManagedThreadId}, " +
            //           $"IsThreadPool={thread.IsThreadPoolThread}");

            // 请求前的日志
            LogRequest(request);

            // 执行下一个拦截器
            var response = await chain.Proceed(request, token).ConfigureAwait(false);

            // 响应后的日志
            LogResponse(response);

            return response;
        }

        private void LogRequest(Request request)
        {
            var logBuilder = new StringBuilder();
            logBuilder.Append("<color=lightblue>【Request Intercepted】</color> ");
            logBuilder.AppendLine($"InconstantConnection URL: {request.InconstantConnectionUrl}");
            logBuilder.AppendLine($"PersistentConnection URL: {request.PersistentConnectionUrl}");

            // 记录请求体信息（简化显示）
            if (request.Body != null)
            {
                logBuilder.AppendLine($"Body Size: {request.Body.Length} bytes");
                try
                {
                    string bodyText = Encoding.UTF8.GetString(request.Body);
                    // 对JSON进行格式化显示
                    if (bodyText.Length > 100)
                    {
                        logBuilder.AppendLine("Body (truncated): " + bodyText.Substring(0, 100) + "...");
                    }
                    else
                    {
                        logBuilder.AppendLine("Body: " + bodyText);
                    }
                }
                catch
                {
                    logBuilder.AppendLine("Body: Binary data");
                }
            }
            
            LoggerUtil.Log($"{TAG}: {logBuilder}");
        }

        private void LogResponse(Response response)
        {
            StringBuilder logBuilder = new StringBuilder();
            // 根据状态码显示不同颜色
            string statusColor = response.NetCode >= 200 && response.NetCode < 300
                ? "<color=green>"
                : "<color=red>";
            logBuilder.Append($"<color=lightblue>【Response Intercepted】</color> ");
            logBuilder.AppendLine($"{statusColor}NetCode: {response.NetCode}, NetMessage: {response.NetMessage}</color>");
            logBuilder.AppendLine($"{statusColor}Code: {response.Code}, Message: {response.Message}</color>");

            // 记录响应体信息（简化显示）
            if (response.Body != null)
            {
                logBuilder.AppendLine($"Body Size: {response.Body.Length} bytes");
                try
                {
                    string bodyText = Encoding.UTF8.GetString(response.Body);
                    // 对JSON进行格式化显示
                    if (bodyText.Length > 100)
                    {
                        logBuilder.AppendLine("Body (truncated): " + bodyText.Substring(0, 100) + "...");
                    }
                    else
                    {
                        logBuilder.AppendLine("Body: " + bodyText);
                    }
                }
                catch
                {
                    logBuilder.AppendLine("Body: Binary data");
                }
            }

            LoggerUtil.Log($"{TAG}: {logBuilder}");
        }
        
    }
}