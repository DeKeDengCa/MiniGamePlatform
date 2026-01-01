using System;
using System.Collections.Generic;
using UnityEngine;
using Astorise.Framework.SDK;

namespace Astorise.Framework.Network
{
    /// <summary>
    /// 登录管理器：负责处理用户登录、登出以及登录结果的本地缓存。
    /// </summary>
    public class LoginManager
    {
        #region 单例模式

        /// <summary>
        /// LoginManager 单例实例。
        /// </summary>
        public static LoginManager Instance = new LoginManager();

        #endregion

        #region 字段定义

        /// <summary>
        /// 当前登录请求的登录结果 key（用于保存缓存）。
        /// </summary>
        private string _currentLoginResultKey;

        /// <summary>
        /// PlayerPrefs 键名前缀，用于登录结果缓存。
        /// </summary>
        private const string LoginResultKeyPrefix = "LoginResult_";

        /// <summary>
        /// 当前登录请求的回调（用于处理登录完成回调）。
        /// </summary>
        private Action<LoginResult, Exception> _currentLoginCallback;

        /// <summary>
        /// 当前登出请求的回调（用于处理登出完成回调）。
        /// </summary>
        private Action<bool, Exception> _currentLogoutCallback;

        /// <summary>
        /// 当前登出请求的登录结果 key（用于清除缓存）。
        /// </summary>
        private string _currentLogoutResultKey;

        #endregion

        #region 公共方法

        /// <summary>
        /// 登录。
        /// </summary>
        /// <param name="loginType">登录类型（例如 google/apple/did 等）</param>
        /// <param name="loginResultKey">登录结果缓存 key（用于复用缓存登录态）</param>
        /// <param name="tryUseCachedResult">是否尝试直接使用本地缓存的登录结果</param>
        /// <param name="callback">完成回调：LoginResult + Exception（exception 非空表示失败/取消）</param>
        public void Login(string loginType, string loginResultKey, bool tryUseCachedResult, Action<LoginResult, Exception> callback)
        {
            if (callback == null)
            {
#if UNITY_DEBUG
                Debug.LogError("[LoginManager] Login: callback 不能为 null");
#endif
                return;
            }

            string cacheKey = GetCacheKey(loginResultKey);

            if (tryUseCachedResult)
            {
                LoginResult cachedResult = LoadCachedLoginResult(cacheKey);
                if (cachedResult != null)
                {
#if UNITY_DEBUG
                    Debug.Log($"[LoginManager] Login: 使用缓存的登录结果，uid={cachedResult.Uid}");
#endif
                    callback(cachedResult, null);
                    return;
                }
            }

            _currentLoginCallback = callback;
            _currentLoginResultKey = loginResultKey;

            if (UnityBridge.Instance == null)
            {
                Exception exception = new InvalidOperationException("UnityBridge.Instance 为 null，无法调用登录");
#if UNITY_DEBUG
                Debug.LogError($"[LoginManager] Login: {exception.Message}");
#endif
                _currentLoginCallback = null;
                callback(null, exception);
                return;
            }

            UnityBridge.Instance.Login(loginType, null);
        }

        /// <summary>
        /// 登出。
        /// </summary>
        /// <param name="loginResultKey">登录结果缓存 key</param>
        /// <param name="callback">完成回调：成功标记 + Exception</param>
        public void Logout(string loginResultKey, Action<bool, Exception> callback)
        {
            if (callback == null)
            {
#if UNITY_DEBUG
                Debug.LogError("[LoginManager] Logout: callback 不能为 null");
#endif
                return;
            }

            _currentLogoutCallback = callback;
            _currentLogoutResultKey = loginResultKey;

            if (UnityBridge.Instance == null)
            {
                Exception exception = new InvalidOperationException("UnityBridge.Instance 为 null，无法调用登出");
#if UNITY_DEBUG
                Debug.LogError($"[LoginManager] Logout: {exception.Message}");
#endif
                _currentLogoutCallback = null;
                _currentLogoutResultKey = null;
                callback(false, exception);
                return;
            }

            UnityBridge.Instance.Logout(null);
        }

        #endregion

        #region 内部方法

        /// <summary>
        /// 处理登录完成回调：由 UnityBridge 调用。
        /// </summary>
        /// <param name="ext">扩展参数，包含登录结果数据</param>
        internal void OnLoginCompletion(Dictionary<string, object> ext)
        {
            Action<LoginResult, Exception> callback = _currentLoginCallback;
            _currentLoginCallback = null;

            if (callback == null)
            {
#if UNITY_DEBUG
                Debug.LogError("[LoginManager] OnLoginCompletion: callback 为 null");
#endif
                return;
            }

            try
            {
                LoginResult loginResult = ParseLoginResult(ext);
                if (loginResult == null)
                {
                    Exception exception = new InvalidOperationException("解析登录结果失败");
#if UNITY_DEBUG
                    Debug.LogError($"[LoginManager] OnLoginCompletion: {exception.Message}");
#endif
                    callback(null, exception);
                    return;
                }

                string loginResultKey = _currentLoginResultKey;
                _currentLoginResultKey = null;

                if (!string.IsNullOrEmpty(loginResultKey))
                {
                    SaveLoginResult(loginResultKey, loginResult);
                }

                callback(loginResult, null);
            }
            catch (Exception exception)
            {
#if UNITY_DEBUG
                Debug.LogError($"[LoginManager] OnLoginCompletion: 处理登录完成回调异常: {exception.Message}");
#endif
                callback(null, exception);
            }
        }

