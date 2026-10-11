# ASTLIBRA.exe 静态逆向发现（V1.5.0, x64, DxLib）

目标：`E:\game\ASTLIBRA\ASTLIBRA\ASTLIBRA.exe`
镜像基址（文件内）：`0x140000000`；运行时经 ASLR 重定位。所有地址以 **RVA** 表示。

## 引擎与来源

- 基于 **DxLib**（DXライブラリ）构建；二进制内保留了源码路径：
  - `...\Common\Source\Common_SaveLoad.cpp`（存档/读档）
  - `...\Common\Source\Common_Graphics.cpp`
  - `...\Develop\DxLib\DxLibSource\...`（DxLib 本体）
- 导入表只有 `steam_api64 / KERNEL32 / USER32 / SHELL32 / WINTRUST`；
  **D3D 是运行时 LoadLibrary 加载**（DxLib 典型做法），所以没有 d3d9/d3d11/dxgi 的静态导入。

## PE 布局

| 段 | RVA | 大小 |
|---|---|---|
| .text | 0x1000 | 0x875EE0 |
| .rdata | 0x877000 | 0x1AB0C4 |
| .data | 0xA23000 | 0x13F3650 |
| .pdata | 0x1E17000 | 0x1D2E0 |

共有 9960 个函数（来自 .pdata）。

## 存档文件格式与路径

统一格式字符串（**UTF-16**）：

- `%s\ASTLIBRA\SAVE\SAVE_%04d.DAT`            @ 0x8CD1F0  ← 主存档槽
- `%s\ASTLIBRA\SAVE\SAVE_SYSTEM_%04d.DAT`     @ 0x8CD1A0
- `%s\ASTLIBRA\SAVE\SAVE_EVENT_%04d.DAT`      @ 0x8CD280
- `%s\ASTLIBRA\SAVE\SAVE_RECORD_%04d.DAT`     @ 0x8CD2D0
- `\ASTLIBRA\SAVE\SAVE_CONFIG.DAT`            @ 0x8CD110
- `\ASTLIBRA\SAVE\SAVE_MUSIC_PLAY.DAT`        @ 0x8CD150
- `\ASTLIBRA\SAVE\SAVE_RETRY.DAT`             @ 0x8CD388

`%s` = `%LOCALAPPDATA%`（即 `C:\Users\<user>\AppData\Local`）。
实际文件：`C:\Users\lenovo\AppData\Local\ASTLIBRA\SAVE\SAVE_%04d.DAT`，大小 29940 字节。
文件模式字符串 `wb`（UTF-16）@ 0x8CCBCC。

存档头魔数 **`ALBR_SAV`**（8 字节）：
- 写入点 `movabs rax, 0x5641535F52424C41` @ 0x1C776 → 存入 `.data 0x1BED740`
- 校验点 @ 0x1CBE4（`cmp`，失败打印 `CRC Error 1/2`）

槽位编号 = `页 * 100 + 索引`；页全局在 `.data 0x1BDF4A0`（`imul reg,[0x1BDF4A0],0x64`）。
例：`SAVE_0002.DAT`=页0索引2，`SAVE_0102.DAT`=页1索引2。

## 关键函数（RVA）

| RVA | 名称 | 说明 |
|---|---|---|
| **0x1C270–0x1CA41** | `SaveCommit` | **写存档**：从游戏全局组装缓冲 → 写 `SAVE_%04d.DAT`（含 ALBR_SAV 头）。参数：`ecx`=槽位索引。返回 `al`：1=成功，2=失败/CRC错误。 |
| **0x1CA50–0x1CF4F** | `LoadSave` | **读存档**：校验文件大小(0x74F4=29940)、比对 ALBR_SAV 头、CRC。参数 `ecx`=槽位索引。 |
| **0x1E5B0–0x20827** | `SaveLoadScreen` | 存档/读档界面的主更新函数（每帧）。在其中：`0x2077D call 0x1C270`（存）、`0x207B9 call 0x1CA50`（读）。 |
| 0x20A15–0x20A60 | 包装 | `xor ecx,ecx; call 0x1E5B0`（mode=存）；无直接调用者（经函数表/间接）。 |
| 0x20B94–0x20C86 | 包装 | `mov cl,1; call 0x1E5B0`（mode=读）。 |

`0x1E5B0` 实参：`cl`=模式（0=存,1=读），`dl`=槽位索引。
调用前需请求标志 `.data 0x1D98024 != 0`（`cmp ...; je skip` @ 0x2071A）。
该标志只在 `0x1FAB9 mov [0x1D98024], dil` 设置、`0x2076E` 清零，均在 `0x1E5B0` 内。

## 调用链（来自运行时 trace，base=0x7FF72B750000）

存档（写 SAVE_0002.DAT）：
```
0x2077D  call SaveCommit(0x1C270)
  → SaveCommit 内 0x1C95A / 0x1C97B 调写函数
    → 0x843C4A → 0x843921 → 0x843B80 → 0x84EF0F
      → 0x84F181 call [IAT WriteFile]   (RVA 0x877238)
  → 0x857AEC call [IAT CreateFileW]     (RVA 0x877018)
```

## 实现路线（待选）

1. **热键直接调 `SaveCommit`**：把 DLL 挂到主线程每帧函数（如 USER32 `PeekMessageW` 的 IAT 钩子），
   热键触发时在主线程调用 `SaveCommit(gameBase+0x1C270)`，`ecx`=目标槽位索引。
   - 优点：与菜单/存档点判定无关；`SaveCommit` 只依赖游戏全局，自包含。
   - 需确认：槽位（当前槽 / 固定快捷槽）、调用时机是否安全。
2. **去掉存档点判定**：让暂停菜单的 Save 在任何地方可用（需要先定位该判定，未完成）。

## 待办 / 未确认

- 游戏"存档点"判定具体在哪（是否菜单 Save 项被禁用）。
- 调用 `SaveCommit` 是否需要在特定游戏状态（如暂停/非战斗）。
- 页全局 0x1BDF4A0 的含义与当前槽位的读取来源。
