using UnityEngine;

namespace NetworkFramework.Protocol
{
    public static class JsonProtocol
    {
    public static string Serialize<T>(T obj)
    {
        return JsonUtility.ToJson(obj);
    }

    public static T Deserialize<T>(string json)
    {
        return JsonUtility.FromJson<T>(json);
    }
    }
}