# Unity 网络请求拦截器链框架

本框架实现了与Android端相似的网络请求拦截器链逻辑，基于责任链模式设计，允许开发者在网络请求的不同阶段进行拦截和处理。

## 框架结构

### 核心接口和类
- **IInterceptor**：拦截器接口，定义了拦截处理方法
- **IInterceptorChain**：拦截器链接口，定义了获取请求和处理下一个拦截器的方法
- **Request**：请求对象，包含URL、方法、请求体和请求头等信息
- **Response**：响应对象，包含状态码、状态消息、响应体和响应头等信息
- **InterceptorChain**：拦截器链的具体实现，负责按顺序执行拦截器

### 拦截器管理器
- **InterceptorManager**：负责管理和执行所有拦截器

### 具体拦截器实现
- **LogInterceptor**：日志拦截器，记录网络请求和响应的详细信息
- **CompressInterceptor**：压缩拦截器，处理请求和响应数据的压缩和解压缩
- **EncryptInterceptor**：加密拦截器，处理请求和响应数据的加密和解密
- **RequestStrategyInterceptor**：请求策略拦截器，根据连接类型选择不同的请求策略
- **BIReportInterceptor**：业务报告拦截器，用于网络请求统计与上报
- **UnityRequestInterceptor**：Unity专用请求拦截器，支持长连接和短连接处理

### 适配器
- **InterceptorAdapter**：将拦截器链集成到现有的网络框架中
- **UnityRequestInterceptorAdapter**：简化UnityRequestInterceptor的使用

## 使用方法

### 1. 使用默认拦截器链发送请求

```csharp
// 发送HTTP请求
NetworkManager.Instance.SendHttpRequestWithInterceptor(url, jsonData, 
    (response) =>
    {
        Debug.Log("请求成功，响应数据: " + response);
    },
    (error) =>
    {
        Debug.LogError("请求失败: " + error);
    }
);

// 发送ProtoBuf请求
NetworkManager.Instance.SendHttpPostRequestWithInterceptor(url, protobufData, 
    (responseData) =>
    {
        Debug.Log("请求成功，响应数据长度: " + responseData.Length + " bytes");
    },
    (error) =>
    {
        Debug.LogError("请求失败: " + error);
    }
);
```

### 2. 自定义拦截器链

```csharp
// 获取拦截器管理器
InterceptorManager manager = NetworkManager.Instance.GetInterceptorManager();

// 清除所有默认拦截器
manager.ClearAllInterceptors();

// 添加自定义拦截器（按照执行顺序添加）
manager.AddInterceptor(new LogInterceptor());
manager.AddInterceptor(new CompressInterceptor());
manager.AddInterceptor(new RequestStrategyInterceptor());
manager.AddInterceptor(new EncryptInterceptor("YourKey", "YourIV"));
manager.AddInterceptor(UnityRequestInterceptorAdapter.Instance.GetInterceptor());
manager.AddInterceptor(new BIReportInterceptor());
```

### 3. 创建自定义拦截器

```csharp
public class CustomHeaderInterceptor : IInterceptor
{
    public async Task<Response> Intercept(IInterceptorChain chain)
    {
        var request = chain.Request();
        var newRequest = request.Clone();
        
        // 添加自定义头部信息
        newRequest.Headers["X-Custom-Header"] = "CustomValue";
        
        // 执行下一个拦截器
        return await chain.Proceed(newRequest).ConfigureAwait(false);
    }
}

// 添加自定义拦截器
NetworkManager.Instance.GetInterceptorManager().AddInterceptor(new CustomHeaderInterceptor());
```

## Unity请求拦截器使用说明

Unity请求拦截器是基于Android端`RequestInterceptor.kt`的逻辑在Unity工程中实现的一套完整网络请求拦截器。

### 功能特性

- 支持同步和异步网络请求
- 支持长连接（WebSocket）和短连接（HTTP/HTTPS）的自动选择
- 提供丰富的请求和响应处理API
- 支持请求超时控制和取消
- 提供友好的适配器接口，简化使用流程
- 支持JSON请求和响应的自动序列化和反序列化
- 完全兼容Unity项目的现有拦截器链

