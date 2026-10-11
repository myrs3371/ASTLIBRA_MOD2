// =============================================================================
// PanelWidgets.cs — 无状态通用控件（模板）
//
// 目的：让各页签的绘制代码只表达业务，不重复布局细节。
// 全部样式取自 PanelTheme，禁止在业务代码里 new GUIStyle。
//
// 用法：在页签绘制里 using static ModPanelKit.PanelWidgets; 之后直接调用。
// =============================================================================

using System;
using UnityEngine;

namespace ModPanelKit;

internal static class PanelWidgets
{
    // -------------------------------------------------------------------------
    // 容器
    // -------------------------------------------------------------------------

    /// <summary>开始一张卡片。title 为空则只有背景没有标题。必须与 EndCard 配对。</summary>
    public static void BeginCard(string title = null)
    {
        GUILayout.BeginVertical(PanelTheme.Card);
        if (string.IsNullOrEmpty(title)) return;

        GUILayout.Label(title, PanelTheme.CardTitle);
        Divider();
    }

    public static void EndCard() => GUILayout.EndVertical();

    /// <summary>1px 水平分隔线。</summary>
    public static void Divider()
    {
        var r = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true));
        GUI.Box(r, GUIContent.none, PanelTheme.Divider);
        GUILayout.Space(4f);
    }

    // -------------------------------------------------------------------------
    // 文本
    // -------------------------------------------------------------------------

    /// <summary>正文（浅灰，自动换行）。</summary>
    public static void Info(string text) => GUILayout.Label(text, PanelTheme.Sub);

    /// <summary>脚注 / 说明（更小更暗）。</summary>
    public static void Hint(string text) => GUILayout.Label(text, PanelTheme.Hint);

    /// <summary>定宽标签，用于表单左列。</summary>
    public static void Caption(string text, float width)
        => GUILayout.Label(text, PanelTheme.Sub, GUILayout.Width(width));

    /// <summary>键值行：左灰标签 + 右侧强调数值。</summary>
    public static void KV(string label, string value, float labelWidth = 96f)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, PanelTheme.Sub, GUILayout.Width(labelWidth));
        GUILayout.Label(value, PanelTheme.Value);
        GUILayout.EndHorizontal();
    }

    /// <summary>带颜色的键值行，用于状态判定（可用 / 禁用 / 警告）。</summary>
    public static void KVColored(string label, string value, Color color, float labelWidth = 96f)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, PanelTheme.Sub, GUILayout.Width(labelWidth));
        var style = new GUIStyle(PanelTheme.Value);
        style.normal.textColor = color;
        GUILayout.Label(value, style);
        GUILayout.EndHorizontal();
    }

    // -------------------------------------------------------------------------
    // 基础控件
    // -------------------------------------------------------------------------

    public static bool Btn(string label, float width, GUIStyle style = null, float height = 21f)
        => GUILayout.Button(label, style ?? PanelTheme.Button,
            GUILayout.Width(width), GUILayout.Height(height));

    /// <summary>占满一行宽度的按钮，用于主操作。</summary>
    public static bool BtnWide(string label, GUIStyle style = null, float height = 23f)
        => GUILayout.Button(label, style ?? PanelTheme.Button,
            GUILayout.ExpandWidth(true), GUILayout.Height(height));

    public static string TextBox(string value, float width)
        => GUILayout.TextField(value ?? string.Empty, PanelTheme.Field,
            GUILayout.Width(width), GUILayout.Height(20f));

    /// <summary>开关行。返回新值，调用方自行写回配置。</summary>
    public static bool ToggleRow(bool value, string label, string hint = null)
    {
        bool result = GUILayout.Toggle(value, "  " + label, PanelTheme.Toggle);
        if (!string.IsNullOrEmpty(hint)) Hint(hint);
        return result;
    }

    /// <summary>搜索框，返回新的搜索词。</summary>
    public static string SearchBox(string value, float width = 150f)
    {
        GUILayout.BeginHorizontal();
        Caption("搜索", 34f);
        string result = TextBox(value, width);
        if (Btn("清空", 42f, PanelTheme.Mini)) result = string.Empty;
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        return result;
    }

    /// <summary>
    /// 一组互斥的筛选标签（胶囊按钮）。返回被点击项的下标，未点击返回 -1。
    /// colorOf 可选，用于给每项上色（例如稀有度配色）。
    /// </summary>
    public static int ChipRow(string[] labels, int selected, float width = 46f,
        Func<int, Color> colorOf = null)
    {
        int clicked = -1;
        GUILayout.BeginHorizontal();
        for (int i = 0; i < labels.Length; i++)
        {
            bool on = selected == i;
            Color saved = GUI.contentColor;
            if (!on && colorOf != null) GUI.contentColor = colorOf(i);

            if (GUILayout.Button(labels[i], on ? PanelTheme.ChipOn : PanelTheme.Chip,
                    GUILayout.Width(width), GUILayout.Height(20f)))
                clicked = i;

            GUI.contentColor = saved;
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        return clicked;
    }

    /// <summary>
    /// 列表行。selected 决定高亮；tint 用 GUI.contentColor 叠色，
    /// 比每帧 new GUIStyle 便宜得多（列表可能几十上百行）。
    /// </summary>
    public static bool ListRow(string label, bool selected, Color? tint = null, float height = 21f)
    {
        Color saved = GUI.contentColor;
        if (!selected && tint.HasValue) GUI.contentColor = tint.Value;

        bool clicked = GUILayout.Button(label,
            selected ? PanelTheme.RowOn : PanelTheme.Row, GUILayout.Height(height));

        GUI.contentColor = saved;
        return clicked;
    }

    /// <summary>
    /// 限高滚动区。外层已有滚动时嵌套使用是允许的，但内层必须限高，
    /// 否则两层滚动会互相抢空间导致布局抖动。
    /// </summary>
    public static Vector2 BeginList(Vector2 scroll, float height)
        => GUILayout.BeginScrollView(scroll, false, false,
            GUIStyle.none, GUI.skin.verticalScrollbar, PanelTheme.Scroll,
            GUILayout.Height(height));

    public static void EndList() => GUILayout.EndScrollView();

    // -------------------------------------------------------------------------
    // 解析辅助
    // -------------------------------------------------------------------------

    public static int ParseInt(string text, int fallback = 0)
        => int.TryParse(text, out var v) ? v : fallback;

    public static float ParseFloat(string text, float fallback = 0f)
        => float.TryParse(text, out var v) ? v : fallback;
}
