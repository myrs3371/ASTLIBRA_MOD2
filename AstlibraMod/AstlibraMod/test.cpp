#include "pch.h"
#include <windows.h>
#include <iostream>

int main()
{
    HMODULE module = LoadLibraryW(L"x64\\Release\\AstlibraMod.dll");

    if (module == nullptr)
    {
        std::cout << "LoadLibraryW failed\n";
        return 1;
    }

    std::cout << "DLL loaded\n";

    FreeLibrary(module);
    return 0;
}
/*
    mov r8d,edi 
    lea edx,qword ptr ss：【rbp+6E】  不知道
    sub r8d,dword ptr ds:[7FF7CE8AA0C8] 最大-当前=能够购买数量
    mov r9b,1 未知
    cmp dword ptr ds:[7FF7CE82992C],128 如果当前商品id=128
    mov ecx,23F 第一个参数
    cmove r8d,r15d 若果是就把最大购买数量=1
    call 40这个函数

	第一个参数ecx = 23F
    第二个参数edx=不知道
    第三个参数r8d=能够购买数量
    第四个参数r9=1 未知

  mov rax,rsp
push rbx
push r12
sub rsp,58
mov qword ptr ds:[rax+8],rbp
xor ebx,ebx
mov qword ptr ds:[rax+10],rsi
lea ebp,qword ptr ds:[rdx+41]
mov qword ptr ds:[rax+18],rdi
mov esi,ecx
mov qword ptr ds:[rax+20],r14
add ecx,69
mov qword ptr ds:[rax-18],r15
mov edi,r8d
movaps xmmword ptr ds:[rax-28],xmm6
lea r14d,qword ptr ds:[rdx+2D]
lea r12d,qword ptr ds:[rbx+1]
test r9b,r9b
jne astlibra.7FF7CCF51B07
movsd xmm6,qword ptr ds:[7FF7CD4FED08]
xorps xmm3,xmm3
mov dword ptr ds:[rax-38],ebx
movaps xmm2,xmm6
mov dword ptr ds:[rax-40],r12d
mov edx,ebp
mov eax,dword ptr ds:[7FF7CE65EAC4]
mov dword ptr ss:[rsp+20],eax
call <astlibra.sub_7FF7CCB172C0>
*/