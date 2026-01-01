using System;
using System.Threading;
using System.Threading.Tasks;
using NetworkFramework.Core.Manager;
using NetworkFramework.Core.Model;
using NetworkFramework.Utils;

namespace NetworkFramework.Interceptor
{
    /// <summary>
    /// 加密拦截器，用于请求和响应数据的加密和解密
    /// </summary>
    public class EncryptInterceptor : IInterceptor
    {
        private const string TAG = "EncryptInterceptor";

        public async Task<Response> Intercept(Request request, IInterceptorChain chain, CancellationToken token)
        {
            // // 当前线程信息
            // var thread = Thread.CurrentThread;
            // LoggerUtil.Log($"{TAG}: Intercept Invoke on Thread " +
            //                   $"Name={thread.Name ?? "null"}, " +
            //                   $"ID={thread.ManagedThreadId}, " +
            //                   $"IsThreadPool={thread.IsThreadPoolThread}");

            if (request == null)
            {
                LoggerUtil.Log($"{TAG}: Request is null, skipping encryption processing");
                return await chain.Proceed(request, token);
            }

            // 根据连接类型获取加密信息
            var cryptoInfo = GetCryptoInfo(request);

            // 验证加密信息是否有效
            if (cryptoInfo == null)
            {
                return new Response
                {
                    NetCode = Error.INVALID_URL,
                    NetMessage = $"({TAG}) Unable to obtain valid encryption information!",
                    SeqId = request.RequestControl.SeqId
                };
            }

            // 检查加密信息是否有效
            if (cryptoInfo.Key == null || cryptoInfo.Key.Length == 0)
            {
                LoggerUtil.Log($"{TAG}: Invalid encryption information, key is empty");
                return EncryptFailResponse(new Exception("Invalid encryption information, key is empty"),
                    request.RequestControl.SeqId);
            }

            Request newReq = request;

            // 从短链加密持有者获取短链加密密钥，如果没有则使用默认格式
            // 长链的话由于建连时，需要发送http请求，所以也需要在header中加上密钥，如果是已经建连，则直接发送数据，那么会忽略掉header
            string shortEncryptKey = InconstantShortEncryptHolder.GetShortEncryptKey(cryptoInfo.Key) ??
                                     $"{cryptoInfo.PublicKeyNo},{cryptoInfo.Cache}";
            request.RequestControl.CryptoInfo = shortEncryptKey;

            // 加密请求体
            if (request.Body != null)
            {
                try
                {
                    byte[] encBody = HandleRequestEncryption(request, cryptoInfo);
                    // 处理空加密结果
                    if (encBody == null)
                    {
                        LoggerUtil.Log($"{TAG}: Failed to encrypt request body, returning empty data");
                        return EncryptFailResponse(new Exception("Data encryption failed, returning empty data"),
                            request.RequestControl.SeqId);
                    }

                    // 创建新的请求对象，设置加密后的body
                    newReq = request.Clone(body: encBody);
                }
                catch (Exception e)
                {
                    string base64data = Convert.ToBase64String(request.Body);
                    LoggerUtil.LogWarning(
                        $"{TAG}: error encrypting {cryptoInfo.PublicKeyNo}: {base64data}, Exception: {e.Message}");
                    return EncryptFailResponse(e, request.RequestControl.SeqId);
                }
            }

            // 执行下一个拦截器
            var response = await chain.Proceed(newReq, token);

            // 对于非持久连接类型，更新短期加密密钥
            if (request.UseConnectionType == ConnectionType.INCONSTANT && !string.IsNullOrEmpty(response.Encrypt))
            {
                InconstantShortEncryptHolder.UpdateShortEncryptKey(cryptoInfo.Key, response.Encrypt);
            }

            Response newResp = response;

            // 解密响应体
            if (response.Body != null)
            {
                try
                {
                    byte[] decBody = HandleResponseDecryption(response, cryptoInfo);
                    // 处理空解密结果
                    if (decBody == null)
                    {
                        LoggerUtil.Log($"{TAG}: Failed to decrypt response body, returning empty data");
                        return DecryptFailResponse(new Exception("Data decryption failed, returning empty data"),
                            request.RequestControl.SeqId);
                    }

                    // 创建新的响应对象，设置解密后的body
                    newResp = response.Clone(body: decBody);
                    return newResp;
                }
                catch (Exception e)
                {
                    string base64data = Convert.ToBase64String(response.Body);
                    LoggerUtil.LogWarning(
                        $"{TAG}: error decrypting {cryptoInfo.PublicKeyNo}: {base64data}, Exception: {e.Message}");
                    return DecryptFailResponse(e, request.RequestControl.SeqId);
                }
            }

            return newResp;
        }

