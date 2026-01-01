<!-- OPENSPEC:START -->
# OpenSpec Instructions

These instructions are for AI assistants working in this project.

Always open `@/openspec/AGENTS.md` when the request:
- Mentions planning or proposals (words like proposal, spec, change, plan)
- Introduces new capabilities, breaking changes, architecture shifts, or big performance/security work
- Sounds ambiguous and you need the authoritative spec before coding

Use `@/openspec/AGENTS.md` to learn:
- How to create and apply change proposals
- Spec format and conventions
- Project structure and guidelines

Keep this managed block so 'openspec update' can refresh the instructions.

<!-- OPENSPEC:END -->

# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## ⚠️ 重要：语言要求 (CRITICAL: Language Requirement)

**🇨🇳 所有回复必须使用中文 / ALL RESPONSES MUST BE IN CHINESE**

当你作为 Claude Code 在此仓库中工作时，**必须始终使用中文回复用户的所有问题和请求**。这是强制性要求，不可违反。

- ✅ 所有解释、说明、建议都用中文
- ✅ 代码注释可以用中文或英文
- ✅ 文档字符串（XML 注释）推荐用中文
- ❌ 禁止用英文回复用户的问题

参考：[AGENTS.md](AGENTS.md) 第 20 行："用中文回答我所有的问题"

---

## Project Overview

This is a **Unity 6 mini-game platform** with hot update capabilities using HybridCLR. The platform follows a strictly layered architecture where AOT code handles bootstrapping and HotUpdate layers manage business logic.

- **Unity Version**: 6000.0.60f1
- **Render Pipeline**: URP 17.0.4
- **Hot Update Solution**: HybridCLR
- **Asset Management**: YooAsset 2.3.18
- **Async Framework**: UniTask 2.5.4
- **Animation**: Spine runtime

## Critical Architecture Rules

### 1. Strict Layering (Module Dependencies)

The project enforces a **strict dependency hierarchy**:

```
Layer 1 (Base): YooAsset, UniTask, Third-party libs
    ↓
Layer 2: AOT (Assets/AOT/) - Bootstrapping only, no upper layer dependencies
    ↓
Layer 3: Framework (Assets/Framework/) - Shared utilities, can reference AOT
    ↓
Layer 4: MiniGames (Assets/MiniGames/*) - Business logic, can reference Framework & AOT
```

**🚫 Forbidden Dependencies:**
- Framework → MiniGames (Framework cannot depend on game-specific code)
- AOT → Framework (AOT is the base layer)
- HotUpdate assemblies cannot be referenced by Framework or AOT

### 2. Async/Await Strategy (Zone-Based)

**Zone A: HotUpdate Layer** (MiniGameCommon, MiniGames)
- ✅ **MUST use**: `UniTask` / `UniTaskVoid`
- ✅ Async methods end with `Async` suffix
- ❌ **FORBIDDEN**: `IEnumerator`, native `Task`, `Thread`

**Zone B: AOT & Shared Layer** (Framework)
- ✅ **Preferred**: Callbacks (`Action`, `Action<T>`), synchronous methods
- ⚠️ **Exception**: UniTask allowed ONLY for UI and resource loading (use sparingly)
- ❌ **FORBIDDEN**: `IEnumerator`, native `Task` in general code

### 3. Resource Loading Rules

**ALL resources MUST go through YooAsset**:
- ✅ Use `ResourceManager.LoadRawFile` for raw files (JSON, text)
- ✅ Use `ResourceManager.LoadAsset` for Unity assets (Prefab, Texture)
- ✅ Use full paths starting with `Assets/` (e.g., `"Assets/Framework/Dll/HotUpdate/Framework.dll.bytes"`)
- ❌ **FORBIDDEN**: `File.ReadAllText`, `File.ReadAllBytes`, direct file system access
- ❌ **FORBIDDEN**: Short file names like `"Framework.dll.bytes"` (must use full paths)

## Common Development Commands

### Building and Testing
```bash
# Unity will handle builds through the Editor
# Use Unity Editor's Build Settings or HybridCLR menu commands

# For YooAsset package builds, use the YooAsset editor tools:
# Window → YooAsset → Asset Bundle Builder
```