### 核心组件

#### 1. UnityRequestInterceptor

主要的拦截器实现，负责处理网络请求的发送和响应。实现了`IInterceptor`接口，可以无缝集成到现有拦截器链中。

##### 主要功能：
- 根据请求类型选择长连接或短连接
- 处理请求超时
- 支持请求取消
- 处理响应解析
- 实现与Android端类似的请求/响应处理逻辑

#### 2. UnityRequestInterceptorAdapter

提供更友好的API接口，简化拦截器的使用。采用单例模式设计，方便在项目中全局访问。

##### 主要功能：
- 提供单例访问模式
- 封装请求创建和发送的复杂逻辑
- 提供同步和异步请求方法
- 提供响应处理工具方法
- 支持JSON对象的自动序列化和反序列化
- 管理协程生命周期

### 集成到现有项目

要将Unity请求拦截器集成到现有项目中，只需将`UnityRequestInterceptor`添加到拦截器链中：

```csharp
// 创建拦截器管理器
InterceptorManager manager = new InterceptorManager();

// 添加其他拦截器
manager.AddInterceptor(new LogInterceptor());
manager.AddInterceptor(new CompressInterceptor());
manager.AddInterceptor(new EncryptInterceptor());

// 添加Unity请求拦截器
manager.AddInterceptor(UnityRequestInterceptorAdapter.Instance.GetInterceptor());

// 发送请求
Request request = new Request { Url = "https://example.com/api", Method = "GET" };
Response response = manager.Execute(request);
```

### 使用示例

#### 同步请求示例

```csharp
// 创建请求
var request = UnityRequestInterceptorAdapter.Instance.CreateRequest("https://example.com/api/data", "GET");

// 设置请求头
UnityRequestInterceptorAdapter.Instance.SetRequestHeader(request, "Accept", "application/json");

// 发送请求
Response response = UnityRequestInterceptorAdapter.Instance.SendRequest(request);

// 处理响应
if (UnityRequestInterceptorAdapter.Instance.IsResponseSuccess(response))
{
    string json = UnityRequestInterceptorAdapter.Instance.ParseJsonResponse(response);
    Debug.Log("请求成功: " + json);
}
else
{
    string error = UnityRequestInterceptorAdapter.Instance.GetResponseErrorMessage(response);
    Debug.LogError("请求失败: " + error);
}
```

#### 异步请求示例（使用Action回调）

```csharp
// 创建请求
var request = UnityRequestInterceptorAdapter.Instance.CreateRequest("https://example.com/api/data", "GET");

// 设置请求头
UnityRequestInterceptorAdapter.Instance.SetRequestHeader(request, "Accept", "application/json");

// 发送异步请求
UnityRequestInterceptorAdapter.Instance.SendRequestAsync(
    request,
    (response) => {
        // 成功回调
        string json = UnityRequestInterceptorAdapter.Instance.ParseJsonResponse(response);
        Debug.Log("请求成功: " + json);
    },
    (response) => {
        // 失败回调
        string error = UnityRequestInterceptorAdapter.Instance.GetResponseErrorMessage(response);
        Debug.LogError("请求失败: " + error);
    }
);
```

#### 发送JSON POST请求

```csharp
// 创建请求
var request = UnityRequestInterceptorAdapter.Instance.CreateRequest("https://example.com/api/submit", "POST");

// 创建JSON对象
var postData = new {
    name = "John",
    age = 30,
    email = "john@example.com"
};

// 设置JSON请求体
UnityRequestInterceptorAdapter.Instance.SetRequestBodyFromJson(request, postData);

// 发送异步请求
UnityRequestInterceptorAdapter.Instance.SendRequestAsync(
    request,
    (response) => {
        Debug.Log("提交成功");
    },
    (response) => {
        Debug.LogError("提交失败");
    }
);
```

#### 使用扩展请求格式

扩展请求格式提供了更多的配置选项，适用于更复杂的业务场景，与Android端的请求格式保持一致。

