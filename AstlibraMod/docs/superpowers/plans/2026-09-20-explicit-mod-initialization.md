# Explicit Mod Initialization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move log file I/O out of `DllMain` and invoke it through an explicitly exported `InitializeMod` function with observable error handling.

**Architecture:** `DllMain` performs only minimal loader-time bookkeeping. `DllLoaderTest` loads the DLL, resolves a stable C ABI export, invokes initialization, then releases its DLL reference on every applicable path.

**Tech Stack:** C++17, Win32 `LoadLibraryW`/`GetProcAddress`/`GetLastError`/`FreeLibrary`, Visual Studio Release x64

---

## File Structure

- Modify `DllLoaderTest/DllLoaderTest.cpp`: own the load, symbol lookup, initialization call, diagnostics, and unload flow.
- Modify `AstlibraMod/dllmain.cpp`: keep `DllMain` minimal and expose the explicit initialization function.

### Task 1: Add a failing exported-function lookup

**Files:**
- Modify: `DllLoaderTest/DllLoaderTest.cpp`

- [x] **Step 1: Replace the loader source**

```cpp
#include <windows.h>
#include <iostream>

using InitializeModFn = BOOL(WINAPI*)();

int main()
{
    HMODULE module = LoadLibraryW(L"..\\x64\\Release\\AstlibraMod.dll");

    if (module == nullptr)
    {
        std::cout << "LoadLibraryW failed. Error: " << GetLastError() << '\n';
        return 1;
    }

    std::cout << "DLL loaded\n";

    auto initializeMod = reinterpret_cast<InitializeModFn>(
        GetProcAddress(module, "InitializeMod"));

    if (initializeMod == nullptr)
    {
        std::cout << "GetProcAddress failed. Error: " << GetLastError() << '\n';
        FreeLibrary(module);
        return 2;
    }

    if (!initializeMod())
    {
        std::cout << "InitializeMod failed\n";
        FreeLibrary(module);
        return 3;
    }

    std::cout << "Mod initialized\n";

    if (!FreeLibrary(module))
    {
        std::cout << "FreeLibrary failed. Error: " << GetLastError() << '\n';
        return 4;
    }

    std::cout << "DLL unloaded\n";
    return 0;
}
```

- [x] **Step 2: Build and run the loader to verify the lookup fails**

Select `Release` and `x64`, set `DllLoaderTest` as the startup project, then run without debugging.

Expected output begins with:

```text
DLL loaded
GetProcAddress failed. Error: 127
```

Error 127 means the DLL was found, but `InitializeMod` has not been exported yet.

### Task 2: Export initialization and remove I/O from DllMain

**Files:**
- Modify: `AstlibraMod/dllmain.cpp`

- [ ] **Step 1: Replace the DLL source**

```cpp
// dllmain.cpp : Defines the entry point for the DLL application.
#include "pch.h"
#include <fstream>

HMODULE g_module = nullptr;

extern "C" __declspec(dllexport) BOOL WINAPI InitializeMod()
{
    std::ofstream log("AstlibraMod.log", std::ios::app);
    if (!log)
    {
        return FALSE;
    }

    log << "AstlibraMod initialized successfully\n";
    return log.good() ? TRUE : FALSE;
}

BOOL APIENTRY DllMain(HMODULE hModule,
                      DWORD reason,
                      LPVOID reserved)
{
    switch (reason)
    {
    case DLL_PROCESS_ATTACH:
        g_module = hModule;
        DisableThreadLibraryCalls(hModule);
        break;
    case DLL_PROCESS_DETACH:
        break;
    }

    return TRUE;
}
```

- [ ] **Step 2: Build the DLL**

Right-click `AstlibraMod`, choose **Build**, with `Release` and `x64` selected.

Expected result:

```text
Build: 1 succeeded, 0 failed
```

- [ ] **Step 3: Run the loader again**

Set `DllLoaderTest` as the startup project and run without debugging.

Expected output:

```text
DLL loaded
Mod initialized
DLL unloaded
```

### Task 3: Verify observable behavior

**Files:**
- Inspect: `DllLoaderTest/AstlibraMod.log`

- [ ] **Step 1: Verify the new log entry**

Open `DllLoaderTest/AstlibraMod.log` and confirm the newest line is:

```text
AstlibraMod initialized successfully
```

- [ ] **Step 2: Run a clean rebuild and final smoke test**

Choose **Build > Rebuild Solution**, retain `Release` and `x64`, then run `DllLoaderTest` once.

Expected: both projects build successfully, the three success messages appear in order, and exactly one new log line is appended for this run.
