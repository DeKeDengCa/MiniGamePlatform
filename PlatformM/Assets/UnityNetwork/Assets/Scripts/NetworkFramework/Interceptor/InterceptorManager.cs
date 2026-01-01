using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using System.Threading.Tasks;
using NetworkFramework.Core.Model;

namespace NetworkFramework.Interceptor
{
    /// <summary>
    /// 拦截器管理器，负责管理和执行所有拦截器
    /// </summary>
    public class InterceptorManager
    {
        private List<IInterceptor> _interceptors = new List<IInterceptor>();

        /// <summary>
        /// 添加拦截器
        /// </summary>
        /// <param name="interceptor">拦截器实例</param>
        public void AddInterceptor(IInterceptor interceptor)
        {
            if (interceptor != null && !_interceptors.Contains(interceptor))
            {
                _interceptors.Add(interceptor);
            }
        }

        /// <summary>
        /// 移除拦截器
        /// </summary>
        /// <param name="interceptor">拦截器实例</param>
        public void RemoveInterceptor(IInterceptor interceptor)
        {
            if (interceptor != null && _interceptors.Contains(interceptor))
            {
                _interceptors.Remove(interceptor);
            }
        }

        /// <summary>
        /// 执行拦截器链
        /// </summary>
        /// <param name="request">请求对象</param>
        /// <returns>处理后的响应</returns>
        public async Task<Response> ExecuteInterceptorChainAsync(Request request,
            Func<Request, CancellationToken, Task<Response>> terminal, CancellationToken token)
        {
            // 创建拦截器链并执行
            var chain = new InterceptorChain(_interceptors, request, request.RequestControl.SeqId);
            return await chain.Proceed(request, token).ConfigureAwait(false);
        }

        /// <summary>
        /// 同步执行拦截器链（阻塞调用）
        /// </summary>
        /// <param name="request">请求对象</param>
        /// <returns>处理后的响应</returns>
        public Response ExecuteInterceptorChain(Request request,
            Func<Request, CancellationToken, Task<Response>> terminal, CancellationToken token)
        {
            // 阻塞等待异步方法完成
            return ExecuteInterceptorChainAsync(request, terminal, token).Result;
        }

        /// <summary>
        /// 清除所有拦截器
        /// </summary>
        public void ClearAllInterceptors()
        {
            _interceptors.Clear();
        }

        /// <summary>
        /// 获取拦截器数量
        /// </summary>
        public int Count => _interceptors.Count;
    }
}