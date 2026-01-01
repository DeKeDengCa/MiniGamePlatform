# 迁移指南

本指南将帮助您将现有项目迁移到最新的网络框架版本。

## 从旧版本迁移

### 1. 插件目录结构调整

#### 问题描述
旧版本的项目可能包含重复的插件目录结构：
- `Assets/Scripts/NetworkFramework/Plugins/`
- `Assets/Plugins/`

#### 解决方案
1. 删除重复的插件目录：
   ```bash
   rm -rf Assets/Scripts/NetworkFramework/Plugins/
   ```

2. 确保所有插件都放在统一的`Assets/Plugins/`目录下

### 2. NativeWebSocket插件安装

#### 问题描述
NativeWebSocket插件可能未正确安装或引用路径错误。

#### 解决方案
1. 使用Package Manager安装（推荐）：
   - 打开Unity编辑器
   - 点击菜单栏 `Window` -> `Package Manager`
   - 点击左上角的`+`号
   - 选择`Add package from git URL...`
   - 输入以下URL并点击`Add`：
     ```
     https://github.com/endel/NativeWebSocket.git#upm
     ```

2. 或使用自定义菜单命令：
   - 在Unity编辑器中，找到并点击菜单栏最后的 `NetworkFramework` 菜单
   - 选择 `Install NativeWebSocket` 选项

### 3. ProtoBuf库更新

#### 问题描述
ProtoBuf库版本可能过旧或不兼容。

#### 解决方案
1. 访问ProtoBuf-net GitHub仓库：https://github.com/protobuf-net/protobuf-net
2. 下载最新版本的预编译DLL文件
3. 将DLL文件放入以下目录：
   ```
   Assets/Plugins/ProtoBuf/
   ```

### 4. 代码引用更新

#### 问题描述
代码中可能存在过时的引用或API调用。

#### 解决方案
1. 更新所有引用：
   ```csharp
   // 旧引用
   using NetworkFramework.Core;
   
   // 新引用
   using NetworkFramework;
   ```

2. 更新消息处理器注册方式：
   ```csharp
   // 旧方式
   NetworkManager.Instance.RegisterMessageHandler<PlayerInfo>(1001, OnPlayerInfoReceived);
   
   // 新方式
   MessageHandlerManager.RegisterHandler<PlayerInfo>(1001, OnPlayerInfoReceived);
   ```

## 常见问题解决

### 1. 编译错误

#### "找不到类型或命名空间"错误
**解决方案**：
1. 确认所有必要的插件都已正确安装
2. 检查Assembly Definition文件引用
3. 重新导入所有资源：`Assets > Reimport All`

### 2. 运行时错误

#### "无法加载DLL"错误
**解决方案**：
1. 检查插件DLL文件的平台设置
2. 确认DLL文件适用于当前平台
3. 重新安装相关插件

### 3. 网络连接问题

#### "连接被拒绝"错误
**解决方案**：
1. 确认服务器正在运行
2. 检查防火墙设置
3. 验证服务器地址和端口配置

### 4. Meta文件问题

#### "has no meta file, but it is in an immutable folder"错误
**解决方案**：
1. 使用我们提供的Meta文件修复工具：
   - 在Unity编辑器中，点击 `NetworkFramework` -> `Fix Meta Files`
   - 点击 `Fix Missing Meta Files` 按钮
   - 点击 `Remove Orphaned Meta Files` 按钮

2. 手动修复步骤：
   - 关闭Unity编辑器
   - 删除以下目录（它们会在下次打开项目时重新生成）：
     ```
     Library/
     Temp/
     Obj/
     ```
   - 重新打开Unity项目

3. 如果问题仍然存在：
   - 确保项目根目录有正确的.gitignore文件
   - 检查是否有文件权限问题
   - 在终端中运行以下命令重新生成meta文件：
     ```bash
     find Assets -name "*.cs" -o -name "*.js" -o -name "*.shader" -o -name "*.dll" | while read file; do if [ ! -e "${file}.meta" ]; then touch "${file}.meta"; fi; done
     ```

## 验证迁移结果

### 1. 检查插件状态
使用插件检查器验证所有必要插件是否已正确安装：
- 在Unity编辑器中，点击 `NetworkFramework` -> `Check Plugin Status`

### 2. 运行示例场景
打开示例场景验证功能是否正常：
- 打开 `Assets/Scenes/MainScene.unity`
- 点击Play按钮运行场景

### 3. 检查控制台输出
查看Unity控制台是否有任何错误或警告信息。

## 支持与反馈

如果在迁移过程中遇到任何问题，请：
1. 检查Unity控制台中的完整错误信息
2. 参考故障排除指南
3. 联系技术支持团队