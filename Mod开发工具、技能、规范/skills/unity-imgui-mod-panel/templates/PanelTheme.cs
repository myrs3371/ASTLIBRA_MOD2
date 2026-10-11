// =============================================================================
// PanelTheme.cs — Unity Mod IMGUI 面板主题（模板，可直接复制到你的 Mod 工程）
//
// 只依赖 UnityEngine，不依赖 BepInEx / MelonLoader / 游戏程序集，也不需要任何图片资源：
// 圆角九宫格纹理是运行时用代码画出来的，中文字体从操作系统取。
//
// 使用方式：
//   1. 把本文件与 ModPanel.cs / PanelWidgets.cs / PanelHost.cs 一起复制进工程；
//   2. 把命名空间 ModPanelKit 改成你自己的；
//   3. 需要换配色时只改「配色」区的常量，不要在业务代码里散落 new GUIStyle。
//
// 关键约束：GUI.skin 只能在 OnGUI 期间访问，所以全部样式都在 EnsureBuilt() 里惰性构建，
// 由绘制入口调用，绝不能在 Awake / 构造函数里初始化。
// =============================================================================

using System;
using UnityEngine;

namespace ModPanelKit;

internal static class PanelTheme
{
    // =========================================================================
    // 对外开关
    // =========================================================================

    /// <summary>是否用系统中文字体替换 Unity 内置字体。改动后需调用 <see cref="Invalidate"/>。</summary>
    public static bool UseSystemFont { get; set; } = true;

    /// <summary>当前实际使用的字体名，用于在设置页显示。</summary>
    public static string FontName { get; private set; } = "(Unity 内置)";

    /// <summary>日志出口。接到 Mod 自己的 Logger 上，默认走 Unity 控制台。</summary>
    public static Action<string> Log { get; set; } = msg => Debug.Log("[PanelTheme] " + msg);

    /// <summary>配色或字体设置变化后强制重建全部样式。</summary>
    public static void Invalidate() => _built = false;

    // =========================================================================
    // 配色（要改主题只改这里）
    // =========================================================================

    private static Color Hex(int rgb, float a = 1f) => new Color(
        ((rgb >> 16) & 0xFF) / 255f,
        ((rgb >> 8) & 0xFF) / 255f,
        (rgb & 0xFF) / 255f,
        a);

    public static readonly Color ColWindow = Hex(0x15171C, 0.97f);
    public static readonly Color ColWindowEdge = Hex(0x3A4050);
    public static readonly Color ColTitleBar = Hex(0x21252E);
    public static readonly Color ColNavBg = Hex(0x1A1D24, 0.90f);
    public static readonly Color ColCard = Hex(0x1E222A, 0.92f);
    public static readonly Color ColCardEdge = Hex(0x2E333F);

    public static readonly Color ColBtn = Hex(0x272C36);
    public static readonly Color ColBtnHover = Hex(0x333A47);
    public static readonly Color ColBtnActive = Hex(0x414A5A);
    public static readonly Color ColBtnEdge = Hex(0x3D4453);

    public static readonly Color ColPrimary = Hex(0xB8862B);
    public static readonly Color ColPrimaryHover = Hex(0xD9A233);
    public static readonly Color ColDanger = Hex(0x8E3A3A);
    public static readonly Color ColDangerHover = Hex(0xAD4949);
    public static readonly Color ColSelected = Hex(0x2F3A4E);
    public static readonly Color ColSelectedEdge = Hex(0x5A7099);

    public static readonly Color ColField = Hex(0x101318);
    public static readonly Color ColFieldEdge = Hex(0x424A5B);

    public static readonly Color TxtMain = Hex(0xE8EAEF);
    public static readonly Color TxtSub = Hex(0xA7AEBB);
    public static readonly Color TxtWeak = Hex(0x79808E);
    public static readonly Color TxtAccent = Hex(0xFFC94D);
    public static readonly Color TxtOk = Hex(0x74D69A);
    public static readonly Color TxtWarn = Hex(0xF2A65A);
    public static readonly Color TxtBad = Hex(0xEB6B6B);

    // =========================================================================
    // 样式
    // =========================================================================

