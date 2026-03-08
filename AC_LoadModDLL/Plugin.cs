using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AC;
using AC.Scene.Touch;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime;
using UnityEngine;
using XLua;

namespace AiComi_LuaMod;

[BepInPlugin("aicomi.luamod", "AiComi LuaMod", "1.0.0")]
public class Plugin : BasePlugin
{
    internal static new ManualLogSource Log = null!;

    public override void Load()
    {
        Log = base.Log;
        Harmony.CreateAndPatchAll(typeof(LuaPatch));
        Harmony.CreateAndPatchAll(typeof(CheatHooks));
        Harmony.CreateAndPatchAll(typeof(TouchMonitorHooks));
        Harmony.CreateAndPatchAll(typeof(TouchSceneAutoApply));
        Log.LogWarning("AiComi LuaMod loaded -- waiting for BuildConditionsFromLua...");
    }
}

// ─────────────────────────────────────────────────────────────
//  Harmony: LuaEnv abgreifen
// ─────────────────────────────────────────────────────────────
[HarmonyPatch(typeof(ParameterContainer))]
internal static class LuaPatch
{
    [HarmonyPostfix, HarmonyPatch(nameof(ParameterContainer.BuildConditionsFromLua))]
    private static void AfterBuildConditions(ParameterContainer __instance)
    {
        var luaEnv = __instance._luaEnv;
        if (luaEnv is null)
        {
            Plugin.Log.LogError("LuaEnv ist null!");
            return;
        }

        Plugin.Log.LogWarning("LuaEnv gefunden! Initialisiere Mod + Console...");
        LuaConsole.Initialize(luaEnv);
        var modsPath =
            Path.Combine(Paths.PluginPath, "lua_scripts", "mods").Replace("\\", "/") + "/";
        luaEnv.Global.Set("MOD_PATH", modsPath);

        try
        {
            var path = Path.Combine(BepInEx.Paths.PluginPath, "AiComi_LuaMod.lua");
            if (File.Exists(path))
            {
                luaEnv.DoString(File.ReadAllText(path), "AiComi_LuaMod");
                Plugin.Log.LogWarning($"Lua-Mod loaded from: {path}");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"Fehler beim Lua-Inject: {ex}");
        }
    }
}

