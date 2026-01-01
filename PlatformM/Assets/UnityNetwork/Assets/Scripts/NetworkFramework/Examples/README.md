# 网络框架UI测试指南

这个文档将指导您如何使用新添加的UI界面来单独调用网络框架中的各个功能示例。

## 功能概述

新添加的UI界面允许您：
- 单独调用WebSocket示例功能
- 单独调用HTTP示例功能  
- 单独调用拦截器示例功能
- 查看所有操作的日志输出

## 使用方法

### 自动初始化

1. 打开Unity编辑器并加载项目
2. 在场景中添加一个空游戏对象
3. 将`NetworkTestInitializer`组件添加到该游戏对象上
4. 默认情况下，`autoCreateUI`和`autoCreateNetworkManager`选项已经勾选，这将在场景加载时自动创建UI和NetworkManager
5. 运行游戏，UI界面将自动显示在屏幕中央

### 手动创建UI

如果您不想使用自动初始化功能，可以手动创建UI：

1. 在场景中创建一个空游戏对象
2. 将`NetworkTestUI`组件添加到该游戏对象上
3. 确保场景中已经存在`NetworkManager`对象
4. 运行游戏，UI界面将显示在屏幕中央

## UI界面功能说明

UI界面分为几个主要部分：

### 输入字段

- **WebSocket URL**: WebSocket服务器地址，默认为`ws://localhost:8080`
- **HTTP URL**: HTTP服务器地址，默认为`http://localhost:8080`
- **Player ID**: 玩家ID，用于测试ProtoBuf消息
- **Player Name**: 玩家名称，用于测试ProtoBuf消息
- **消息内容**: 要发送的JSON消息内容

### WebSocket功能按钮

- **连接WebSocket**: 连接到指定的WebSocket服务器
- **发送JSON消息**: 发送JSON格式的WebSocket消息
- **发送ProtoBuf消息**: 发送ProtoBuf格式的WebSocket消息
- **断开连接**: 断开WebSocket连接

### HTTP功能按钮

- **发送HTTP请求**: 发送普通的HTTP请求
- **发送带拦截器的HTTP请求**: 发送经过拦截器链处理的HTTP请求

### 拦截器功能按钮

- **使用默认拦截器链**: 使用NetworkManager中配置的默认拦截器链
- **设置自定义拦截器链**: 设置一个自定义的拦截器链

### 日志区域和清除按钮

- **日志区域**: 显示所有操作的日志输出，包括连接状态、发送/接收的消息等
- **清除日志**: 清除当前显示的所有日志

## 注意事项

1. 确保您的服务器已经启动并可以接受连接
2. 根据您的实际环境修改URL和测试数据
3. 所有操作的结果和错误信息都会显示在日志区域
4. 使用ProtoBuf功能时，请确保已经从.proto文件生成了相应的C#类
5. 如果需要添加更多的测试功能，可以扩展NetworkTestUI类

## 故障排除

- 如果UI没有显示，请检查是否正确添加了NetworkTestUI组件
- 如果网络请求失败，请检查服务器地址是否正确以及服务器是否正在运行
- 如果遇到编译错误，请确保所有必要的命名空间都已正确引用

祝您测试愉快！

## UnityRequestInterceptorExample 使用说明

### 功能概述

`UnityRequestInterceptorExample`是一个综合示例，展示了如何使用`UnityRequestInterceptor`处理各类网络请求，包括：
- 同步GET请求
- 异步GET请求（使用接口回调）
- 异步GET请求（使用Action回调）
- JSON格式POST请求
- 扩展请求格式
- 请求取消操作

### 使用方法

1. 在场景中添加一个空游戏对象
2. 将`UnityRequestInterceptorExample`组件添加到该游戏对象上
3. 默认情况下，脚本会在`Start()`方法中调用`SendAsynchronousGetRequestWithAction()`方法
4. 如果需要测试其他类型的请求，可以取消注释`Start()`方法中的相应代码行
5. 运行游戏，观察控制台输出的请求结果

### 关键方法说明

- **SendSynchronousGetRequest()**: 发送同步GET请求，会阻塞主线程直到请求完成
- **SendAsynchronousGetRequestWithInterface()**: 使用接口回调发送异步GET请求
- **SendAsynchronousGetRequestWithAction()**: 使用Action委托发送异步GET请求
- **SendJsonPostRequest()**: 发送JSON格式的POST请求
- **SendExtendedRequest()**: 使用扩展请求格式发送请求
- **ProcessResponse(Response response)**: 处理请求响应的通用方法
- **CancelRequestAfterDelay(string requestId, float delaySeconds)**: 延迟取消指定ID的请求

### 注意事项

- 示例中使用了`https://jsonplaceholder.typicode.com`作为测试服务器
- 实际项目中，请替换为您自己的服务器地址
- 所有请求的结果和错误信息都会显示在Unity编辑器的控制台中