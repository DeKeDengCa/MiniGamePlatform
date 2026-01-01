# 故障排除指南

## 常见错误及解决方案

### 1. 项目打开时出现错误

#### 问题描述
当打开unity-network项目时，Unity编辑器报告错误。

#### 可能原因及解决方案

##### NativeWebSocket插件缺失
**错误信息**：找不到NativeWebSocket相关的类型或命名空间
**解决方案**：
1. 从GitHub下载NativeWebSocket库：
   ```bash
   # 在项目根目录执行
   git clone https://github.com/endel/NativeWebSocket.git
   ```
2. 将NativeWebSocket库的DLL文件复制到`Assets/Plugins/`目录：
   ```bash
   cp NativeWebSocket/Plugins/WebSocket.jslib Assets/Plugins/
   cp NativeWebSocket/Plugins/WebSocket.dll Assets/Plugins/
   ```

##### 插件目录结构混乱
**问题**：项目中存在多个插件目录
**解决方案**：
1. 删除重复的插件目录：
   ```bash
   rm -rf Assets/Scripts/NetworkFramework/Plugins/
   ```
2. 确保所有插件都放在`Assets/Plugins/`目录下

##### Assembly Definition引用问题
**错误信息**：找不到程序集引用
**解决方案**：
1. 检查`NetworkFramework.asmdef`文件确保引用正确
2. 重新导入所有资源：
   - 在Unity编辑器中，选择`Assets > Reimport All`

### 2. 编译错误

#### ProtoBuf相关错误
**错误信息**：找不到Google.Protobuf命名空间
**解决方案**：
1. 确认`protobuf-net.dll`存在于`Assets/Plugins/ProtoBuf/`目录
2. 检查DLL文件的平台设置是否正确：
   - 在Unity编辑器中选中DLL文件
   - 在Inspector面板中确保所有平台都被选中

#### WebSocket相关错误
**错误信息**：找不到NativeWebSocket命名空间
**解决方案**：
1. 确认NativeWebSocket库已正确安装
2. 检查`WebSocketTransport.cs`文件中的using语句是否正确

### 3. 运行时错误

#### 网络连接失败
**错误信息**：无法连接到WebSocket服务器
**解决方案**：
1. 确认服务器正在运行
2. 检查连接URL是否正确
3. 确认防火墙设置允许连接

#### 消息处理错误
**错误信息**：消息反序列化失败
**解决方案**：
1. 确认ProtoBuf消息类已正确生成
2. 检查消息ID是否与注册的ID匹配

### 4. Meta文件错误

#### "has no meta file, but it is in an immutable folder"错误
**错误信息**：资源文件缺少对应的.meta文件
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

## 项目清理步骤

如果项目出现严重错误，可以按以下步骤清理：

1. 删除Library目录：
   ```bash
   rm -rf Library/
   ```

2. 重新打开Unity项目，让Unity重新生成Library目录

3. 确认所有插件都已正确安装

4. 重新导入所有资源

## 联系支持

如果以上解决方案都无法解决问题，请：
1. 检查Unity控制台中的完整错误信息
2. 提供错误信息和项目版本给技术支持团队