// ─────────────────────────────────────────────────────────────
//  Harmony: Cheat Hooks
// ─────────────────────────────────────────────────────────────
internal static class CheatHooks
{
    // ── No Favor Loss: negative Favor-Änderungen blockieren ──
    //    _b__105_0 ist der Favor-Adder Lambda aus SetupParameterModification
    public static bool NoFavorLoss = false;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(AC.User.NPCData), "_SetupParameterModification_b__105_0")]
    private static void NoFavorLossHook(ref int add)
    {
        if (NoFavorLoss && add < 0)
            add = 0;
    }

    // ── No Mood Loss: Mood sinkt nie ──────────────────────────
    //    _b__105_1 ist der Mood-Adder Lambda aus SetupParameterModification
    public static bool NoMoodLoss = false;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(AC.User.NPCData), "_SetupParameterModification_b__105_1")]
    private static void NoMoodLossHook(ref int add)
    {
        if (NoMoodLoss && add < 0)
            add = 0;
    }

    // ── No Dislike (Touch Scene): Miss-Gauge steigt nie ──────
    public static bool NoDislike = false;

    // ── Show Next H Button von Anfang an ──────────────────────
    public static bool ShowNextH = false;

    // ── Force Positive Choice: alle Dialog-Antworten lösen positiv aus ──
    public static bool ForcePositiveChoice = false;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ILLGAMES.ADV.Commands.Base.Choice), "_Do_b__6_1")]
    private static void ForcePositiveOnClick(ILLGAMES.ADV.Choice choice)
    {
        if (!ForcePositiveChoice || choice == null)
            return;

        var tag = choice.TagToJump;
        if (string.IsNullOrEmpty(tag))
            return;

        bool isNegative = tag.StartsWith("Bad") || tag == "はずれ";
        if (!isNegative)
            return;

        try
        {
            var rt = choice._rectTransform;
            if (rt == null)
                return;
            var parent = rt.parent;
            if (parent == null)
                return;

            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child == rt)
                    continue;

                var sibling = child.gameObject.GetComponent<ILLGAMES.ADV.Choice>();
                if (sibling == null)
                    continue;

                var siblingTag = sibling.TagToJump;
                if (
                    !string.IsNullOrEmpty(siblingTag)
                    && !siblingTag.StartsWith("Bad")
                    && siblingTag != "はずれ"
                )
                {
                    Plugin.Log.LogInfo($"ForcePositive: swapped '{tag}' → '{siblingTag}'");
                    choice.TagToJump = siblingTag;
                    if (choice._outPositiveState != null)
                        choice._outNegativeState = choice._outPositiveState;
                    return;
                }
            }
            Plugin.Log.LogWarning($"ForcePositive: no positive sibling found for '{tag}'");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"ForcePositive: {ex.Message}");
        }
    }

    // ── Lua-Code für Touch-Cheats ─────────────────────────────
    internal const string LuaNoDislike =
        @"
        local tc = CS.UnityEngine.Object.FindObjectOfType(typeof(CS.AC.Scene.Touch.TouchController))
        if tc == nil then print('[NoDislike] TouchController nicht gefunden') return end
        xlua.private_accessible(typeof(CS.AC.Scene.Touch.TouchController))
        local s = tc._sliderMiss
        s.maxValue = 0  s.minValue = 0
        local sd = tc._systemData
        sd.CharaProbability = 100
        local bp = sd.BaseProbability
        for i=0,bp.Length-1 do bp[i]=100 end
        local pp = sd.PartsProbability
        for i=0,pp.Length-1 do
            local r=pp[i] for j=0,r.Length-1 do r[j]=100 end
        end
        print('[NoDislike] Werte gesetzt!')
    ";

    internal const string LuaShowNextH =
        @"
        local all = CS.UnityEngine.Resources.FindObjectsOfTypeAll(typeof(CS.UnityEngine.GameObject))
        for i=0,all.Length-1 do
            local go = all[i]
            if go.name == 'Button Next H' and not go.activeSelf then
                go:SetActive(true)
                print('[ShowNextH] Button aktiviert!')
                return
            end
        end
        print('[ShowNextH] Button nicht gefunden')
    ";
}

// ─────────────────────────────────────────────────────────────
//  TouchScene Auto-Apply: aktive Cheats beim Start automatisch anwenden
// ─────────────────────────────────────────────────────────────
[HarmonyPatch(typeof(TouchController), nameof(TouchController.AddMissGauge))]
public static class TouchSceneAutoApply
{
    private static bool _applied = false;
    private static IntPtr _lastTcPtr = IntPtr.Zero;

    [HarmonyPostfix]
    private static void OnAddMissGauge(TouchController __instance)
    {
        // Neue TouchController-Instanz = neue Touch Scene → _applied zurücksetzen
        var ptr = Il2CppInterop.Runtime.IL2CPP.Il2CppObjectBaseToPtr(__instance);
        if (ptr != _lastTcPtr)
        {
            _lastTcPtr = ptr;
            _applied = false;
            Plugin.Log.LogInfo("[TouchScene] Neue Instanz erkannt – Reset");
        }

        if (_applied)
            return;
        _applied = true;
        Plugin.Log.LogInfo("[TouchScene] Applying active cheats...");
        if (CheatHooks.NoDislike)
        {
            LuaConsole.RunLuaStatic(CheatHooks.LuaNoDislike);
            ApplyUnlockCursors();
        }
        if (CheatHooks.ShowNextH)
            ApplyShowNextH(__instance);
    }