        /// <summary>
        /// 处理登出完成回调：由 UnityBridge 调用。
        /// </summary>
        /// <param name="ext">扩展参数</param>
        internal void OnLogoutCompletion(Dictionary<string, object> ext)
        {
            Action<bool, Exception> callback = _currentLogoutCallback;
            string loginResultKey = _currentLogoutResultKey;
            _currentLogoutCallback = null;
            _currentLogoutResultKey = null;

            if (callback == null)
            {
#if UNITY_DEBUG
                Debug.LogError("[LoginManager] OnLogoutCompletion: callback 为 null");
#endif
                return;
            }

            try
            {
                if (!string.IsNullOrEmpty(loginResultKey))
                {
                    ClearLoginResult(loginResultKey);
                }

                callback(true, null);
            }
            catch (Exception exception)
            {
#if UNITY_DEBUG
                Debug.LogError($"[LoginManager] OnLogoutCompletion: 处理登出完成回调异常: {exception.Message}");
#endif
                callback(false, exception);
            }
        }

        /// <summary>
        /// 保存登录结果到本地缓存。
        /// </summary>
        /// <param name="loginResultKey">登录结果缓存 key</param>
        /// <param name="loginResult">登录结果</param>
        private void SaveLoginResult(string loginResultKey, LoginResult loginResult)
        {
            if (loginResult == null)
            {
                return;
            }

            string cacheKey = GetCacheKey(loginResultKey);

            try
            {
                string jsonString = JsonUtility.ToJson(loginResult);
                PlayerPrefs.SetString(cacheKey, jsonString);
                PlayerPrefs.Save();
#if UNITY_DEBUG
                Debug.Log($"[LoginManager] SaveLoginResult: 保存登录结果成功，key={cacheKey}, uid={loginResult.Uid}");
#endif
            }
            catch (Exception exception)
            {
#if UNITY_DEBUG
                Debug.LogError($"[LoginManager] SaveLoginResult: 保存登录结果失败: {exception.Message}");
#endif
            }
        }

        /// <summary>
        /// 清除登录结果缓存。
        /// </summary>
        /// <param name="loginResultKey">登录结果缓存 key</param>
        private void ClearLoginResult(string loginResultKey)
        {
            string cacheKey = GetCacheKey(loginResultKey);

            try
            {
                if (PlayerPrefs.HasKey(cacheKey))
                {
                    PlayerPrefs.DeleteKey(cacheKey);
                    PlayerPrefs.Save();
#if UNITY_DEBUG
                    Debug.Log($"[LoginManager] ClearLoginResult: 清除登录结果缓存成功，key={cacheKey}");
#endif
                }
            }
            catch (Exception exception)
            {
#if UNITY_DEBUG
                Debug.LogError($"[LoginManager] ClearLoginResult: 清除登录结果缓存失败: {exception.Message}");
#endif
            }
        }

        /// <summary>
        /// 从本地缓存读取登录结果。
        /// </summary>
        /// <param name="cacheKey">完整的缓存 key</param>
        /// <returns>登录结果，如果不存在或解析失败则返回 null</returns>
        private LoginResult LoadCachedLoginResult(string cacheKey)
        {
            if (!PlayerPrefs.HasKey(cacheKey))
            {
                return null;
            }

            try
            {
                string jsonString = PlayerPrefs.GetString(cacheKey);
                if (string.IsNullOrEmpty(jsonString))
                {
                    return null;
                }

                LoginResult loginResult = JsonUtility.FromJson<LoginResult>(jsonString);
                return loginResult;
            }
            catch (Exception exception)
            {
#if UNITY_DEBUG
                Debug.LogError($"[LoginManager] LoadCachedLoginResult: 读取登录结果缓存失败: {exception.Message}");
#endif
                return null;
            }
        }

        /// <summary>
        /// 从原生回调数据中解析登录结果。
        /// </summary>
        /// <param name="ext">扩展参数，包含登录结果数据</param>
        /// <returns>解析后的登录结果，如果解析失败则返回 null</returns>
        private LoginResult ParseLoginResult(Dictionary<string, object> ext)
        {
            Dictionary<string, object> loginData = (Dictionary<string, object>)ext["data"];

            LoginResult loginResult = new LoginResult();

            loginResult.Type = loginData["type"] as string ?? string.Empty;

            loginResult.Uid = (long)loginData["uid"];

            loginResult.IsFirstRegister = (bool)loginData["isFirstRegister"];

            loginResult.AccessToken = loginData["access_token"] as string ?? string.Empty;

            loginResult.RefreshToken = loginData["refresh_token"] as string ?? string.Empty;

            loginResult.AvatarUrl = (string)loginData["avatarUrl"];

            loginResult.UserName = loginData["username"] as string ?? string.Empty;

            return loginResult;
        }

        /// <summary>
        /// 获取完整的缓存 key。
        /// </summary>
        /// <param name="loginResultKey">登录结果缓存 key</param>
        /// <returns>完整的缓存 key</returns>
        private string GetCacheKey(string loginResultKey)
        {
            if (string.IsNullOrEmpty(loginResultKey))
            {
                return LoginResultKeyPrefix + "default";
            }
            return LoginResultKeyPrefix + loginResultKey;
        }

        #endregion
    }
}
