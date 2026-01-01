### 用户

#### 用户信息

* ServiceName: gene-api
* 文件名：api/user.proto
* 方法列表：

| 方法名             | 说明                                                         |
| ------------------ | ------------------------------------------------------------ |
| GetUserInfo        | 获取用户信息, 通过参数 target_uid 与当前用户 uid 是否一致来区分主客态. |
| UpdateUserInfo     | 更新用户信息, 注册时首次填写用户资料也是这个接口             |
| GetInitInfo        | 获取注册页面的信息, 客户端应当在获取失败时也不影响注册流程.  |
| GetRandomNickname  | 获取一批随机昵称                                             |
| BatchGetUser       | 批量获取用户信息                                             |
| UpdateIntroduction | 更新用户个人介绍                                             |
| UpdateNickname     | 更新用户昵称                                                 |
| UpdateGender       | 更新用户性别                                                 |
| UpdateAvatar       | 更新用户头像                                                 |
| UpdateCountry      | 更新用户国家                                                 |
| UpdateBirthday     | 更新用户生日                                                 |

#### 用户相册

* ServiceName: gene-api
* 文件名：api/album.proto
* 方法列表：

| 方法名          | 说明         |
| :-------------- | ------------ |
| GetUserAlbum    | 获取用户相册 |
| UpdateUserAlbum | 更新用户相册 |
| DelUserAlbum    | 删除用户相册 |
| AddUserAlbum    | 增加用户相册 |

### 举报

* ServiceName: gene-api
* 文件名：api/feedback.proto
* 方法列表：

| 方法名               | 说明          |
| -------------------- | ------------- |
| ListFeedbackCategory | 举报分类-列表 |
| CreateFeedback       | 举报-新增     |

### 文件上传

* ServiceName: gene-api
* 文件名：api/file.proto
* 方法列表：

| 方法名            | 说明                                          |
| ----------------- | --------------------------------------------- |
| GetUploadInfo     | 获取文件上传信息, 返回文件上传的目标地址.     |
| ConfirmUploadInfo | 确认上传信息, 用于服务端对文件进行鉴黄等处理. |

### 分享

* ServiceName: gene-api
* 文件名：api/share.proto
* 方法列表：

| 方法名       | 说明         |
| ------------ | ------------ |
| GetShareInfo | 获取分享信息 |



### 融云IM

* ServiceName: gene-api
* 文件名：api/im.proto
* 方法列表：

| 方法名   | 说明                        |
| -------- | --------------------------- |
| GetToken | 获取token,定时获取刷新token |



### 上报协议

* ServiceName: gene-api
* 文件名：api/report.proto
* 方法列表：

| 方法名    | 说明     |
| --------- | -------- |
| Heartbeat | 心跳上报 |

### 菜单功能

* ServiceName: gene-api
* 文件名：api/menu.proto
* 方法列表：

| 方法名     | 说明                      |
| ---------- | ------------------------- |
| GetTabs    | 获取应用底部菜单 tab 配置 |
| GetBanners | 获取应用 banner 配置      |

## 房间

#### 房间列表

* ServiceName: gene-api
* 文件名：api/room_list.proto
* 方法列表：

| 方法名           | 说明                   |
| ---------------- | ---------------------- |
| ListRoomCategory | 获取房间分类列表       |
| ListRoomCard     | 获取房间卡片列表，首页 |

#### 房间模式

* ServiceName: gene-api
* 文件名：api/room_mode.proto
* 方法列表：

| 方法名                  | 说明                    |
| ----------------------- | ----------------------- |
| ListRoomModesByCategory | 模式分类&&模式配置-列表 |

#### 房间公屏消息

* ServiceName: gene-api
* 文件名：api/room_msg.proto
* 方法列表：

| 方法名          | 说明         |
| --------------- | ------------ |
| SendRoomMessage | 发送公屏消息 |

### 直播间

#### 设置相关

* ServiceName: gene-api
* 文件名：api/room.proto
* 方法列表：

| 方法名                | 说明             |
| --------------------- | ---------------- |
| SetRoomName           | 修改房间名称     |
| SetRoomIntro          | 修改房间公告     |
| SetRoomCover          | 修改房间封面     |
| SetRoomBackground     | 修改房间背景     |
| ChangeLayout          | 修改房间布局     |
| ChangeRoomMode        | 修改房间模式     |
| ChangeRoomPerm        | 修改房间权限     |
| ChangeSeatPerm        | 修改上麦模式     |
| ChangeSeatScoreSwitch | 修改麦位积分开关 |

#### 管理员

* ServiceName: gene-api
* 文件名：api/room.proto
* 方法列表：

