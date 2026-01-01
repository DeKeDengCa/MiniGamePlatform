# Google.Protobuf 使用指南

## 概述

本项目使用 Google.Protobuf 作为序列化框架。Google.Protobuf 是 Google 开发的高效序列化库，具有以下特点：

- 高效的数据序列化
- 跨平台支持
- 强类型定义
- 向后兼容性

## 安装和配置

### 1. DLL 文件

项目中使用的 Google.Protobuf 库文件位于：
`Assets/Plugins/ProtoBuf/protobuf-net.dll`

尽管文件名为 protobuf-net.dll，但实际上包含了 Google.Protobuf 命名空间。

### 2. 命名空间引用

在使用 Google.Protobuf 时，需要引用正确的命名空间：

```csharp
using Google.Protobuf;
```

### 3. Assembly Definition 配置

NetworkFramework.asmdef 文件已经配置了对 protobuf-net.dll 的引用：

```json
{
    "name": "NetworkFramework",
    "references": [
        "endel.nativewebsocket",
        "protobuf-net"
    ],
    "precompiledReferences": [
        "protobuf-net.dll"
    ]
}
```

## 使用方法

### 1. 序列化和反序列化

项目提供了 GoogleProtoBufProtocol 类来处理序列化和反序列化：

```csharp
// 序列化
byte[] data = GoogleProtoBufProtocol.Serialize(message);

// 反序列化
T message = GoogleProtoBufProtocol.Deserialize<T>(data);
```

### 2. 使用 Serializer 工具类

也可以使用 Serializer 工具类进行序列化操作：

```csharp
// 序列化
byte[] data = Serializer.SerializeToProtoBuf(message);

// 反序列化
T message = Serializer.DeserializeFromProtoBuf<T>(data);
```

## 生成 ProtoBuf 类

如果需要从 .proto 文件生成 C# 类，请使用以下步骤：

1. 安装 protoc 编译器
2. 使用以下命令生成 C# 类：

```bash
protoc --csharp_out=. player_info.proto
```

## 常见问题和解决方案

### 1. CS0246 错误：找不到类型或命名空间名称 'ProtoBuf'

这通常是因为使用了错误的命名空间。确保使用：

```csharp
using Google.Protobuf;
```

而不是：

```csharp
using ProtoBuf;
```

### 2. 缺少 DLL 引用

确保 NetworkFramework.asmdef 文件正确引用了 protobuf-net.dll。

### 3. 属性名称不匹配

生成的 ProtoBuf 类使用特定的属性名称，请检查生成的类文件以确认正确的属性名称。

## 测试

项目包含以下测试脚本：

1. `ProtoBufTest.cs` - 基本的序列化/反序列化测试
2. `GoogleProtoBufTest.cs` - 使用 Serializer 工具类的测试

可以通过运行 `ProtoBufTestScene.unity` 场景来执行这些测试。