### Package Management
```bash
# YooAsset build output is in the yoo/ directory
# BBQAsset/, BBQRaw/, FrameworkRaw/ contain built asset bundles
```

### Hot Update DLL Management
- Hot update DLLs are stored in `Assets/Framework/Dll/HotUpdate/`
- Use HybridCLR editor tools to manage AOT assemblies
- Main entry point: [Assets/AOT/Runtime/Main.cs](Assets/AOT/Runtime/Main.cs)

## Code Architecture

### Assembly Structure

| Assembly | Namespace | Purpose | Can Reference |
|----------|-----------|---------|---------------|
| **AOT** | `Astorise.Framework.AOT` | Bootstrapping, hot update launcher | YooAsset, UniTask, HybridCLR.Runtime |
| **Framework** | `Astorise.Framework.Core` | Shared utilities, resource management, UI | AOT, YooAsset, UniTask, Spine |
| **BBQHotUpdate** | `Astorise.MiniGames.BBQ` | BBQ game business logic | Framework, AOT, UniTask, YooAsset |
| **PinBallHotUpdate** | `Astorise.MiniGames.PinBall` | PinBall game business logic | Framework, AOT, UniTask, YooAsset |
| **ExampleTest** | (varies) | Test code and examples | Framework, AOT, YooAsset, Spine |

### Key Entry Points

1. **Main Entry**: [Assets/AOT/Runtime/Main.cs](Assets/AOT/Runtime/Main.cs) - Application bootstrap
2. **Hot Update**: [Assets/AOT/Runtime/HotUpdateLauncher.cs](Assets/AOT/Runtime/HotUpdateLauncher.cs) - DLL loading and updates
3. **Resource Manager**: [Assets/Framework/Runtime/ResourceManager.cs](Assets/Framework/Runtime/ResourceManager.cs) - Asset lifecycle management
4. **Spine Manager**: [Assets/Framework/Runtime/SpineManager.cs](Assets/Framework/Runtime/SpineManager.cs) - Animation management

### Directory Layout

```
Assets/
├── AOT/                      # AOT bootstrapping layer (no business logic)
├── Framework/                # Shared framework (AOT + HotUpdate compatible)
│   ├── Runtime/              # Core framework code
│   ├── Config/               # Configuration files
│   └── Dll/HotUpdate/        # Hot update DLL storage
├── MiniGames/                # Individual mini-games (HotUpdate layer)
│   ├── BBQ/                  # BBQ mini-game
│   └── PinBall/              # PinBall mini-game
└── Test/                     # Test code (excluded from builds)
```

## Code Standards (Critical)

### No `var` Keyword
**🚫 STRICTLY FORBIDDEN**: Never use `var` keyword. All types must be explicit.

```csharp
// ✅ Correct
int count = 10;
string name = "Player";
List<GameObject> objects = new List<GameObject>();

// ❌ Wrong - Will be rejected
var count = 10;
var name = "Player";
var objects = new List<GameObject>();
```

### Naming Conventions
- **Types** (class, struct, interface, enum): `PascalCase`
- **Methods, Properties**: `PascalCase`
- **Parameters, Local Variables**: `camelCase`
- **Private Fields**: `_camelCase` (with underscore prefix)
- **Constants**: `PascalCase` (e.g., `DefaultMaxConcurrentDownloads`)
- **Namespaces**: Must be three layers - `Astorise.{Layer}.{Module}`

### Code Organization
1. **Fields First**: All field definitions must be at the top of the class, before any methods
2. **Public Before Private**: Public methods first, then private methods
3. **Use Regions**: Group related functionality with `#region` blocks
4. **One Type Per File**: One public class per `.cs` file (file name matches class name)
5. **Exception for Constants**: Each module can have one constants/enums file containing multiple definitions

### Logging
All logs must be wrapped in conditional compilation:

```csharp
// Standard log
#if UNITY_DEBUG
Debug.Log("[ModuleName] Message");
#endif

// Module-specific log
#if UNITY_DEBUG && MODULE_NAME
Debug.Log("[ModuleName] Message");
#endif

// Error log
#if UNITY_DEBUG
Debug.LogError("[ModuleName] Error message");
#endif
```

### Exception Handling
- **Only use try-catch for external/uncontrolled operations**: File I/O, network calls, database operations
- **Avoid try-catch for code under our control**: Let exceptions propagate naturally to callers
- **Rationale**: Overuse obscures error handling logic and makes debugging harder

### XML Documentation
- **Required** for all public methods, properties, and fields
- Use `///` format
- Keep one blank line between documented items
- Only document parameters that aren't self-evident

```csharp
/// <summary>初始化某个小游戏的所有已注册包。</summary>
/// <param name="gameName">游戏名称</param>
/// <param name="onCompleted">完成回调</param>
public static void InitializeGame(GameName gameName, ResultCallback onCompleted);
```

### GC Optimization
- **Preallocate collections**: Initialize during setup, reuse with `Clear()`
- **Avoid high-frequency `new`**: Cache collections and reuse them
- **Minimize string operations**: Avoid `ToString()` and string concatenation in hot paths
- **Use value types for Dictionary keys**: Prefer `int`, `enum` over `string`

### Magic Numbers
**🚫 FORBIDDEN**: No magic numbers in code. Use named constants.

```csharp
// ✅ Correct
private const int DefaultMaxConcurrentDownloads = 10;
private const int DefaultRetryCount = 3;

// ❌ Wrong
if (retryCount > 3) { ... }  // What does 3 mean?
```

## OpenSpec Workflow

This project uses **OpenSpec** for spec-driven development. Before making significant changes:

1. **Check existing work**: Run `openspec list` and `openspec list --specs`
2. **For new features/breaking changes**: Create a proposal in `openspec/changes/`
3. **Read standards**: Always consult [openspec/project.md](openspec/project.md) for detailed conventions
4. **Validate**: Run `openspec validate [change-id] --strict` before implementation
5. **Get approval**: Wait for proposal approval before starting implementation

**When to create proposals:**
- Adding new features or functionality
- Making breaking changes (API, schema, architecture)
- Changing performance characteristics
- Updating security patterns
- Multi-file changes affecting core systems

**Skip proposals for:**
- Bug fixes (restoring intended behavior)
- Typos, formatting, comments
- Non-breaking dependency updates
- Configuration changes
- Tests for existing behavior

## Important Files

- [AGENTS.md](AGENTS.md) - High-level AI assistant instructions (Chinese language requirement)
- [openspec/AGENTS.md](openspec/AGENTS.md) - Detailed OpenSpec workflow guide
- [openspec/project.md](openspec/project.md) - Comprehensive coding standards (must read!)
- [Assets/AOT/Runtime/Main.cs](Assets/AOT/Runtime/Main.cs) - Application entry point
- [Assets/Framework/Runtime/ResourceManager.cs](Assets/Framework/Runtime/ResourceManager.cs) - Resource loading

## Testing

Tests are located in [Assets/Test/Example/](Assets/Test/Example/) and use Unity Test Framework:
- Use `ExampleTest.asmdef` assembly
- Can reference Framework and AOT assemblies
- May use UniTask for async testing
- Test directory excluded from production builds

## MCP for Unity

The project includes **MCP for Unity** (com.coplaydev.unity-mcp) for Unity automation. Use it for batch/automated operations on Unity assets (Prefabs, Sprites, ScriptableObjects, import settings, etc.).

## Before Starting Any Task

1. ✅ **用中文回复** - 所有回复必须使用中文
2. ✅ Read [openspec/project.md](openspec/project.md) for coding standards
3. ✅ Check active changes with `openspec list`
4. ✅ Review existing specs with `openspec list --specs`
5. ✅ Understand the module dependency rules (no circular dependencies!)
6. ✅ Determine correct async strategy based on layer (UniTask vs callbacks)
7. ✅ Plan resource loading through YooAsset (never direct file access)
8. ✅ Remember: No `var` keyword, explicit types only
