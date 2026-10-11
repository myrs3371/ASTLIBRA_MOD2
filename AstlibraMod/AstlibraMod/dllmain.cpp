// dllmain.cpp : 定义 DLL 应用程序的入口点。
#include "pch.h"
#include <fstream>
#include <filesystem>
#include <string>
#include<cstdint>
#include <array>
#include <cstring>
// 游戏版本相关的固定偏移：这些值来自 x64dbg 的分析结果。
constexpr uintptr_t kCurrentItemIdOffset = 0x1D1992C;     // 当前商品 ID
constexpr uintptr_t kPurchaseEntryOffset = 0x405D27;      // Hook 入口
constexpr uintptr_t kPurchaseContinueOffset = 0x405D2D;   // Hook 返回位置
constexpr uintptr_t kRelayOffset = 0x40DB10;               // 游戏内中继位置
constexpr uintptr_t kLeftArrowCoordinateOffset = 0x441B72 + 2;  // add ecx, imm8 的立即数
constexpr uintptr_t kRightArrowCoordinateOffset = 0x441BB0 + 2; // lea ecx, [rsi+imm8] 的立即数
constexpr unsigned char kOriginalLeftArrowCoordinate = 0x37;
constexpr unsigned char kOriginalRightArrowCoordinate = 0x9B;
constexpr unsigned char kPatchedLeftArrowCoordinate = 0x10;
constexpr unsigned char kPatchedRightArrowCoordinate = 0xAF;
constexpr size_t kEntryPatchSize = 6;
constexpr size_t kRelayPatchSize = 14;
HMODULE g_module = nullptr;
bool g_patchInstalled = false;
uintptr_t GetGameBase() {
    // ASLR 会让游戏每次启动时基址变化，所以运行时重新获取。
    return reinterpret_cast<uintptr_t>(GetModuleHandleW(nullptr));
}
std::wstring GetDllPath() {
    wchar_t path[MAX_PATH]{};
	DWORD length = GetModuleFileNameW(g_module, path, MAX_PATH);
    if (length == 0 || length == MAX_PATH) {
        return L"";
    }
    return std::wstring(path, length);
}
bool WriteLoadLog() {
    std::filesystem::path dllPath(GetDllPath());
    if (dllPath.empty()) {
        return false;
    }
	std::filesystem::path logPath = dllPath.parent_path() / "AstlibraMod.log";
    std::ofstream log(logPath, std::ios::app);
    if (!log.is_open()) {
        return false;
    }
    log << "DLL path: " << dllPath.string() << '\n';
    log << "Log path: " << logPath.string() << '\n';
    log << "AstlibraMod initialized successfully\n";
    return log.good();
}
extern "C" {
    // C++ 定义地址变量，patch.asm 通过 EXTERN 引用；不再修改 mov 的立即数。
    uintptr_t g_itemIdAddress = 0;
    uintptr_t g_purchaseContinue = 0;
    // 这里只取汇编入口地址。它依赖游戏现场，不能作为普通 C++ 函数调用。
    void PurchaseHook();
    // 这些是 ASM 的位置标签，不是这里新建的数组。
    extern unsigned char EntryCodeStart[], EntryCodeEnd[];
    extern unsigned char RelayCodeStart[], RelayCodeEnd[], RelayTarget[];
    extern unsigned char OriginalCodeStart[], OriginalCodeEnd[];
    extern unsigned char PaddingStart[], PaddingEnd[];
}

// 「随时存档」功能，实现在 QuickSave.cpp。
// 两个功能各跑各的线程：任一边失败都不影响另一边。
extern "C" void QuickSaveSetModule(HMODULE self);
extern "C" DWORD WINAPI QuickSaveWorker(LPVOID param);
extern "C" void QuickSaveShutdown();

size_t CodeSize(const unsigned char* begin, const unsigned char* end)
{
    // 结束地址减开始地址，得到两个标签之间的字节数。
    return reinterpret_cast<uintptr_t>(end) - reinterpret_cast<uintptr_t>(begin);
}