    public static GUIStyle Window { get; private set; }
    public static GUIStyle TitleBar { get; private set; }
    public static GUIStyle TitleText { get; private set; }
    public static GUIStyle TitleNote { get; private set; }
    public static GUIStyle NavBg { get; private set; }
    public static GUIStyle NavItem { get; private set; }
    public static GUIStyle NavItemOn { get; private set; }
    public static GUIStyle Card { get; private set; }
    public static GUIStyle CardTitle { get; private set; }
    public static GUIStyle Label { get; private set; }
    public static GUIStyle Sub { get; private set; }
    public static GUIStyle Hint { get; private set; }
    public static GUIStyle Value { get; private set; }
    public static GUIStyle Button { get; private set; }
    public static GUIStyle ButtonPrimary { get; private set; }
    public static GUIStyle ButtonDanger { get; private set; }
    public static GUIStyle Mini { get; private set; }
    public static GUIStyle Chip { get; private set; }
    public static GUIStyle ChipOn { get; private set; }
    public static GUIStyle Row { get; private set; }
    public static GUIStyle RowOn { get; private set; }
    public static GUIStyle Field { get; private set; }
    public static GUIStyle Toggle { get; private set; }
    public static GUIStyle Divider { get; private set; }
    public static GUIStyle Scroll { get; private set; }

    private static bool _built;

    /// <summary>必须在 OnGUI 内调用。纹理为空说明被引擎回收过，会自动重建。</summary>
    public static void EnsureBuilt()
    {
        if (_built && Window != null && Window.normal.background != null) return;
        _built = true;

        Font font = ResolveFont();

        Window = Solid(ColWindow, ColWindowEdge, 10, 1);
        Window.padding = new RectOffset(0, 0, 0, 0);
        Window.overflow = new RectOffset(0, 0, 0, 0);

        TitleBar = Solid(ColTitleBar, ColTitleBar, 10, 0);
        TitleBar.padding = new RectOffset(12, 12, 0, 0);

        TitleText = Text(font, 14, FontStyle.Bold, TxtMain, TextAnchor.MiddleLeft);
        TitleNote = Text(font, 10, FontStyle.Normal, TxtWeak, TextAnchor.MiddleLeft);
        Label = Text(font, 13, FontStyle.Normal, TxtMain, TextAnchor.MiddleLeft, wrap: true);
        Sub = Text(font, 12, FontStyle.Normal, TxtSub, TextAnchor.MiddleLeft, wrap: true);
        Hint = Text(font, 11, FontStyle.Normal, TxtWeak, TextAnchor.UpperLeft, wrap: true);
        Value = Text(font, 13, FontStyle.Bold, TxtAccent, TextAnchor.MiddleLeft, wrap: true);
        CardTitle = Text(font, 13, FontStyle.Bold, TxtMain, TextAnchor.MiddleLeft);

        NavBg = Solid(ColNavBg, ColNavBg, 0, 0);
        NavBg.padding = new RectOffset(6, 6, 8, 8);

        NavItem = Clickable(font, 13, new Color(ColNavBg.r, ColNavBg.g, ColNavBg.b, 0f),
            ColBtnHover, ColSelected, TxtSub, TxtMain, 6, 0);
        NavItem.alignment = TextAnchor.MiddleLeft;
        NavItem.padding = new RectOffset(10, 6, 0, 0);
        NavItem.margin = new RectOffset(0, 0, 1, 1);

        NavItemOn = Clickable(font, 13, ColSelected, ColSelected, ColSelected,
            TxtAccent, TxtAccent, 6, 1, ColSelectedEdge);
        NavItemOn.fontStyle = FontStyle.Bold;
        NavItemOn.alignment = TextAnchor.MiddleLeft;
        NavItemOn.padding = new RectOffset(10, 6, 0, 0);
        NavItemOn.margin = new RectOffset(0, 0, 1, 1);

        Card = Solid(ColCard, ColCardEdge, 8, 1);
        Card.padding = new RectOffset(10, 10, 8, 9);
        Card.margin = new RectOffset(0, 0, 3, 5);

        Button = Clickable(font, 12, ColBtn, ColBtnHover, ColBtnActive, TxtMain, Color.white, 6, 1, ColBtnEdge);
        ButtonPrimary = Clickable(font, 12, ColPrimary, ColPrimaryHover, ColPrimaryHover,
            Hex(0x1B1408), Color.white, 6, 0);
        ButtonPrimary.fontStyle = FontStyle.Bold;
        ButtonDanger = Clickable(font, 12, ColDanger, ColDangerHover, ColDangerHover,
            Hex(0xFFE9E9), Color.white, 6, 0);
        Mini = Clickable(font, 11, ColBtn, ColBtnHover, ColBtnActive, TxtSub, Color.white, 5, 1, ColBtnEdge);
        Mini.padding = new RectOffset(4, 4, 0, 0);

        Chip = Clickable(font, 11, ColBtn, ColBtnHover, ColBtnActive, TxtSub, Color.white, 9, 1, ColBtnEdge);
        Chip.padding = new RectOffset(6, 6, 2, 2);
        ChipOn = Clickable(font, 11, ColSelected, ColSelected, ColSelected,
            TxtAccent, TxtAccent, 9, 1, ColSelectedEdge);
        ChipOn.fontStyle = FontStyle.Bold;
        ChipOn.padding = new RectOffset(6, 6, 2, 2);

        // 列表行的文字色用 TxtMain，这样再叠 GUI.contentColor 着色时不会偏暗
        Row = Clickable(font, 12, Hex(0x1B1F26, 0.55f), ColBtnHover, ColBtnActive,
            TxtMain, Color.white, 5, 0);
        Row.alignment = TextAnchor.MiddleLeft;
        Row.padding = new RectOffset(8, 8, 0, 0);
        Row.margin = new RectOffset(0, 0, 1, 1);
        RowOn = Clickable(font, 12, ColSelected, ColSelected, ColSelected,
            TxtAccent, TxtAccent, 5, 1, ColSelectedEdge);
        RowOn.fontStyle = FontStyle.Bold;
        RowOn.alignment = TextAnchor.MiddleLeft;
        RowOn.padding = new RectOffset(8, 8, 0, 0);
        RowOn.margin = new RectOffset(0, 0, 1, 1);

        Field = new GUIStyle(GUI.skin.textField);
        ApplyBg(Field, Nine(ColField, ColFieldEdge, 5, 1), 6);
        Field.normal.textColor = TxtMain;
        Field.focused.textColor = Color.white;
        Field.hover.textColor = TxtMain;
        Field.alignment = TextAnchor.MiddleLeft;
        Field.padding = new RectOffset(6, 6, 0, 0);
        Field.fontSize = 12;
        if (font != null) Field.font = font;

        Toggle = new GUIStyle(GUI.skin.toggle);
        Toggle.normal.textColor = TxtSub;
        Toggle.onNormal.textColor = TxtAccent;
        Toggle.hover.textColor = TxtMain;
        Toggle.onHover.textColor = TxtAccent;
        Toggle.fontSize = 12;
        Toggle.padding = new RectOffset(20, 0, 0, 0);
        if (font != null) Toggle.font = font;

        Divider = Solid(Hex(0x2C313C), Hex(0x2C313C), 0, 0);
        Scroll = new GUIStyle(GUI.skin.scrollView) { padding = new RectOffset(0, 4, 0, 0) };
    }