        /// <summary>
        /// 处理请求加密
        /// </summary>
        private byte[] HandleRequestEncryption(Request request, CryptoInfo cryptoInfo)
        {
            if (request == null || cryptoInfo == null || cryptoInfo.Key == null)
            {
                return request?.Body;
            }

            try
            {
                // 获取请求体数据
                byte[] requestBody = request.Body;

                if (requestBody == null)
                {
                    return requestBody;
                }

                // 使用AES加密请求体数据
                byte[] encryptedData = CryptoHelper.NormalAESEncrypt(requestBody, cryptoInfo.Key);

                return encryptedData;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogWarning($"{TAG}: Failed to encrypt request: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 处理响应解密
        /// </summary>
        private byte[] HandleResponseDecryption(Response response, CryptoInfo cryptoInfo)
        {
            if (response == null || cryptoInfo == null || cryptoInfo.Key == null)
            {
                LoggerUtil.Log(
                    $"{TAG}: Decryption parameter check failed - response: {response != null}, cryptoInfo: {cryptoInfo != null}, key: {cryptoInfo?.Key != null}");
                return response?.Body;
            }

            try
            {
                // 获取响应体数据
                byte[] responseBody = response.Body;

                if (responseBody == null || responseBody.Length == 0)
                {
                    LoggerUtil.Log($"{TAG}: Response body is empty, skipping decryption");
                    return responseBody;
                }

                LoggerUtil.Log($"{TAG}: Starting to decrypt response data, length: {responseBody.Length}");
                LoggerUtil.Log($"{TAG}: Key length: {cryptoInfo.Key.Length}");

                // 使用AES解密响应体数据
                byte[] decryptedData = CryptoHelper.NormalAESDecrypt(responseBody, cryptoInfo.Key);

                if (decryptedData != null)
                {
                    LoggerUtil.Log($"{TAG}: Response decryption successful, decrypted length: {decryptedData.Length}");
                }
                else
                {
                    LoggerUtil.LogWarning($"{TAG}: Decryption returned null");
                }

                return decryptedData;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"{TAG}: Failed to decrypt response - {ex.GetType().Name}: {ex.Message}");
                LoggerUtil.LogError($"{TAG}: Exception stack trace: {ex.StackTrace}");
                return response.Body;
            }
        }

        /// <summary>
        /// 根据请求获取加密信息
        /// </summary>
        /// <param name="request">请求对象</param>
        /// <returns>加密信息对象</returns>
        private CryptoInfo GetCryptoInfo(Request request)
        {
            switch (request.UseConnectionType)
            {
                case ConnectionType.PERSISTENT:
                    // 验证持久连接URL
                    return CryptoManager.GetPersistentConnectionCryptoInfo(request.PersistentConnectionUrl);

                default:
                    // 对于其他连接类型，使用非持久连接的加密信息
                    return CryptoManager.GetInconstantConnectionCryptoInfo();
            }
        }

        /// <summary>
        /// 加密失败响应
        /// </summary>
        /// <param name="e">异常对象</param>
        /// <param name="seqId">序列ID</param>
        /// <returns>错误响应</returns>
        private Response EncryptFailResponse(Exception e, long seqId)
        {
            return new Response
            {
                NetCode = Error.DATA_ENCRYPT_FAIL,
                NetMessage = $"request data encrypt fail! type:{e.GetType().Name},msg:{e.Message}",
                SeqId = seqId
            };
        }

        /// <summary>
        /// 解密失败响应
        /// </summary>
        /// <param name="e">异常对象</param>
        /// <param name="seqId">序列ID</param>
        /// <returns>错误响应</returns>
        private Response DecryptFailResponse(Exception e, long seqId)
        {
            return new Response
            {
                NetCode = Error.DATA_DECRYPT_FAIL,
                NetMessage = $"response data decrypt fail! type:{e.GetType().Name},msg:{e.Message}",
                SeqId = seqId
            };
        }
    }


    /// <summary>
    /// 错误码定义
    /// </summary>
    public static class Error
    {
        public const int INVALID_URL = -1001;
        public const int DATA_ENCRYPT_FAIL = -1002;
        public const int DATA_DECRYPT_FAIL = -1003;
    }
}