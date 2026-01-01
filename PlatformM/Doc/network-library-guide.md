#UnityBridge的接口定义 https://nemo.yuque.com/elxshe/unity/ypw0fpqbvtg6c72u
《游戏-原生SDK交互接口定义文档》 https://nemo.yuque.com/elxshe/unity/gfs8owxe4131z16c
#


# Network Library Guide（网络库使用指南）

本指南覆盖两部分：
- `Assets/Scripts/GameFramework/Network`：上一套程序迁移来的“游戏侧封装”（对业务更友好）。
- `Assets/Scripts/NetworkFramework`：底层网络库（拦截器链、加密/压缩、HTTP + WebSocket 适配器等）。

并重点补全 **LoginSystem 登录全流程细节**（含 `GetOrUpdateUserInfo`、`DownloadAvatar`、`UpdateUserAvatar` 上传链路等）。

---

## 1. 关键概念与分层

### 1.1 NetworkSystem / NetworkChannel（GameFramework 层）
- `NetworkSystem`：负责保存全局的短链/长链基址，管理多个 `NetworkChannel`，并在 `Clock` Tick 中驱动（当前 `NetworkChannel.OnTick` 为空实现）。
- `NetworkChannel`：一个“业务通道”，持有：
  - `inconstantUrl`（短链 HTTP）
  - `persistentRawUrl`（长链 raw ws，用于 Connect）
  - `persistentUrl`（可能带 `to-biz` 的 ws，用于实际请求与 push 订阅）
  - `accountToken`（token）
  - `PushMessageHandler`（推送回调）

### 1.2 NetworkManager（NetworkFramework 层）
- `NetworkManager`：单例入口，核心能力：
  - 初始化：`Init(inconstantUrl, persistentUrl, token, appNetConfig)`
  - 请求：`Request(Request, CancellationToken)`（通过拦截器链）
  - 长链建连：`Connect(wsUrl, CancellationToken)`（仅建立连接，不发送数据）
  - push 分发：按 URL 维度注册/注销 push handler
- 适配器：
  - 短链：`HttpNetworkAdapter`（BestHTTP HTTP POST）
  - 长链：`WebSocketManager` → `BestHttpWebSocketAdapter`（BestHTTP WebSocket）
- 默认拦截器链（构造函数里注入）：
  - `LogInterceptor` → `CompressInterceptor` → `EncryptInterceptor` → `HeaderInterceptor` → `NetworkRequestInterceptor(选择适配器)`

---

## 2. 网络初始化：何时发生、顺序是什么

### 2.1 系统注册（ServiceLocator）
在 `GameFramework.Launch.GameInitiator.InitSystems()` 中注册：
- `ServiceLocator.Register(new NetworkSystem())`
- `ServiceLocator.Register(new LoginSystem())` 等

### 2.2 启动期初始化（注入 URL + 公钥）
在 `GameFramework.Launch.Logic.LaunchLogic.Startup()`：
- 调用 `UnityBridge.getConfigInfo`
- 返回字段（Editor 模拟见 `EditorBridgeImpl`）：
  - `publicKey` / `publicKeyId`
  - `inconstantUrl`（HTTP 网关基址）
  - `persistentUrl`（WebSocket 网关基址）
- 随后调用：`NetworkSystem.Initialize(inconstantUrl, persistentUrl, new AppNetConfig(publicKeyId, publicKey))`
  - 内部会调用：`NetworkManager.Instance.Init(...)`

### 2.3 登录后初始化（注入 token + 建 channel）
在 `GameFramework.Login.LoginSystem.SetLoginResult(LoginResult)`：
- `NetworkSystem.SetAccountToken(loginResult.AccessToken)`（更新已有 channel 的 token）
- `NetworkSystem.CreateChannel(NetworkConstants.DefaultNetworkChannel, options)`（创建默认 channel）
  - 注意：当前代码里是 **先 SetAccountToken 再 CreateChannel**，因此 token 更新“已有 channel”不会影响刚创建的默认 channel；但 CreateChannel 会把 token 传给 `NetworkChannel` 构造函数，所以默认 channel 仍然拥有 token。

---

## 3. URL query 参数：含义、来源、复制规则

