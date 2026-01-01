using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace Astorise.Framework.AOT
{
    /// <summary>
    /// 程序集引用管理器：负责管理已加载的 DLL 程序集引用，并提供反射调用功能。
    /// </summary>
    public static class AssemblyReferenceManager
    {
        #region 字段定义

        /// <summary>
        /// 已注册的程序集字典（key: 程序集名称, value: Assembly 对象）。
        /// </summary>
        private static readonly Dictionary<string, Assembly> _registeredAssemblies = new Dictionary<string, Assembly>();

        #endregion

        #region 注册程序集

        /// <summary>
        /// 注册程序集到管理器。
        /// </summary>
        /// <param name="assemblyName">程序集名称</param>
        /// <param name="assembly">程序集对象</param>
        public static void RegisterAssembly(string assemblyName, Assembly assembly)
        {
            if (string.IsNullOrEmpty(assemblyName))
            {
#if UNITY_DEBUG
                Debug.LogError("[AssemblyReferenceManager] 注册程序集失败：程序集名称为空");
#endif
                return;
            }

            if (assembly == null)
            {
#if UNITY_DEBUG
                Debug.LogError($"[AssemblyReferenceManager] 注册程序集失败：程序集对象为 null，assemblyName={assemblyName}");
#endif
                return;
            }

            _registeredAssemblies[assemblyName] = assembly;

#if UNITY_DEBUG
            Debug.Log($"[AssemblyReferenceManager] 程序集注册成功：{assemblyName}");
#endif
        }

        /// <summary>
        /// 获取已注册的程序集。
        /// </summary>
        /// <param name="assemblyName">程序集名称</param>
        /// <returns>程序集对象，如果未注册则返回 null</returns>
        public static Assembly GetAssembly(string assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName))
            {
#if UNITY_DEBUG
                Debug.LogError("[AssemblyReferenceManager] 获取程序集失败：程序集名称为空");
#endif
                return null;
            }

            Assembly assembly;
            if (_registeredAssemblies.TryGetValue(assemblyName, out assembly))
            {
                return assembly;
            }

#if UNITY_DEBUG
            Debug.LogError($"[AssemblyReferenceManager] 程序集未注册：{assemblyName}");
#endif
            return null;
        }

        /// <summary>
        /// 检查程序集是否已注册。
        /// </summary>
        /// <param name="assemblyName">程序集名称</param>
        /// <returns>是否已注册</returns>
        public static bool IsRegistered(string assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName))
            {
                return false;
            }

            return _registeredAssemblies.ContainsKey(assemblyName);
        }

        #endregion

        #region 反射调用函数（0个参数）

        /// <summary>
        /// 调用指定程序集中指定类型的指定静态方法（无参数）- 路由方法。
        /// Editor 下直接调用，Runtime 下使用 Assembly。
        /// </summary>
        /// <param name="assemblyName">程序集名称</param>
        /// <param name="typeName">类型全名（包含命名空间）</param>
        /// <param name="methodName">方法名</param>
        public static void InvokeMethod(string assemblyName, string typeName, string methodName)
        {
#if UNITY_EDITOR
            InvokeMethodEditor(assemblyName, typeName, methodName);
#else
            InvokeMethodRuntime(assemblyName, typeName, methodName);
#endif
        }

        /// <summary>
        /// Runtime 版本：调用指定程序集中指定类型的指定静态方法（无参数）。
        /// </summary>
        /// <param name="assemblyName">程序集名称</param>
        /// <param name="typeName">类型全名（包含命名空间）</param>
        /// <param name="methodName">方法名</param>
        private static void InvokeMethodRuntime(string assemblyName, string typeName, string methodName)
        {
            try
            {
                Assembly assembly = GetAssembly(assemblyName);
                if (assembly == null)
                {
                    return;
                }

                Type type = assembly.GetType(typeName);
                if (type == null)
                {
#if UNITY_DEBUG
                    Debug.LogError($"[AssemblyReferenceManager] Runtime 模式：类型未找到：{typeName}，程序集：{assemblyName}");
#endif
                    return;
                }

                MethodInfo methodInfo = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
                if (methodInfo == null)
                {
#if UNITY_DEBUG
                    Debug.LogError($"[AssemblyReferenceManager] Runtime 模式：方法未找到：{methodName}，类型：{typeName}，程序集：{assemblyName}");
#endif
                    return;
                }

                methodInfo.Invoke(null, null);
            }
            catch (Exception exception)
            {
#if UNITY_DEBUG
                Debug.LogError($"[AssemblyReferenceManager] Runtime 模式：调用方法失败：{methodName}，类型：{typeName}，程序集：{assemblyName}，异常：{exception.Message}");
#endif
            }
        }

        /// <summary>
        /// Editor 版本：调用指定程序集中指定类型的指定静态方法（无参数）- 直接使用反射。
        /// </summary>
        /// <param name="assemblyName">程序集名称</param>
        /// <param name="typeName">类型全名（包含命名空间）</param>
        /// <param name="methodName">方法名</param>
        private static void InvokeMethodEditor(string assemblyName, string typeName, string methodName)
        {
            try
            {
                Assembly assembly = Assembly.Load(assemblyName);
                if (assembly == null)
                {
#if UNITY_DEBUG
                    Debug.LogError($"[AssemblyReferenceManager] Editor 模式：程序集加载失败：{assemblyName}");
#endif
                    return;
                }

                Type type = assembly.GetType(typeName);
                if (type == null)
                {
#if UNITY_DEBUG
                    Debug.LogError($"[AssemblyReferenceManager] Editor 模式：类型未找到：{typeName}，程序集：{assemblyName}");
#endif
                    return;
                }

                MethodInfo methodInfo = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
                if (methodInfo == null)
                {
#if UNITY_DEBUG
                    Debug.LogError($"[AssemblyReferenceManager] Editor 模式：方法未找到：{methodName}，类型：{typeName}，程序集：{assemblyName}");
#endif
                    return;
                }

                methodInfo.Invoke(null, null);
            }
            catch (Exception exception)
            {
#if UNITY_DEBUG
                Debug.LogError($"[AssemblyReferenceManager] Editor 模式：调用方法失败：{methodName}，类型：{typeName}，程序集：{assemblyName}，异常：{exception.Message}");
#endif
            }
        }

        #endregion

        #region 反射调用函数（1个参数）

        /// <summary>
        /// 调用指定程序集中指定类型的指定静态方法（1个参数）- 路由方法。
        /// Editor 下直接调用，Runtime 下使用 Assembly。
        /// </summary>
        /// <typeparam name="T1">参数1类型</typeparam>
        /// <param name="assemblyName">程序集名称</param>
        /// <param name="typeName">类型全名（包含命名空间）</param>
        /// <param name="methodName">方法名</param>
        /// <param name="arg1">参数1</param>
        public static void InvokeMethod<T1>(string assemblyName, string typeName, string methodName, T1 arg1)
        {
#if UNITY_EDITOR
            InvokeMethodEditor(assemblyName, typeName, methodName, arg1);
#else
            InvokeMethodRuntime(assemblyName, typeName, methodName, arg1);
#endif
        }

        /// <summary>
        /// Runtime 版本：调用指定程序集中指定类型的指定静态方法（1个参数）。
        /// </summary>
        /// <typeparam name="T1">参数1类型</typeparam>
        /// <param name="assemblyName">程序集名称</param>
        /// <param name="typeName">类型全名（包含命名空间）</param>
        /// <param name="methodName">方法名</param>
        /// <param name="arg1">参数1</param>
        private static void InvokeMethodRuntime<T1>(string assemblyName, string typeName, string methodName, T1 arg1)
        {
            try
            {
                Assembly assembly = GetAssembly(assemblyName);
                if (assembly == null)
                {
                    return;
                }

                Type type = assembly.GetType(typeName);
                if (type == null)
                {
#if UNITY_DEBUG
                    Debug.LogError($"[AssemblyReferenceManager] Runtime 模式：类型未找到：{typeName}，程序集：{assemblyName}");
#endif
                    return;
                }

                Type[] parameterTypes = new Type[] { typeof(T1) };
                MethodInfo methodInfo = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static, null, parameterTypes, null);
                if (methodInfo == null)
                {
#if UNITY_DEBUG
                    Debug.LogError($"[AssemblyReferenceManager] Runtime 模式：方法未找到：{methodName}，类型：{typeName}，程序集：{assemblyName}");
#endif
                    return;
                }

                object[] parameters = new object[] { arg1 };
                methodInfo.Invoke(null, parameters);
            }
            catch (Exception exception)
            {
#if UNITY_DEBUG
                Debug.LogError($"[AssemblyReferenceManager] Runtime 模式：调用方法失败：{methodName}，类型：{typeName}，程序集：{assemblyName}，异常：{exception.Message}");
#endif
            }
        }

        /// <summary>
        /// Editor 版本：调用指定程序集中指定类型的指定静态方法（1个参数）- 直接使用反射。
        /// </summary>
        /// <typeparam name="T1">参数1类型</typeparam>
        /// <param name="assemblyName">程序集名称</param>
        /// <param name="typeName">类型全名（包含命名空间）</param>
        /// <param name="methodName">方法名</param>
        /// <param name="arg1">参数1</param>
        private static void InvokeMethodEditor<T1>(string assemblyName, string typeName, string methodName, T1 arg1)
        {
            try
            {
                Assembly assembly = Assembly.Load(assemblyName);
                if (assembly == null)
                {
#if UNITY_DEBUG
                    Debug.LogError($"[AssemblyReferenceManager] Editor 模式：程序集加载失败：{assemblyName}");
#endif
                    return;
                }

                Type type = assembly.GetType(typeName);
                if (type == null)
                {
#if UNITY_DEBUG
                    Debug.LogError($"[AssemblyReferenceManager] Editor 模式：类型未找到：{typeName}，程序集：{assemblyName}");
#endif
                    return;
                }

                Type[] parameterTypes = new Type[] { typeof(T1) };
                MethodInfo methodInfo = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static, null, parameterTypes, null);
                if (methodInfo == null)
                {
#if UNITY_DEBUG
                    Debug.LogError($"[AssemblyReferenceManager] Editor 模式：方法未找到：{methodName}，类型：{typeName}，程序集：{assemblyName}");
#endif
                    return;
                }

                object[] parameters = new object[] { arg1 };
                methodInfo.Invoke(null, parameters);
            }
            catch (Exception exception)
            {
#if UNITY_DEBUG
                Debug.LogError($"[AssemblyReferenceManager] Editor 模式：调用方法失败：{methodName}，类型：{typeName}，程序集：{assemblyName}，异常：{exception.Message}");
#endif
            }
        }

        #endregion

        #region 反射调用函数（2个参数）

        /// <summary>
        /// 调用指定程序集中指定类型的指定静态方法（2个参数）- 路由方法。
        /// Editor 下直接调用，Runtime 下使用 Assembly。
        /// </summary>
        /// <typeparam name="T1">参数1类型</typeparam>
        /// <typeparam name="T2">参数2类型</typeparam>
        /// <param name="assemblyName">程序集名称</param>
        /// <param name="typeName">类型全名（包含命名空间）</param>
        /// <param name="methodName">方法名</param>
        /// <param name="arg1">参数1</param>
        /// <param name="arg2">参数2</param>
        public static void InvokeMethod<T1, T2>(string assemblyName, string typeName, string methodName, T1 arg1, T2 arg2)
        {
#if UNITY_EDITOR
            InvokeMethodEditor(assemblyName, typeName, methodName, arg1, arg2);
#else
            InvokeMethodRuntime(assemblyName, typeName, methodName, arg1, arg2);
#endif
        }

        /// <summary>
        /// Runtime 版本：调用指定程序集中指定类型的指定静态方法（2个参数）。
        /// </summary>
        /// <typeparam name="T1">参数1类型</typeparam>
        /// <typeparam name="T2">参数2类型</typeparam>
        /// <param name="assemblyName">程序集名称</param>
        /// <param name="typeName">类型全名（包含命名空间）</param>
        /// <param name="methodName">方法名</param>
        /// <param name="arg1">参数1</param>
        /// <param name="arg2">参数2</param>
        private static void InvokeMethodRuntime<T1, T2>(string assemblyName, string typeName, string methodName, T1 arg1, T2 arg2)
        {
            try
            {
                Assembly assembly = GetAssembly(assemblyName);
                if (assembly == null)
                {
                    return;
                }

                Type type = assembly.GetType(typeName);
                if (type == null)
                {
#if UNITY_DEBUG
                    Debug.LogError($"[AssemblyReferenceManager] Runtime 模式：类型未找到：{typeName}，程序集：{assemblyName}");
#endif
                    return;
                }

                Type[] parameterTypes = new Type[] { typeof(T1), typeof(T2) };
                MethodInfo methodInfo = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static, null, parameterTypes, null);
                if (methodInfo == null)
                {
#if UNITY_DEBUG
                    Debug.LogError($"[AssemblyReferenceManager] Runtime 模式：方法未找到：{methodName}，类型：{typeName}，程序集：{assemblyName}");
#endif
                    return;
                }

                object[] parameters = new object[] { arg1, arg2 };
                methodInfo.Invoke(null, parameters);
            }
            catch (Exception exception)
            {
#if UNITY_DEBUG
                Debug.LogError($"[AssemblyReferenceManager] Runtime 模式：调用方法失败：{methodName}，类型：{typeName}，程序集：{assemblyName}，异常：{exception.Message}");
#endif
            }
        }

        /// <summary>
        /// Editor 版本：调用指定程序集中指定类型的指定静态方法（2个参数）- 直接使用反射。
        /// </summary>
        /// <typeparam name="T1">参数1类型</typeparam>
        /// <typeparam name="T2">参数2类型</typeparam>
        /// <param name="assemblyName">程序集名称</param>
        /// <param name="typeName">类型全名（包含命名空间）</param>
        /// <param name="methodName">方法名</param>
        /// <param name="arg1">参数1</param>
        /// <param name="arg2">参数2</param>
        private static void InvokeMethodEditor<T1, T2>(string assemblyName, string typeName, string methodName, T1 arg1, T2 arg2)
        {
            try
            {
                Assembly assembly = Assembly.Load(assemblyName);
                if (assembly == null)
                {
#if UNITY_DEBUG
                    Debug.LogError($"[AssemblyReferenceManager] Editor 模式：程序集加载失败：{assemblyName}");
#endif
                    return;
                }

                Type type = assembly.GetType(typeName);
                if (type == null)
                {
#if UNITY_DEBUG
                    Debug.LogError($"[AssemblyReferenceManager] Editor 模式：类型未找到：{typeName}，程序集：{assemblyName}");
#endif
                    return;
                }

                Type[] parameterTypes = new Type[] { typeof(T1), typeof(T2) };
                MethodInfo methodInfo = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static, null, parameterTypes, null);
                if (methodInfo == null)
                {
#if UNITY_DEBUG
                    Debug.LogError($"[AssemblyReferenceManager] Editor 模式：方法未找到：{methodName}，类型：{typeName}，程序集：{assemblyName}");
#endif
                    return;
                }

                object[] parameters = new object[] { arg1, arg2 };
                methodInfo.Invoke(null, parameters);
            }
            catch (Exception exception)
            {
#if UNITY_DEBUG
                Debug.LogError($"[AssemblyReferenceManager] Editor 模式：调用方法失败：{methodName}，类型：{typeName}，程序集：{assemblyName}，异常：{exception.Message}");
#endif
            }
        }

        #endregion

        #region 反射调用函数（3个参数）

        /// <summary>
        /// 调用指定程序集中指定类型的指定静态方法（3个参数）- 路由方法。
        /// Editor 下直接调用，Runtime 下使用 Assembly。
        /// </summary>
        /// <typeparam name="T1">参数1类型</typeparam>
        /// <typeparam name="T2">参数2类型</typeparam>
        /// <typeparam name="T3">参数3类型</typeparam>
        /// <param name="assemblyName">程序集名称</param>
        /// <param name="typeName">类型全名（包含命名空间）</param>
        /// <param name="methodName">方法名</param>
        /// <param name="arg1">参数1</param>
        /// <param name="arg2">参数2</param>
        /// <param name="arg3">参数3</param>
        public static void InvokeMethod<T1, T2, T3>(string assemblyName, string typeName, string methodName, T1 arg1, T2 arg2, T3 arg3)
        {
#if UNITY_EDITOR
            InvokeMethodEditor(assemblyName, typeName, methodName, arg1, arg2, arg3);
#else
            InvokeMethodRuntime(assemblyName, typeName, methodName, arg1, arg2, arg3);
#endif
        }

        /// <summary>
        /// Runtime 版本：调用指定程序集中指定类型的指定静态方法（3个参数）。
        /// </summary>
        /// <typeparam name="T1">参数1类型</typeparam>
        /// <typeparam name="T2">参数2类型</typeparam>
        /// <typeparam name="T3">参数3类型</typeparam>
        /// <param name="assemblyName">程序集名称</param>
        /// <param name="typeName">类型全名（包含命名空间）</param>
        /// <param name="methodName">方法名</param>
        /// <param name="arg1">参数1</param>
        /// <param name="arg2">参数2</param>
        /// <param name="arg3">参数3</param>
        private static void InvokeMethodRuntime<T1, T2, T3>(string assemblyName, string typeName, string methodName, T1 arg1, T2 arg2, T3 arg3)
        {
            try
            {
                Assembly assembly = GetAssembly(assemblyName);
                if (assembly == null)
                {
                    return;
                }

                Type type = assembly.GetType(typeName);
                if (type == null)
                {
#if UNITY_DEBUG
                    Debug.LogError($"[AssemblyReferenceManager] Runtime 模式：类型未找到：{typeName}，程序集：{assemblyName}");
#endif
                    return;
                }

                Type[] parameterTypes = new Type[] { typeof(T1), typeof(T2), typeof(T3) };
                MethodInfo methodInfo = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static, null, parameterTypes, null);
                if (methodInfo == null)
                {
#if UNITY_DEBUG
                    Debug.LogError($"[AssemblyReferenceManager] Runtime 模式：方法未找到：{methodName}，类型：{typeName}，程序集：{assemblyName}");
#endif
                    return;
                }

                object[] parameters = new object[] { arg1, arg2, arg3 };
                methodInfo.Invoke(null, parameters);
            }
            catch (Exception exception)
            {
#if UNITY_DEBUG
                Debug.LogError($"[AssemblyReferenceManager] Runtime 模式：调用方法失败：{methodName}，类型：{typeName}，程序集：{assemblyName}，异常：{exception.Message}");
#endif
            }
        }

        /// <summary>
        /// Editor 版本：调用指定程序集中指定类型的指定静态方法（3个参数）- 直接使用反射。
        /// </summary>
        /// <typeparam name="T1">参数1类型</typeparam>
        /// <typeparam name="T2">参数2类型</typeparam>
        /// <typeparam name="T3">参数3类型</typeparam>
        /// <param name="assemblyName">程序集名称</param>
        /// <param name="typeName">类型全名（包含命名空间）</param>
        /// <param name="methodName">方法名</param>
        /// <param name="arg1">参数1</param>
        /// <param name="arg2">参数2</param>
        /// <param name="arg3">参数3</param>
        private static void InvokeMethodEditor<T1, T2, T3>(string assemblyName, string typeName, string methodName, T1 arg1, T2 arg2, T3 arg3)
        {
            try
            {
                Assembly assembly = Assembly.Load(assemblyName);
                if (assembly == null)
                {
#if UNITY_DEBUG
                    Debug.LogError($"[AssemblyReferenceManager] Editor 模式：程序集加载失败：{assemblyName}");
#endif
                    return;
                }

                Type type = assembly.GetType(typeName);
                if (type == null)
                {
#if UNITY_DEBUG
                    Debug.LogError($"[AssemblyReferenceManager] Editor 模式：类型未找到：{typeName}，程序集：{assemblyName}");
#endif
                    return;
                }

                Type[] parameterTypes = new Type[] { typeof(T1), typeof(T2), typeof(T3) };
                MethodInfo methodInfo = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static, null, parameterTypes, null);
                if (methodInfo == null)
                {
#if UNITY_DEBUG
                    Debug.LogError($"[AssemblyReferenceManager] Editor 模式：方法未找到：{methodName}，类型：{typeName}，程序集：{assemblyName}");
#endif
                    return;
                }

                object[] parameters = new object[] { arg1, arg2, arg3 };
                methodInfo.Invoke(null, parameters);
            }
            catch (Exception exception)
            {
#if UNITY_DEBUG
                Debug.LogError($"[AssemblyReferenceManager] Editor 模式：调用方法失败：{methodName}，类型：{typeName}，程序集：{assemblyName}，异常：{exception.Message}");
#endif
            }
        }

        #endregion

        #region 反射调用函数（4个参数）

        /// <summary>
        /// 调用指定程序集中指定类型的指定静态方法（4个参数）- 路由方法。
        /// Editor 下直接调用，Runtime 下使用 Assembly。
        /// </summary>
        /// <typeparam name="T1">参数1类型</typeparam>
        /// <typeparam name="T2">参数2类型</typeparam>
        /// <typeparam name="T3">参数3类型</typeparam>
        /// <typeparam name="T4">参数4类型</typeparam>
        /// <param name="assemblyName">程序集名称</param>
        /// <param name="typeName">类型全名（包含命名空间）</param>
        /// <param name="methodName">方法名</param>
        /// <param name="arg1">参数1</param>
        /// <param name="arg2">参数2</param>
        /// <param name="arg3">参数3</param>
        /// <param name="arg4">参数4</param>
        public static void InvokeMethod<T1, T2, T3, T4>(string assemblyName, string typeName, string methodName, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
#if UNITY_EDITOR
            InvokeMethodEditor(assemblyName, typeName, methodName, arg1, arg2, arg3, arg4);
#else
            InvokeMethodRuntime(assemblyName, typeName, methodName, arg1, arg2, arg3, arg4);
#endif
        }

        /// <summary>
        /// Runtime 版本：调用指定程序集中指定类型的指定静态方法（4个参数）。
        /// </summary>
        /// <typeparam name="T1">参数1类型</typeparam>
        /// <typeparam name="T2">参数2类型</typeparam>
        /// <typeparam name="T3">参数3类型</typeparam>
        /// <typeparam name="T4">参数4类型</typeparam>
        /// <param name="assemblyName">程序集名称</param>
        /// <param name="typeName">类型全名（包含命名空间）</param>
        /// <param name="methodName">方法名</param>
        /// <param name="arg1">参数1</param>
        /// <param name="arg2">参数2</param>
        /// <param name="arg3">参数3</param>
        /// <param name="arg4">参数4</param>
        private static void InvokeMethodRuntime<T1, T2, T3, T4>(string assemblyName, string typeName, string methodName, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            try
            {
                Assembly assembly = GetAssembly(assemblyName);
                if (assembly == null)
                {
                    return;
                }

                Type type = assembly.GetType(typeName);
                if (type == null)
                {
#if UNITY_DEBUG
                    Debug.LogError($"[AssemblyReferenceManager] Runtime 模式：类型未找到：{typeName}，程序集：{assemblyName}");
#endif
                    return;
                }

                Type[] parameterTypes = new Type[] { typeof(T1), typeof(T2), typeof(T3), typeof(T4) };
                MethodInfo methodInfo = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static, null, parameterTypes, null);
                if (methodInfo == null)
                {
#if UNITY_DEBUG
                    Debug.LogError($"[AssemblyReferenceManager] Runtime 模式：方法未找到：{methodName}，类型：{typeName}，程序集：{assemblyName}");
#endif
                    return;
                }

                object[] parameters = new object[] { arg1, arg2, arg3, arg4 };
                methodInfo.Invoke(null, parameters);
            }
            catch (Exception exception)
            {
#if UNITY_DEBUG
                Debug.LogError($"[AssemblyReferenceManager] Runtime 模式：调用方法失败：{methodName}，类型：{typeName}，程序集：{assemblyName}，异常：{exception.Message}");
#endif
            }
        }

        /// <summary>
        /// Editor 版本：调用指定程序集中指定类型的指定静态方法（4个参数）- 直接使用反射。
        /// </summary>
        /// <typeparam name="T1">参数1类型</typeparam>
        /// <typeparam name="T2">参数2类型</typeparam>
        /// <typeparam name="T3">参数3类型</typeparam>
        /// <typeparam name="T4">参数4类型</typeparam>
        /// <param name="assemblyName">程序集名称</param>
        /// <param name="typeName">类型全名（包含命名空间）</param>
        /// <param name="methodName">方法名</param>
        /// <param name="arg1">参数1</param>
        /// <param name="arg2">参数2</param>
        /// <param name="arg3">参数3</param>
        /// <param name="arg4">参数4</param>
        private static void InvokeMethodEditor<T1, T2, T3, T4>(string assemblyName, string typeName, string methodName, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            try
            {
                Assembly assembly = Assembly.Load(assemblyName);
                if (assembly == null)
                {
#if UNITY_DEBUG
                    Debug.LogError($"[AssemblyReferenceManager] Editor 模式：程序集加载失败：{assemblyName}");
#endif
                    return;
                }

                Type type = assembly.GetType(typeName);
                if (type == null)
                {
#if UNITY_DEBUG
                    Debug.LogError($"[AssemblyReferenceManager] Editor 模式：类型未找到：{typeName}，程序集：{assemblyName}");
#endif
                    return;
                }

                Type[] parameterTypes = new Type[] { typeof(T1), typeof(T2), typeof(T3), typeof(T4) };
                MethodInfo methodInfo = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static, null, parameterTypes, null);
                if (methodInfo == null)
                {
#if UNITY_DEBUG
                    Debug.LogError($"[AssemblyReferenceManager] Editor 模式：方法未找到：{methodName}，类型：{typeName}，程序集：{assemblyName}");
#endif
                    return;
                }

                object[] parameters = new object[] { arg1, arg2, arg3, arg4 };
                methodInfo.Invoke(null, parameters);
            }
            catch (Exception exception)
            {
#if UNITY_DEBUG
                Debug.LogError($"[AssemblyReferenceManager] Editor 模式：调用方法失败：{methodName}，类型：{typeName}，程序集：{assemblyName}，异常：{exception.Message}");
#endif
            }
        }

        #endregion

        #region 异步反射调用函数（返回 UniTask）

        /// <summary>
        /// 调用指定程序集中指定类型的指定静态方法（无参数），并返回 UniTask。
        /// </summary>
        /// <param name="assemblyName">程序集名称</param>
        /// <param name="typeName">类型全名（包含命名空间）</param>
        /// <param name="methodName">方法名</param>
        /// <returns>UniTask</returns>
        public static async UniTask InvokeMethodAsync(string assemblyName, string typeName, string methodName)
        {
#if UNITY_EDITOR
            await InvokeMethodAsyncEditor(assemblyName, typeName, methodName);
#else
            await InvokeMethodAsyncRuntime(assemblyName, typeName, methodName);
#endif
        }

        /// <summary>
        /// Runtime 版本：调用指定程序集中指定类型的指定静态方法（无参数），并返回 UniTask。
        /// </summary>
        /// <param name="assemblyName">程序集名称</param>
        /// <param name="typeName">类型全名（包含命名空间）</param>
        /// <param name="methodName">方法名</param>
        /// <returns>UniTask</returns>
        private static async UniTask InvokeMethodAsyncRuntime(string assemblyName, string typeName, string methodName)
        {
            try
            {
                Assembly assembly = GetAssembly(assemblyName);
                if (assembly == null)
                {
#if UNITY_DEBUG
                    Debug.LogError($"[AssemblyReferenceManager] Runtime 模式：程序集未找到：{assemblyName}");
#endif
                    return;
                }

                Type type = assembly.GetType(typeName);
                if (type == null)
                {
#if UNITY_DEBUG
                    Debug.LogError($"[AssemblyReferenceManager] Runtime 模式：类型未找到：{typeName}，程序集：{assemblyName}");
#endif
                    return;
                }

                MethodInfo methodInfo = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
                if (methodInfo == null)
                {
#if UNITY_DEBUG
                    Debug.LogError($"[AssemblyReferenceManager] Runtime 模式：方法未找到：{methodName}，类型：{typeName}，程序集：{assemblyName}");
#endif
                    return;
                }

                object result = methodInfo.Invoke(null, null);
                if (result is UniTask uniTask)
                {
                    await uniTask;
                }
                else if (result is System.Threading.Tasks.Task task)
                {
                    await task;
                }
            }
            catch (Exception exception)
            {
#if UNITY_DEBUG
                Debug.LogError($"[AssemblyReferenceManager] Runtime 模式：调用方法失败：{methodName}，类型：{typeName}，程序集：{assemblyName}，异常：{exception.Message}");
#endif
            }
        }

        /// <summary>
        /// Editor 版本：调用指定程序集中指定类型的指定静态方法（无参数），并返回 UniTask。
        /// </summary>
        /// <param name="assemblyName">程序集名称</param>
        /// <param name="typeName">类型全名（包含命名空间）</param>
        /// <param name="methodName">方法名</param>
        /// <returns>UniTask</returns>
        private static async UniTask InvokeMethodAsyncEditor(string assemblyName, string typeName, string methodName)
        {
            try
            {
                Assembly assembly = Assembly.Load(assemblyName);
                if (assembly == null)
                {
#if UNITY_DEBUG
                    Debug.LogError($"[AssemblyReferenceManager] Editor 模式：程序集加载失败：{assemblyName}");
#endif
                    return;
                }

                Type type = assembly.GetType(typeName);
                if (type == null)
                {
#if UNITY_DEBUG
                    Debug.LogError($"[AssemblyReferenceManager] Editor 模式：类型未找到：{typeName}，程序集：{assemblyName}");
#endif
                    return;
                }

                MethodInfo methodInfo = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
                if (methodInfo == null)
                {
#if UNITY_DEBUG
                    Debug.LogError($"[AssemblyReferenceManager] Editor 模式：方法未找到：{methodName}，类型：{typeName}，程序集：{assemblyName}");
#endif
                    return;
                }

                object result = methodInfo.Invoke(null, null);
                if (result is UniTask uniTask)
                {
                    await uniTask;
                }
                else if (result is System.Threading.Tasks.Task task)
                {
                    await task;
                }
            }
            catch (Exception exception)
            {
#if UNITY_DEBUG
                Debug.LogError($"[AssemblyReferenceManager] Editor 模式：调用方法失败：{methodName}，类型：{typeName}，程序集：{assemblyName}，异常：{exception.Message}");
#endif
            }
        }

        #endregion
    }
}

