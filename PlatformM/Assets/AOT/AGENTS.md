# AOT Agent Notes

## HotUpdateManager Editor 环境判断规则

### 设计原则

**HotUpdateManager 不使用 `UNITY_EDITOR` 宏进行业务逻辑判断。**

### 统一方式

1. **设置 Editor 环境**：在 `Main.cs` 中调用 `HotUpdateManager.SetIsEditorEnvironment(bool isEditor)` 设置是否在 Editor 环境
2. **判断 Editor 环境**：所有 Editor 相关的判断都使用 `_isEditorEnvironment` 参数进行运行时判断
3. **Editor 模式控制**：通过 `SetUseEditorMode(bool useEditorMode)` 控制是否使用 Editor 模式（仅在 Editor 环境下有效）

### 实现细节

- `_isEditorEnvironment`：由外部设置（通常在 `Main.cs` 中），表示是否在 Editor 环境
- `_useEditorMode`：Editor 模式开关，仅在 `_isEditorEnvironment == true` 时有效
- 所有 Editor 相关的 API 调用（如 `EditorSimulateModeHelper`、`EditorSimulateModeParameters`）都通过 `_isEditorEnvironment` 判断，不使用编译时宏

### 例外情况

以下情况**必须保留**条件编译：
- `using UnityEditor;` - UnityEditor 命名空间只在 Editor 中可用，必须条件编译

### 最佳实践

1. 在 `Main.cs` 的 `Start()` 方法中统一设置 Editor 环境
2. 所有 Editor 相关的业务逻辑都通过 `_isEditorEnvironment` 判断
3. 避免在业务逻辑中使用 `#if UNITY_EDITOR` 宏
4. 提供清晰的错误信息，当在非 Editor 环境下调用 Editor 相关功能时

---

## 默认游戏包命名规范

### 设计原则

**每个 Game 都有默认的两个包：`{gameName}Asset` 和 `{gameName}Raw`，其中 `{gameName}` 是游戏名称。**

### 包命名规则

对于给定的游戏名称 `gameName`，系统会自动使用以下两个默认包：

- **Asset 包**：`{gameName}Asset` - 用于存放 Unity 资源
- **Raw 包**：`{gameName}Raw` - 用于存放非 Unity 资源

### 资源分类规则

#### Asset 包（{gameName}Asset）

**Asset 包用于存放 Unity 资源**，包括但不限于：

- Prefab（预制体）
- Texture（纹理）
- Material（材质）
- Sprite（精灵图）
- Animation（动画）
- Audio Clip（音频片段，如果是 Unity 格式）
- 其他 Unity 可以识别和导入的资源类型

#### Raw 包（{gameName}Raw）

**Raw 包用于存放非 Unity 资源**，包括但不限于：

- 音频文件（如 .mp3, .wav, .ogg 等原始音频格式）
- 视频文件（如 .mp4, .mov 等视频格式）
- DLL 文件（.dll.bytes）
- 配置文件（.bytes, .json 等）
- 其他需要在运行时直接读取的原始文件

### 示例

对于游戏名称为 "BBQ" 的游戏：

- Asset 包：`BBQAsset` - 存放 Unity 资源（Prefab、Texture、Material 等）
- Raw 包：`BBQRaw` - 存放原始文件（音频、视频、dll、配置等）

### 最佳实践

1. 在创建游戏包配置时，确保每个游戏都有这两个默认包
2. 严格按照资源类型分类，Unity 资源放入 Asset 包，原始文件放入 Raw 包
3. 在代码中引用包时，使用 `{gameName}Asset` 和 `{gameName}Raw` 的命名规范


        private Request GetRequestFromPool()
        {
            if (_requestPool.Count <= 0)
            {
            	return new Request();
            }
            Request request = _requestPool.Pop();
            // 清理字段
            request.InconstantConnectionUrl = null;
            request.PersistentConnectionUrl = null;
            request.Token = null;
            request.Body = null;
            request.RequestControl = null;
            return request;
        }

            if (_networkManager != null)
            {
                _networkManager.SendRequest(_service, messageID, message, ConnectionType.INCONSTANT, callback);
            }
            改成
            if (_networkManager == null)
            {
            	return;
            }
            _networkManager.SendRequest(_service, messageID, message, ConnectionType.INCONSTANT, callback);