    // =========================================================================
    // 样式工厂
    // =========================================================================

    private static GUIStyle Text(Font font, int size, FontStyle fs, Color color,
        TextAnchor anchor, bool wrap = false)
    {
        var s = new GUIStyle(GUI.skin.label)
        {
            fontSize = size,
            fontStyle = fs,
            alignment = anchor,
            wordWrap = wrap,
            padding = new RectOffset(0, 0, 1, 1),
        };
        s.normal.textColor = color;
        s.hover.textColor = color;
        if (font != null) s.font = font;
        return s;
    }

    /// <summary>纯背景块（卡片 / 标题栏 / 分隔线）。</summary>
    private static GUIStyle Solid(Color fill, Color edge, int radius, int border)
    {
        var s = new GUIStyle();
        ApplyBg(s, Nine(fill, edge, radius, border), radius + border + 1);
        return s;
    }

    /// <summary>可交互样式，带 hover / active 三态背景，这是「有反馈」的关键。</summary>
    private static GUIStyle Clickable(Font font, int size,
        Color normal, Color hover, Color active,
        Color textNormal, Color textHover,
        int radius, int border, Color edge = default)
    {
        var s = new GUIStyle(GUI.skin.button)
        {
            fontSize = size,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = false,
            padding = new RectOffset(6, 6, 0, 0),
            margin = new RectOffset(2, 2, 2, 2),
        };
        if (font != null) s.font = font;

        int slice = radius + border + 1;
        var bgN = Nine(normal, border > 0 ? edge : normal, radius, border);
        var bgH = Nine(hover, border > 0 ? edge : hover, radius, border);
        var bgA = Nine(active, border > 0 ? edge : active, radius, border);

        s.border = new RectOffset(slice, slice, slice, slice);
        s.normal.background = bgN;
        s.hover.background = bgH;
        s.active.background = bgA;
        s.focused.background = bgH;
        s.onNormal.background = bgA;
        s.onHover.background = bgH;
        s.onActive.background = bgA;
        s.onFocused.background = bgA;

        s.normal.textColor = textNormal;
        s.hover.textColor = textHover;
        s.active.textColor = textHover;
        s.focused.textColor = textHover;
        s.onNormal.textColor = textHover;
        s.onHover.textColor = textHover;
        s.onActive.textColor = textHover;
        s.onFocused.textColor = textHover;
        return s;
    }

