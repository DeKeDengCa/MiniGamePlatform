using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetworkFramework.Core.Model;
using NetworkFramework.Utils;
using Scommon;

namespace NetworkFramework.Interceptor
{
    /// <summary>
    /// 请求头拦截器，用于添加必要的HTTP请求头
    /// 基于Android端ConnectionManager.kt中的inconstantRequest方法逻辑实现
    /// </summary>
    public class HeaderInterceptor : IInterceptor
    {
        // 常量定义，与Android端保持一致
        private const string HEADER_TOKEN = "X-Token-Bin";
        private const string HEADER_PUB_PARAM = "X-Pubpara-Bin";
        private const string HEADER_REQ_CONTROL = "X-Reqcontrol-Bin";
        private const string HEADER_RESP_CONTROL = "X-Rspcontrol-Bin";
        private const string HEADER_CONTENT_TYPE = "Content-Type";
        private const string HEADER_ACCEPT = "Accept";
        private const string HEADER_PROTOBUF = "application/protobuf";
        private const string HEADER_JSON = "application/json";
        private const string HEADER_REJOIN = "X-Rejoin-Bin";


        /// <summary>
        /// 拦截请求并添加必要的header，同时解析响应中的header
        /// 对应Android端inconstantRequest方法中的header添加和解析逻辑
        /// </summary>
        /// <param name="chain">拦截器链</param>
        /// <param name="token">取消令牌</param>
        /// <returns>响应对象</returns>
        public async Task<Response> Intercept(Request request, IInterceptorChain chain, CancellationToken token)
        {
            // 克隆请求以避免修改原始请求
            var newRequest = request.Clone();

            // 添加上下文相关的header
            AddHeaders(newRequest);

            // 继续执行拦截器链并获取响应
            var response = await chain.Proceed(newRequest, token).ConfigureAwait(false);

            // 克隆响应以避免修改原始响应
            var newResponse = response.Clone();

            // 解析响应中的header信息
            ParseResponseHeaders(newResponse, request.ContentType);

            return newResponse;
        }

        /// <summary>
        /// 解析响应中的header信息
        /// 根据ConnectionManager.kt中的inconstantRequest方法中的响应处理逻辑实现
        /// </summary>
        /// <param name="response">响应对象</param>
        /// <param name="contentType">内容类型</param>
        private void ParseResponseHeaders(Response response, ContentType contentType)
        {
            // 如果响应头中包含HEADER_RESP_CONTROL，则进行解析处理（忽略大小写）
            // 对应Android端ConnectionManager.kt中inconstantRequest方法中的响应控制逻辑
            string responseControlValue = GetHeaderValueIgnoreCase(response.Headers, HEADER_RESP_CONTROL);
            if (!string.IsNullOrEmpty(responseControlValue))
            {
                // 这里可以根据需要实现对HEADER_RESP_CONTROL的具体解析逻辑
                // 例如提取响应控制信息并设置到Response对象的其他属性中
                response.ResponseControl = RestoreHeader(responseControlValue);
                RestoreData(response, contentType);
            }
        }

        /// <summary>
        /// 为请求添加必要的header
        /// 根据ConnectionManager.kt中的inconstantRequest方法实现
        /// </summary>
        /// <param name="request">HTTP请求对象</param>
        private void AddHeaders(Request request)
        {
            // 直接从Request对象中获取属性，无需通过Context
            string token = request.Token;
            ContentType contentType = request.ContentType;

            if (contentType == ContentType.Proto)
            {
                request.Headers[HEADER_CONTENT_TYPE] = HEADER_PROTOBUF;
                request.Headers[HEADER_ACCEPT] = HEADER_PROTOBUF;
            }
            else
            {
                request.Headers[HEADER_CONTENT_TYPE] = HEADER_JSON;
                request.Headers[HEADER_ACCEPT] = HEADER_JSON;
            }

            // 根据ConnectionManager.kt中的逻辑，为请求添加header
            if (request.PublicParams != null)
            {
                byte[] publicParamsData = request.PublicParams.Encode(contentType);
                string pubParamHeader = ConvertHeader(publicParamsData);
                request.Headers[HEADER_PUB_PARAM] = pubParamHeader;
            }

            if (request.RequestControl != null)
            {
                // 添加rout_ket逻辑
                var serviceName = request.RequestControl?.Service;
                if (!string.IsNullOrEmpty(serviceName) &&
                    RouteKeyRegistry.TryGetRouteKey(serviceName, out var routeKey))
                {
                    request.RequestControl.RouteKey = routeKey;
                }

                byte[] requestControlData = request.RequestControl.Encode(contentType);
                string reqControlHeader = ConvertHeader(requestControlData);
                request.Headers[HEADER_REQ_CONTROL] = reqControlHeader;
            }

            // 如果有token，则添加token header
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers[HEADER_TOKEN] = token;
            }
        }