| 方法名          | 说明               |
| --------------- | ------------------ |
| ListRoomAdmin   | 房间管理员列表     |
| SetRoomAdmin    | 设置房间管理员     |
| RemoveRoomAdmin | 移除房间管理员     |
| MyManageRooms   | 查询我管理房间信息 |

 #### 房间相关

* ServiceName: gene-api
* 文件名：api/room.proto
* 方法列表：

| 方法名             | 说明                                                         |
| ------------------ | ------------------------------------------------------------ |
| RefreshRtcToken    | 刷新rtc token                                                |
| RefreshRtmToken    | 刷新rtm token                                                |
| MyRoom             | 查询自己的房间信息                                           |
| BatchGetRooms      | 批量查询房间信息                                             |
| ListLayout         | 查询支持的房间模式列表                                       |
| GetRoomState       | 获取房间信息，包括房间信息和状态信息                         |
| GetUserCurrentRoom | 查询用户当前所在房间                                         |
| ListOnlineUser     | 查询房间在线的人列表                                         |
| BatchGetRoomUser   | 房间内批量查询用户信息                                       |
| Create             | 创建房间，用户第一次进自己房间，返回 code LIVE_ROOM_NOT_CREATE，然后走创建房间流程 |
| Enter              | 进入房间                                                     |
| Reenter            | 重连                                                         |
| Leave              | 离开房间                                                     |
| KickOff            | 踢出房间                                                     |

#### 用户麦位操作

* ServiceName: gene-api
* 文件名：api/room.proto
* 方法列表：

| 方法名    | 说明                                                         |
| --------- | ------------------------------------------------------------ |
| ListSeat  | 麦位列表                                                     |
| EnterSeat | 上麦/换麦                                                    |
| LeaveSeat | 下麦                                                         |
| OpenMic   | 开麦：麦上用户将自己麦位的麦克风开启，若被房主/管理员禁音，则无法开麦 |
| CloseMic  | 闭麦：麦上用户将自己麦位的麦克风关闭                         |

#### 管理员麦位操作

* ServiceName: gene-api
* 文件名：api/room.proto
* 方法列表：

| 方法名     | 说明                                                         |
| ---------- | ------------------------------------------------------------ |
| LockSeat   | 麦位封锁：将该麦位进行封锁，封锁后用户无法点击进行上麦       |
| UnlockSeat | 解除封锁：将该麦位进行解封，解封后用户可点击进行上麦         |
| MuteSeat   | 麦位禁音：将该麦位的麦克风禁音，禁音后该麦位仍可进行上下麦，但无法进行发音 |
| UnmuteSeat | 解除麦位禁音：将该麦位的禁音进行解除，解除后用户在该麦位可进行发音 |
| KickSeat   | 踢下麦：将麦上用户踢出麦位，但用户仍在该房间内，没有被踢出房间 |
| PickSeat   | 抱上麦：将房间内的用户邀请上麦，支持需/无需该用户本人同意，默认为需本人同意 |

#### 申请上麦

* ServiceName: gene-api
* 文件名：api/room.proto
* 方法列表：

| 方法名                  | 说明              |
| ----------------------- | ----------------- |
| ApplyEnterSeat          | 申请上麦          |
| GetApplyEnterSeatStatus | 获取申请上麦状态  |
| CancelApplyEnterSeat    | 取消申请上麦      |
| AcceptSeatInvitation    | 接受邀请上麦      |
| RejectSeatInvitation    | 拒绝邀请上麦      |
| ApplyEnterSeatUserList  | 申请上麦用户列表  |
| ApproveApplyEnterSeat   | 申请上麦审批-同意 |
| RejectApplyEnterSeat    | 申请上麦审批-拒绝 |

### 房间背景

* ServiceName: privilege-api
* 文件名：protobuf/api/privilege/{category|mall|bag}.proto
* 方法列表：

| 方法名            | 说明             | 文件名         |
| ----------------- | ---------------- | -------------- |
| ListCategory      | 分类列表         | category.proto |
| ListMallGoods     | 房间背景商品列表 | mall.proto     |
| GetUsingPrivilege | 查询使用中权益   | bag.proto      |

### 用户/房间关系

#### 关系数量

* ServiceName: relation-api
* 文件名：protobuf/api/relation/num.proto
* 方法列表：

| 方法名             | 说明             |
| ------------------ | ---------------- |
| GetUserRelationNum | 获取用户关系数量 |
| GetRoomRelationNum | 获取房间关系数量 |

#### 房间黑名单

* ServiceName: relation-api
* 文件名：protobuf/api/relation/room_black.proto
* 方法列表：

| 方法名              | 说明                       |
| ------------------- | -------------------------- |
| AddRoomBlackUser    | 添加用户到黑名单           |
| RemoveRoomBlackUser | 移除用户黑名单             |
| GetRoomBlackList    | 获取黑名单列表             |
| CheckInRoomBlack    | 批量检查用户是否在黑名单中 |