    private static void ApplyBg(GUIStyle s, Texture2D tex, int slice)
    {
        s.border = new RectOffset(slice, slice, slice, slice);
        s.normal.background = tex;
        s.hover.background = tex;
        s.active.background = tex;
        s.focused.background = tex;
        s.onNormal.background = tex;
        s.onHover.background = tex;
        s.onActive.background = tex;
        s.onFocused.background = tex;
    }

    // =========================================================================
    // 圆角九宫格纹理生成
    // =========================================================================

    /// <summary>
    /// 生成用于九宫格拉伸的圆角矩形纹理。
    /// 尺寸取 2*(radius+border+1)+2，保证四角完整、中间只留 2px 供拉伸。
    /// 边缘用覆盖率做 1px 抗锯齿，否则圆角会有明显锯齿。
    /// hideFlags 必须设 HideAndDontSave，否则切场景时纹理会被销毁，样式变成白块。
    /// </summary>
    private static Texture2D Nine(Color fill, Color edge, int radius, int border)
    {
        int slice = radius + border + 1;
        int size = Mathf.Max(4, slice * 2 + 2);
        var tex = new Texture2D(size, size, TextureFormat.ARGB32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave,
        };

        var px = new Color[size * size];
        float r = Mathf.Max(0.001f, radius + border);
        float inner = Mathf.Max(0f, r - border);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float cx = x + 0.5f;
                float cy = y + 0.5f;
                // 到「圆角圆心矩形」的距离：只在四角处非零
                float dx = Mathf.Max(0f, Mathf.Max(r - cx, cx - (size - r)));
                float dy = Mathf.Max(0f, Mathf.Max(r - cy, cy - (size - r)));
                float d = Mathf.Sqrt(dx * dx + dy * dy);

                float outerCov = Mathf.Clamp01(r - d + 0.5f);     // 外轮廓覆盖率
                float innerCov = Mathf.Clamp01(inner - d + 0.5f);  // 描边内侧覆盖率

                Color c = border > 0 ? Color.Lerp(edge, fill, innerCov) : fill;
                c.a *= outerCov;
                px[y * size + x] = c;
            }
        }

        tex.SetPixels(px);
        tex.Apply(false, false);
        return tex;
    }

    // =========================================================================
    // 系统中文字体
    // =========================================================================

    private static readonly string[] FontCandidates =
    {
        "Microsoft YaHei UI", "Microsoft YaHei", "微软雅黑",
        "Noto Sans SC", "Source Han Sans SC", "PingFang SC",
        "SimHei", "黑体", "SimSun", "宋体",
    };

    private static Font _cachedFont;
    private static bool _fontResolved;

    /// <summary>
    /// Unity 内置 GUI 字体（Arial）渲染 CJK 要走 fallback，字重与间距都很糟。
    /// 这里换成系统中文字体；失败返回 null（沿用默认），不影响功能。
    /// </summary>
    private static Font ResolveFont()
    {
        if (!UseSystemFont)
        {
            FontName = "(Unity 内置)";
            return null;
        }

        if (_fontResolved && _cachedFont != null)
        {
            FontName = _cachedFont.name;
            return _cachedFont;
        }
        _fontResolved = true;

        try
        {
            string[] installed = Font.GetOSInstalledFontNames();
            foreach (string want in FontCandidates)
            {
                if (installed == null || Array.IndexOf(installed, want) < 0) continue;
                var f = Font.CreateDynamicFontFromOSFont(want, 14);
                if (f == null) continue;
                f.hideFlags = HideFlags.HideAndDontSave;
                _cachedFont = f;
                FontName = want;
                Log?.Invoke($"面板字体已切换为系统字体：{want}");
                return f;
            }
            Log?.Invoke("未找到候选中文字体，沿用 Unity 内置字体。");
        }
        catch (Exception ex)
        {
            Log?.Invoke($"加载系统字体失败，沿用内置字体：{ex.Message}");
        }

        FontName = "(Unity 内置)";
        return null;
    }
}
