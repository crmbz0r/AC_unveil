using System;
using System.Linq;
using BepInEx.Configuration;
using UnityEngine;

namespace AiComi_LuaMod;

// ─────────────────────────────────────────────────────────────
//  Console Settings — Config + Settings Panel UI
// ─────────────────────────────────────────────────────────────

public static class ConsoleConfig
{
    // ── Appearance ────────────────────────────────────────────
    public static ConfigEntry<string> FontName = null!;
    public static ConfigEntry<int> FontSize = null!;
    public static ConfigEntry<float> BackgroundOpacity = null!;
    public static ConfigEntry<string> AccentColor = null!;

    // ── Title ─────────────────────────────────────────────────
    public static ConfigEntry<bool> RainbowEnabled = null!;
    public static ConfigEntry<bool> MarqueeEnabled = null!;

    // ── Behavior ──────────────────────────────────────────────
    public static ConfigEntry<int> OutputBufferSize = null!;
    public static ConfigEntry<bool> OutputWordWrap = null!;
    public static ConfigEntry<bool> AutoScrollOutput = null!;

    // ── Auto-Restore Cheats ───────────────────────────────────
    public static ConfigEntry<bool> AutoRestoreCheats = null!;
    public static ConfigEntry<bool> SaveNoclip = null!;
    public static ConfigEntry<bool> SaveGhostMode = null!;
    public static ConfigEntry<bool> SaveNoTalkTimeReduction = null!;
    public static ConfigEntry<bool> SaveNoFavorLoss = null!;
    public static ConfigEntry<bool> SaveNoMoodLoss = null!;
    public static ConfigEntry<bool> SaveForcePositiveChoice = null!;
    public static ConfigEntry<bool> SaveAlwaysAcceptTouch = null!;
    public static ConfigEntry<bool> SaveUnlockItems = null!;
    public static ConfigEntry<bool> SaveNoDislike = null!;
    public static ConfigEntry<bool> SaveShowNextH = null!;
    public static ConfigEntry<bool> SaveNoTouchInterruption = null!;

    public static bool Initialized { get; private set; }

    // ── Color ↔ Hex helpers ───────────────────────────────────
    private static string ToHex(Color c) =>
        $"#{Mathf.RoundToInt(c.r * 255):X2}{Mathf.RoundToInt(c.g * 255):X2}{Mathf.RoundToInt(c.b * 255):X2}";

    public static Color ParseHex(string hex, Color fallback)
    {
        if (string.IsNullOrEmpty(hex))
            return fallback;
        hex = hex.TrimStart('#');
        if (hex.Length != 6)
            return fallback;
        if (
            byte.TryParse(hex[..2], System.Globalization.NumberStyles.HexNumber, null, out var r)
            && byte.TryParse(
                hex[2..4],
                System.Globalization.NumberStyles.HexNumber,
                null,
                out var g
            )
            && byte.TryParse(
                hex[4..6],
                System.Globalization.NumberStyles.HexNumber,
                null,
                out var b
            )
        )
            return new Color(r / 255f, g / 255f, b / 255f);
        return fallback;
    }

    public static Color GetAccentColor() => ParseHex(AccentColor?.Value, new Color(0.4f, 1f, 0.4f));

    public static void SetAccentColor(Color c) => AccentColor.Value = ToHex(c);

