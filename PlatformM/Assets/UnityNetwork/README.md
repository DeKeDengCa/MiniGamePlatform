# Unity网络框架

这是一个基于Unity的网络框架，支持HTTP短连接和WebSocket长连接，通过拦截器链机制实现了灵活的网络请求处理。框架采用模块化设计，提供了完善的错误处理、消息分发等功能，支持JSON和Protobuf两种数据格式，适用于各类Unity网络游戏和应用的网络通信需求。

## 🏗️ 架构设计

### 核心组件

1. **NetworkManager** - 网络管理器，采用单例模式，负责初始化和管理网络组件
2. **WebSocketManager** - WebSocket连接管理器，支持多连接管理
3. **BestHttpWebSocketAdapter** - WebSocket适配器，基于BestHTTP实现
4. **HttpNetworkAdapter** - HTTP适配器，基于BestHTTP实现
5. **DefaultNetworkAdapterSelector** - 适配器选择器，根据连接类型选择合适的适配器
6. **拦截器链系统** - 基于责任链模式的请求/响应处理机制
7. **UnityMainThread** - 主线程调度器，确保UI操作在主线程执行

### 拦截器系统

框架采用拦截器链模式，支持以下拦截器：

- **LogInterceptor** - 日志拦截器，记录请求和响应信息
- **CompressInterceptor** - 压缩拦截器，处理数据压缩和解压缩
- **EncryptInterceptor** - 加密拦截器，处理数据加密和解密
- **HeaderInterceptor** - 请求头拦截器，添加必要的HTTP头信息
- **NetworkRequestInterceptor** - 网络请求拦截器，执行实际的网络请求

### 连接类型

- **PERSISTENT** - 长连接（WebSocket）
- **INCONSTANT** - 短连接（HTTP）
- **PERSISTENT_PRECEDE** - 长连接优先，不可用时自动切换到短连接
- **INCONSTANT_PRECEDE** - 短连接优先，失败时自动切换到长连接

## 🚀 快速开始

### 基本初始化

框架需要通过NetworkManager进行初始化：

```csharp
// 获取网络管理器实例
var networkManager = NetworkManager.Instance;

// 初始化网络配置
networkManager.Init(
    inconstantConnectionUrl: "https://your-api-server.com",
    persistentConnectionUrl: "wss://your-websocket-server.com",
    config: new AppNetConfig("", "")
);
```

### 发送HTTP请求

```csharp
// 创建HTTP请求
var request = new Request
{
    InconstantConnectionUrl = "https://your-api-server.com/api/login",
    UseConnectionType = ConnectionType.INCONSTANT,
    RequestControl = new RequestControl
    {
        Service = "UserService",
        Method = "Login",
        Reason = RPCReason.UserAction
    }
};

// 设置请求体
var loginData = new { username = "test", password = "123456" };
request.Body = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(loginData));

// 发送请求
try
{
    using var cts = new CancellationTokenSource();
    Response response = await NetworkManager.Instance.Request(request, cts.Token, 0);
    if (response.IsSuccess)
    {
        Debug.Log($"登录成功: {response.Data}");
    }
    else
    {
        Debug.LogError($"登录失败: {response.NetMessage}");
    }
}
catch (Exception ex)
{
    Debug.LogError($"请求异常: {ex.Message}");
}
```

### WebSocket连接和消息发送

```csharp
// 连接WebSocket
try
{
    var connectRequest = new Request
    {
        PersistentConnectionUrl = "wss://your-websocket-server.com",
        UseConnectionType = ConnectionType.PERSISTENT,
        RequestControl = new RequestControl
        {
            Service = "WebSocketService",
            Method = "Connect",
            Reason = RPCReason.UserAction
        }
    };
    
    using var cts = new CancellationTokenSource();
    Response response = await NetworkManager.Instance.Connect(connectRequest, cts.Token, 0);
    if (response.IsSuccess)
    {
        Debug.Log("WebSocket连接成功");
    }
}
catch (Exception ex)
{
    Debug.LogError($"WebSocket连接失败: {ex.Message}");
}

// 发送WebSocket消息
var messageRequest = new Request
{
    PersistentConnectionUrl = "wss://your-websocket-server.com",
    UseConnectionType = ConnectionType.PERSISTENT,
    RequestControl = new RequestControl
    {
        Service = "MessageService",
        Method = "SendMessage",
        Reason = RPCReason.UserAction
    }
};

var message = new { action = "ping", timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
messageRequest.Body = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(message));

using var cts2 = new CancellationTokenSource();
await NetworkManager.Instance.Request(messageRequest, cts2.Token, 0);
```

### 推送消息处理

推送消息处理通过WebSocketManager和MessageProcessor进行管理。框架会自动处理接收到的WebSocket消息：

```csharp
// 推送消息会通过NetworkManager的OnPushMessage事件自动处理
// 具体的消息处理逻辑在MessageProcessor中实现

// 在主线程中处理推送消息
UnityMainThread.Post(() =>
{
    // 更新UI逻辑
    UpdateNotificationUI();
});
```

## 📦 Protobuf支持

框架完整支持Google Protobuf，提供了自动化的代码生成工具。

### 代码生成

使用项目根目录下的 `generate_proto_code.sh` 脚本自动生成C#代码：

```bash
# 在项目根目录执行
./generate_proto_code.sh
```

脚本功能：
- 自动扫描 `Assets/Scripts/NetworkFramework/Models/Proto/` 目录下的.proto文件
- 排除Google标准类型（避免与Google.Protobuf.WellKnownTypes冲突）
- 生成的C#文件自动应用PascalCase命名规范
- 按目录结构组织生成的代码

