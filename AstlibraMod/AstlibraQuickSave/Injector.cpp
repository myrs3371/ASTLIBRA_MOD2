// AstlibraInject —— 把指定 DLL 注入到已运行的进程（按 PID）。
// 用法：AstlibraInject.exe <pid> <dll 路径>
#include <windows.h>

#include <cstdio>
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

}  // namespace

int wmain(int argc, wchar_t** argv)
{
    if (argc < 3) {
        std::fwprintf(stderr, L"usage: AstlibraInject <pid> <dll path>\n");
        return 2;
    }
    const DWORD pid = wcstoul(argv[1], nullptr, 10);
    const std::filesystem::path dll(argv[2]);
    if (!std::filesystem::exists(dll)) {
        std::fwprintf(stderr, L"dll not found: %ls\n", dll.c_str());
        return 1;
    }
    HANDLE process = OpenProcess(PROCESS_ALL_ACCESS, FALSE, pid);
    if (process == nullptr) {
        std::fwprintf(stderr, L"OpenProcess(%lu) failed: %lu\n", pid, GetLastError());
        return 1;
    }
    const bool ok = InjectDll(process, std::filesystem::absolute(dll));
    CloseHandle(process);
    std::wprintf(L"inject %ls -> pid %lu: %ls\n", dll.c_str(), pid, ok ? L"ok" : L"failed");
    return ok ? 0 : 1;
}