    public static void Init(ConfigFile cfg)
    {
        // Appearance
        FontName = cfg.Bind(
            "Appearance",
            "FontName",
            "Cascadia Mono",
            "Font name (from OS installed fonts)"
        );
        FontSize = cfg.Bind(
            "Appearance",
            "FontSize",
            16,
            new ConfigDescription(
                "Font size for output/input areas",
                new AcceptableValueRange<int>(10, 28)
            )
        );
        BackgroundOpacity = cfg.Bind(
            "Appearance",
            "BackgroundOpacity",
            0.97f,
            new ConfigDescription(
                "Window background opacity",
                new AcceptableValueRange<float>(0.50f, 1.0f)
            )
        );
        AccentColor = cfg.Bind(
            "Appearance",
            "AccentColor",
            ToHex(new Color(0.4f, 1f, 0.4f)),
            "Accent color — used for output text, active buttons, enabled cheats, and title when rainbow is off (hex #RRGGBB)"
        );

        // Title
        RainbowEnabled = cfg.Bind(
            "Title",
            "RainbowEnabled",
            true,
            "Rainbow wave effect on title text"
        );
        MarqueeEnabled = cfg.Bind(
            "Title",
            "MarqueeEnabled",
            true,
            "Scrolling marquee on title text"
        );

        // Behavior
        OutputBufferSize = cfg.Bind(
            "Behavior",
            "OutputBufferSize",
            32000,
            new ConfigDescription(
                "Max output buffer characters",
                new AcceptableValueRange<int>(4000, 128000)
            )
        );
        OutputWordWrap = cfg.Bind("Behavior", "OutputWordWrap", true, "Word wrap in output area");
        AutoScrollOutput = cfg.Bind(
            "Behavior",
            "AutoScrollOutput",
            true,
            "Auto-scroll output to bottom after execution"
        );

        // Auto-Restore Cheats
        AutoRestoreCheats = cfg.Bind(
            "Cheats.AutoRestore",
            "Enabled",
            false,
            "Re-enable saved cheats on next launch"
        );
        SaveNoclip = cfg.Bind("Cheats.AutoRestore", "Noclip", false, "");
        SaveGhostMode = cfg.Bind("Cheats.AutoRestore", "GhostMode", false, "");
        SaveNoTalkTimeReduction = cfg.Bind("Cheats.AutoRestore", "NoTalkTimeReduction", false, "");
        SaveNoFavorLoss = cfg.Bind("Cheats.AutoRestore", "NoFavorLoss", false, "");
        SaveNoMoodLoss = cfg.Bind("Cheats.AutoRestore", "NoMoodLoss", false, "");
        SaveForcePositiveChoice = cfg.Bind("Cheats.AutoRestore", "ForcePositiveChoice", false, "");
        SaveAlwaysAcceptTouch = cfg.Bind("Cheats.AutoRestore", "AlwaysAcceptTouch", false, "");
        SaveUnlockItems = cfg.Bind("Cheats.AutoRestore", "UnlockItems", false, "");
        SaveNoDislike = cfg.Bind("Cheats.AutoRestore", "NoDislike", false, "");
        SaveShowNextH = cfg.Bind("Cheats.AutoRestore", "ShowNextH", false, "");
        SaveNoTouchInterruption = cfg.Bind("Cheats.AutoRestore", "NoTouchInterruption", false, "");

        Initialized = true;
    }

    public static void ApplySavedCheatStates()
    {
        if (!Initialized || !AutoRestoreCheats.Value)
            return;

        ExplorationSceneHooks.NoclipEnabled = SaveNoclip.Value;
        ExplorationSceneHooks.GhostModeEnabled = SaveGhostMode.Value;
        DialogSceneHooks.NoTalkTimeReduction = SaveNoTalkTimeReduction.Value;
        DialogSceneHooks.NoFavorLoss = SaveNoFavorLoss.Value;
        DialogSceneHooks.NoMoodLoss = SaveNoMoodLoss.Value;
        DialogSceneHooks.ForcePositiveChoice = SaveForcePositiveChoice.Value;
        DialogSceneHooks.AlwaysAcceptTouch = SaveAlwaysAcceptTouch.Value;
        TouchSceneHooks.UnlockItems = SaveUnlockItems.Value;
        TouchSceneHooks.NoDislike = SaveNoDislike.Value;
        TouchSceneHooks.ShowNextH = SaveShowNextH.Value;
        TouchSceneHooks.NoTouchInterruption = SaveNoTouchInterruption.Value;

        if (SaveNoTalkTimeReduction.Value)
            DialogSceneHooks.ApplyNoTalkTimeReduction();
        if (SaveGhostMode.Value)
            ExplorationSceneHooks.ApplyBlindness();

        Plugin.Log.LogInfo("[Settings] Restored saved cheat states");
    }

