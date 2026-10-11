// =============================================================================
// ModPanel.cs — 面板窗口壳基类（模板）
//
// 提供：自绘标题栏 / 左侧竖排导航 / 顶部状态条 / 滚动内容区 / 底部条 /
//       整窗缩放 / 拖动 / 右下角拖拽改尺寸 / 系统光标守卫 / 按键捕获 / 操作提示。
//
// 用法：继承并实现 Title、TabLabels、DrawTab；需要时覆写 DrawStatusStrip / OnOpen。
//
//   internal sealed class MyPanel : ModPanelBase
//   {
//       protected override string Title => "我的 Mod 控制面板 v1.0.0";
//       protected override string TitleNote => "（免费分享，禁止盗卖）";
//       protected override string[] TabLabels => new[] { "概览", "玩家", "设置" };
//       protected override void DrawTab(int index) { ... }
//   }
//
// 只依赖 UnityEngine。
// =============================================================================

using System;
using UnityEngine;

namespace ModPanelKit;

internal abstract class ModPanelBase
{
    // -------------------------------------------------------------------------
    // 可覆写的配置
    // -------------------------------------------------------------------------

    /// <summary>窗口 IMGUI ID。若目标游戏自带 OnGUI 窗口，必须避开它的 ID。</summary>
    protected virtual int WindowId => 61701;

    protected abstract string Title { get; }
    protected virtual string TitleNote => null;
    protected abstract string[] TabLabels { get; }
    protected abstract void DrawTab(int index);

    /// <summary>标题栏下方常驻的一行状态。不需要就不覆写。</summary>
    protected virtual void DrawStatusStrip() { }

    /// <summary>每次打开面板时调用，用来把游戏当前值同步进输入框。</summary>
    protected virtual void OnOpen() { }

    /// <summary>切换页签时调用。</summary>
    protected virtual void OnTabChanged(int index) { }

    protected virtual float MinWidth => 560f;
    protected virtual float MaxWidth => 1100f;
    protected virtual float MinHeight => 320f;
    protected virtual float MaxHeight => 1000f;
    protected virtual float NavWidth => 104f;
    protected virtual float DefaultWidth => 680f;
    protected virtual float DefaultHeight => 560f;

    /// <summary>缩放值的持久化出口，接到 Mod 配置上；不需要就留空。</summary>
    protected virtual float LoadScale() => 1f;
    protected virtual void SaveScale(float value) { }

    protected const float TitleBarHeight = 32f;
    private const float MinScale = 0.6f;
    private const float MaxScale = 2.5f;
    protected const float ScaleStep = 0.1f;

    // -------------------------------------------------------------------------
    // 状态
    // -------------------------------------------------------------------------

    private bool _visible;
    private int _tab;
    private Rect _rect;
    private float _scale = 1f;
    private bool _scaleLoaded;
    private bool _resizing;
    private Vector2 _scroll;

    private string _toast;
    private float _toastUntil;

    private bool _savedCursorVisible;
    private bool _cursorForced;

    /// <summary>正在等待玩家按键的绑定项；null 表示不在捕获状态。</summary>
    private Action<KeyCode> _captureSetter;
    private object _captureToken;

    protected ModPanelBase()
    {
        _rect = new Rect(28f, 28f, DefaultWidth, DefaultHeight);
    }

    public bool Visible => _visible;
    protected int CurrentTab => _tab;
    protected Rect WindowRect => _rect;

    // -------------------------------------------------------------------------
    // 开关与每帧维护
    // -------------------------------------------------------------------------

    public void Toggle()
    {
        _visible = !_visible;
        if (_visible) OnOpen();
        else CancelCapture();
        UpdateCursor();
    }

    public void Close()
    {
        _visible = false;
        CancelCapture();
        UpdateCursor();
    }

    /// <summary>必须由宿主 MonoBehaviour 的 Update 每帧调用。</summary>
    public void Tick() => UpdateCursor();

    /// <summary>在面板底部显示一条短暂提示，用于反馈按钮操作结果。</summary>
    protected void Notify(string text, float seconds = 2.5f)
    {
        _toast = text;
        _toastUntil = Time.unscaledTime + seconds;
    }

    /// <summary>
    /// 面板打开期间强制显示系统光标。
    ///
    /// 很多游戏用自绘准星（跟随鼠标的 UI 对象）并执行 Cursor.visible = false。
    /// IMGUI 绘制在所有 Canvas 之上，会把那个准星完全盖住，玩家就看不到指针在哪。
    /// 因此面板可见时打开系统光标，关闭后还原游戏原本的设置。
    /// 游戏可能在场景加载 / UI 显示时再次关闭光标，所以这里每帧确认而不是只设一次。
    /// </summary>
    private void UpdateCursor()
    {
        if (_visible)
        {
            if (!_cursorForced)
            {
                _savedCursorVisible = Cursor.visible;
                _cursorForced = true;
            }
            if (!Cursor.visible) Cursor.visible = true;
        }
        else if (_cursorForced)
        {
            _cursorForced = false;
            Cursor.visible = _savedCursorVisible;
        }
    }

