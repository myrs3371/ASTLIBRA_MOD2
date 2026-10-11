# Session: ASTLIBRA C++ Mod 基础

## Learner Profile

- Level: beginner
- Language: zh-CN
- Started: 2026-09-08
- Goal: 从零完成并理解一个可由独立测试程序加载的 x64 C++ DLL

## Concept Map

| # | Concept | Prerequisites | Status | Score | Last Reviewed | Review Interval |
|---|---|---|---|---|---|---|
| 1 | Solution、Project 与 Build Configuration | - | mastered | 80% | 2026-09-08 | 1d |
| 2 | 编译 DLL 与定位输出 | 1 | mastered | 85% | 2026-09-19 | 1d |
| 3 | DllMain 与加载生命周期 | 2 | in-progress | 75% | - | - |
| 4 | 安全地写入加载日志 | 3 | in-progress | 70% | - | - |
| 5 | 创建 DllLoaderTest | 2 | mastered | 85% | 2026-09-19 | 1d |
| 6 | LoadLibraryW、错误处理与 FreeLibrary | 3, 5 | in-progress | 75% | - | - |
| 7 | 端到端验证 | 4, 6 | mastered | 85% | 2026-09-19 | 1d |

## Misconceptions

| # | Concept | Misconception | Root Cause | Status | Counter-Example Used |
|---|---|---|---|---|---|
| 1 | switch/case 控制流 | 坚持认为 switch 只执行匹配的 case，执行完会自动退出，即使没有 break | 将 case 标签误认为独立代码块，而非程序跳转进入的标签 | active | 用连续输出 1 和 2 的可运行示例验证 |

## Session Log