### Protobuf消息使用

```csharp
// 定义.proto文件后生成C#类，然后使用：
var playerInfo = new PlayerInfo
{
    PlayerId = 1001,
    Name = "张三",
    Level = 10
};

// 发送Protobuf消息
var request = new Request
{
    InconstantConnectionUrl = "https://your-api-server.com/api/player",
    ConnectionType = ConnectionType.INCONSTANT,
    Method = "POST",
    ContentType = "application/protobuf",
    Body = playerInfo.ToByteArray()
};

using var cts = new CancellationTokenSource();
Response response = await NetworkManager.Instance.Request(request, cts.Token, 0);
```

## 🔧 高级功能

### 自定义拦截器

```csharp
public class CustomInterceptor : IInterceptor
{
    public async Task<Response> Intercept(Request request, IInterceptorChain chain, CancellationToken token)
    {
        // 请求前处理
        Debug.Log($"发送请求: {request.InconstantConnectionUrl}");
        
        // 调用下一个拦截器
        var response = await chain.Proceed(request, token);
        
        // 响应后处理
        Debug.Log($"收到响应: {response.NetCode}");
        
        return response;
    }
}

// 添加自定义拦截器
NetworkManager.Instance.AddInterceptor(new CustomInterceptor());
```

### 任务优先级调度

```csharp
// 使用优先级任务调度器
var scheduler = NetworkManager.Instance.Scheduler;

// 高优先级任务
await scheduler.ScheduleAsync(() => SendImportantRequest(), TaskPriority.High);

// 低优先级任务
await scheduler.ScheduleAsync(() => SendBackgroundRequest(), TaskPriority.Low);
```

### 主线程操作

```csharp
// 在主线程执行UI操作
UnityMainThread.Post(() =>
{
    // 安全地更新UI
    uiText.text = "更新的文本";
    button.interactable = true;
});

// 立即执行或入队到主线程
UnityMainThread.Run(() =>
{
    // 如果当前在主线程则立即执行，否则入队等待
    transform.position = Vector3.zero;
});
```

## 📋 示例和测试

### 运行示例

1. 打开 `Assets/Scenes/MainScene.unity` 场景
2. 查看 `Assets/Scripts/NetworkFramework/Examples/` 目录下的示例代码
3. 运行场景并查看控制台输出

### 测试UI

项目包含了完整的测试UI（`UIManager.cs`），提供以下功能：
- WebSocket连接测试
- HTTP请求测试
- Protobuf消息测试
- 拦截器链测试
- 实时日志显示

## 🛠️ 最佳实践

### 错误处理

```csharp
try
{
    using var cts = new CancellationTokenSource();
var response = await NetworkManager.Instance.Request(request, cts.Token, 0);
    if (response.IsSuccess)
    {
        // 处理成功响应
    }
    else
    {
        // 处理业务错误
        Debug.LogWarning($"业务错误: {response.NetMessage}");
    }
}
catch (OperationCanceledException)
{
    Debug.Log("请求被取消");
}
catch (TimeoutException)
{
    Debug.LogError("请求超时");
}
catch (Exception ex)
{
    Debug.LogError($"网络异常: {ex.Message}");
}
```

### 资源管理

```csharp
public class NetworkComponent : MonoBehaviour
{
    private CancellationTokenSource _cancellationTokenSource;
    
    void Start()
    {
        _cancellationTokenSource = new CancellationTokenSource();
    }
    
    void OnDestroy()
    {
        // 取消所有网络请求
        _cancellationTokenSource?.Cancel();
        
        // 断开WebSocket连接
        var disconnectRequest = new Request
        {
            UseConnectionType = ConnectionType.PERSISTENT,
            RequestControl = new RequestControl
            {
                Service = "WebSocketService",
                Method = "Disconnect",
                Reason = RPCReason.UserAction
            }
        };

        using var cts = new CancellationTokenSource();
        await NetworkManager.Instance.Disconnect(disconnectRequest, cts.Token, 0);
    }
}
```

### 性能优化

1. **合理使用连接类型**：根据业务需求选择合适的连接类型
2. **批量处理消息**：对频繁的推送消息进行节流处理
3. **内存管理**：及时释放大型响应数据
4. **连接池管理**：复用WebSocket连接，避免频繁创建

## 📚 依赖项

- **Unity 2020.3+** - 基础运行环境
- **BestHTTP** - HTTP和WebSocket通信库
- **Google.Protobuf** - Protobuf序列化支持
- **System.Threading.Tasks** - 异步编程支持

## 🔄 版本历史

### v2.0.0 (当前版本)
- ✅ 重构网络管理器架构，采用更清晰的模块化设计
- ✅ 实现完整的拦截器链系统
- ✅ 添加Protobuf完整支持和自动代码生成
- ✅ 优化WebSocket多连接管理
- ✅ 修复Google Protobuf标准类型冲突问题
- ✅ 添加任务优先级调度器
- ✅ 改进主线程调度机制
- ✅ 更新Unity API兼容性（修复FindObjectOfType过时警告）

### v1.0.0
- 初始版本，实现基本的HTTP和WebSocket功能

## 🚨 注意事项

1. **Unity版本兼容性**：建议使用Unity 2020.3或更高版本
2. **Protobuf冲突**：代码生成脚本已自动排除Google标准类型，避免冲突
3. **主线程安全**：所有UI操作必须通过UnityMainThread调度器执行
4. **资源清理**：在组件销毁时及时清理网络资源和事件监听器
5. **异常处理**：网络请求应始终包含适当的异常处理逻辑
