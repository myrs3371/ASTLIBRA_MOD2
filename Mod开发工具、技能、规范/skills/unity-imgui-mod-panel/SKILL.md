---
name: unity-imgui-mod-panel
description: 为 Unity 游戏 Mod（BepInEx / MelonLoader）编写或改版游戏内面板时使用 —— 调试面板、作弊菜单、控制面板、设置窗口。涉及 OnGUI、IMGUI、GUI.Window、GUIStyle、GUI.skin、面板美化、深色主题、圆角九宫格纹理、页签导航、按键绑定 UI、中文字体、面板遮挡鼠标指针、GUILayout 布局错乱等问题时都适用。提供可直接复制且已验证可编译的主题与窗口壳模板。
---

# Unity Mod 游戏内 IMGUI 面板规范

统一 Mod 面板的视觉、布局与工程做法。目标：**不依赖游戏任何 UI 资源、不引入第三方 UI 库，
纯代码画出一个深色、圆角、有交互反馈的中文面板**，在任何 Unity 游戏里都能直接落地。

## 技术边界（先确认这些成立）

- **绘制手段只有 IMGUI**：`OnGUI` + `GUI.Window` + `GUILayout`，来自 `UnityEngine.IMGUIModule`。
  它是 Unity 内置的，任何 Unity 游戏都有，不需要游戏提供 Canvas / 预制体 / 图集。
- **视觉资源全部运行时生成**：圆角九宫格背景用 `Texture2D` + 逐像素覆盖率算出来，
  中文字体用 `Font.CreateDynamicFontFromOSFont` 从操作系统取。**零外部资源文件**。
- **对加载器的依赖**：只需要一个能挂 `MonoBehaviour` 的宿主。BepInEx / MelonLoader 都行。
  模板本身只 `using UnityEngine`，配置读写用委托注入，不绑定任何加载器 API。
- **不要用**：ImGui.NET（需要原生 dll 与渲染钩子）、UnityExplorer 的 UI 层、
  游戏自己的 UGUI/TMP 体系（会被游戏的 Canvas 层级、本地化、动画状态机反复干扰）。
- **IL2CPP 差异**：IMGUI 照常可用，但自定义 `MonoBehaviour` 必须
  `ClassInjector.RegisterTypeInIl2Cpp<T>()` 并提供 `IntPtr` 构造函数。

## 标准布局

```
┌──────────────────────────────────────────────────────────────┐
│ 标题 vX.Y.Z  （小字副标题）        [自定义按钮] [×]          │ 自绘标题栏 32px
├──────────┬───────────────────────────────────────────────────┤
│ 概览     │ ┌─ 状态条（常驻关键数值）────────────────────┐   │
│ 玩家     │ └────────────────────────────────────────────┘   │
│ 属性     │ ┌─ 卡片标题 ─────────────────────────────────┐   │
│ 怪物     │ │ 键值行 / 按钮行 / 输入行                   │   │ 滚动内容区
│ 关卡     │ │ 脚注说明（更小更暗）                       │   │
│ 设置     │ └────────────────────────────────────────────┘   │
│          │                                                   │
│ 局内     │                                                   │
├──────────┴───────────────────────────────────────────────────┤
│ 缩放 － 1.0x ＋ 重置   操作结果提示 / 快捷键说明          ◢ │ 底部条
└──────────────────────────────────────────────────────────────┘
```

- **左侧竖排导航**，不用 `GUILayout.Toolbar`。横排页签超过 5 个中文标签就会挤到看不清，
  竖排可以放 10 个以上且随时能加。
- **内容一律装进卡片**（`BeginCard(title)`），卡片之间靠 margin 分隔，不要裸排控件。
- **三级文字**：`Value`（强调数值，琥珀）→ `Sub`（正文，浅灰）→ `Hint`（脚注，更小更暗）。
  每张卡片末尾用 `Hint` 写一句"这个功能的边界/副作用是什么"。
- **主操作用 `ButtonPrimary`，破坏性操作用 `ButtonDanger`**，其余用 `Button`。
  一屏里主按钮不超过 2 个。
