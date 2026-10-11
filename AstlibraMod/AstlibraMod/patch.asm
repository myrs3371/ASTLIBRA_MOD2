; 这两个变量定义在 C++ 中，保存本次游戏运行的地址。QWORD 表示 8 字节。
EXTERN g_itemIdAddress:QWORD
EXTERN g_purchaseContinue:QWORD

.code

; PUBLIC 让链接器能把这些名字与 C++ 中的声明对应起来。
PUBLIC PurchaseHook
PUBLIC EntryCodeStart, EntryCodeEnd
PUBLIC RelayCodeStart, RelayCodeEnd, RelayTarget
PUBLIC OriginalCodeStart, OriginalCodeEnd
PUBLIC PaddingStart, PaddingEnd

PurchaseHook PROC
    ; 在 DLL 内直接执行，不再把这段代码复制到 codeCave。
    ; 分别保存标志位和 RAX，并不是保存全部寄存器。
    pushfq
    push rax
    ; 第一次读取：取出 C++ 变量中存放的商品 ID 地址。
    mov rax, qword ptr [g_itemIdAddress]
    ; 第二次读取：读取这个地址上的 4 字节商品 ID，与木箭 ID 比较。
    cmp dword ptr [rax], 0FAh
    ; pop 不改变比较结果；jne 仍然依据上面的 cmp 进行分支。
    pop rax
    jne original

    ; 木箭把上限设为 999，游戏后续代码仍负责扣除已有数量。
    mov r8d, 999
    jmp finish

original:
    ; 非木箭执行原来的 mov，保持游戏给出的上限。
    mov r8d, edi

finish:
    ; 恢复进入 Hook 前的标志，补回被覆盖的 lea。
    popfq
    ; lea 计算 RBP + 6Eh，并把结果的低 32 位放入 EDX，不读取内存。
    lea edx, [rbp+6Eh]
    ; 从变量取出返回地址，跳回购买入口后 6 字节的位置。
    jmp qword ptr [g_purchaseContinue]
PurchaseHook ENDP

; 入口模板：C++ 把这 6 字节复制到游戏购买入口。
; $ 是当前指令位置；两个固定偏移的差就是入口到中继的距离。
; 复制后相对距离不变，因此跳到游戏的 +40DB10h。
EntryCodeStart LABEL BYTE
    jmp NEAR PTR $ + (040DB10h - 0405D27h)
    nop
EntryCodeEnd LABEL BYTE

; 中继模板：jmp 读取紧随其后的 8 字节目标地址，不修改寄存器或栈。
; C++ 复制模板，再把 PurchaseHook 的地址填入副本的 RelayTarget 位置。
RelayCodeStart LABEL BYTE
    jmp qword ptr [RelayTarget]
RelayTarget QWORD 0              ; 地址数据，不是指令；CPU 不执行这一行的数据。
RelayCodeEnd LABEL BYTE

; 原入口指令的样本，用于写入前比对和失败后的恢复。
OriginalCodeStart LABEL BYTE
    mov r8d, edi
    lea edx, [rbp+6Eh]
OriginalCodeEnd LABEL BYTE

; REPT 在汇编时重复生成 14 条 int 3，作为中继区域的原始字节样本。
; 匹配这些字节只确认内容符合预期，不单独证明这里可以安全覆盖。
PaddingStart LABEL BYTE
    REPT 14
        int 3
    ENDM
PaddingEnd LABEL BYTE

; LABEL BYTE 只给当前位置命名，不分配空间；结束标签用于计算模板长度。
END
