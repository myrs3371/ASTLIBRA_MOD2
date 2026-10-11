// QuickSave.cpp —— 「随时存档」功能（合并自 AstlibraQuickSave 工程）。
//
// 原理（见 analysis/FINDINGS.md）：
//   ASTLIBRA 是一个 DxLib 原生 x64 程序。主循环每帧按 .data 0x1BABB43
//   这个“当前界面 ID”字节分派：0=场景, 1=存档界面, 4=读档界面, ...
//   正常游戏只有站在存档点上交互时才会把该字节设成 1。
//   本功能在按热键时复现存档点交互的写入，等价于在任意位置打开存档界面，
//   之后完全走游戏原本的存档流程（界面 → 选槽 → SaveCommit）。
//
// 因此本功能不直接写存档文件，只切换游戏界面状态。
//
// 本文件由 dllmain.cpp 的 DllMain 启动：
//   QuickSaveSetModule(hModule) -> CreateThread(QuickSaveWorker)
#include "pch.h"

#include <winnt.h>

#include <cstdint>
#include <cstdio>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <string>

namespace {

HMODULE g_module = nullptr;
HANDLE g_stopEvent = nullptr;

// ---- 仅适用于 ASTLIBRA V1.5.0 的固定 RVA ----
constexpr uintptr_t kScreenIdOffset = 0x1BABB43;  // .data: 当前界面 ID
constexpr uint8_t kScreenField = 0;               // 场景
constexpr uint8_t kScreenSaveMenu = 1;            // 存档界面
constexpr uint8_t kScreenLoadMenu = 4;            // 读档界面（当前未使用，仅作记录）

// 游戏自己“打开存档菜单”时（RVA 0x3FA5A0，存档点距离判定通过后）写入的几个字节：
//   0x3FA737  mov byte [0x1D99F84], 0
//   0x3FA740  mov byte [0x1BD25C7], 0
//   0x3FA747  mov byte [0x1BABB43], 1   <- 界面 = 存档菜单
//   0x3FA755  mov byte [0x1BAC36B], 1
// 本功能复现同样的写入，等价于在任意位置按下存档点的交互。
constexpr uintptr_t kFlagAOffset = 0x1D99F84;
constexpr uintptr_t kFlagBOffset = 0x1BD25C7;
constexpr uintptr_t kMenuOpenFlagOffset = 0x1BAC36B;

// 版本校验：存档/读档分派器里 `cmp byte ptr [rip+disp], 1` 的原字节
// （RVA 0x4533FF = 80 3D 3D 87 75 01 01）。
constexpr uintptr_t kVersionSigOffset = 0x4533FF;
const unsigned char kVersionSig[] = {0x80, 0x3D, 0x3D, 0x87, 0x75, 0x01, 0x01};

std::filesystem::path ModuleDirectory()
{
    wchar_t buffer[MAX_PATH]{};
    const DWORD length = GetModuleFileNameW(g_module, buffer, MAX_PATH);
    return length == 0 || length == MAX_PATH
        ? std::filesystem::path{}
        : std::filesystem::path(buffer, buffer + length).parent_path();
}

// 日志与弓箭补丁共用同一个 AstlibraMod.log，每行加前缀以便区分来源。
void Log(const std::string& message)
{
    const auto path = ModuleDirectory() / "AstlibraMod.log";
    std::ofstream stream(path, std::ios::app);
    if (stream) {
        stream << "[QuickSave] " << message << '\n';
    }
}

std::string Hex(uint64_t value)
{
    char buffer[32]{};
    std::snprintf(buffer, sizeof(buffer), "0x%llX",
        static_cast<unsigned long long>(value));
    return buffer;
}

uintptr_t GameBase()
{
    return reinterpret_cast<uintptr_t>(GetModuleHandleW(nullptr));
}

// 校验目标进程是否是需要支持的 ASTLIBRA 版本。
bool VersionMatches(uintptr_t base)
{
    const auto* sig = reinterpret_cast<const unsigned char*>(base + kVersionSigOffset);
    for (std::size_t i = 0; i < sizeof(kVersionSig); ++i) {
        if (sig[i] != kVersionSig[i]) {
            return false;
        }
    }
    return true;
}

uint8_t ReadScreenId(uintptr_t base)
{
    return *reinterpret_cast<const volatile uint8_t*>(base + kScreenIdOffset);
}

void WriteByte(uintptr_t base, uintptr_t offset, uint8_t value)
{
    auto* target = reinterpret_cast<uint8_t*>(base + offset);
    DWORD oldProtection = 0;
    if (VirtualProtect(target, sizeof(uint8_t), PAGE_READWRITE, &oldProtection)) {
        *target = value;
        DWORD unused = 0;
        VirtualProtect(target, sizeof(uint8_t), oldProtection, &unused);
    } else {
        *target = value;  // .data 通常本就可写
    }
}

// 复现游戏“存档点交互成功”时对界面状态的写入。
void OpenSaveMenu(uintptr_t base)
{
    WriteByte(base, kFlagAOffset, 0);
    WriteByte(base, kFlagBOffset, 0);
    WriteByte(base, kScreenIdOffset, kScreenSaveMenu);
    WriteByte(base, kMenuOpenFlagOffset, 1);
}

// 应急恢复：把界面拉回场景，并清掉菜单标志。当界面卡在非场景状态时用。
void ForceFieldScreen(uintptr_t base)
{
    WriteByte(base, kMenuOpenFlagOffset, 0);
    WriteByte(base, kScreenIdOffset, kScreenField);
}

// 安装 WriteFile 调用点钩子，用于确认存档真的写盘（只记录，不修改数据）。
HANDLE (WINAPI* g_realCreateFileW)(LPCWSTR, DWORD, DWORD, LPSECURITY_ATTRIBUTES, DWORD, DWORD, HANDLE) = nullptr;
BOOL (WINAPI* g_realWriteFile)(HANDLE, LPCVOID, DWORD, LPDWORD, LPOVERLAPPED) = nullptr;
HANDLE g_saveHandle = INVALID_HANDLE_VALUE;

std::string Narrow(const wchar_t* value)
{
    if (!value) return {};
    char buffer[512]{};
    WideCharToMultiByte(CP_UTF8, 0, value, -1, buffer, sizeof(buffer), nullptr, nullptr);
    return buffer;
}

HANDLE WINAPI TracedCreateFileW(LPCWSTR name, DWORD access, DWORD share, LPSECURITY_ATTRIBUTES attrs,
    DWORD disposition, DWORD flags, HANDLE templateFile)
{
    const HANDLE handle = g_realCreateFileW(name, access, share, attrs, disposition, flags, templateFile);
    const std::string path = Narrow(name);
    if (path.find("SAVE_") != std::string::npos) {
        Log("CreateFileW " + path + " handle=" + Hex(reinterpret_cast<uintptr_t>(handle)));
        if (handle != INVALID_HANDLE_VALUE) {
            g_saveHandle = handle;
        }
    }
    return handle;
}

BOOL WINAPI TracedWriteFile(HANDLE handle, LPCVOID data, DWORD bytes, LPDWORD written, LPOVERLAPPED overlapped)
{
    const BOOL result = g_realWriteFile(handle, data, bytes, written, overlapped);
    if (handle == g_saveHandle) {
        Log("WriteFile SAVE bytes=" + std::to_string(bytes) + " ok=" + (result ? "1" : "0"));
    }
    return result;
}

bool PatchImport(const char* dllName, const char* functionName, void* replacement, void** original)
{
    auto* base = reinterpret_cast<std::byte*>(GetModuleHandleW(nullptr));
    auto* dos = reinterpret_cast<IMAGE_DOS_HEADER*>(base);
    if (!base || dos->e_magic != IMAGE_DOS_SIGNATURE) return false;
    auto* nt = reinterpret_cast<IMAGE_NT_HEADERS64*>(base + dos->e_lfanew);
    const auto& dir = nt->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_IMPORT];
    if (!dir.VirtualAddress) return false;
    auto* imports = reinterpret_cast<IMAGE_IMPORT_DESCRIPTOR*>(base + dir.VirtualAddress);
    for (; imports->Name; ++imports) {
        const char* importedDll = reinterpret_cast<const char*>(base + imports->Name);
        if (_stricmp(importedDll, dllName) != 0) continue;
        auto* thunk = reinterpret_cast<IMAGE_THUNK_DATA64*>(base + imports->FirstThunk);
        for (; thunk->u1.Function; ++thunk) {
            auto** slot = reinterpret_cast<void**>(&thunk->u1.Function);
            void* current = *slot;
            HMODULE kernel = GetModuleHandleA(dllName);
            void* target = kernel ? reinterpret_cast<void*>(GetProcAddress(kernel, functionName)) : nullptr;
            if (target && current == target) {
                DWORD protection = 0;
                if (!VirtualProtect(slot, sizeof(void*), PAGE_READWRITE, &protection)) return false;
                *original = current;
                *slot = replacement;
                VirtualProtect(slot, sizeof(void*), protection, &protection);
                FlushInstructionCache(GetCurrentProcess(), slot, sizeof(void*));
                return true;
            }
        }
    }
    return false;
}