    public static void SaveCheatStates()
    {
        if (!Initialized || !AutoRestoreCheats.Value)
            return;

        SaveNoclip.Value = ExplorationSceneHooks.NoclipEnabled;
        SaveGhostMode.Value = ExplorationSceneHooks.GhostModeEnabled;
        SaveNoTalkTimeReduction.Value = DialogSceneHooks.NoTalkTimeReduction;
        SaveNoFavorLoss.Value = DialogSceneHooks.NoFavorLoss;
        SaveNoMoodLoss.Value = DialogSceneHooks.NoMoodLoss;
        SaveForcePositiveChoice.Value = DialogSceneHooks.ForcePositiveChoice;
        SaveAlwaysAcceptTouch.Value = DialogSceneHooks.AlwaysAcceptTouch;
        SaveUnlockItems.Value = TouchSceneHooks.UnlockItems;
        SaveNoDislike.Value = TouchSceneHooks.NoDislike;
        SaveShowNextH.Value = TouchSceneHooks.ShowNextH;
        SaveNoTouchInterruption.Value = TouchSceneHooks.NoTouchInterruption;
    }
}

// ─────────────────────────────────────────────────────────────
//  Settings Panel — partial class LuaConsole
// ─────────────────────────────────────────────────────────────
public partial class LuaConsole
{
    private bool _settingsVisible = false;
    private Rect _settingsRect = new Rect(60, 610, 400, 540);
    private Vector2 _settingsScroll = Vector2.zero;
    private bool _settingsResizing = false;
    private Vector2 _settingsResizeAnchorMouse;
    private Vector2 _settingsResizeAnchorSize;

    private const float MIN_SETTINGS_WIDTH = 360f;
    private const float MIN_SETTINGS_HEIGHT = 400f;

    // Font list (lazy-loaded)
    private string[]? _fontNames;
    private int _fontSelIdx = -1;

    public static void InitConfig(BepInEx.Configuration.ConfigFile cfg) => ConsoleConfig.Init(cfg);

    public static void ApplySavedCheatStates() => ConsoleConfig.ApplySavedCheatStates();

    private void InvalidateStyles()
    {
        _monoFont = null;
        _styleOutput = null;
        _styleInput = null;
        _styleBtn = null;
        _styleBtnActive = null;
        _styleToggleOn = null;
        _styleToggleOff = null;
        _styleRainbow = null;
        _styleWindow = null;
        _styleToggle = null;
        _texCache.Clear();
    }

    private static bool IsLikelyMonospace(string name)
    {
        var n = name.ToLowerInvariant();
        return n.Contains("mono")
            || n.Contains("courier")
            || n.Contains("consola")
            || n.Contains("fixed")
            || n.Contains("inconsolata")
            || n.Contains("hack")
            || n.Contains("menlo")
            || n.Contains("anonymous")
            || n.Contains("jetbrains")
            || n.Contains("envy")
            || n.Contains("source code")
            || n.Contains("nerd font")
            || n.Contains("fira code")
            || n.Contains("cascadia");
    }

    private void EnsureFontList()
    {
        if (_fontNames != null)
            return;
        var all = Font.GetOSInstalledFontNames();
        _fontNames = all.Where(IsLikelyMonospace)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        // Fallback: if no monospace fonts detected, show full list
        if (_fontNames.Length == 0)
            _fontNames = all.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray();
        if (ConsoleConfig.Initialized)
        {
            _fontSelIdx = Array.FindIndex(
                _fontNames,
                f => f.Equals(ConsoleConfig.FontName.Value, StringComparison.OrdinalIgnoreCase)
            );
            if (_fontSelIdx < 0)
                _fontSelIdx = 0;
        }
    }