- 顶部状态条放"随时要看"的数值；页签内放"要改"的东西。

## 设计令牌（改主题只改 PanelTheme 的配色区）

| 用途 | 值 | 用途 | 值 |
|---|---|---|---|
| 窗口底 | `#15171C` a=.97 | 主文字 | `#E8EAEF` |
| 标题栏 | `#21252E` | 正文 | `#A7AEBB` |
| 导航栏 | `#1A1D24` a=.90 | 脚注 | `#79808E` |
| 卡片 | `#1E222A` a=.92 | 强调值 | `#FFC94D` |
| 卡片描边 | `#2E333F` | 正常 | `#74D69A` |
| 按钮 / 悬停 / 按下 | `#272C36` / `#333A47` / `#414A5A` | 警告 | `#F2A65A` |
| 主按钮 | `#B8862B` → `#D9A233` | 危险 | `#EB6B6B` |
| 选中态 / 描边 | `#2F3A4E` / `#5A7099` | 输入框 | `#101318` |

字号：标题 14 粗 / 副标题 10 / 卡片标题 13 粗 / 正文 12~13 / 脚注 11。
圆角：窗口 10、卡片 8、按钮 6、列表行 5、胶囊 9。控件高度：按钮 21、导航项 26、输入框 20。

## 硬性规则

1. **样式只在 `OnGUI` 内构建**。`GUI.skin` 在 `Awake`/构造函数里访问会警告并拿到错误值。
   用 `PanelTheme.EnsureBuilt()` 惰性构建，且判断 `normal.background == null` 时重建。
2. **业务代码里禁止 `new GUIStyle`**。列表每行 new 一个会造成明显 GC。
   要给单行上色用 `GUI.contentColor` 临时叠色（用完还原）。
3. **生成的 `Texture2D` / `Font` 必须设 `hideFlags = HideFlags.HideAndDontSave`**，
   否则场景切换时被回收，面板变成白块或全黑。
4. **自绘标题栏后必须 `GUILayout.Space(标题栏高度 + 6)`**。标题栏是绝对坐标绘制的，
   而 `GUILayout` 的游标仍在 y=0，不推就会重叠。
5. **`GUI.Window` 的 ID 必须避开游戏自带的 OnGUI 窗口**。先在反编译代码里搜
   `GUILayout.Window` / `GUI.Window` 确认游戏用了哪些 ID。
6. **整窗缩放用 `GUI.matrix`**，不要手动改各控件字号。`GUI.matrix` 会同时缩放输入坐标，
   鼠标命中自动正确；但屏幕边界判断要除以缩放比换算到逻辑坐标系。
7. **面板可见期间强制 `Cursor.visible = true`，关闭时还原**，且要每帧确认。见踩坑 §1。
8. **按键捕获在 `GUI.Window` 之前处理**，并在进入捕获时 `GUIUtility.keyboardControl = 0`，
   否则按键被页面上的 `TextField` 吃掉。`keyCode == KeyCode.None` 的字符事件要跳过。
9. **未绑定的按键用 `KeyCode.None` 表示，轮询时显式短路**，不要依赖 `GetKeyDown(None)` 的行为。
10. **`OnGUI` 里必须 try/catch 吞掉异常**。异常会打断 `GUILayout` 的 Begin/End 配对，
    之后整帧布局全乱，日志里出现一堆 `Mismatched GUILayout`。
11. **嵌套滚动区的内层必须限高**（`GUILayout.Height(n)`），否则两层互相抢空间导致抖动。
12. **面板文案全部本地化到目标用户语言**。名称优先取游戏自己的本地化表
    （玩家切语言会跟随），取不到再用自己维护的表，**绝不回退到游戏数据表里的开发者备注名**
    （常常是韩文/日文）。取自游戏本地化表的文本要先剥掉 `<...>` 富文本标签，IMGUI 不解析它们。

## 模板文件

`templates/` 下 4 个文件已在真实 Unity 6 程序集上验证编译通过（0 警告 0 错误）：

