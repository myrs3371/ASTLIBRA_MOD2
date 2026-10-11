# 显式 Mod 初始化设计

## 目标

把文件 I/O 从 `DllMain` 移出，并由 `DllLoaderTest` 显式启动 Mod 初始化。保持当前项目简单、可观察，并为后续 Mod 功能提供稳定入口。

## DLL 接口

- `DllMain` 在 `DLL_PROCESS_ATTACH` 中只保存 `HMODULE`、调用 `DisableThreadLibraryCalls`，随后立即返回。
- DLL 导出 C ABI 函数 `BOOL InitializeMod()`。
- `InitializeMod()` 负责以追加模式写入 `AstlibraMod.log`，成功返回 `TRUE`，失败返回 `FALSE`。
- 第一版初始化保持同步执行，不创建后台线程，也不增加重复初始化状态。

## 加载器流程

1. 使用 `LoadLibraryW` 加载 `AstlibraMod.dll`。
2. 加载失败时打印 `GetLastError()` 并退出。
3. 使用 `GetProcAddress` 查找 `InitializeMod`。
4. 查找失败时打印 `GetLastError()`，调用 `FreeLibrary` 后退出。
5. 调用初始化函数，并根据布尔返回值报告成功或失败。
6. 调用 `FreeLibrary`；返回失败时打印 `GetLastError()`。

## 边界与错误处理

- 保留当前相对 DLL 路径，本阶段不处理 EXE 目录定位。
- 日志仍写入进程工作目录，便于延续现有验证方式。
- 使用 `extern "C"` 避免 C++ 名称修饰，使 `GetProcAddress("InitializeMod")` 稳定匹配。
- 使用 `__declspec(dllexport)` 直接导出函数，不新增 `.def` 文件。

## 验收标准

- Release x64 的 DLL 与加载器均能成功构建。
- 加载器依次报告 DLL 加载和 Mod 初始化成功。
- 工作目录中的 `AstlibraMod.log` 新增一行加载记录。
- `dumpbin /exports` 或 `GetProcAddress` 能证明 `InitializeMod` 已导出。
- 正常路径最终执行 `FreeLibrary`；所有失败路径返回非零退出码。

