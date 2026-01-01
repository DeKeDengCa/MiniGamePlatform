# Framework Agent Notes

- ResourceManager：外部调用资源加载时优先使用带 `packageName` 参数的 `LoadAssetAsync`（或后续的同步/包装版本），保持接口简洁、避免直接传递 `ResourcePackage`。
- SpineManager：UI 场景请使用 `PlayAnimationUI` 系列；玩法逻辑侧使用 `GenerateSpineAnimationAsync` / `PlayAnimation` 系列。