    // -------------------------------------------------------------------------
    // 绘制入口（由宿主的 OnGUI 调用）
    // -------------------------------------------------------------------------

    public void Draw()
    {
        if (!_visible) return;

        if (!_scaleLoaded)
        {
            _scaleLoaded = true;
            _scale = Mathf.Clamp(LoadScale(), MinScale, MaxScale);
        }

        PanelTheme.EnsureBuilt();
        HandleKeyCapture();

        // 整窗缩放（含文本）。GUI.matrix 会同时缩放输入坐标，无需手工换算鼠标位置
        Matrix4x4 saved = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(_scale, _scale, 1f));

        _rect.width = Mathf.Clamp(_rect.width, MinWidth, MaxWidth);
        _rect.height = Mathf.Clamp(_rect.height, MinHeight, MaxHeight);

        // 防止窗口被拖出屏幕（注意换算到逻辑坐标系）
        float logicalW = Screen.width / _scale;
        float logicalH = Screen.height / _scale;
        _rect.x = Mathf.Clamp(_rect.x, -_rect.width + 120f, Mathf.Max(0f, logicalW - 120f));
        _rect.y = Mathf.Clamp(_rect.y, 0f, Mathf.Max(0f, logicalH - 40f));

        _rect = GUI.Window(WindowId, _rect, DrawWindowInternal, GUIContent.none, PanelTheme.Window);