| 文件 | 作用 |
|---|---|
| `PanelTheme.cs` | 配色、圆角九宫格纹理生成、系统中文字体、全套 `GUIStyle` |
| `ModPanel.cs` | `ModPanelBase` 抽象基类：标题栏 / 导航 / 缩放 / 拖动 / 拖拽改尺寸 / 光标守卫 / 按键捕获 / 提示条 |
| `PanelWidgets.cs` | 卡片、键值行、按钮、输入框、开关、搜索框、胶囊筛选、列表行、限高滚动 |
| `PanelHost.cs` | 常驻 `MonoBehaviour`：按键轮询 + 光标 Tick + `OnGUI` 转发 |

接线（BepInEx Mono 为例）：

```csharp
internal sealed class MyPanel : ModPanelBase
{
    protected override string Title => $"我的 Mod 控制面板 v{Plugin.Version}";
    protected override string TitleNote => "（免费分享，禁止盗卖）";
    protected override string[] TabLabels => new[] { "概览", "玩家", "设置" };
    protected override int WindowId => 61701;              // 避开游戏自带窗口 ID

    protected override float LoadScale() => Plugin.Instance.PanelScale.Value;
    protected override void SaveScale(float v) => Plugin.Instance.PanelScale.Value = v;

    protected override void DrawTab(int index)
    {
        switch (index)
        {
            case 0: DrawOverview(); break;
            // ...
        }
    }

    private void DrawOverview()
    {
        BeginCard("常用操作");
        GUILayout.BeginHorizontal();
        Caption("金币", 44f);
        if (Btn("+1000", 58f, PanelTheme.ButtonPrimary)) { /* ... */ Notify("金币 +1000"); }
        GUILayout.EndHorizontal();
        Hint("说明这个操作的边界与副作用。");
        EndCard();
    }
}

// Plugin.Awake 里：
PanelTheme.Log = msg => Logger.LogInfo(msg);
var go = new GameObject("MyMod_Host");
UnityEngine.Object.DontDestroyOnLoad(go);
go.hideFlags = HideFlags.HideAndDontSave;
var host = go.AddComponent<PanelHost>();
host.Panel = new MyPanel();
host.ToggleKey = () => Config_ToggleKey.Value;
host.LogError = msg => Logger.LogError(msg);
```

按键绑定 UI 直接用基类的 `KeyBindRow`：

```csharp
KeyBindRow("打开面板", cfg.ToggleKey.Value, k => cfg.ToggleKey.Value = k, cfg.ToggleKey);
```

## 踩过的坑

完整清单与原因分析见 `reference/pitfalls.md`。最容易栽的三条：

- **面板挡住鼠标指针**：很多游戏用自绘准星并 `Cursor.visible = false`，IMGUI 画在所有 Canvas
  之上把准星盖住，玩家不知道指针在哪。必须在面板可见期间自己打开系统光标。
- **标题栏和内容重叠**：绝对坐标绘制 + `GUILayout` 游标未推进。
- **面板打开后游戏仍在响应输入**：IMGUI 不会阻断新 InputSystem。要么接受，
  要么在面板可见时请求游戏自身的暂停接口（注意外部关闭路径不会通知你，需每帧兜底释放）。

## 完成前自检

- [ ] 面板打开后能看到系统鼠标指针，关闭后恢复游戏原本的准星
- [ ] 标题栏、状态条、内容区、底部条互不重叠；窗口拖到屏幕边缘不会消失
- [ ] 缩放 0.6x ~ 2.5x 下文字与控件同步缩放，鼠标命中位置正确
- [ ] 切换场景后面板样式正常（纹理没被回收成白块）
- [ ] 日志里没有 `Mismatched GUILayout` / `Getting control ... position in a group with only ... controls`
- [ ] 所有可见文本都是目标语言，没有漏出外语 Label 或 `<COLOR=...>` 标签
- [ ] 每个功能卡片都有一句 `Hint` 说明边界与副作用
- [ ] 按键可在面板内改绑，`Esc` 能取消，未绑定状态不会误触发
- [ ] 单文件不超过 500 行，页签内容按语义拆成多个 partial 文件