    internal static void ApplyShowNextH(TouchController? tc = null)
    {
        if (tc == null)
            tc = UnityEngine
                .Object.FindObjectOfType(Il2CppType.Of<TouchController>())
                ?.TryCast<TouchController>();
        if (tc == null)
        {
            Plugin.Log.LogWarning("[ShowNextH] TouchController nicht gefunden");
            return;
        }
        try
        {
            var btn = tc._buttonH;
            if (btn != null && !btn.gameObject.activeSelf)
            {
                btn.gameObject.SetActive(true);
                Plugin.Log.LogInfo("[ShowNextH] _buttonH aktiviert");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning("[ShowNextH] " + ex.Message);
        }
    }

    private static void ApplyUnlockCursors()
    {
        var gcType = Il2CppType.Of<ILLGAMES.Unity.Component.GameCursor>();
        var all = Resources.FindObjectsOfTypeAll(gcType);
        if (all.Length < 2) return;

        var gcExplore = all[0].TryCast<ILLGAMES.Unity.Component.GameCursor>();
        var gcTouch   = all[1].TryCast<ILLGAMES.Unity.Component.GameCursor>();
        if (gcExplore == null || gcTouch == null) return;

        gcTouch._anameTex = gcExplore._anameTex;
        gcTouch.UpdateCursorLock();
        Plugin.Log.LogInfo("[UnlockCursors] Applied");
    }

    public static void Reset() => _applied = false;
}

// ─────────────────────────────────────────────────────────────
//  Touch Monitor Hooks
//  Captures private TouchController fields into public statics
//  so Lua can read them via CS.AiComi_LuaMod.TouchMonitorHooks
// ─────────────────────────────────────────────────────────────
[HarmonyPatch]
public static class TouchMonitorHooks
{
    // ── Snapshot metadata ─────────────────────────────────────
    public static bool Captured { get; private set; }
    public static int SnapCount { get; private set; }

    // ── Gauges ────────────────────────────────────────────────
    public static float MissGauge { get; private set; } // 0..1, -1=unavailable
    public static float PleasureGauge { get; private set; } // 0..1, -1=unavailable
    public static bool MissOver { get; private set; }
    public static float GaugeUpSpeed { get; private set; }
    public static float GaugeDownSpeed { get; private set; }

    // ── Touch state ───────────────────────────────────────────
    public static bool IsHit { get; private set; }
    public static bool IsDrag { get; private set; }
    public static bool IsKiss { get; private set; }
    public static int NowHitParts { get; private set; } // PartsKind int
    public static int NoTouch { get; private set; } // NoTouchKind int
    public static int MainPoint { get; private set; }

    // ── Motion ────────────────────────────────────────────────
    public static float MotionParam { get; private set; }
    public static float SpeedBody { get; private set; }
    public static float SpeedHand { get; private set; }

    // ── Correction values ─────────────────────────────────────
    public static float Correction0 { get; private set; }
    public static float Correction1 { get; private set; }
    public static float Correction2 { get; private set; }
    public static float Correction3 { get; private set; }

    // ── Other ─────────────────────────────────────────────────
    public static bool IsOrgasm { get; private set; }
    public static int OrgasmCount { get; private set; }

    // ── Last AddMissGauge call ────────────────────────────────
    public static int LastMissArea { get; private set; } = -99;
    public static bool LastMissHit { get; private set; }

    // ── Arrays as pipe-separated strings ─────────────────────
    // AreaKind values: None=-1, BigSuccess=0, Success=1, Failure=2, BigFailure=3
    public static string AreaKindStr { get; private set; } = "";
    public static string AreaProbStr { get; private set; } = "";
    public static string TouchedAreaStr { get; private set; } = "";