        GUI.matrix = saved;
    }

    private void DrawWindowInternal(int id)
    {
        DrawTitleBar();

        // 标题栏是绝对坐标绘制的，这里必须把布局游标推到它下方，否则内容会压在标题上
        GUILayout.Space(TitleBarHeight + 6f);

        GUILayout.BeginHorizontal();
        DrawNav();

        GUILayout.BeginVertical();
        GUILayout.Space(2f);
        DrawStatusStrip();

        _scroll = GUILayout.BeginScrollView(_scroll, false, false,
            GUIStyle.none, GUI.skin.verticalScrollbar, PanelTheme.Scroll,
            GUILayout.ExpandHeight(true));

        DrawTab(_tab);

        GUILayout.EndScrollView();
        GUILayout.EndVertical();

        GUILayout.Space(8f);
        GUILayout.EndHorizontal();

        DrawFooter();
        DrawResizeHandle();

        // 标题栏左侧区域可拖动窗口，右侧留给按钮
        GUI.DragWindow(new Rect(0f, 0f, _rect.width - 120f, TitleBarHeight));
    }

    // -------------------------------------------------------------------------
    // 标题栏
    // -------------------------------------------------------------------------

    /// <summary>标题栏右侧的自定义按钮区，返回下一个按钮的右边界 x。</summary>
    protected virtual float DrawTitleBarExtras(float x) => x;

    private void DrawTitleBar()
    {
        GUI.Box(new Rect(0f, 0f, _rect.width, TitleBarHeight), GUIContent.none, PanelTheme.TitleBar);

        float titleWidth = PanelTheme.TitleText.CalcSize(new GUIContent(Title)).x;
        GUI.Label(new Rect(12f, 0f, titleWidth + 6f, TitleBarHeight), Title, PanelTheme.TitleText);

        if (!string.IsNullOrEmpty(TitleNote))
        {
            GUI.Label(new Rect(12f + titleWidth + 8f, 0f, 220f, TitleBarHeight),
                TitleNote, PanelTheme.TitleNote);
        }

        float x = _rect.width - 8f - 26f;
        if (GUI.Button(new Rect(x, 6f, 26f, 20f), "×", PanelTheme.Button)) Close();

        DrawTitleBarExtras(x);
    }

    // -------------------------------------------------------------------------
    // 左侧导航
    // -------------------------------------------------------------------------

    private void DrawNav()
    {
        GUILayout.BeginVertical(PanelTheme.NavBg,
            GUILayout.Width(NavWidth), GUILayout.ExpandHeight(true));

        string[] labels = TabLabels;
        for (int i = 0; i < labels.Length; i++)
        {
            bool on = _tab == i;
            if (GUILayout.Button(labels[i], on ? PanelTheme.NavItemOn : PanelTheme.NavItem,
                    GUILayout.Height(26f)) && !on)
            {
                _tab = i;
                OnTabChanged(i);
            }
        }

        GUILayout.FlexibleSpace();
        DrawNavFooter();
        GUILayout.EndVertical();
    }

    /// <summary>导航栏底部的小字（例如「局内 / 局外」状态）。</summary>
    protected virtual void DrawNavFooter() { }

    // -------------------------------------------------------------------------
    // 底部条 / 缩放 / 尺寸
    // -------------------------------------------------------------------------

    private void DrawFooter()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Space(8f);

        GUILayout.Label("缩放", PanelTheme.Hint, GUILayout.Width(26f));
        if (GUILayout.Button("－", PanelTheme.Mini, GUILayout.Width(24f), GUILayout.Height(18f)))
            SetScale(_scale - ScaleStep);
        GUILayout.Label($"{_scale:0.0}x", PanelTheme.Sub, GUILayout.Width(30f));
        if (GUILayout.Button("＋", PanelTheme.Mini, GUILayout.Width(24f), GUILayout.Height(18f)))
            SetScale(_scale + ScaleStep);
        if (GUILayout.Button("重置", PanelTheme.Mini, GUILayout.Width(34f), GUILayout.Height(18f)))
            SetScale(1f);

        GUILayout.Space(10f);

        if (!string.IsNullOrEmpty(_toast) && Time.unscaledTime < _toastUntil)
        {
            var style = new GUIStyle(PanelTheme.Sub);
            style.normal.textColor = PanelTheme.TxtOk;
            GUILayout.Label(_toast, style);
        }
        else
        {
            DrawFooterHint();
        }

        GUILayout.FlexibleSpace();
        GUILayout.Space(18f);
        GUILayout.EndHorizontal();
        GUILayout.Space(4f);
    }

    /// <summary>没有提示要显示时，底部展示的常驻文字（一般是快捷键说明）。</summary>
    protected virtual void DrawFooterHint() { }

    /// <summary>右下角拖拽改尺寸。窗口内的 mousePosition 已是相对窗口原点的坐标。</summary>
    private void DrawResizeHandle()
    {
        var handle = new Rect(_rect.width - 16f, _rect.height - 16f, 14f, 14f);
        GUI.Label(handle, "◢", PanelTheme.Hint);

        Event e = Event.current;
        if (e == null) return;

        if (e.type == EventType.MouseDown && handle.Contains(e.mousePosition))
        {
            _resizing = true;
            e.Use();
        }
        else if (_resizing && e.type == EventType.MouseDrag)
        {
            _rect.width = Mathf.Clamp(e.mousePosition.x + 8f, MinWidth, MaxWidth);
            _rect.height = Mathf.Clamp(e.mousePosition.y + 8f, MinHeight, MaxHeight);
            e.Use();
        }
        else if (_resizing && e.type == EventType.MouseUp)
        {
            _resizing = false;
            e.Use();
        }
    }

    protected void SetScale(float value)
    {
        _scale = Mathf.Clamp(Mathf.Round(value * 100f) / 100f, MinScale, MaxScale);
        SaveScale(_scale);
    }

    protected float Scale => _scale;

    /// <summary>把窗口恢复到默认位置与尺寸。</summary>
    protected void ResetWindowRect() => _rect = new Rect(28f, 28f, DefaultWidth, DefaultHeight);

    // -------------------------------------------------------------------------
    // 按键捕获
    // -------------------------------------------------------------------------

    /// <summary>
    /// 「点一下再按键」式的按键绑定行。
    /// token 用于区分是哪一行在捕获（传该配置项对象本身即可）。
    /// </summary>
    protected void KeyBindRow(string label, KeyCode current, Action<KeyCode> setter,
        object token, string hint = null)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, PanelTheme.Sub, GUILayout.Width(118f));

        bool capturing = _captureToken != null && ReferenceEquals(_captureToken, token);
        string text = capturing ? "请按下任意键…" : current.ToString();

        if (GUILayout.Button(text, capturing ? PanelTheme.ChipOn : PanelTheme.Button,
                GUILayout.Width(132f), GUILayout.Height(21f)))
        {
            if (capturing)
            {
                CancelCapture();
            }
            else
            {
                _captureSetter = setter;
                _captureToken = token;
                // 必须清掉键盘焦点，否则按键会被页面上的 TextField 吃掉
                GUIUtility.keyboardControl = 0;
                Notify("按 Esc 可取消设置。", 4f);
            }
        }

        if (GUILayout.Button("清除", PanelTheme.Mini, GUILayout.Width(42f), GUILayout.Height(21f)))
            setter?.Invoke(KeyCode.None);

        GUILayout.EndHorizontal();
        if (!string.IsNullOrEmpty(hint)) GUILayout.Label(hint, PanelTheme.Hint);
    }

    /// <summary>
    /// 在窗口绘制之前处理键盘事件，避免被窗口内的 TextField 抢走。
    /// Esc 取消，其余任意键写入。
    /// </summary>
    private void HandleKeyCapture()
    {
        if (_captureToken == null) return;

        Event e = Event.current;
        if (e == null || e.type != EventType.KeyDown) return;

        KeyCode key = e.keyCode;
        if (key == KeyCode.None) return;   // 字符事件没有 keyCode，跳过

        if (key == KeyCode.Escape)
        {
            Notify("已取消按键设置。");
        }
        else
        {
            _captureSetter?.Invoke(key);
            Notify($"已设置为 {key}");
        }

        CancelCapture();
        e.Use();
    }

    private void CancelCapture()
    {
        _captureSetter = null;
        _captureToken = null;
    }
}
