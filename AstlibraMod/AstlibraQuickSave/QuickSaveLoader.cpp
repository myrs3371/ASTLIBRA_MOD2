#include <windows.h>

#include <filesystem>
#include <string>

namespace {

bool InjectDll(HANDLE process, const std::filesystem::path& dllPath)
{
    const auto path = dllPath.wstring();
    const SIZE_T bytes = (path.size() + 1) * sizeof(wchar_t);
    void* remote = VirtualAllocEx(process, nullptr, bytes, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
    if (remote == nullptr) return false;

    bool ok = WriteProcessMemory(process, remote, path.c_str(), bytes, nullptr);
    HMODULE kernel32 = GetModuleHandleW(L"kernel32.dll");
    auto load = kernel32 == nullptr ? nullptr : GetProcAddress(kernel32, "LoadLibraryW");
    HANDLE thread = ok && load != nullptr
        ? CreateRemoteThread(process, nullptr, 0, reinterpret_cast<LPTHREAD_START_ROUTINE>(load), remote, 0, nullptr)
        : nullptr;
    if (thread == nullptr) ok = false;
    if (thread != nullptr) {
        ok = ok && WaitForSingleObject(thread, 10000) == WAIT_OBJECT_0;
        CloseHandle(thread);
    }
    VirtualFreeEx(process, remote, 0, MEM_RELEASE);
    return ok;
}

}

int WINAPI wWinMain(HINSTANCE, HINSTANCE, PWSTR, int)
{
    const std::filesystem::path root = L"E:\\game\\ASTLIBRA\\ASTLIBRA";
    const auto exe = root / L"ASTLIBRA.exe";
    const auto dll = root / L"AstlibraQuickSave.dll";
    if (!std::filesystem::exists(exe) || !std::filesystem::exists(dll)) {
        MessageBoxW(nullptr, L"找不到 ASTLIBRA.exe 或 AstlibraQuickSave.dll", L"AstlibraQuickSave", MB_OK | MB_ICONERROR);
        return 1;
    }

    STARTUPINFOW startup{};
    startup.cb = sizeof(startup);
    PROCESS_INFORMATION process{};
    if (!CreateProcessW(exe.c_str(), nullptr, nullptr, nullptr, FALSE, 0, nullptr, root.c_str(), &startup, &process)) {
        MessageBoxW(nullptr, L"启动 ASTLIBRA.exe 失败", L"AstlibraQuickSave", MB_OK | MB_ICONERROR);
        return 1;
    }
    CloseHandle(process.hThread);
    const bool injected = InjectDll(process.hProcess, dll);
    CloseHandle(process.hProcess);
    if (!injected) {
        MessageBoxW(nullptr, L"AstlibraQuickSave.dll 注入失败", L"AstlibraQuickSave", MB_OK | MB_ICONERROR);
        return 1;
    }
    return 0;
}