    // ── Capture ───────────────────────────────────────────────
    private static void CaptureState(TouchController tc)
    {
        if (tc == null)
            return;
        Captured = true;
        MissGauge = tc._sliderMiss != null ? tc._sliderMiss.value : -1f;
        PleasureGauge = tc._sliderF != null ? tc._sliderF.value : -1f;
        MissOver = tc._missOver;
        GaugeUpSpeed = tc._gaugeUpSpeed;
        GaugeDownSpeed = tc._gaugeDownSpeed;
        IsHit = tc._isHit;
        IsDrag = tc._isDrag;
        IsKiss = tc._isKiss;
        NowHitParts = (int)tc._nowHitParts;
        NoTouch = (int)tc._noTouch;
        MainPoint = tc._mainPoint;
        MotionParam = tc._motionParam;
        SpeedBody = tc._speedBodyParam;
        SpeedHand = tc._speedHandParam;
        Correction0 = tc._correctionValue0;
        Correction1 = tc._correctionValue1;
        Correction2 = tc._correctionValue2;
        Correction3 = tc._correctionValue3;
        IsOrgasm = tc._isOrgasm;
        OrgasmCount = tc._orgasmCount;

        var ak = tc._areaKind;
        if (ak != null)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < ak.Length; i++)
            {
                if (i > 0)
                    sb.Append('|');
                sb.Append((int)ak[i]);
            }
            AreaKindStr = sb.ToString();
        }
        var ap = tc._areaProbability;
        if (ap != null)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < ap.Length; i++)
            {
                if (i > 0)
                    sb.Append('|');
                sb.Append(ap[i]);
            }
            AreaProbStr = sb.ToString();
        }
        var ta = tc._touchedArea;
        if (ta != null)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < ta.Length; i++)
            {
                if (i > 0)
                    sb.Append('|');
                sb.Append(ta[i] ? 'T' : '.');
            }
            TouchedAreaStr = sb.ToString();
        }
        SnapCount++;
    }

    // ── Patches ───────────────────────────────────────────────
    [HarmonyPostfix, HarmonyPatch(typeof(TouchController), nameof(TouchController.AddMissGauge))]
    private static void OnAddMiss(TouchController __instance, int area, bool __result)
    {
        LastMissArea = area;
        LastMissHit = __result;
        CaptureState(__instance);
    }

    [HarmonyPostfix, HarmonyPatch(typeof(TouchController), nameof(TouchController.SubMisssGauge))]
    private static void OnSubMiss(TouchController __instance) => CaptureState(__instance);

    [HarmonyPostfix, HarmonyPatch(typeof(TouchController), nameof(TouchController.TouchPart))]
    private static void OnTouchPart(TouchController __instance) => CaptureState(__instance);

    // Breiterer Fallback: Update() fangen
    [HarmonyPostfix, HarmonyPatch(typeof(TouchController), "Update")]
    private static void OnUpdate(TouchController __instance) => CaptureState(__instance);

    // Manuell aus Lua aufrufbar
    public static void ForceCapture()
    {
        var tc = UnityEngine.Object.FindObjectOfType<TouchController>();
        if (tc != null)
            CaptureState(tc);
        else
            Plugin.Log.LogWarning("[TouchMonitor] ForceCapture: TouchController not found");
    }
}

// ─────────────────────────────────────────────────────────────
//  In-Game Lua Console + Cheat Panel  (F9)
// ─────────────────────────────────────────────────────────────
public class LuaConsole : MonoBehaviour
{
    public static void Initialize(LuaEnv env)
    {
        if (_instance != null)
        {
            _instance._luaEnv = env;
            return;
        }
        Il2CppInterop.Runtime.Injection.ClassInjector.RegisterTypeInIl2Cpp<LuaConsole>();
        var go = new GameObject("AiComi_LuaConsole");
        UnityEngine.Object.DontDestroyOnLoad(go);
        _instance = go.AddComponent<LuaConsole>();
        _instance._luaEnv = env;
        Plugin.Log.LogWarning("Lua Console ready -- press F9 to open");
    }

    // ── State ─────────────────────────────────────────────────
    private static LuaConsole? _instance;
    private LuaEnv? _luaEnv;
    private bool _consoleVisible = false;
    private bool _cheatVisible = false;

    // Console
    private string _input = "";
    private string _output = "";
    private Vector2 _outputScroll = Vector2.zero;
    private Rect _consoleRect = new Rect(60, 40, 750, 560);
    private readonly List<string> _history = new();
    private int _historyIdx = -1;

    // Cheat panel
    private Rect _cheatRect = new Rect(810, 80, 280, 420);

    // Styles – jeden Frame neu zuweisen (billig); Texturen permanent cachen (teuer)
    private GUIStyle? _styleOutput,
        _styleInput,
        _styleBtn,
        _styleBtnActive,
        _styleWindow,
        _styleToggle;

    // Texturen permanent cachen – überleben GC und Skin-Reset
    private readonly Dictionary<int, Texture2D> _texCache = new();

    public LuaConsole(IntPtr ptr)
        : base(ptr) { }

