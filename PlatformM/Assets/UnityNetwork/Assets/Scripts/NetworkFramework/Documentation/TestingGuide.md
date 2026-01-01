# 测试指南

## 概述

本文档说明如何测试Google.Protobuf集成是否正常工作。

## 测试步骤

### 1. 打开测试场景

1. 在Unity编辑器中，打开`Assets/Scenes/ProtoBufTestScene.unity`场景
2. 确保场景中有一个GameObject挂载了以下脚本之一：
   - `ProtoBufTest.cs`
   - `GoogleProtoBufTest.cs`
   - `ValidationTest.cs`

### 2. 运行测试

1. 点击Unity编辑器中的播放按钮
2. 查看控制台输出，应该能看到类似以下的日志：
   ```
   序列化后的数据长度: X 字节
   反序列化结果 - ID: 1001, Name: TestPlayer, Level: 10, Score: 1500
   数据一致性检查: 通过
   ```

### 3. 验证结果

如果所有测试都通过，您应该看到以下输出：
- 没有CS0246错误
- 序列化和反序列化成功
- 数据一致性检查通过

## 常见问题

### 1. 如果仍然出现CS0246错误

1. 检查`Serializer.cs`文件是否使用了正确的命名空间：
   ```csharp
   using Google.Protobuf;
   ```

2. 确保`NetworkFramework.asmdef`文件正确引用了`protobuf-net.dll`

3. 重新导入所有资源：
   - 在Unity编辑器中，选择`Assets -> Reimport All`

### 2. 如果出现其他编译错误

1. 检查PlayerInfo.cs文件中的属性名称是否与测试脚本中使用的名称一致
2. 确保所有测试脚本都使用了正确的命名空间引用

## 额外测试

您可以创建自己的测试脚本来验证功能：

```csharp
using UnityEngine;
using Google.Protobuf;

public class MyProtoBufTest : MonoBehaviour
{
    void Start()
    {
        // 创建一个PlayerInfo实例
        PlayerInfo playerInfo = new PlayerInfo
        {
            PlayerId = 1,
            Name = "Test Player",
            Level = 10,
            Score = 1500
        };

        // 序列化
        byte[] data = GoogleProtoBufProtocol.Serialize(playerInfo);
        Debug.Log("Serialized data length: " + data.Length);

        // 反序列化
        PlayerInfo deserializedPlayerInfo = GoogleProtoBufProtocol.Deserialize<PlayerInfo>(data);
        Debug.Log("Deserialized player info - Id: " + deserializedPlayerInfo.PlayerId + 
                  ", Name: " + deserializedPlayerInfo.Name + 
                  ", Level: " + deserializedPlayerInfo.Level +
                  ", Score: " + deserializedPlayerInfo.Score);
    }
}
```

## 支持

如果在测试过程中遇到任何问题，请查看以下文档：
- `GoogleProtobufUsage.md` - Google.Protobuf使用指南
- `Troubleshooting.md` - 故障排除指南
- `SetupGuide.md` - 设置指南