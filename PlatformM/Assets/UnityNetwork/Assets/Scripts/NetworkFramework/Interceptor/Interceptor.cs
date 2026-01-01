using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NetworkFramework.Core.Model;

namespace NetworkFramework.Interceptor
{
    /// <summary>
    /// 拦截器接口，用于处理网络请求和响应
    /// </summary>
    public interface IInterceptor
    {
        /// <summary>
        /// 拦截处理方法
        /// </summary>
        /// <param name="chain">拦截器链</param>
        /// <returns>处理后的响应</returns>
        Task<Response> Intercept(Request request, IInterceptorChain chain, CancellationToken token);
    }

    /// <summary>
    /// 拦截器链接口
    /// </summary>
    public interface IInterceptorChain
    {
        /// <summary>
        /// 获取当前请求ID
        /// </summary>
        long Id();

        /// <summary>
        /// 处理下一个拦截器
        /// </summary>
        /// <param name="request">请求对象</param>
        /// <returns>响应对象</returns>
        Task<Response> Proceed(Request request, CancellationToken token);
    }


    /// <summary>
    /// 拦截器链实现
    /// </summary>
    public class InterceptorChain : IInterceptorChain
    {
        private readonly IList<IInterceptor> _interceptors;
        private readonly Request _request;
        private readonly long _id;
        private int _startIndex;

        public InterceptorChain(IList<IInterceptor> interceptors, Request request, long id)
        {
            _interceptors = interceptors;
            _startIndex = 0;
            _request = request;
            _id = id;
        }


        public long Id()
        {
            return _id;
        }

        public Task<Response> Proceed(Request request, CancellationToken token)
        {
            if (_startIndex >= _interceptors.Count)
            {
                throw new InvalidOperationException(
                    "InterceptorChain reached end without terminal. Did you forget to add NetworkRequestInterceptor?");
            }

            // 下一个拦截器
            var next = _interceptors[_startIndex++];
            return next.Intercept(request, this, token);
        }
    }
}