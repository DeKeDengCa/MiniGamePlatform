using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NetworkFramework.Core.Model;
using NetworkFramework.Utils;

namespace NetworkFramework.Interceptor
{
    /// <summary>
    /// 业务报告拦截器，用于网络请求统计与上报
    /// </summary>
    public class BIReportInterceptor : IInterceptor
    {
        private const string TAG = "BIReportInterceptor";
        
        // 连续失败计数
        private Dictionary<string, int> _continuousFailureCount = new Dictionary<string, int>();

        public async Task<Response> Intercept(Request request, IInterceptorChain chain, CancellationToken token)
        {
            LoggerUtil.Log($"{TAG}: Intercept Invoke");
            
            // 执行下一个拦截器
            Response response;
            try
            {
                response = await chain.Proceed(request, token).ConfigureAwait(false);
                
            }
            catch (System.Exception e)
            {
               
                LoggerUtil.LogError($"{TAG}: Request failed with exception: {e.Message}");
                
                // 创建一个错误响应
                response = new Response
                {
                };
            }
            
            
            return response;
        }

    }
}