### 3.1 来源：平台下发为主，Unity 侧少量追加
- **平台下发（`UnityBridge.getConfigInfo`）**
  - Editor 模拟（`EditorBridgeImpl`）示例：
    - `inconstantUrl = http://dev-api.ruok.live/sgw/api?app=astrorise`
    - `persistentUrl = ws://dev-api.ruok.live/sgw/ws?app=astrorise`
  - `app`：网关识别应用/业务域的参数（Unity 侧应按原样透传）。

- **Unity 侧追加（`NetworkSystem.CreateChannel`）**
  - `debug-uid=<uid>`：仅 `UNITY_EDITOR` 下追加，用于联调定位用户（由 `NetworkChannelOptions.DebugUserId` 提供）。
  - `to-biz=<biz>`：当 `NetworkChannelOptions.ToBiz` 非空时追加，用于**长连接（WebSocket）握手阶段的业务路由**（例如 `bbq`）。

#### 3.1.1 `to-biz` 的实际作用（结合本工程实现）
结合本工程的调用路径，`to-biz` 的作用可以用一句话概括：

> **它把“同一个网关入口的 WebSocket 长连接”路由到指定业务（biz）的后端通道/服务集群，从而决定该连接能收到/能发送哪些业务的 push 与长链请求。**

在代码层面的体现：
- **拼接位置**：`NetworkSystem.CreateChannel` 在构造 `persistentConnectionUrl` 时追加 `&to-biz=<ToBiz>`。
- **影响范围**：该 `persistentConnectionUrl` 会被写入 `Request.PersistentConnectionUrl`，从而影响：
  - `NotifyRequest(...)`（长链请求）走哪条 ws URL
  - `RegisterPushHandler(persistentConnectionUrl, ...)`（push 订阅）绑定在哪条 ws URL
- **实践例子（BBQ）**：
  - 进入玩法后创建 `bbqChannel`：`ToBiz="bbq"` → 该 channel 的 ws URL 带 `to-biz=bbq`
  - 当收到匹配相关 push 后，用 `bbqChannel` 发 `Game.EnterRoom`（长链），确保请求落到 BBQ 业务通道。