bool WriteProtectedMemory(void* address, const void* data, size_t size)
{
    // 先开放代码页写权限，再复制 MASM 生成的字节。
    DWORD oldProtection = 0;
    if (!VirtualProtect(address, size, PAGE_EXECUTE_READWRITE, &oldProtection)) {
        return false;
    }
    std::memcpy(address, data, size);
    // 刷新指令缓存，并恢复之前的页面权限；两项都成功才返回 true。
    const BOOL flushed = FlushInstructionCache(GetCurrentProcess(), address, size);
    DWORD unusedProtection = 0;
    const BOOL restored = VirtualProtect(address, size, oldProtection, &unusedProtection);
    return flushed && restored;
}

bool PatchCoordinateByte(uintptr_t address, unsigned char expected, unsigned char replacement)
{
    const auto current = *reinterpret_cast<const unsigned char*>(address);
    if (current != expected) {
        return false;
    }
    return WriteProtectedMemory(reinterpret_cast<void*>(address), &replacement, sizeof(replacement));
}

bool RestoreCoordinateByte(uintptr_t address, unsigned char original)
{
    return WriteProtectedMemory(reinterpret_cast<void*>(address), &original, sizeof(original));
}

extern "C" __declspec(dllexport) DWORD WINAPI InitializeMod(LPVOID)
{
    if (g_patchInstalled) {
        // 避免已完成安装后的再次调用；此布尔值不是并发锁。
        return TRUE;
    }
    // 第一步：用本次基址计算游戏入口和中继地址。
    const uintptr_t gameBase = GetGameBase();
    const uintptr_t purchaseEntry = gameBase + kPurchaseEntryOffset;
    const uintptr_t relayAddress = gameBase + kRelayOffset;
    const uintptr_t leftArrowCoordinate = gameBase + kLeftArrowCoordinateOffset;
    const uintptr_t rightArrowCoordinate = gameBase + kRightArrowCoordinateOffset;
    // 通过汇编标签找到地址数据的位置。
    const size_t targetOffset = CodeSize(RelayCodeStart, RelayTarget);
    // 第二步：确认各个模板长度符合约定，防止复制越界。
    if (CodeSize(EntryCodeStart, EntryCodeEnd) != kEntryPatchSize ||
        CodeSize(OriginalCodeStart, OriginalCodeEnd) != kEntryPatchSize ||
        CodeSize(RelayCodeStart, RelayCodeEnd) != kRelayPatchSize ||
        CodeSize(PaddingStart, PaddingEnd) != kRelayPatchSize ||
        targetOffset > kRelayPatchSize - sizeof(uintptr_t)) {
        return FALSE;
    }
    // 第三步：确认游戏内存与预期原始内容一致，否则不安装。
    if (std::memcmp(reinterpret_cast<const void*>(purchaseEntry),
                    OriginalCodeStart, kEntryPatchSize) != 0 ||
        std::memcmp(reinterpret_cast<const void*>(relayAddress),
                    PaddingStart, kRelayPatchSize) != 0) {
        return FALSE;
    }
    // 只在预期的游戏版本上修改两个箭头的横坐标立即数。
    if (*reinterpret_cast<const unsigned char*>(leftArrowCoordinate) != kOriginalLeftArrowCoordinate ||
        *reinterpret_cast<const unsigned char*>(rightArrowCoordinate) != kOriginalRightArrowCoordinate) {
        return FALSE;
    }
    // 第四步：把运行时地址交给 DLL 内的汇编代码。
    g_itemIdAddress = gameBase + kCurrentItemIdOffset;
    g_purchaseContinue = gameBase + kPurchaseContinueOffset;
    // 第五步：准备中继副本，填的是 QWORD 地址数据，不是手写指令操作码。
    std::array<unsigned char, kRelayPatchSize> relayPatch{};
    std::memcpy(relayPatch.data(), RelayCodeStart, relayPatch.size());
    const uintptr_t hookAddress = reinterpret_cast<uintptr_t>(&PurchaseHook);
    std::memcpy(relayPatch.data() + targetOffset, &hookAddress, sizeof(hookAddress));

    // 第六步：先写中继。入口尚未改动时，正常购买流程还不会走到它。
    if (!WriteProtectedMemory(reinterpret_cast<void*>(relayAddress),
                              relayPatch.data(), relayPatch.size())) {
        WriteProtectedMemory(reinterpret_cast<void*>(relayAddress),
                             PaddingStart, kRelayPatchSize);
        return FALSE;
    }

    if (!PatchCoordinateByte(leftArrowCoordinate,
                             kOriginalLeftArrowCoordinate,
                             kPatchedLeftArrowCoordinate) ||
        !PatchCoordinateByte(rightArrowCoordinate,
                             kOriginalRightArrowCoordinate,
                             kPatchedRightArrowCoordinate)) {
        RestoreCoordinateByte(leftArrowCoordinate, kOriginalLeftArrowCoordinate);
        RestoreCoordinateByte(rightArrowCoordinate, kOriginalRightArrowCoordinate);
        WriteProtectedMemory(reinterpret_cast<void*>(relayAddress),
                             PaddingStart, kRelayPatchSize);
        return FALSE;
    }
    // 最后写入口，让游戏开始走“入口 -> 中继 -> PurchaseHook -> 游戏”。
    // 此函数没有暂停游戏线程，6 字节复制也不是原子操作。
    if (!WriteProtectedMemory(reinterpret_cast<void*>(purchaseEntry),
                              EntryCodeStart, kEntryPatchSize)) {
        // 失败时先恢复入口，确认恢复成功后再恢复中继，避免悬空跳转。
        if (WriteProtectedMemory(reinterpret_cast<void*>(purchaseEntry),
                                 OriginalCodeStart, kEntryPatchSize)) {
            WriteProtectedMemory(reinterpret_cast<void*>(relayAddress),
                                 PaddingStart, kRelayPatchSize);
        }
        RestoreCoordinateByte(leftArrowCoordinate, kOriginalLeftArrowCoordinate);
        RestoreCoordinateByte(rightArrowCoordinate, kOriginalRightArrowCoordinate);
        return FALSE;
    }
    g_patchInstalled = true;
    WriteLoadLog();
    return TRUE;
}
BOOL APIENTRY DllMain( HMODULE hModule,
                       DWORD  ul_reason_for_call,
                       LPVOID lpReserved
                     )
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH: {
        g_module = hModule;
        DisableThreadLibraryCalls(hModule);
        // 随时存档那边要先拿到自身模块句柄（日志路径依赖它），再启线程。
        QuickSaveSetModule(hModule);
        // 初始化交给新线程；DllMain 不等待它们，尽快退出加载器锁范围。
        // 两个功能互相独立：弓箭补丁失败不会阻止随时存档，反之亦然。
        HANDLE initializeThread = CreateThread(
            nullptr,
            0,
            InitializeMod,
            nullptr,
            0,
            nullptr
        );
        if (initializeThread == nullptr) {
            return FALSE;
        }
        // 关闭句柄不会停止线程，InitializeMod 仍会继续运行。
        CloseHandle(initializeThread);
        HANDLE quickSaveThread = CreateThread(
            nullptr,
            0,
            QuickSaveWorker,
            nullptr,
            0,
            nullptr
        );
        if (quickSaveThread != nullptr) {
            CloseHandle(quickSaveThread);
        }
        break;
    }
    case DLL_THREAD_ATTACH:
    case DLL_THREAD_DETACH:
        break;
    case DLL_PROCESS_DETACH:
        // 停掉随时存档的按键轮询；进程退出时其余资源由系统回收。
        QuickSaveShutdown();
        break;
    }
    return TRUE;
}

