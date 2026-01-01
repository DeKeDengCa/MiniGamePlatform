using System;
using Google.Protobuf;
using UnityEngine;

namespace NetworkFramework.Utils
{
    public static class Serializer
    {
        // 序列化对象为JSON
        public static string SerializeToJson<T>(T obj)
        {
            try
            {
                if (obj == null)
                {
                    LoggerUtil.LogWarning("Serializer.SerializeToJson: Input object is null");
                    return null;
                }

                string result = JsonUtility.ToJson(obj);
                LoggerUtil.LogVerbose(
                    $"Serializer.SerializeToJson: Successfully serialized object of type {typeof(T).Name}");
                return result;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError(
                    $"Serializer.SerializeToJson: Serialization failed for object type: {typeof(T).Name}, Exception: {ex.Message}");
                LoggerUtil.LogException(ex);
                return null;
            }
        }

        // 反序列化JSON为对象
        public static T DeserializeFromJson<T>(string json)
        {
            try
            {
                if (string.IsNullOrEmpty(json))
                {
                    LoggerUtil.LogWarning("Serializer.DeserializeFromJson: Input JSON string is null or empty");
                    return default;
                }

                T result = JsonUtility.FromJson<T>(json);
                LoggerUtil.LogVerbose(
                    $"Serializer.DeserializeFromJson: Successfully deserialized to object of type {typeof(T).Name}");
                return result;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError(
                    $"Serializer.DeserializeFromJson: Deserialization failed for target type: {typeof(T).Name}, JSON: {json?.Substring(0, Math.Min(100, json?.Length ?? 0))}..., Exception: {ex.Message}");
                LoggerUtil.LogException(ex);
                return default;
            }
        }

        // 序列化对象为ProtoBuf
        public static byte[] SerializeToProtoBuf<T>(T obj) where T : IMessage<T>
        {
            try
            {
                if (obj == null)
                {
                    LoggerUtil.LogWarning("Serializer.SerializeToProtoBuf: Input object is null");
                    return null;
                }

                byte[] result = obj.ToByteArray();
                LoggerUtil.LogVerbose(
                    $"Serializer.SerializeToProtoBuf: Successfully serialized object of type {typeof(T).Name}, byte length: {result?.Length ?? 0}");
                return result;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError(
                    $"Serializer.SerializeToProtoBuf: Serialization failed for object type: {typeof(T).Name}, Exception: {ex.Message}");
                LoggerUtil.LogException(ex);
                return null;
            }
        }

        // 反序列化ProtoBuf为对象
        public static T DeserializeFromProtoBuf<T>(byte[] data) where T : IMessage<T>, new()
        {
            try
            {
                if (data == null)
                {
                    LoggerUtil.LogWarning("Serializer.DeserializeFromProtoBuf: Input data is null or empty");
                    return default;
                }

                T message = new T();
                message.MergeFrom(data);
                LoggerUtil.LogVerbose(
                    $"Serializer.DeserializeFromProtoBuf: Successfully deserialized to object of type {typeof(T).Name}, input byte length: {data.Length}");
                return message;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError(
                    $"Serializer.DeserializeFromProtoBuf: Deserialization failed for target type: {typeof(T).Name}, data length: {data?.Length ?? 0}, Exception: {ex.Message}");
                LoggerUtil.LogException(ex);
                return default;
            }
        }
    }
}