语雀参考（参数口径）：[`to-biz` 等参数说明](https://nemo.yuque.com/zgk9q7/infra/pcrot4zvhtbtgbb5#kgx6j)

#### 3.1.2 其它常见参数：哪些在 URL，哪些不在 URL
在本工程里，“看起来像参数”的东西分两类：

- **A. URL query 参数（主要影响网关路由/握手/调试）**
  - `app`：应用/业务域标识（由平台下发，Unity 透传）
  - `to-biz`：长连接业务路由（Unity 侧按 channel 追加）
  - `debug-uid`：Editor 联调辅助参数（Unity 侧仅 Editor 追加）
  - `uid`：某些环境可能由平台/网关追加；本工程在 push 订阅匹配时会移除 `uid`（见 3.2）

- **B. “公参”（不一定在 URL，更多是放在请求 Header 里）**
  - NetworkFramework 里存在 `PublicParams`，会编码为 `X-Pubpara-Bin` header（`HeaderInterceptor` 添加）
  - 字段大致对应：
    - `ver/verc`：客户端版本名/版本号
    - `did`：设备 ID
    - `lan/cou`：语言/国家码
    - `pkg`：包名
    - `gender`：性别（如果业务使用）
    - `rel/cha/subCha`：发布/渠道相关信息（通常用于灰度、统计、路由）
    - `biz/bizv`：业务标识/业务版本（与 `to-biz` 的区别是：它更偏“请求公参”，而 `to-biz` 是“长连接握手路由”）
  - 现状提醒：`LaunchLogic.Startup` 里 `GetPublicParamAsync()` 目前是注释掉的，因此这些公参未必已经在运行时真实注入。

### 3.2 `uid=`：订阅匹配的“清洗”规则（重要）
`NetworkManager` 内部对 push 订阅 URL 做了清洗：`CleanUrl(url)` 会把 query 中的 `uid=` 移除后作为订阅 key。

这意味着：
- 某些环境下 `persistentUrl` 可能被平台/网关追加 `uid=`，但 **push 订阅匹配时会忽略 `uid`**，避免同一条长链因 uid 变化导致“订阅不命中”。

### 3.3 如何复制/复用 URL（给策划/联调/写配置的人看的）
- **基址**：永远以 `getConfigInfo` 返回的 URL 为准（它包含服务端所需的基础 query）。
- **追加参数**：只追加你需要的参数，并正确拼接 `?` 与 `&`。
  - 当前 `CreateChannel` 直接用 `&debug-uid=...` / `&to-biz=...` 拼接，隐含前提：基址已经有 `?`（例如 `?app=...`）。
  - 如果未来 `getConfigInfo` 下发的 URL 不含 `?`，则需要在实现侧补齐“健壮拼接”。

---

## 4. 短链接 vs 长链接：应该怎么调用

### 4.1 短链接（HTTP）：SendRequest
- 调用入口：`NetworkSystem.SendRequest(...)` / `NetworkChannel.SendRequest(...)`
- 连接类型：`ConnectionType.INCONSTANT`
- 传输：`HttpNetworkAdapter`（BestHTTP）对 `inconstantUrl` 发 HTTP POST
- 特点：
  - `service/method/seqId/routeKey/加密信息` 等不放在 URL 中，而是通过控制字段与 headers 编码传递
  - token 作为 header：`X-Token-Bin`

### 4.2 长链接（WebSocket）：NotifyRequest
- 调用入口：`NetworkSystem.NotifyRequest(...)` / `NetworkChannel.NotifyRequest(...)`
- 连接类型：`ConnectionType.PERSISTENT`
- 传输：`WebSocketManager` → `BestHttpWebSocketAdapter`
- 建连方式：
  - **懒连接**：第一次 `NotifyRequest` 会自动触发 `ConnectAsync`
  - **显式预连接**：`NetworkChannel.Connect()`
    - 当前默认 channel 的 `Connect()` 用的是 `persistentRawUrl`（不带 `to-biz`）；而“玩法 channel”通常会带 `to-biz`，第一次 `NotifyRequest` 时会按 `persistentUrl` 懒连接。

---

## 5. LoginSystem 登录流程（全细节）

本节目标：把“点登录 → 拿到 token → 拉/补用户信息 → 进入 Gameplay”这段网络链路讲清楚，便于排查卡登录、卡头像、卡进玩法。

### 5.1 入口：显示登录 UI（不自动登录）
`LoginSystem.TryLogin()`：
- 加载并实例化 `Resources/Login/UILogin` Prefab
- `DontDestroyOnLoad`
- 调用 `UILoginScreen.Initialize()` 绑定按钮回调
- 调用 `UILoginScreen.Reset()` 放开按钮交互

### 5.2 登录按钮点击：先复用缓存，再走 Native 登录
`UILoginScreen.Login(loginType)`：
- 先把按钮/条款 Toggle 置为不可交互
- 调用 `LoginSystem.TryUseExistingLoginResult()`：
  - 如果 `PlayerPrefs` 里存在 `LoginSystem.LoginResultKey` 对应字符串，尝试反序列化 `LoginResult`
  - 若成功：直接走后续“常规登录流程”（相当于跳过 Native 登录）
- 若没有缓存或反序列化失败：调用 `UnityBridge.Instance.Login(loginType)`（Native 登录）

补充：`UILoginScreen.OnInputChanged` 会把输入框内容写入 `LoginSystem.LoginResultKey`，用于决定缓存 key。

### 5.3 Native 登录回调：loginCompletion
Native 会回调到 `GameFrameCallbackHandlers.CallUnityMessageCallbackSender("loginCompletion", ext, completion)`：
- `code == 1`：成功
  - 从 `ext["data"]` 中读取：
    - `type / uid / isFirstRegister / access_token / refresh_token / username / avatarUrl`
  - 组装为 `GameFramework.Login.LoginResult` 并调用 `LoginSystem.OnLoginResult(loginResult)`
- `code == -999`：取消 或其他 code：`LoginSystem.OnLoginResult(null)`

### 5.4 收到 LoginResult：写入 PlayerSystem、注入 token、创建默认 channel
`LoginSystem.OnLoginResult(loginResult)`：
- loginResult 非空：
  - `SetLoginResult(loginResult)`
  - `GetOrUpdateUserInfo()`
- loginResult 为空：`_loginScreen.Reset()` 允许重新点登录

`LoginSystem.SetLoginResult(loginResult)` 做的事：
- `PlayerSystem.SetLoginResult(loginResult)`（后续通过 `playerSystem.SelfUid` 读 uid）
- `NetworkSystem.SetAccountToken(loginResult.AccessToken)`（更新所有已存在 channel 的 token）
- 创建默认 channel：`NetworkSystem.CreateChannel(NetworkConstants.DefaultNetworkChannel, options)`
  - `options.AccountToken = access_token`
  - `options.DebugUserId = playerSystem.SelfUid`
  - 该 channel 会向 `NetworkManager` 注册 push handler（按 URL）

### 5.5 GetOrUpdateUserInfo：首登 vs 非首登两条分支
`LoginSystem.GetOrUpdateUserInfo()`：

#### 5.5.1 非首登（IsFirstRegister=false）
- 发送短链请求：`User.GetUserInfo`
  - service：`NetworkConstants.UserService`（`astrorise.platform.user`）
  - method：`NetworkConstants.GetUserInfoMethod`（`User.GetUserInfo`）
  - body：`GetUserInfoReq { TargetUid=selfUid, GameId=GameFrameworkConfigs.GameId }`
- 回调 `OnGetUserInfoResponse`：
  - 解析 `GetUserInfoRsp`
  - `PlayerSystem.SetUserInfo(rsp)`
  - `OnLoginFinish()`（进入下一阶段：保存缓存并进入玩法）

#### 5.5.2 首登（IsFirstRegister=true）
目标：保证用户至少拥有一个头像，并同步到服务端（即使上传失败也会尝试走 UpdateUserInfo）。

分两种情况：

- **(A) loginResult.AvatarUrl 为空**：用本地默认头像
  - `Resources.Load<Sprite>("Images/DefaultAvatar")`
  - `sprite.texture.EncodeToPNG()` 得到 png bytes
  - 创建 `_updateUserAvatar = new UpdateUserAvatar("image/png", bytes)`
  - 订阅 `_updateUserAvatar.Callback += OnUpdateUserAvatarCallback`
  - `_updateUserAvatar.Execute()`（进入“上传链路”）

- **(B) loginResult.AvatarUrl 非空**：先下载再上传
  - 调用 `DownloadAvatar(avatarUrl, retryAttempt: 3)`
  - `ImageSystem.Download(avatarUrl, callback)`：
    - 成功：`texture.EncodeToPNG()` → 走与 (A) 相同的上传链路
    - 失败：递归重试 `retryAttempt - 1`；重试耗尽后当前实现是“静默失败”（没有 UI 提示，也不会继续调用 UpdateUserInfo）

> 注意：首登分支里，“下载失败最终静默”的行为与“尽量保证进入玩法”目标可能不一致，排查卡首登时需要重点关注这里是否有异常被吞掉。

#### 5.5.3 首登头像上传链路（UpdateUserAvatar）
`UpdateUserAvatar.Execute()` → `RequestUploadInfo(3)`：

1) **请求上传信息（短链 HTTP）**
- 请求：`Common.GetUploadInfo`
  - method：`NetworkConstants.GetUploadInfoMethod`（`Common.GetUploadInfo`）
  - body：`GetUploadInfoReq { Scene=Avatar, FileType="IMAGE", MimeType="image/png" }`