```csharp
// 创建扩展请求
var extendedRequest = UnityRequestInterceptorAdapter.Instance.CreateExtendedRequest(
    "https://example.com/api",    // URL
    "userService",               // 服务名称
    "getUserInfo",               // 方法名称
    RequestConnectionType.INCONSTANT,    // 连接类型
    ContentType.JSON,             // 内容类型
    "userToken123",              // Token
    5000                          // 超时时间（毫秒）
);

// 创建请求体数据
var requestData = new Dictionary<string, object>
{
    { "userId", "123456" },
    { "includeDetails", true }
};

// 设置请求体
string jsonBody = JsonUtility.ToJson(requestData);
UnityRequestInterceptorAdapter.Instance.SetRequestBody(extendedRequest.Request, jsonBody);

// 发送请求
Response response = UnityRequestInterceptorAdapter.Instance.SendExtendedRequest(extendedRequest);

// 处理响应
if (UnityRequestInterceptorAdapter.Instance.IsResponseSuccess(response))
{
    // 解析响应
    UserInfo userInfo = UnityRequestInterceptorAdapter.Instance.ParseJsonResponseToObject<UserInfo>(response);
    if (userInfo != null)
    {
        Debug.Log("获取用户信息成功: " + userInfo.name);
    }
}
```

#### 取消请求

```csharp
// 创建请求
var request = UnityRequestInterceptorAdapter.Instance.CreateRequest("https://example.com/api/long-operation", "GET");

// 生成请求ID
string requestId = System.Guid.NewGuid().ToString();
request.Tag = requestId;

// 发送异步请求
UnityRequestInterceptorAdapter.Instance.SendRequestAsync(
    request,
    OnSuccess,
    OnFailure
);

// 在需要的时候取消请求
void CancelLongOperation()
{
    UnityRequestInterceptorAdapter.Instance.CancelRequest(requestId);
}
```

### 高级配置

#### 自定义连接类型

框架支持两种连接类型：

- `RequestConnectionType.PERSISTENT`: 长连接（WebSocket）
- `RequestConnectionType.INCONSTANT`: 短连接（HTTP/HTTPS）

可以根据请求的特性选择合适的连接类型。

#### 自定义内容类型

支持多种内容类型：

- `ContentType.JSON`: JSON格式
- `ContentType.FORM`: 表单格式
- `ContentType.PROTOBUF`: Protobuf格式

#### 超时设置

可以在创建扩展请求时设置超时时间，默认为30秒。

### 错误处理

框架提供了多种错误处理方法：

```csharp
// 检查响应是否成功
if (UnityRequestInterceptorAdapter.Instance.IsResponseSuccess(response))
{
    // 处理成功情况
}
// 检查是否超时
else if (UnityRequestInterceptorAdapter.Instance.IsResponseTimeout(response))
{
    // 处理超时情况
}
// 检查是否内部错误
else if (UnityRequestInterceptorAdapter.Instance.IsResponseInternalError(response))
{
    // 处理内部错误
}
else
{
    // 处理其他错误
    string errorMessage = UnityRequestInterceptorAdapter.Instance.GetResponseErrorMessage(response);
    int errorCode = UnityRequestInterceptorAdapter.Instance.GetResponseStatusCode(response);
}
```

## 拦截器执行顺序

请求前的执行顺序：按照添加顺序依次执行

```
拦截器1 → 拦截器2 → 拦截器3 → ... → 实际请求发送
```

请求后的处理顺序：按照添加顺序的相反顺序执行

```
实际请求发送 → ... → 拦截器3 → 拦截器2 → 拦截器1
```

## 注意事项

1. 在实际项目中，加密密钥和IV应该从安全的配置中获取，而不是硬编码
2. 对于大型项目，可以根据实际需求扩展更多类型的拦截器
3. 拦截器的执行顺序非常重要，应该根据功能需求合理安排
4. 异步请求需要在Unity主线程中发起
5. 长时间运行的请求应该考虑设置合理的超时时间
6. 取消请求后，相应的回调仍会被调用，但会返回取消状态
7. 对于频繁的网络请求，建议使用连接池或复用请求对象
8. 在移动设备上，应该考虑网络状态的变化，避免在无网络时发起请求