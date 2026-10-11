cmake_minimum_required(VERSION 3.20)
project(AstlibraQuickSave LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

if(MSVC)
    # 源码是 UTF-8；不告诉 MSVC 的话中文注释会按本地代码页解析而错乱。
    add_compile_options(/utf-8 /W3)
    add_compile_definitions(NOMINMAX)
    # 静态链接 CRT，注入的 DLL 不依赖目标机上是否装了 VC++ 运行时。
    set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreaded")
endif()

add_library(AstlibraQuickSave SHARED QuickSaveMod.cpp)
target_compile_definitions(AstlibraQuickSave PRIVATE UNICODE _UNICODE WIN32_LEAN_AND_MEAN)
# 去掉 MinGW 的 lib 前缀：产物需要叫 AstlibraQuickSave.dll，加载器按这个名字找。
set_target_properties(AstlibraQuickSave PROPERTIES PREFIX "" OUTPUT_NAME "AstlibraQuickSave")

add_executable(AstlibraInject Injector.cpp)
target_compile_definitions(AstlibraInject PRIVATE UNICODE _UNICODE WIN32_LEAN_AND_MEAN)
if(MSVC)
    target_link_options(AstlibraInject PRIVATE /ENTRY:wmainCRTStartup)
endif()

add_executable(QuickSaveLoader WIN32 QuickSaveLoader.cpp)
target_compile_definitions(QuickSaveLoader PRIVATE UNICODE _UNICODE WIN32_LEAN_AND_MEAN)
if(MSVC)
    # MSVC 的 WINDOWS 子系统默认入口是 WinMain；我们的入口是 wWinMain。
    target_link_options(QuickSaveLoader PRIVATE /ENTRY:wWinMainCRTStartup)
else()
    target_link_options(QuickSaveLoader PRIVATE -municode)
endif()