- 成功响应：解析 `GetUploadInfoRsp`，得到：
  - `_putUrl`：对象存储/上传地址（外部直传）
  - `_getUrl`：访问地址（用于确认）
  - `_filePath`：服务端识别用的文件路径（最终写回用户资料）
- 成功后进入 `UploadImage(3)`
- 失败：重试 `retryAttempt - 1`；耗尽后 `Callback(false)`

2) **上传图片（外部 HTTP PUT，不走网关）**
- `ImageSystem.Upload(bytes, mime, putUrl, callback)`：
  - HTTP PUT 到 `_putUrl`
  - Header：`Content-Type: image/png`
  - Body：png bytes
- 成功：进入 `ConfirmUpload()`
- 失败：重试；耗尽后 `Callback(false)`

3) **确认上传（短链 HTTP）**
- 请求：`Common.ConfirmUpload`
  - method：`NetworkConstants.ConfirmUploadMethod`（`Common.ConfirmUpload`）
  - body：`ConfirmUploadReq { GetUrl=_getUrl, FilePath=_filePath }`
- 成功响应：解析 `ConfirmUploadRsp`
  - `Passed=true` → `Callback(true)`
  - 否则 → `Callback(false)`

#### 5.5.4 上传回调：无论成功失败都会 UpdateUserInfo
`LoginSystem.OnUpdateUserAvatarCallback(bool success)`：
- 当前实现 **不区分 success**：两支都会调用 `UpdateUserInfo()`

