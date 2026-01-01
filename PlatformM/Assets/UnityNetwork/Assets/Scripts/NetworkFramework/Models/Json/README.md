# JSON模型定义

## 目录结构
```
Models/Json/
└── *.cs             # JSON模型类定义
```

## 使用说明

1. 在此目录下创建C#类文件定义JSON数据结构
2. 使用[System.Serializable]特性标记可序列化的类
3. 在代码中使用JsonUtility进行序列化/反序列化

## 示例JSON模型类
```csharp
using System;

[Serializable]
public class PlayerInfo
{
    public int playerId;
    public string name;
    public int level;
}

[Serializable]
public class GameState
{
    public int gameId;
    public PlayerInfo[] players;
    public int status;
}
```