- [2026-09-08] Diagnosed level: beginner; Visual Studio C++ build experience: none.
- [2026-09-08] Confirmed project is open and Release/x64 controls are visible.
- [2026-09-08] Concept 1 started.
- [2026-09-08] Identified DllMain as the DLL lifecycle entry point after a guided prompt.
- [2026-09-08] Distinguished project configuration files from source-code files; needed clarification because the two-part check was ambiguous.
- [2026-09-08] Added fstream and filesystem headers manually; understands that headers make library tools available without executing code.
- [2026-09-08] Added a global HMODULE variable initialized to nullptr; treating the module handle as the DLL identity.
- [2026-09-08] Saved the module handle during DLL_PROCESS_ATTACH and built successfully as Debug x64; output DLL: x64\\Debug\\AstlibraMod.dll.
- [2026-09-08] Paused after explaining DisableThreadLibraryCalls(hModule). Next step: add it after g_module = hModule, then build Debug x64.
- [2026-09-13] Correctly recalled that DisableThreadLibraryCalls suppresses thread attach/detach notifications, but initially described it as closing DLL thread notifications; clarified that it neither unloads the DLL nor prevents thread creation.
- [2026-09-13] Could not yet predict switch fall-through when a case has no break. Current focus: reason through a minimal switch example before returning to DllMain.
- [2026-09-13] Predicted that execution automatically exits after case 1 without break. Logged the case-boundary mental model for targeted correction.
- [2026-09-13] Same switch/case assumption resurfaced after questioning the purpose of break. Escalating to a concrete runnable counter-example.
- [2026-09-13] Learner chose to verify switch fall-through by creating and running a test program. Next action: create a separate Console App project so the experiment has a main entry point and does not alter the DLL project type.
- [2026-09-13] Learner prefers a disposable test.cpp in the current directory. Adapted practice: compile it independently as a temporary console executable instead of adding it to the DLL project.
- [2026-09-13] Build failure diagnosed from AstlibraMod/x64/Release/AstlibraMod.log: test.cpp includes <iostream> before the required precompiled header. With /Yu pch.h, MSVC ignores content before pch.h, so std::cout is unavailable (C2039/C2065). Next check: move pch.h to the first include and rebuild.
- [2026-09-13] Rebuild succeeded and produced x64/Release/AstlibraMod.dll. Windows then reported it was not a valid Win32 application because the DynamicLibrary output was opened directly as an executable; this is an artifact-type mismatch, not an x86/x64 build failure.
- [2026-09-13] Located Visual Studio 2026 x64 environment at E:\vs studio\VC\Auxiliary\Build\vcvars64.bat. Independently compiled and ran test.cpp; observed output was 1 followed by 2, confirming fall-through. Misconception remains active pending learner explanation and a novel check.
- [2026-09-13] Learner independently ran the test and reported output 1, 2. Experimental evidence confirms fall-through when case 1 has no break; next check is applying this to DllMain.
- [2026-09-13] Added break after DLL_PROCESS_ATTACH and learner confirmed Release x64 rebuild succeeded, producing x64/Release/AstlibraMod.dll.
- [2026-09-13] Explained std::ofstream append-mode logging and added the WriteLoadLog() call in DllMain. Rebuild succeeded.
- [2026-09-13] Clarified LoadLibraryW loads a DLL into the current process and FreeLibrary releases its module reference. Prepared test.cpp as a temporary loader; corrected relative DLL path to ..\\x64\\Release\\AstlibraMod.dll because test.cpp is under AstlibraMod\\AstlibraMod.
- [2026-09-13] Paused before running the temporary loader. Next session: compile/run test.exe from the Visual Studio 2026 x64 Developer Command Prompt, confirm "DLL loaded", then locate and inspect AstlibraMod.log.
- [2026-09-19] Created DllLoaderTest as a Visual Studio console project and ran it directly from Visual Studio.
- [2026-09-19] First run returned "LoadLibraryW failed" with exit code 1 because the relative DLL path did not leave the loader project's working directory.
- [2026-09-19] Learner independently added `..\\` to the DLL path. The loader then found the Release x64 DLL, printed "DLL loaded", and the load log was verified.
- [2026-09-19] Demonstrated that relative paths are resolved from the process working directory, not inherently from the source file or executable location.
- [2026-09-19] Session paused after completing the first end-to-end loader -> DLL -> log verification. Next session: move non-trivial initialization/file I/O out of `DllMain`, improve `LoadLibraryW` error reporting, and verify `FreeLibrary` behavior.
- [2026-09-20] Spaced review: correctly recalled that the DLL relative path is resolved from the current directory; refined this to the process working directory, which may differ from the executable directory.
- [2026-09-20] Identified missing failure handling around log writing, but has not yet identified the loader-lock restriction that makes file I/O inside `DllMain` unsafe. Continuing with a targeted counter-example.
- [2026-09-20] Correctly predicted that re-entering DLL loading while `DllMain` runs under the loader lock can leave the process stuck. Needs to explain the circular wait before the loader-lock model is considered understood.
- [2026-09-20] Learner requested a faster, project-first pace and chose to perform Visual Studio edits personally. Agreed design: export `InitializeMod`, call it through `GetProcAddress`, add Win32 error codes, and verify `FreeLibrary`.
- [2026-09-20] Added loader-side `GetProcAddress` flow and observed Win32 error 127 as expected: the DLL loads, but `InitializeMod` is not exported yet. Learner requested inline explanations rather than being sent to plan documents.
- [2026-09-20] Learner understood the function-pointer declaration `BOOL (WINAPI*)()` after separating return type, calling convention, pointer marker, and parameter list. Session paused before exporting `InitializeMod` from `dllmain.cpp`.
- [2026-10-02] Resumed with the launcher-to-game DLL injection path. Learner correctly identified that a launcher-side `LoadLibraryW` call loads the DLL into the launcher process, not the game process.
- [2026-10-02] Learner predicted that `processInfo.hProcess` remains usable after `CloseHandle`. Testing this handle-lifetime model with a concrete follow-up before editing the launcher.
- [2026-10-02] After the counter-example, learner correctly chose to keep `processInfo.hProcess` open until DLL injection finishes; checking the distinction from `processInfo.hThread` before practice.
- [2026-10-02] Learner requested a less verbose workflow. From now on, give one small code edit directly with a short explanation, avoid repeated comprehension questions, and wait for completion before the next edit.
- [2026-10-02] Current implementation step: in `DllLoaderTest/DllLoader.cpp`, close `processInfo.hThread` immediately, keep `processInfo.hProcess` open across the upcoming injection code, then close it before returning.
