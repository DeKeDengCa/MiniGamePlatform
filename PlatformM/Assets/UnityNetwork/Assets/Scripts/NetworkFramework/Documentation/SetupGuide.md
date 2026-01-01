# 网络框架设置指南

## 概述
本指南将帮助您设置和配置Unity网络通信框架，该框架支持ProtoBuf和JSON双协议，基于WebSocket和HTTP实现。

## 安装步骤

### 1. 安装NativeWebSocket插件

#### 方法一：使用包管理器（推荐）
1. 打开Unity编辑器
2. 点击菜单栏 `Window` -> `Package Manager`
3. 点击左上角的`+`号
4. 选择`Add package from git URL...`
5. 输入以下URL并点击`Add`：
   ```
   https://github.com/endel/NativeWebSocket.git#upm
   ```

#### 方法二：使用自定义菜单命令
1. 在Unity编辑器中，找到并点击菜单栏最后的 `NetworkFramework` 菜单
2. 选择 `Install NativeWebSocket` 选项
3. Unity将自动在后台安装插件

### 2. 导入ProtoBuf库

1. 访问ProtoBuf-net GitHub仓库：https://github.com/protobuf-net/protobuf-net
2. 下载最新版本的预编译DLL文件：
3. 将DLL文件放入以下目录：
   ```
   Assets/Scripts/NetworkFramework/Plugins/ProtoBuf/
   ```

### 3. 创建ProtoBuf消息类

1. 在以下目录创建.proto文件：
   ```
   Assets/Scripts/NetworkFramework/Models/Proto/
   ```
2. 使用protogen工具生成C#类文件：
   ```bash
   # 安装protogen工具
   dotnet tool install --global protobuf-net.Protogen
   
   # 生成C#类
   protogen -i:player_info.proto -o:PlayerInfo.cs
   ```

### 4. 配置服务器地址

1. 打开以下脚本文件：
   - `WebSocketExample.cs`
   - `HttpExample.cs`
2. 修改服务器地址：
   ```csharp
   // WebSocket URL
   public string websocketUrl = "ws://localhost:8080";
   
   // HTTP服务器URL
   public string httpServerUrl = "http://localhost:8080";
   ```

## 运行示例

1. 打开场景文件：
   ```
   Assets/Scenes/MainScene.unity
   ```
2. 点击Unity编辑器中的`Play`按钮运行场景

## 测试服务器

如果您没有可用的测试服务器，可以使用以下简单的Node.js服务器进行测试：

```javascript
// server.js
const WebSocket = require('ws');
const express = require('express');
const http = require('http');

// 创建HTTP服务器
const app = express();
const server = http.createServer(app);

// 创建WebSocket服务器
const wss = new WebSocket.Server({ server });

wss.on('connection', (ws) => {
  console.log('客户端已连接');
  
  // 发送欢迎消息
  ws.send(JSON.stringify({
    type: "welcome",
    message: "欢迎连接到WebSocket服务器"
  }));
  
  ws.on('message', (data) => {
    console.log('收到消息:', data);
    
    // 回显消息
    if (data instanceof Buffer) {
      // 二进制消息（ProtoBuf）
      ws.send(data);
    } else {
      // 文本消息（JSON）
      ws.send(data);
    }
  });
  
  ws.on('close', () => {
    console.log('客户端已断开连接');
  });
});

// HTTP端点
app.use(express.json());

app.post('/api/player', (req, res) => {
  console.log('收到HTTP请求:', req.body);
  res.json({
    playerId: req.body.playerId,
    playerName: req.body.name,
    level: 1,
    score: 1000,
    message: "玩家数据已接收"
  });
});

const PORT = process.env.PORT || 8080;
server.listen(PORT, () => {
  console.log(`服务器运行在端口 ${PORT}`);
});
```

安装并运行服务器：
```bash
npm install ws express
node server.js
```

## 故障排除

### 常见问题

1. **NativeWebSocket插件未找到**
   - 确保已正确安装插件
   - 检查Unity控制台是否有相关错误信息

2. **ProtoBuf序列化错误**
   - 确保消息类已正确标记[ProtoContract]和[ProtoMember]特性
   - 检查protobuf-net.dll是否已正确导入

3. **网络连接失败**
   - 检查服务器地址是否正确
   - 确保服务器正在运行
   - 检查防火墙设置

4. **Meta文件错误**
   - 如果出现"has no meta file, but it is in an immutable folder"错误，请使用我们提供的修复工具：
     - 在Unity编辑器中，点击 `NetworkFramework` -> `Fix Meta Files`
     - 点击 `Fix Missing Meta Files` 按钮
   - 或运行项目根目录的修复脚本：
     ```bash
     ./fix_meta_files.sh
     ```

### 调试技巧

1. 使用Unity的网络调试工具
2. 查看控制台日志输出
3. 在NetworkManager中启用详细日志记录

## 扩展功能

### 添加新的传输协议
1. 实现`INetworkInterface`接口
2. 在`NetworkManager`中添加相应的管理方法

### 添加新的序列化协议
1. 创建新的协议处理类
2. 实现序列化/反序列化方法
3. 在`NetworkManager`中添加相应的发送方法

## 支持与反馈

如有任何问题或建议，请联系开发团队或在GitHub上提交Issue。