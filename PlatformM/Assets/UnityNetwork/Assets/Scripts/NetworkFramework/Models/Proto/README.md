# ProtoBuf模型定义

## 目录结构
```
Models/Proto/
├── *.proto          # ProtoBuf定义文件
└── Generated/       # 自动生成的C#代码
```

## 使用说明

1. 在此目录下创建.proto文件定义数据结构
2. 使用protogen工具从.proto文件生成C#类
3. 生成的C#类将放在Generated目录下
4. 在代码中使用生成的类进行ProtoBuf序列化/反序列化

## 示例.proto文件
```protobuf
syntax = "proto3";

// 玩家信息
message PlayerInfo {
    int32 playerId = 1;
    string name = 2;
    int32 level = 3;
}

// 游戏状态
message GameState {
    int32 gameId = 1;
    repeated PlayerInfo players = 2;
    int32 status = 3;
}
```