#### 房间关注

* ServiceName: relation-api
* 文件名：protobuf/api/relation/room_follow.proto
* 方法列表：

| 方法名           | 说明                              |
| ---------------- | --------------------------------- |
| FollowRoom       | 关注房间                          |
| UnFollowRoom     | 取消关注                          |
| GetFollowRoomIDs | 获取用户关注房间ID列表            |
| GetRoomFansIDs   | 粉丝列表 （关注房间的用户ID列表） |

#### 用户黑名单

* ServiceName: relation-api
* 文件名：protobuf/api/relation/user_black.proto
* 方法列表：

| 方法名               | 说明                           |
| -------------------- | ------------------------------ |
| AddBlackUser         | 添加用户黑名单                 |
| RemoveBlackUser      | 移除用户黑名单                 |
| GetBlackUserList     | 获取用户黑名单列表             |
| CheckUserBlackStatus | 检查用户拉黑状态               |
| CheckInUserBlack     | 批量检查用户是否在黑名单中     |
| AddUserRoomBlack     | 添加用户房间黑名单             |
| RemoveUserRoomBlack  | 移除用户房间黑名单             |
| GetUserRoomBlackList | 获取用户房间黑名单列表         |
| CheckInUserRoomBlack | 批量检查是否在用户房间黑名单中 |

#### 用户关注

* ServiceName: relation-api
* 文件名：protobuf/api/relation/user_follow.proto
* 方法列表：

| 方法名        | 说明     |
| ------------- | -------- |
| Follow        | 关注     |
| UnFollow      | 取消关注 |
| GetFollowUIDs | 关注列表 |
| GetFansIDs    | 粉丝列表 |
| CheckIsFollow | 是否关注 |

#### 好友列表

* ServiceName: relation-api
* 文件名：protobuf/api/relation/user_friend.proto
* 方法列表：

| 方法名        | 说明     |
| ------------- | -------- |
| GetFriendUIDs | 好友列表 |

#### 关注列表-带房间信息

* ServiceName: gene-api
* 文件名：api/follow.proto
* 方法列表：

| 方法名            | 说明                   |
| ----------------- | ---------------------- |
| GetFollowRoomList | 获取我关注的房间列表   |
| GetBrowseRoomList | 获取我浏览过的房间列表 |

### 账号服务

* ServiceName: rpc.social.account
* 文件名：protobuf/api/account/account.proto
* 方法列表：

| 方法名                | 说明                         |
| --------------------- | ---------------------------- |
| Login                 | 登录注册, 首次登录即注册     |
| Logout                | 登出                         |
| Withdraw              | 注销                         |
| WithdrawCancel        | 取消注销                     |
| UsePassword           | 是否使用密码登录             |
| SendCode              | 发送验证码，验证码分使用场景 |
| ChangePassword        | 修改密码                     |
| ResetPassword         | 重置密码                     |
| CheckVerificationCode | 校验验证码-分场景            |
| RefreshToken          | 刷新token                    |
| ListCountry           | 获取国家列表                 |

### 营收服务

#### 礼物

* ServiceName: api.micro.social.revenue
* 文件名：protobuf/api/revenue/gift.proto
* 方法列表：

| 方法名                    | 说明                   |
| ------------------------- | ---------------------- |
| GetGiftPanel              | 获取礼物面板           |
| GetGiftBag                | 获取礼物背包           |
| SendGift                  | 赠送礼物               |
| ListGiftRecord            | 查询礼物记录           |
| GetGiftStats              | 获取特殊礼物的统计数据 |
| ListGiftStats             | 分页获取收送礼统计     |
| GetCustomizedSendGiftInfo | 获取业务自定义送礼信息 |
| ClaimGift                 | 领取礼物               |

#### 货币兑换

* ServiceName: api.micro.social.revenue
* 文件名：protobuf/api/revenue/exchange.proto
* 方法列表：

| 方法名            | 说明         |
| ----------------- | ------------ |
| GetExchangeConfig | 获取兑换配置 |
| Exchange          | 兑换         |
| ExchangeRecord    | 兑换记录     |
| PreExchange       | 预兑换       |

#### 钱包

* ServiceName: api.micro.social.revenue
* 文件名：protobuf/api/revenue/wallet.proto
* 方法列表：

| 方法名                    | 说明                                  |
| ------------------------- | ------------------------------------- |
| GetBalance                | 获取自己的余额, 支持多个币种同时查询. |
| ListTransactionDetail     | 查询交易记录                          |
| ListTransactionSearchType | 获取交易记录类型,根据不同场景返回     |
| TransferBalance           | 转账                                  |