void InstallSaveTrace()
{
    void* original = nullptr;
    if (PatchImport("KERNEL32.dll", "CreateFileW", reinterpret_cast<void*>(&TracedCreateFileW), &original)) {
        g_realCreateFileW = reinterpret_cast<decltype(g_realCreateFileW)>(original);
    }
    original = nullptr;
    if (PatchImport("KERNEL32.dll", "WriteFile", reinterpret_cast<void*>(&TracedWriteFile), &original)) {
        g_realWriteFile = reinterpret_cast<decltype(g_realWriteFile)>(original);
    }
    Log(g_realCreateFileW && g_realWriteFile ? "save trace installed" : "save trace install failed");
}

// 边沿检测的按键：返回 true 表示刚按下。
bool Pressed(int virtualKey, bool& wasDown)
{
    const bool isDown = (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
    const bool pressed = isDown && !wasDown;
    wasDown = isDown;
    return pressed;
}

DWORD WINAPI Worker(void*)
{
    Sleep(1500);  // 等游戏主模块稳定

    const uintptr_t base = GameBase();
    if (base == 0 || !VersionMatches(base)) {
        Log("unsupported ASTLIBRA build (version signature mismatch); quicksave inactive");
        return 0;
    }
    Log("ASTLIBRA V1.5.0 detected, base=" + Hex(base));
    InstallSaveTrace();

    bool f5 = false, f7 = false, f8 = false;
    while (WaitForSingleObject(g_stopEvent, 25) == WAIT_TIMEOUT) {
        if (Pressed(VK_F5, f5)) {
            const uint8_t before = ReadScreenId(base);
            // 只在场景界面触发，避免和其它界面状态互相干扰。
            if (before == kScreenField) {
                OpenSaveMenu(base);
                Log("F5 save: screen 0 -> 1");
            } else {
                Log("F5 ignored: screen=" + std::to_string(before) + " (not in field)");
            }
        }
        if (Pressed(VK_F8, f8)) {
            ForceFieldScreen(base);
            Log("F8 recovery: screen -> 0");
        }
        if (Pressed(VK_F7, f7)) {
            auto byteAt = [base](uintptr_t offset) {
                return static_cast<int>(*reinterpret_cast<const volatile uint8_t*>(base + offset));
            };
            Log("F7 diag: screen=" + std::to_string(ReadScreenId(base))
                + " flagA=" + std::to_string(byteAt(kFlagAOffset))
                + " flagB=" + std::to_string(byteAt(kFlagBOffset))
                + " menuOpen=" + std::to_string(byteAt(kMenuOpenFlagOffset)));
        }
    }
    return 0;
}

}  // namespace

// ---- 供 dllmain.cpp 使用的入口 ----
// DllMain 必须先把自身模块句柄传进来（日志路径依赖它），再启动工作线程。
extern "C" void QuickSaveSetModule(HMODULE self)
{
    g_module = self;
}

extern "C" DWORD WINAPI QuickSaveWorker(LPVOID)
{
    if (g_stopEvent == nullptr) {
        g_stopEvent = CreateEventW(nullptr, TRUE, FALSE, nullptr);
        if (g_stopEvent == nullptr) {
            Log("failed to create stop event; quicksave worker aborted");
            return 0;
        }
    }
    return Worker(nullptr);
}

extern "C" void QuickSaveShutdown()
{
    if (g_stopEvent != nullptr) {
        SetEvent(g_stopEvent);
    }
}
