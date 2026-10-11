// =============================================================================
// PanelHost.cs — 常驻宿主 MonoBehaviour（模板）
//
// 职责：按键轮询、每帧维护面板光标、把 OnGUI 转发给面板。
// 挂在 DontDestroyOnLoad 的 GameObject 上，跨场景存活。
//
// BepInEx (Mono) 里这样创建：
//     var go = new GameObject("MyMod_Host");
//     UnityEngine.Object.DontDestroyOnLoad(go);
//     go.hideFlags = HideFlags.HideAndDontSave;
//     go.AddComponent<PanelHost>();
//
// BepInEx (IL2CPP) / MelonLoader 下自定义 MonoBehaviour 必须先注册类型：
//     ClassInjector.RegisterTypeInIl2Cpp<PanelHost>();
// 且 IL2CPP 需要 IntPtr 构造函数：
//     public PanelHost(IntPtr ptr) : base(ptr) { }
// =============================================================================

using System;
using UnityEngine;

namespace ModPanelKit;

internal sealed class PanelHost : MonoBehaviour
{
    /// <summary>面板实例。由创建方在 AddComponent 后赋值，或在 Awake 里自行 new。</summary>
    public ModPanelBase Panel { get; set; }

    /// <summary>开关面板的按键。接到 Mod 配置上。</summary>
    public Func<KeyCode> ToggleKey { get; set; } = () => KeyCode.F1;

    /// <summary>日志出口，默认走 Unity 控制台。</summary>
    public Action<string> LogError { get; set; } = msg => Debug.LogError("[PanelHost] " + msg);

    private void Update()
    {
        if (Panel == null) return;

        try
        {
            if (Pressed(ToggleKey?.Invoke() ?? KeyCode.None))
                Panel.Toggle();
        }
        catch (Exception ex)
        {
            LogError?.Invoke($"按键处理异常：{ex.Message}");
        }

        // 面板打开期间强制显示系统光标，必须每帧调用
        Panel.Tick();
    }

    /// <summary>KeyCode.None 表示未绑定，显式短路，避免误触发。</summary>
    private static bool Pressed(KeyCode key)
        => key != KeyCode.None && Input.GetKeyDown(key);

    private void OnGUI()
    {
        try
        {
            Panel?.Draw();
        }
        catch (Exception ex)
        {
            // IMGUI 里抛异常会破坏 GUILayout 的 Begin/End 配对，导致后续整帧布局错乱，
            // 因此这里必须吞掉并记录，绝不能让异常穿出 OnGUI。
            LogError?.Invoke($"绘制面板异常：{ex}");
        }
    }

    private void OnDestroy()
    {
        // 关闭面板以还原系统光标状态，否则卸载 Mod 后指针会一直显示
        Panel?.Close();
    }
}
