# IMGUI 面板踩坑清单

每条都是实际开发中遇到并解决过的问题，按"现象 → 原因 → 解决"记录。

---

## 1. 面板打开后看不见鼠标指针（最影响体验）

**现象**：面板画出来了，按钮能点（悬停高亮会亮），但屏幕上完全看不到指针，
只能靠悬停反馈盲摸。

**原因**：这类游戏不用系统光标，而是自己画一个跟随鼠标的 UI 对象（准星 / 十字线），
同时把系统光标关掉。典型实现：

```csharp
// 游戏侧代码（反编译所见）
Cursor.visible = false;                       // 关掉系统箭头
// 另有一个 MouseCanvas / CursorCanvas，用 Update 把 UI 对象移到鼠标位置
```

而 IMGUI（`OnGUI`）绘制在**所有 Canvas 之上**，面板把那个准星完全盖住了。
准星还在动，只是被遮住看不见。

**解决**：面板可见期间强制打开系统光标，关闭时还原原值。

```csharp
private bool _savedCursorVisible;
private bool _cursorForced;

private void UpdateCursor()   // 宿主 MonoBehaviour 的 Update 里每帧调
{
    if (_visible)
    {
        if (!_cursorForced) { _savedCursorVisible = Cursor.visible; _cursorForced = true; }
        if (!Cursor.visible) Cursor.visible = true;     // 每帧确认，不是只设一次
    }
    else if (_cursorForced)
    {
        _cursorForced = false;
        Cursor.visible = _savedCursorVisible;
    }
}
```

**为什么要每帧确认**：游戏会在场景加载、UI 显示、输入模式切换等多个时机重新执行
`Cursor.visible = false`（常见于 `InputController.OnInitialize` 与光标 Canvas 的
`OnInitialize`）。只在打开面板那一刻设一次，过一会儿就被游戏改回去了。

**排查方法**：反编译后搜 `Cursor.visible`、`Cursor.SetCursor`、`Cursor.lockState`，
就能确认游戏用的是哪套方案。若见到 `Cursor.lockState = CursorLockMode.Locked`
（第一人称游戏常见），还需要在面板打开时临时改成 `CursorLockMode.None`，否则指针会被吸在屏幕中心。

**不要做的事**：不要去隐藏/移动游戏那个准星对象。它可能被游戏的动画状态机驱动，
你改了会引发别的问题；开系统光标是零侵入方案。

---

## 2. 自绘标题栏和下面的内容重叠

**现象**：标题文字和第一张卡片糊在一起。

**原因**：标题栏用绝对坐标画（`GUI.Box(new Rect(0,0,w,32), ...)`），
这不消耗 `GUILayout` 的布局游标，游标仍在 y=0，所以后面 `GUILayout` 的内容从窗口顶部开始排。

**解决**：画完标题栏立刻推进游标。

```csharp
DrawTitleBar();                              // 绝对坐标
GUILayout.Space(TitleBarHeight + 6f);        // 必须补这一行
GUILayout.BeginHorizontal();                 // 之后才是正常布局
```

**为什么不用 `GUI.Window` 自带的 title 参数**：那样标题的位置、字体、颜色都受
window style 的 `padding` / `alignment` / `contentOffset` 联动影响，很难调准，
也放不了副标题和右上角按钮。自绘 + 手动推游标更可控。

---

## 3. 样式在 Awake 里初始化，拿到的是错的

**现象**：面板全是 Unity 默认灰白样式，或者报
`GUI Error: You are pushing more GUIClips than you are popping`、
`Skin can only be used in OnGUI`。

**原因**：`GUI.skin` 只在 IMGUI 渲染上下文里有效。

**解决**：惰性构建，由绘制入口调用；并且要能检测到纹理被回收后重建。

```csharp
public static void EnsureBuilt()
{
    if (_built && Window != null && Window.normal.background != null) return;
    _built = true;
    // ... new GUIStyle(GUI.skin.button) 等
}
```

---

## 4. 切场景后面板变成白块 / 全黑

**原因**：运行时 `new Texture2D(...)` 生成的纹理属于场景资源，
`SceneManager.LoadScene` 时被销毁，`GUIStyle.normal.background` 变成 null。

**解决**：生成的 `Texture2D` 与 `Font` 都设 `hideFlags = HideFlags.HideAndDontSave`；
同时在 `EnsureBuilt` 里判空重建作为双保险。

---

## 5. 圆角有锯齿 / 圆角被拉伸变形

**原因一**：直接用纯色纹理画圆角，边缘没有抗锯齿。
**解决**：逐像素算覆盖率（到圆角圆心矩形的距离），`alpha *= Clamp01(r - d + 0.5)`。

