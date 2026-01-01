# ProtoBuf消息系统使用指南

## 概述

本框架支持使用Google ProtoBuf进行高效的消息序列化和反序列化。通过消息ID机制，可以方便地注册和处理不同类型的消息。

## 核心组件

### 1. MessageHandlerManager
消息处理器管理器，用于注册和管理基于消息ID的ProtoBuf消息处理器。

### 2. GoogleProtoBufProtocol
提供ProtoBuf消息的序列化和反序列化功能。

### 3. NetworkManager
网络管理器，提供发送ProtoBuf消息的接口。

## 使用方法

### 1. 定义ProtoBuf消息

首先需要定义ProtoBuf消息格式，例如：

```protobuf
syntax = "proto3";

message PlayerInfo {
    int32 playerId = 1;
    string name = 2;
    int32 level = 3;
    int64 score = 4;
}

message GameState {
    string state = 1;
    int64 timestamp = 2;
}
```

然后使用protoc编译器生成C#代码。

### 2. 注册消息处理器

在代码中注册消息处理器：

```csharp
// 注册PlayerInfo消息处理器
MessageHandlerManager.RegisterHandler<PlayerInfo>(1001, (msg) =>
{
    Debug.Log($"收到PlayerInfo消息: ID={msg.PlayerId}, Name={msg.Name}, Level={msg.Level}");
    // 处理消息...
});

// 注册GameState消息处理器
MessageHandlerManager.RegisterHandler<GameState>(1002, (msg) =>
{
    Debug.Log($"收到GameState消息: State={msg.State}, Timestamp={msg.Timestamp}");
    // 处理消息...
});
```

### 3. 发送消息

使用NetworkManager发送ProtoBuf消息：

```csharp
// 创建PlayerInfo消息
var playerInfo = new PlayerInfo
{
    PlayerId = 1001,
    Name = "张三",
    Level = 10
};

// 发送PlayerInfo消息
NetworkManager.Instance.SendMessage(1001, playerInfo);

// 创建GameState消息
var gameState = new GameState
{
    State = "playing",
    Timestamp = DateTime.Now.Ticks
};

// 发送GameState消息
NetworkManager.Instance.SendMessage(1002, gameState);
```

### 4. 接收消息

消息接收和处理由MessageHandlerManager自动完成。当WebSocket收到带有消息ID的ProtoBuf消息时，会自动根据消息ID调用相应的处理器。

## 注意事项

1. 每个消息类型应该有唯一的ID
2. 消息处理器应该在程序启动时注册
3. 在适当的时候取消注册消息处理器以避免内存泄漏
4. 确保ProtoBuf消息定义与服务器端保持一致

## 示例代码

参考项目中的`MessageHandlerExample.cs`和`ProtoBufTestExample.cs`文件获取完整的使用示例。