    // ── Unity ─────────────────────────────────────────────────
    void OnApplicationFocus(bool focus)
    {
        if (focus)
            _styleRainbow = null;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F9))
        {
            _consoleVisible = !_consoleVisible;
            if (!_consoleVisible)
                _cheatVisible = false;
        }
    }

    void OnGUI()
    {
        if (!_consoleVisible && !_cheatVisible)
            return;
        InitStyles();

        var bgTex = Tex(new Color(0.13f, 0.13f, 0.14f, 0.97f));

        if (_consoleVisible)
        {
            GUI.DrawTexture(_consoleRect, bgTex);
            _consoleRect = GUI.Window(
                42424,
                _consoleRect,
                (GUI.WindowFunction)DrawConsole,
                "",
                _styleWindow!
            );
        }

        if (_cheatVisible)
        {
            _cheatRect = new Rect(
                _consoleRect.x + _consoleRect.width + 6f,
                _consoleRect.y,
                _cheatRect.width,
                _cheatRect.height
            );
            GUI.DrawTexture(_cheatRect, bgTex);
            _cheatRect = GUI.Window(
                42425,
                _cheatRect,
                (GUI.WindowFunction)DrawCheatPanel,
                "  Cheat Panel",
                _styleWindow!
            );
        }
    }

    // ── Console Window ────────────────────────────────────────
    private void DrawConsole(int id)
    {
        // Rainbow Titel – feste Höhe reservieren damit nichts überlappt
        GUILayout.Space(26f);
        DrawRainbowTitle(new Rect(0, 2f, _consoleRect.width, 22f), "  ★ AiComi Lua Console  [F9]");

        const float TOOLBAR_H = 28f;
        const float LABEL_H = 18f;
        const float INPUT_H = 100f;
        const float PADDING = 4f;
        float outputH =
            _consoleRect.height - 26f - TOOLBAR_H - INPUT_H - LABEL_H * 2 - PADDING * 6 - 20f;
        if (outputH < 60f)
            outputH = 60f;

        // Toolbar
        GUILayout.BeginHorizontal(GUILayout.Height(TOOLBAR_H));
        if (GUILayout.Button("▶  Run", _styleBtn!, GUILayout.Width(80)))
            Execute();
        if (GUILayout.Button("Clear", _styleBtn!, GUILayout.Width(70)))
            _output = "";
        if (GUILayout.Button("↑", _styleBtn!, GUILayout.Width(30)))
            HistoryUp();
        if (GUILayout.Button("↓", _styleBtn!, GUILayout.Width(30)))
            HistoryDown();
        GUILayout.FlexibleSpace();
        var cheatLabel = _cheatVisible ? "⚡ Cheats  ✓" : "⚡ Cheats";
        var cheatStyle = _cheatVisible ? _styleBtnActive! : _styleBtn!;
        if (GUILayout.Button(cheatLabel, cheatStyle, GUILayout.Width(100)))
            _cheatVisible = !_cheatVisible;
        GUILayout.EndHorizontal();

        // Output
        GUILayout.Label("Output:", GUILayout.Height(LABEL_H));
        _outputScroll = GUILayout.BeginScrollView(_outputScroll, GUILayout.Height(outputH));
        GUILayout.TextArea(_output, _styleOutput!, GUILayout.ExpandHeight(true));
        GUILayout.EndScrollView();

        // Input
        GUILayout.Label("Lua:  (Ctrl+Enter = Run)", GUILayout.Height(LABEL_H));
        GUI.SetNextControlName("LuaInput");
        _input = GUILayout.TextArea(_input, _styleInput!, GUILayout.Height(INPUT_H));

        var e = Event.current;
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Return && e.control)
        {
            Execute();
            e.Use();
        }

        GUI.DragWindow(new Rect(0, 0, _consoleRect.width, 20));
    }

    // ── Cheat Panel ───────────────────────────────────────────
    private GUIStyle? _styleToggleOn,
        _styleToggleOff;

    private bool DrawToggle(bool value, string label)
    {
        if (_styleToggleOn == null)
        {
            _styleToggleOn = new GUIStyle(_styleBtn!)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
            };
            _styleToggleOn.normal.background = Tex(new Color(0.10f, 0.28f, 0.10f, 1f));
            _styleToggleOn.hover.background = Tex(new Color(0.13f, 0.35f, 0.13f, 1f));
            _styleToggleOn.normal.textColor = new Color(0.4f, 1f, 0.4f);
            _styleToggleOn.hover.textColor = new Color(0.5f, 1f, 0.5f);

            _styleToggleOff = new GUIStyle(_styleBtn!)
            {
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
            };
            _styleToggleOff.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
            _styleToggleOff.hover.textColor = new Color(0.85f, 0.85f, 0.85f);
        }

        var style = value ? _styleToggleOn! : _styleToggleOff!;
        var symbol = value ? "<ON> " : "[OFF]";
        if (GUILayout.Button($"{symbol}  {label}", style))
            return !value;
        return value;
    }

    private void DrawCheatPanel(int id)
    {
        GUILayout.Space(4);

        // ── Dialog & Reactions ──
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("~ Dialog & Reactions", GUI.skin.label);
        CheatHooks.NoFavorLoss = DrawToggle(CheatHooks.NoFavorLoss, "No Favor Loss");
        CheatHooks.NoMoodLoss = DrawToggle(CheatHooks.NoMoodLoss, "No Mood Loss");
        CheatHooks.ForcePositiveChoice = DrawToggle(
            CheatHooks.ForcePositiveChoice,
            "Force Positive Choice"
        );
        GUILayout.EndVertical();

        GUILayout.Space(5);

        // ── Touch Scene ──
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("~ Touch Scene", GUI.skin.label);

        // No Dislike: beim Aktivieren Lua-Code ausführen
        var prevNoDislike = CheatHooks.NoDislike;
        CheatHooks.NoDislike = DrawToggle(CheatHooks.NoDislike, "No Dislike");
        if (CheatHooks.NoDislike && !prevNoDislike)
        {
            TouchSceneAutoApply.Reset();
            RunLua(CheatHooks.LuaNoDislike);
        }

        var prevShowNextH = CheatHooks.ShowNextH;
        CheatHooks.ShowNextH = DrawToggle(CheatHooks.ShowNextH, "Show Next H");
        if (CheatHooks.ShowNextH && !prevShowNextH)
        {
            TouchSceneAutoApply.Reset();
            TouchSceneAutoApply.ApplyShowNextH();
        }

        GUILayout.EndVertical();

        GUILayout.Space(5);

        // ── Quick Actions ──
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("~ Quick Actions", GUI.skin.label);

        if (_eventSnapshot != null)
            GUILayout.Label($"  [*] Snapshot: {_eventSnapshot.Count} events saved", GUI.skin.label);
        else
            GUILayout.Label("  [ ] No snapshot", GUI.skin.label);

        if (GUILayout.Button("  [+] Unlock All Events", _styleBtn!))
        {
            TakeEventSnapshot();
            RunLua(
                @"
                local em = CS.AC.Lua.EventTable.EventMemory
                for i=0,6 do em:Add(i) end
                em:Add(300)
                for i=30,69 do em:Add(i) end
                for i=100,110 do em:Add(i) end
                for i=200,206 do em:Add(i) end
                for i=208,218 do em:Add(i) end
                for i=221,223 do em:Add(i) end
                em:Add(230) em:Add(231) em:Add(232) em:Add(240)
                print('All events unlocked! Count: ' .. em.Count)
            "
            );
        }

        GUI.enabled = _eventSnapshot != null;
        if (GUILayout.Button("  [-] Restore Snapshot", _styleBtn!))
            RestoreEventSnapshot();
        GUI.enabled = true;

        if (GUILayout.Button("  [?] Dump EventMemory", _styleBtn!))
            RunLua(
                @"
                local em = CS.AC.Lua.EventTable.EventMemory
                print('EventMemory (' .. em.Count .. '):')
                local e = em:GetEnumerator()
                while e:MoveNext() do print('  ID: ' .. tostring(e.Current)) end
            "
            );

        GUILayout.EndVertical();

        GUI.DragWindow(new Rect(0, 0, _cheatRect.width, 20));
    }

    // ── Event Snapshot ────────────────────────────────────────
    private HashSet<int>? _eventSnapshot;

    private void TakeEventSnapshot()
    {
        if (_luaEnv is null)
            return;
        _eventSnapshot = new HashSet<int>();
        // Snapshot via Lua auslesen
        var results = _luaEnv.DoString(
            @"
            local snap = {}
            local em = CS.AC.Lua.EventTable.EventMemory
            local e = em:GetEnumerator()
            while e:MoveNext() do
                snap[#snap+1] = e.Current
            end
            return table.unpack(snap)
        ",
            "snapshot"
        );
        if (results != null)
            foreach (var r in results)
                if (r != null)
                    try
                    {
                        _eventSnapshot.Add(Convert.ToInt32(r));
                    }
                    catch { }
        AppendOutput($"[Snapshot] {_eventSnapshot.Count} events saved\n");
    }

    private void RestoreEventSnapshot()
    {
        if (_luaEnv is null || _eventSnapshot == null)
            return;
        var ids = string.Join(",", _eventSnapshot);
        RunLua(
            $@"
            local em = CS.AC.Lua.EventTable.EventMemory
            em:Clear()
            for _, id in ipairs({{{ids}}}) do
                em:Add(id)
            end
            print('Events wiederhergestellt: ' .. em.Count)
        "
        );
    }

    // ── Execute ───────────────────────────────────────────────
    private void Execute()
    {
        var code = _input.Trim();
        if (string.IsNullOrEmpty(code))
            return;
        _input = "";
        if (_history.Count == 0 || _history[^1] != code)
            _history.Add(code);
        _historyIdx = -1;
        RunLua(code, showInput: true);
    }

    private void RunLua(string code, bool showInput = false) => RunLuaStatic(code, showInput);

    public static void RunLuaStatic(string code, bool showInput = false)
    {
        if (_instance?._luaEnv is null)
        {
            Plugin.Log.LogWarning("[LuaConsole] RunLuaStatic: LuaEnv not available");
            return;
        }
        _instance.RunLuaInternal(code, showInput);
    }

    private void RunLuaInternal(string code, bool showInput = false)
    {
        if (_luaEnv is null)
        {
            AppendOutput("[ERROR] LuaEnv not available!\n");
            return;
        }
        if (showInput)
            AppendOutput($"> {code.Trim()}\n");

        var sb = new StringBuilder();

        // print() über Lua-Seite umleiten – kein generisches Get<T> nötig
        _luaEnv.DoString(
            @"
            __orig_print = print
            print = function(...)
                local args = {...}
                local parts = {}
                for i=1,#args do parts[i] = tostring(args[i]) end
                __console_output = (__console_output or '') .. table.concat(parts, '\t') .. '\n'
            end
        ",
            "print_redirect"
        );
        _luaEnv.DoString("__console_output = ''", "clear_output");

        try
        {
            var results = _luaEnv.DoString(code, "console");
            if (results != null && results.Length > 0)
                sb.AppendLine(
                    "=> "
                        + string.Join(
                            ", ",
                            Array.ConvertAll<object, string>(results, r => r?.ToString() ?? "nil")
                        )
                );
        }
        catch (Exception ex)
        {
            sb.AppendLine($"[ERROR] {ex.Message}");
        }
        finally
        {
            // Output einsammeln und print() wiederherstellen
            try
            {
                var output = _luaEnv.DoString("return __console_output or ''", "get_output");
                if (output != null && output.Length > 0 && output[0] != null)
                {
                    var s = output[0].ToString();
                    if (s.Length > 0)
                        sb.Insert(0, s);
                }
            }
            catch { }
            _luaEnv.DoString(
                "print = __orig_print  __orig_print = nil  __console_output = nil",
                "restore_print"
            );
        }

        if (sb.Length > 0)
            AppendOutput(sb.ToString());
        _outputScroll = new Vector2(0, float.MaxValue);
    }

    // ── History ───────────────────────────────────────────────
    private void HistoryUp()
    {
        if (_history.Count == 0)
            return;
        _historyIdx = _historyIdx < 0 ? _history.Count - 1 : Math.Max(0, _historyIdx - 1);
        _input = _history[_historyIdx];
    }

    private void HistoryDown()
    {
        if (_historyIdx < 0)
            return;
        _historyIdx++;
        if (_historyIdx >= _history.Count)
        {
            _historyIdx = -1;
            _input = "";
        }
        else
            _input = _history[_historyIdx];
    }

    // ── Marquee / LED Laufschrift ─────────────────────────────
    private GUIStyle? _styleRainbow;
    private float _marqueeOffset = 0f;
    private const float MARQUEE_SPEED = 55f;

    private void DrawRainbowTitle(Rect area, string text)
    {
        if (_styleRainbow == null)
        {
            _styleRainbow = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
            };
        }

        float charW = _styleRainbow.CalcSize(new GUIContent("W")).x;
        float totalW = charW * text.Length;

        // Offset weiterbewegen: von rechts rein, nach links raus, dann wieder von rechts
        _marqueeOffset += MARQUEE_SPEED * Time.deltaTime;
        if (_marqueeOffset > totalW + area.width)
            _marqueeOffset = 0f;

        float speed = 0.4f;
        float wave = 0.03f;
        float startX = area.width - _marqueeOffset;

        GUI.BeginGroup(new Rect(area.x, area.y, area.width, area.height));
        for (int i = 0; i < text.Length; i++)
        {
            float xPos = startX + i * charW;
            if (xPos > -charW && xPos < area.width)
            {
                float hue = ((i * wave - Time.realtimeSinceStartup * speed) % 1f);
                if (hue < 0)
                    hue += 1f;
                _styleRainbow.normal.textColor = Color.HSVToRGB(hue, 1f, 1f);
                GUI.Label(
                    new Rect(xPos, 2f, charW + 2f, area.height - 2f),
                    text[i].ToString(),
                    _styleRainbow
                );
            }
        }
        GUI.EndGroup();
        _styleRainbow.normal.textColor = Color.white;
    }

    // ── Helpers ───────────────────────────────────────────────
    private void AppendOutput(string text)
    {
        _output += text;
        if (_output.Length > 32000)
            _output = _output[(_output.Length - 32000)..];
    }

    // Textur aus Cache holen oder neu erstellen – überlebt GC dauerhaft
    private Texture2D Tex(Color col)
    {
        int key = col.GetHashCode();
        if (_texCache.TryGetValue(key, out var existing) && existing != null)
            return existing;
        var t = new Texture2D(1, 1);
        t.SetPixel(0, 0, col);
        t.Apply();
        t.hideFlags = HideFlags.HideAndDontSave;
        UnityEngine.Object.DontDestroyOnLoad(t);
        _texCache[key] = t;
        return t;
    }

    // Styles werden jeden Frame neu zugewiesen – Texturen kommen aus Cache
    private void InitStyles()
    {
        var bg = new Color(0.13f, 0.13f, 0.14f, 0.97f);
        var bgInput = new Color(0.10f, 0.10f, 0.11f, 1f);
        var bgBtn = new Color(0.22f, 0.22f, 0.25f, 1f);
        var bgBtnHov = new Color(0.30f, 0.30f, 0.35f, 1f);
        var textMain = new Color(0.92f, 0.92f, 0.92f);
        var textGreen = new Color(0.4f, 1f, 0.4f);
        var accent = new Color(0.25f, 0.25f, 0.30f, 1f);

        _styleWindow = new GUIStyle(GUI.skin.window) { fontSize = 17, fontStyle = FontStyle.Bold };
        _styleWindow.normal.background = null;
        _styleWindow.onNormal.background = null;
        _styleWindow.normal.textColor = new Color(0, 0, 0, 0);

        _styleOutput = new GUIStyle(GUI.skin.textArea)
        {
            fontSize = 16,
            wordWrap = true,
            richText = false,
        };
        _styleOutput.normal.background = Tex(bgInput);
        _styleOutput.focused.background = Tex(bgInput);
        _styleOutput.normal.textColor = textMain;
        _styleOutput.focused.textColor = textMain;

        _styleInput = new GUIStyle(GUI.skin.textArea) { fontSize = 17, wordWrap = true };
        _styleInput.normal.background = Tex(bgInput);
        _styleInput.focused.background = Tex(new Color(0.12f, 0.12f, 0.16f, 1f));
        _styleInput.normal.textColor = Color.white;
        _styleInput.focused.textColor = Color.white;

        _styleBtn = new GUIStyle(GUI.skin.button)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleCenter,
        };
        _styleBtn.normal.background = Tex(bgBtn);
        _styleBtn.hover.background = Tex(bgBtnHov);
        _styleBtn.active.background = Tex(accent);
        _styleBtn.normal.textColor = textMain;
        _styleBtn.hover.textColor = Color.white;

        _styleBtnActive = new GUIStyle(_styleBtn) { fontStyle = FontStyle.Bold };
        _styleBtnActive.normal.background = Tex(new Color(0.15f, 0.35f, 0.15f, 1f));
        _styleBtnActive.hover.background = Tex(new Color(0.18f, 0.42f, 0.18f, 1f));
        _styleBtnActive.normal.textColor = textGreen;
        _styleBtnActive.hover.textColor = textGreen;

        _styleToggle = new GUIStyle(GUI.skin.toggle) { fontSize = 16 };
        _styleToggle.normal.textColor = textMain;
        _styleToggle.hover.textColor = Color.white;
    }
}