**原因二**：九宫格 `border` 设得不对，四角被拉伸。
**解决**：纹理尺寸取 `2*(radius+border+1)+2`，`GUIStyle.border` 设为 `radius+border+1`，
中间只留 2px 供拉伸。

---

## 6. 窗口 ID 撞车，两个窗口互相抢焦点/位置

**现象**：面板和游戏自带的调试窗口同时出现时，拖动一个另一个也跟着动，或者只显示一个。

**原因**：`GUI.Window(id, ...)` 的 id 是全局唯一键。很多游戏自己留了 OnGUI 调试窗口
（常见 ID 是随手写的 4 位数）。

**解决**：反编译搜 `GUI.Window` / `GUILayout.Window` 找出游戏占用的 ID，选个不重的
（例如 61701）。在面板源码注释里写明避开了哪个 ID。

---

## 7. 按键绑定时按下的键被输入框吃掉

**现象**：点"改绑"按钮后按键没反应，或者按下的字符跑进了页面上某个 `TextField`。

**原因**：`TextField` 拿着 `GUIUtility.keyboardControl`，键盘事件先给它。

**解决**：两件事一起做。

```csharp
// 1. 进入捕获时清掉键盘焦点
GUIUtility.keyboardControl = 0;

// 2. 在 GUI.Window 调用之前处理 KeyDown（此时还没有控件抢事件）
private void HandleKeyCapture()
{
    if (_captureToken == null) return;
    Event e = Event.current;
    if (e == null || e.type != EventType.KeyDown) return;
    if (e.keyCode == KeyCode.None) return;      // 字符事件没有 keyCode
    // ... 写入并 e.Use();
}
```

---

## 8. 缩放后鼠标点不准

**原因**：手动把每个控件的 `fontSize`/`Rect` 乘系数，但输入坐标没换算。

**解决**：整窗用 `GUI.matrix` 缩放，文本与命中测试会一起缩放，什么都不用改：

```csharp
Matrix4x4 saved = GUI.matrix;
GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1f));
_rect = GUI.Window(...);
GUI.matrix = saved;
```

唯一要注意的是**屏幕边界判断必须换算到逻辑坐标系**：`Screen.width / s`，
否则缩放后窗口会被限制在错误范围里。

---

## 9. 日志里刷 `Mismatched GUILayout` / 布局整帧错乱

**原因**：`OnGUI` 里某处抛了异常，`BeginHorizontal` 之后的 `EndHorizontal` 没执行。

**解决**：宿主的 `OnGUI` 整体 try/catch，异常只记录不外抛。
另外任何 `Begin*` / `End*` 都要保证配对，`return` 之前记得 `End`。

---

## 10. 列表卡顿

**原因**：列表每行 `new GUIStyle(...)` 来改颜色。几十行 × 每帧 = 明显 GC。

**解决**：样式复用，颜色用 `GUI.contentColor` 临时叠加：

```csharp
Color saved = GUI.contentColor;
if (!selected) GUI.contentColor = rowTint;
GUILayout.Button(label, selected ? Theme.RowOn : Theme.Row);
GUI.contentColor = saved;
```

注意：`contentColor` 是**乘算**，所以基础样式的 `textColor` 要偏亮（用主文字色而不是灰色），
否则叠色后会偏暗。

---

## 11. 嵌套滚动区抖动

**原因**：外层内容区已经是 `BeginScrollView`，内层列表又用不限高的 `BeginScrollView`，
两者互相抢剩余空间。

**解决**：内层必须 `GUILayout.Height(固定值)`。

---

## 12. 面板打开后游戏还在响应鼠标键盘

**现象**：在面板上点按钮，角色也开火/移动了。

**原因**：IMGUI 只在自己的区域消费 IMGUI 事件，**不会阻断 Unity 新 InputSystem 的输入**。
游戏用 `InputAction` 读输入，完全感知不到你的窗口。

**可选解法**（按侵入性递增）：
1. 接受它（调试面板场景下通常可接受）；
2. 面板打开时调用游戏自身的暂停接口（最干净，但见 §13）；
3. 面板打开时 `InputActions.Disable()` 之类（可能破坏游戏内部状态，慎用）。

---

## 13. 用了游戏的暂停接口，结果游戏永久卡在暂停

**原因**：游戏的暂停常是"按请求者 ID 计数"的
（`RequestPause(id)` / `ReleasePause(id)`）。而玩家可以通过**你不知道的路径**
关闭你打开的那个 UI —— 例如 UI 自带的关闭按钮、游戏全局的 ESC 分发链。
那些路径不会通知你，于是你永远不会调 `ReleasePause`。

**解决**：不要只在自己的关闭逻辑里释放，要每帧比对真实可见性兜底：