        /// <summary>
        /// 转换header数据
        /// 对于JSON格式，为了安全和兼容性考虑，需要转为base64
        /// 对于PROTOBUF格式，直接转为UTF-8字符串
        /// </summary>
        /// <param name="headerData">header数据字节数组</param>
        /// <returns>转换后的字符串</returns>
        private string ConvertHeader(byte[] headerData)
        {
            if (headerData == null)
                throw new ArgumentNullException(nameof(headerData));

            // 先将byte[]数组进行base64编码
            string base64Encoded = Convert.ToBase64String(headerData);
            // 然后将base64编码后的字符串转换为UTF-8编码的字符串
            return Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(base64Encoded));
        }


        /// <summary>
        /// 恢复header数据
        /// 将字符串转换为UTF-8编码的byte[]数组
        /// </summary>
        /// <param name="data">字符串数据</param>
        /// <returns>UTF-8编码的byte[]数组</returns>
        private byte[] RestoreHeader(string data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            // 直接将字符串转换为Base64的byte[]数组
            return Convert.FromBase64String(data);
        }


        private void RestoreData(Response response, ContentType contentType)
        {
            RspNotifyControl rspNotifyControl = null;
            if (contentType == ContentType.Proto)
            {
                rspNotifyControl = Serializer.DeserializeFromProtoBuf<RspNotifyControl>(response.ResponseControl);
            }
            else if (contentType == ContentType.Json)
            {
                rspNotifyControl =
                    Serializer.DeserializeFromJson<RspNotifyControl>(Encoding.UTF8.GetString(response.ResponseControl));
            }

            if (rspNotifyControl != null)
            {
                response.Code = rspNotifyControl.Result.Code;
                response.Message = rspNotifyControl.Result.Message;
                // response.Toast = rspNotifyControl.Toast.Msg;
                response.Encrypt = rspNotifyControl.Encrypt;
                response.ServerTime = rspNotifyControl.TsMs;
                response.CompressType = rspNotifyControl.Compress;
                
                // 更新route_key
                RouteKeyRegistry.UpdateFromControl(rspNotifyControl);
            }
        }

        /// <summary>
        /// 忽略大小写获取Header值
        /// </summary>
        /// <param name="headers">Header字典</param>
        /// <param name="key">Header键名</param>
        /// <returns>Header值，如果不存在则返回null</returns>
        private string GetHeaderValueIgnoreCase(System.Collections.Generic.Dictionary<string, string> headers,
            string key)
        {
            if (headers == null || string.IsNullOrEmpty(key))
                return null;

            foreach (var kvp in headers)
            {
                if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value;
                }
            }

            return null;
        }

        /// <summary>
        /// 忽略大小写检查Header是否存在
        /// </summary>
        /// <param name="headers">Header字典</param>
        /// <param name="key">Header键名</param>
        /// <returns>是否存在</returns>
        private bool ContainsHeaderIgnoreCase(System.Collections.Generic.Dictionary<string, string> headers, string key)
        {
            if (headers == null || string.IsNullOrEmpty(key))
                return false;

            foreach (var kvp in headers)
            {
                if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 忽略大小写设置Header值（如果已存在相同名称的Header则先删除）
        /// </summary>
        /// <param name="headers">Header字典</param>
        /// <param name="key">Header键名</param>
        /// <param name="value">Header值</param>
        private void SetHeaderIgnoreCase(System.Collections.Generic.Dictionary<string, string> headers, string key,
            string value)
        {
            if (headers == null || string.IsNullOrEmpty(key))
                return;

            // 先删除已存在的相同名称的Header（忽略大小写）
            var keysToRemove = new System.Collections.Generic.List<string>();
            foreach (var kvp in headers)
            {
                if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (var keyToRemove in keysToRemove)
            {
                headers.Remove(keyToRemove);
            }

            // 添加新的Header
            headers[key] = value;
        }
    }
}