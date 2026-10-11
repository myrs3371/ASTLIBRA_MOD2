#include <windows.h>
#include <string>
#include <filesystem>
std::wstring GetWindowsErrorMessage(DWORD errorCode)
{
    wchar_t* buffer = nullptr;
    DWORD length = FormatMessageW(
        FORMAT_MESSAGE_ALLOCATE_BUFFER |
        FORMAT_MESSAGE_FROM_SYSTEM |
        FORMAT_MESSAGE_IGNORE_INSERTS,
        nullptr,
        errorCode,
        MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT),
        reinterpret_cast<wchar_t*>(&buffer),
        0,
        nullptr
    );

    if (length == 0 || buffer == nullptr) {
        return L"未知的 Windows 错误";
    }

    std::wstring message(buffer, length);
    LocalFree(buffer);

    while (!message.empty() &&
        (message.back() == L'\r' || message.back() == L'\n')) {
        message.pop_back();
    }
    return message;
}
bool InjectDll(HANDLE process, const std::filesystem::path& dllPath, DWORD& errorCode)
{
    const std::wstring path = dllPath.wstring();
    const SIZE_T pathSize = (path.size() + 1) * sizeof(wchar_t);
    void* remotePath = VirtualAllocEx(process, nullptr, pathSize,
        MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
    if (remotePath == nullptr) {
        errorCode = GetLastError();
        return false;
    }
    if (!WriteProcessMemory(process, remotePath, path.c_str(), pathSize, nullptr)) {
        errorCode = GetLastError();
        VirtualFreeEx(process, remotePath, 0, MEM_RELEASE);
        return false;
    }
    HMODULE kernel32Module = GetModuleHandleW(L"kernel32.dll");
    FARPROC loadLibraryAddress = kernel32Module == nullptr ? nullptr :
        GetProcAddress(kernel32Module, "LoadLibraryW");
    if (loadLibraryAddress == nullptr) {
        errorCode = GetLastError();
        VirtualFreeEx(process, remotePath, 0, MEM_RELEASE);
        return false;
    }
    HANDLE remoteThread = CreateRemoteThread(process, nullptr, 0,
        reinterpret_cast<LPTHREAD_START_ROUTINE>(loadLibraryAddress),
        remotePath, 0, nullptr);
    if (remoteThread == nullptr) {
        errorCode = GetLastError();
        VirtualFreeEx(process, remotePath, 0, MEM_RELEASE);
        return false;
    }
    DWORD waitResult = WaitForSingleObject(remoteThread, 10000);
    bool succeeded = false;
    if (waitResult == WAIT_OBJECT_0) {
        DWORD loadResult = 0;
        if (!GetExitCodeThread(remoteThread, &loadResult)) {
            errorCode = GetLastError();
        } else if (loadResult == 0) {
            errorCode = ERROR_DLL_INIT_FAILED;
        } else {
            succeeded = true;
        }
    } else if (waitResult == WAIT_TIMEOUT) {
        errorCode = WAIT_TIMEOUT;
    } else {
        errorCode = GetLastError();
    }
    CloseHandle(remoteThread);
    VirtualFreeEx(process, remotePath, 0, MEM_RELEASE);
    return succeeded;
}

int WINAPI wWinMain(HINSTANCE, HINSTANCE, PWSTR, int)
{
	// 获取当前目录
    wchar_t currentDirectory[MAX_PATH]{};
    DWORD length = GetCurrentDirectoryW(MAX_PATH, currentDirectory);
    if (length == 0 || length >= MAX_PATH) return 0;
    // 寻找游戏 exe
    std::filesystem::path gamePath = std::filesystem::path(currentDirectory) / L"ASTLIBRA.exe";
    if (!std::filesystem::exists(gamePath)) return 0;
	// 启动游戏
    STARTUPINFOW startupInfo{};
    startupInfo.cb = sizeof(startupInfo);
    PROCESS_INFORMATION processInfo{};
    if (!CreateProcessW(gamePath.c_str(), nullptr, nullptr, nullptr, FALSE, 0,
        nullptr, currentDirectory, &startupInfo, &processInfo)) return 0;
    CloseHandle(processInfo.hThread);
    // 注入 DLL
    std::filesystem::path dllPath = std::filesystem::path(currentDirectory) / L"AstlibraMod.dll";
    if (!std::filesystem::exists(dllPath)) {
        MessageBoxW(nullptr, L"当前目录未找到 AstlibraMod.dll", L"注入失败", MB_OK | MB_ICONERROR);
        CloseHandle(processInfo.hProcess);
        return 0;
    }
    DWORD injectionError = ERROR_SUCCESS;
    if (!InjectDll(processInfo.hProcess, dllPath, injectionError)) {
        std::wstring message =
            L"DLL 注入失败。\n\n原因：" + GetWindowsErrorMessage(injectionError) +
            L"\n错误码：" + std::to_wstring(injectionError);
        MessageBoxW(nullptr, message.c_str(), L"注入失败", MB_OK | MB_ICONERROR);
        CloseHandle(processInfo.hProcess);
        return 0;
    }
    MessageBoxW(nullptr, L"DLL 注入成功", L"AstlibraMod", MB_OK | MB_ICONINFORMATION);
    CloseHandle(processInfo.hProcess);
    return 0;
}