```csharp
public static void Tick()
{
    if (_pauseHeld && !IsOpen) ReleasePause();   // IsOpen 读的是那个 UI 的真实 IsVisible
}
```

**排查方法**：反编译搜游戏的全局 ESC 处理（常见于某个 `GameController.Update`），
看它会直接 `CloseCanvas()` 哪些 UI。

---

## 14. 面板里的名字显示成外语（韩文/日文）

**现象**：怪物名、道具名显示成开发者的母语。

**原因**：数据表里的 `Label` 字段通常是**开发者备注名**，不是给玩家看的；
玩家看到的名字来自本地化表（`NameKey` → StringTable）。而本地化表**未必覆盖所有条目**
（例如只翻译了会显示名字的 Boss，普通杂兵一个都没翻）。
若代码写成"本地化取不到就回退 Label"，就会漏出外语。

**解决**：
1. 先**解包核实覆盖率**，不要假设。数据表通常是 `Resources` 里的 JSON TextAsset：
   ```
   AssetStudio.CLI.exe "<游戏>_Data/resources.assets" out --game Normal --types TextAsset --names "Monster_.*"
   ```
   本地化 StringTable 一般在 Addressable bundle 里按语言分包，
   MonoBehaviour 无 TypeTree 时用 `--export_type Raw` 导出 `.dat` 再按 UTF-8 扫字符串。
2. 优先级定为：**游戏本地化表 → 自己维护的中文表 → 英文 Key**。永不回退到 `Label`。
3. 从本地化表取的文本可能内嵌 TMP 富文本标签（`<COLOR=#6F2522>地狱</COLOR>飞碟`），
   IMGUI 不解析富文本，会原样显示，必须先剥掉 `<...>`。
4. 变体条目（`Xxx_Lv2`/`_Lv3`）不要各写一条，从 Key 提取后缀拼接即可。
5. 写完用脚本核对覆盖率：把数据表所有 key 与你的中文表 diff，确认无缺失。

---

## 15. 本地化取词函数的失败行为不一致

以某商业项目的 helper 为例：表名传错 → 内部 `GetTable()` 返回 null 后**抛 NRE**；
表存在但条目缺失 → **返回空字符串**。

**解决**：外部调用统一包一层，try/catch + 把空串归一为 null，再走回退链。

---

## 16. 加载器配置文件里留着已删功能的旧键

**现象**：功能已经从 Mod 里删掉了，但玩家的配置文件里还有
`启用某功能 = true`，玩家以为还能用。

**原因**（BepInEx）：`ConfigFile` 会把"文件里读到但代码没有 `Bind`"的键存进私有的
`OrphanedEntries`，并在 `Save()` 时原样写回，属于有意设计（防止误删别的版本的配置）。

**解决**：绑定完所有配置项后，反射清空该字典再存盘。

```csharp
var prop = typeof(ConfigFile).GetProperty("OrphanedEntries",
    BindingFlags.Instance | BindingFlags.NonPublic);
if (prop?.GetValue(Config) is System.Collections.IDictionary d && d.Count > 0)
{
    d.Clear();
    Config.Save();
}
```

用 try/catch 包住，取不到成员就跳过（版本升级可能改名）。

---

## 17. IL2CPP 下宿主组件挂不上去

**现象**：`AddComponent<PanelHost>()` 抛异常或返回 null。

**原因**：IL2CPP 运行时不认识托管侧新定义的类型。

**解决**：
```csharp
ClassInjector.RegisterTypeInIl2Cpp<PanelHost>();     // 注册一次
```
并给组件加 IL2CPP 需要的构造函数：
```csharp
public PanelHost(IntPtr ptr) : base(ptr) { }
```
IMGUI 本身在 IL2CPP 下可正常使用，`Font.CreateDynamicFontFromOSFont` 也可用。

---

## 18. 中文字体又粗又糊

**原因**：Unity 内置 GUI 字体是 Arial，CJK 靠系统 fallback，字重与间距都很差。

**解决**：换系统中文字体，并做好回退。

```csharp
string[] installed = Font.GetOSInstalledFontNames();
foreach (var want in new[]{ "Microsoft YaHei UI", "Microsoft YaHei", "Noto Sans SC", "SimHei" })
{
    if (Array.IndexOf(installed, want) < 0) continue;
    var f = Font.CreateDynamicFontFromOSFont(want, 14);
    if (f != null) { f.hideFlags = HideFlags.HideAndDontSave; return f; }
}
return null;    // 回退 Unity 内置，功能不受影响
```

整个过程 try/catch，并提供开关让玩家在字体异常时关掉。
字号交给 `GUIStyle.fontSize`，动态字体会按需生成字形，不用为每个字号各建一个 Font。