`LoginSystem.UpdateUserInfo()`（短链 HTTP）：
- 请求：`User.UpdateUserInfo`
  - method：`NetworkConstants.UpdateUserInfoMethod`（`User.UpdateUserInfo`）
  - body：`UpdateUserInfoReq`
    - `BaseUserInfo.Uid = loginResult.Uid`
    - `BaseUserInfo.Avatar = _updateUserAvatar.FilePath`（注意这里用的是 FilePath，不是 URL）
    - `GameUserInfo.GameId = GameFrameworkConfigs.GameId`
- 回调 `OnUpdateUserInfoResponse`：
  - 解析 `UpdateUserInfoRsp`
  - `PlayerSystem.UpdateUserInfo(rsp)`
  - `OnLoginFinish()`

### 5.6 登录收尾：落缓存、进入 Gameplay
`LoginSystem.OnLoginFinish()`：
- 强制把 `playerSystem.LoginResult.IsFirstRegister = false`
- `PlayerPrefs.SetString(LoginResultKey, Json(loginResult))` 并 `PlayerPrefs.Save()`
- `LoadGame().Forget()`：
  - 清理并销毁登录 UI
  - Editor：直接从 `AppDomain` 找到 `Gameplay` 程序集
  - 非 Editor：`AssemblyRegistry.Get("Gameplay")`
  - 反射调用：`Gameplay.Entry.GameEntry.Run()`

### 5.7 GetUserTitle：登录阶段的额外短链请求
`GetOrUpdateUserInfo()` 最后总会调用 `playerSystem.GetUserTitleReq()`：
- 仅在 `_titleIdList == null` 时发送短链 `User.GetUserTitle`

---

## 6. 进入玩法后：channel、长连接、push、EnterRoom

### 6.1 创建玩法 channel（带 to-biz）
`Gameplay.Entry.GameEntry.InitNetworkChannels()`：
- 创建 `bbqChannel`
- `NetworkChannelOptions.ToBiz = "bbq"`（会为 ws url 追加 `&to-biz=bbq`）
- `OnPushMessage = BBQFacade.OnPushMessage`

### 6.2 建立默认长连接并开始收 push
`Gameplay.Facade.GameplayFacade.InitializeFacade()`：
- 找到默认 channel：`defaultChannel`
- 追加 `PushMessageHandler += OnPushMessage`
- 调用 `defaultChannel.Connect()` 建立长连接（ws）

### 6.3 关键 push：匹配通知 → EnterRoom（长链）
`GameplayFacade.OnPushMessage(PushMessage)`：
- 当 `NotifyPkg == "api.astrorise.platform.NotifyMatchGame"`：
  - 解析 `NotifyMatchGame`
  - 使用 **bbqChannel** 发送长链 `NotifyRequest`：
    - service：`GameplayNetworkConstants.BBQService`（`astrorise.argames.bbq.room`）
    - method：`GameplayNetworkConstants.EnterRoomMethod`（`Game.EnterRoom`）
  - 成功后：`BBQFacade.ReadyGame(rsp.RoomInfo)`

---

## 7. 常见问题（排查 Checklist）

### 7.1 卡在“登录后黑屏/不进玩法”
- 是否走到了 `OnLoginFinish()`（可搜日志/断点）
- 是否 `GetUserInfo`/`UpdateUserInfo` 的回调里出现 exception（当前回调只在 exception==null 时继续）
- 是否 `AssemblyRegistry.Get("Gameplay")` 为空（非 Editor 环境）

### 7.2 首登头像相关问题
- `DownloadAvatar` 失败最终是静默的（没有回退到默认头像），可能导致首登流程不完整
- 上传链路里：
  - `GetUploadInfo` 是短链网关请求
  - `PUT putUrl` 是直传对象存储（不走网关）
  - `ConfirmUpload` 再回到短链网关

### 7.3 收不到 push
- 是否调用了 `channel.Connect()`（默认 channel 在 `GameplayFacade.InitializeFacade` 里连接）
- 是否对同一条 URL 注册了 push handler（`NetworkChannel` 构造时会注册）
- 注意 `uid=` 会在订阅匹配时被清洗：同 URL 不同 uid 仍应匹配同一订阅 key