    // ── Color Slider Helper ───────────────────────────────────
    private Color DrawColorField(string label, Color color)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(50));
        // Swatch
        var prev = GUI.backgroundColor;
        GUI.backgroundColor = color;
        GUILayout.Box("", GUILayout.Width(24), GUILayout.Height(18));
        GUI.backgroundColor = prev;
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("R", GUILayout.Width(14));
        color.r = GUILayout.HorizontalSlider(color.r, 0f, 1f);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("G", GUILayout.Width(14));
        color.g = GUILayout.HorizontalSlider(color.g, 0f, 1f);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("B", GUILayout.Width(14));
        color.b = GUILayout.HorizontalSlider(color.b, 0f, 1f);
        GUILayout.EndHorizontal();

        return color;
    }

    // ── Settings Panel Drawer ─────────────────────────────────
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "CodeQuality",
        "IDE0060:Remove unused parameter",
        Justification = "Required by GUI.Window callback signature"
    )]
    private void DrawSettingsPanel(int id)
    {
        if (!ConsoleConfig.Initialized)
            return;

        GUILayout.Space(6);
        _settingsScroll = GUILayout.BeginScrollView(_settingsScroll);

        // ── Appearance ────────────────────────────────────────
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("~ Appearance");

        // Font selector
        EnsureFontList();
        GUILayout.BeginHorizontal();
        GUILayout.Label("Font:", GUILayout.Width(40));
        if (GUILayout.Button("◀", _styleBtn!, GUILayout.Width(26)))
        {
            _fontSelIdx = (_fontSelIdx - 1 + _fontNames!.Length) % _fontNames.Length;
            ConsoleConfig.FontName.Value = _fontNames[_fontSelIdx];
            InvalidateStyles();
        }
        GUILayout.Label(_fontNames![_fontSelIdx < 0 ? 0 : _fontSelIdx], GUILayout.MinWidth(100));
        if (GUILayout.Button("▶", _styleBtn!, GUILayout.Width(26)))
        {
            _fontSelIdx = (_fontSelIdx + 1) % _fontNames.Length;
            ConsoleConfig.FontName.Value = _fontNames[_fontSelIdx];
            InvalidateStyles();
        }
        GUILayout.EndHorizontal();

        // Font size
        GUILayout.BeginHorizontal();
        GUILayout.Label($"Size: {ConsoleConfig.FontSize.Value}", GUILayout.Width(70));
        int newSize = Mathf.RoundToInt(
            GUILayout.HorizontalSlider(ConsoleConfig.FontSize.Value, 10f, 28f)
        );
        if (newSize != ConsoleConfig.FontSize.Value)
        {
            ConsoleConfig.FontSize.Value = newSize;
            InvalidateStyles();
        }
        GUILayout.EndHorizontal();

        // Background opacity
        GUILayout.BeginHorizontal();
        GUILayout.Label(
            $"Opacity: {ConsoleConfig.BackgroundOpacity.Value:F2}",
            GUILayout.Width(100)
        );
        float newOpacity = GUILayout.HorizontalSlider(
            ConsoleConfig.BackgroundOpacity.Value,
            0.50f,
            1.0f
        );
        if (Mathf.Abs(newOpacity - ConsoleConfig.BackgroundOpacity.Value) > 0.005f)
        {
            ConsoleConfig.BackgroundOpacity.Value = newOpacity;
            InvalidateStyles();
        }
        GUILayout.EndHorizontal();

        // Word wrap
        bool newWrap = GUILayout.Toggle(ConsoleConfig.OutputWordWrap.Value, "  Word Wrap");
        if (newWrap != ConsoleConfig.OutputWordWrap.Value)
        {
            ConsoleConfig.OutputWordWrap.Value = newWrap;
            InvalidateStyles();
        }

        // Accent color — drives output text, active buttons, cheats ON, and title (when rainbow off)
        var accentCol = DrawColorField("Accent:", ConsoleConfig.GetAccentColor());
        if (accentCol != ConsoleConfig.GetAccentColor())
        {
            ConsoleConfig.SetAccentColor(accentCol);
            InvalidateStyles();
        }
        GUILayout.EndVertical();

        GUILayout.Space(4);

        // ── Title Bar ─────────────────────────────────────────
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("~ Title Bar");

        bool newRainbow = GUILayout.Toggle(ConsoleConfig.RainbowEnabled.Value, "  Rainbow Wave");
        if (newRainbow != ConsoleConfig.RainbowEnabled.Value)
            ConsoleConfig.RainbowEnabled.Value = newRainbow;

        bool newMarquee = GUILayout.Toggle(ConsoleConfig.MarqueeEnabled.Value, "  Marquee Scroll");
        if (newMarquee != ConsoleConfig.MarqueeEnabled.Value)
            ConsoleConfig.MarqueeEnabled.Value = newMarquee;

        if (!ConsoleConfig.RainbowEnabled.Value)
            GUILayout.Label("  (Title uses Accent Color when rainbow is off)", GUI.skin.label);
        GUILayout.EndVertical();

        GUILayout.Space(4);

        // ── Behavior ──────────────────────────────────────────
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("~ Behavior");

        GUILayout.BeginHorizontal();
        GUILayout.Label(
            $"Buffer: {ConsoleConfig.OutputBufferSize.Value / 1000}k",
            GUILayout.Width(90)
        );
        int newBuf = Mathf.RoundToInt(
            GUILayout.HorizontalSlider(ConsoleConfig.OutputBufferSize.Value, 4000f, 128000f)
        );
        newBuf = (newBuf / 1000) * 1000; // snap to 1k
        if (newBuf != ConsoleConfig.OutputBufferSize.Value)
            ConsoleConfig.OutputBufferSize.Value = newBuf;
        GUILayout.EndHorizontal();

        ConsoleConfig.AutoScrollOutput.Value = GUILayout.Toggle(
            ConsoleConfig.AutoScrollOutput.Value,
            "  Auto-scroll Output"
        );
        GUILayout.EndVertical();

        GUILayout.Space(4);

        // ── Auto-Restore Cheats ───────────────────────────────
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("~ Auto-Restore Cheats");

        bool newAutoRestore = GUILayout.Toggle(
            ConsoleConfig.AutoRestoreCheats.Value,
            "  Remember enabled cheats"
        );
        if (newAutoRestore != ConsoleConfig.AutoRestoreCheats.Value)
        {
            ConsoleConfig.AutoRestoreCheats.Value = newAutoRestore;
            if (newAutoRestore)
                ConsoleConfig.SaveCheatStates();
        }

        if (ConsoleConfig.AutoRestoreCheats.Value)
            GUILayout.Label("  Cheat states are saved automatically.", GUI.skin.label);
        GUILayout.EndVertical();

        GUILayout.Space(8);

        // ── Reset ─────────────────────────────────────────────
        if (GUILayout.Button("Reset to Defaults", _styleBtn!))
        {
            ConsoleConfig.FontName.Value = (string)ConsoleConfig.FontName.DefaultValue;
            ConsoleConfig.FontSize.Value = (int)ConsoleConfig.FontSize.DefaultValue;
            ConsoleConfig.BackgroundOpacity.Value = (float)
                ConsoleConfig.BackgroundOpacity.DefaultValue;
            ConsoleConfig.AccentColor.Value = (string)ConsoleConfig.AccentColor.DefaultValue;
            ConsoleConfig.RainbowEnabled.Value = (bool)ConsoleConfig.RainbowEnabled.DefaultValue;
            ConsoleConfig.MarqueeEnabled.Value = (bool)ConsoleConfig.MarqueeEnabled.DefaultValue;
            ConsoleConfig.OutputBufferSize.Value = (int)ConsoleConfig.OutputBufferSize.DefaultValue;
            ConsoleConfig.OutputWordWrap.Value = (bool)ConsoleConfig.OutputWordWrap.DefaultValue;
            ConsoleConfig.AutoScrollOutput.Value = (bool)
                ConsoleConfig.AutoScrollOutput.DefaultValue;
            _fontSelIdx = Array.FindIndex(
                _fontNames!,
                f => f.Equals("Cascadia Mono", StringComparison.OrdinalIgnoreCase)
            );
            if (_fontSelIdx < 0)
                _fontSelIdx = 0;
            InvalidateStyles();
        }

        GUILayout.EndScrollView();

        DrawResizeGripHandle(_settingsRect);
        GUI.DragWindow(new Rect(0, 0, _settingsRect.width, 20));
    }
}
