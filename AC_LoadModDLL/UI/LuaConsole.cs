using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using XLua;

namespace AiComi_LuaMod;

// ─────────────────────────────────────────────────────────────
//  In-Game Lua Console + Cheat Panel  (F9)
// ─────────────────────────────────────────────────────────────
public partial class LuaConsole : MonoBehaviour
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
    private string _searchTerm = "";
    private int _searchMatchCount = 0;
    private Vector2 _outputScroll = Vector2.zero;
    private Vector2 _inputScroll = Vector2.zero;
    private Rect _consoleRect = new Rect(60, 40, 750, 560);
    private readonly List<string> _history = new();
    private int _historyIdx = -1;

    private const float RESIZE_GRIP_SIZE = 14f;
    private const float MIN_CONSOLE_WIDTH = 600f;
    private const float MIN_CONSOLE_HEIGHT = 420f;
    private const float MIN_CHEAT_WIDTH = 300f;
    private const float MIN_CHEAT_HEIGHT = 330f;

    private bool _consoleResizing = false;
    private bool _cheatResizing = false;

    // Anchor values recorded at drag-start; resize is applied as a delta so the
    // window size never jumps when the user first clicks the grip.
    private Vector2 _consoleResizeAnchorMouse;
    private Vector2 _consoleResizeAnchorSize;
    private Vector2 _cheatResizeAnchorMouse;
    private Vector2 _cheatResizeAnchorSize;
    private bool _hasStoredCursorState = false;
    private CursorLockMode _storedCursorLockMode = CursorLockMode.None;
    private bool _storedCursorVisible = true;

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
            {
                _cheatVisible = false;
                _settingsVisible = false;
            }
        }

        if (_consoleVisible || _cheatVisible || _settingsVisible)
            EnsureCursorUnlocked();
        else
            RestoreCursorState();
    }

    void OnDestroy()
    {
        RestoreCursorState();
    }

    void OnGUI()
    {
        if (!_consoleVisible && !_cheatVisible && !_settingsVisible)
            return;
        InitStyles();

        float bgAlpha = ConsoleConfig.Initialized ? ConsoleConfig.BackgroundOpacity.Value : 0.97f;
        var bgTex = Tex(new Color(0.13f, 0.13f, 0.14f, bgAlpha));

        if (_consoleVisible)
        {
            // Process resize BEFORE GUI.Window so Event.current is not yet consumed by the window.
            HandleResizeGripEvents(
                ref _consoleRect,
                ref _consoleResizing,
                ref _consoleResizeAnchorMouse,
                ref _consoleResizeAnchorSize,
                MIN_CONSOLE_WIDTH,
                MIN_CONSOLE_HEIGHT,
                autoHeight: false
            );
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
            // Auto-fit height to content; preserve width across position recalculation.
            float cheatHeight = _cheatContentHeight > 0f ? _cheatContentHeight : _cheatRect.height;
            _cheatRect = new Rect(
                _consoleRect.x + _consoleRect.width + 6f,
                _consoleRect.y,
                _cheatRect.width,
                cheatHeight
            );
            // Process resize BEFORE GUI.Window while Event.current is still unmodified.
            HandleResizeGripEvents(
                ref _cheatRect,
                ref _cheatResizing,
                ref _cheatResizeAnchorMouse,
                ref _cheatResizeAnchorSize,
                MIN_CHEAT_WIDTH,
                MIN_CHEAT_HEIGHT,
                autoHeight: true
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

        if (_settingsVisible)
        {
            HandleResizeGripEvents(
                ref _settingsRect,
                ref _settingsResizing,
                ref _settingsResizeAnchorMouse,
                ref _settingsResizeAnchorSize,
                MIN_SETTINGS_WIDTH,
                MIN_SETTINGS_HEIGHT,
                autoHeight: false
            );
            GUI.DrawTexture(_settingsRect, bgTex);
            _settingsRect = GUI.Window(
                42426,
                _settingsRect,
                (GUI.WindowFunction)DrawSettingsPanel,
                "  Settings",
                _styleWindow!
            );
        }

        ConsumeGameMouseInput();
    }

    // ── Console Window ────────────────────────────────────────
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "CodeQuality",
        "IDE0060:Remove unused parameter",
        Justification = "Required by GUI.Window callback signature"
    )]
    private void DrawConsole(int id)
    {
        GUILayout.Space(26f);
        DrawRainbowTitle(
            new Rect(0, 2f, _consoleRect.width, 22f),
            " ★ AiComi Lua Console ԅ(¯﹃¯ԅ) ★   [F9]   ★ AiComi Lua Console (～￣▽￣)～ "
        );

        const float TOOLBAR_H = 28f;
        const float LABEL_H = 18f;
        const float INPUT_H = 100f;
        const float PADDING = 4f;
        float outputH =
            _consoleRect.height - 26f - TOOLBAR_H - INPUT_H - LABEL_H * 2 - PADDING * 6 - 20f;
        if (outputH < 60f)
            outputH = 60f;

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
        var settingsStyle = _settingsVisible ? _styleBtnActive! : _styleBtn!;
        if (GUILayout.Button("cfg", settingsStyle, GUILayout.Width(38)))
            _settingsVisible = !_settingsVisible;
        var cheatLabel = _cheatVisible ? "▨ Cheats ▶" : "▨ Cheats ▧";
        var cheatStyle = _cheatVisible ? _styleBtnActive! : _styleBtn!;
        if (GUILayout.Button(cheatLabel, cheatStyle, GUILayout.Width(100)))
            _cheatVisible = !_cheatVisible;
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal(GUILayout.Height(LABEL_H));
        GUILayout.Label("Output:", GUILayout.Width(55));
        GUILayout.Label("Find:", GUILayout.Width(33));
        _searchTerm = GUILayout.TextField(
            _searchTerm,
            GUILayout.Width(140),
            GUILayout.Height(LABEL_H)
        );
        if (_searchMatchCount > 0)
            GUILayout.Label($"{_searchMatchCount} found", GUILayout.Width(65));
        else if (!string.IsNullOrEmpty(_searchTerm))
            GUILayout.Label("0 found", GUILayout.Width(65));
        if (GUILayout.Button("Copy", _styleBtn!, GUILayout.Width(50), GUILayout.Height(LABEL_H)))
            GUIUtility.systemCopyBuffer = _output;
        GUILayout.EndHorizontal();

        bool searching = !string.IsNullOrEmpty(_searchTerm);
        string displayOutput = searching ? HighlightSearch(_output, _searchTerm) : _output;
        _styleOutput!.richText = searching;

        _outputScroll = GUILayout.BeginScrollView(_outputScroll, GUILayout.Height(outputH));
        GUILayout.TextArea(displayOutput, _styleOutput, GUILayout.ExpandHeight(true));
        GUILayout.EndScrollView();

        GUILayout.Label("Lua:  (Ctrl+Enter = Run)", GUILayout.Height(LABEL_H));
        GUI.SetNextControlName("LuaInput");

        // Intercept Ctrl+Enter BEFORE the TextArea processes Event.current.
        // Consuming the event here prevents the TextArea from inserting a newline
        // while still allowing Execute() to run with the current _input value.
        var kev = Event.current;
        bool ctrlEnterPressed =
            kev.type == EventType.KeyDown
            && (kev.keyCode == KeyCode.Return || kev.keyCode == KeyCode.KeypadEnter)
            && kev.control;
        if (ctrlEnterPressed)
            kev.Use();

        _inputScroll = GUILayout.BeginScrollView(_inputScroll, GUILayout.Height(INPUT_H));
        _input = GUILayout.TextArea(_input, _styleInput!, GUILayout.ExpandHeight(true));
        GUILayout.EndScrollView();

        if (ctrlEnterPressed)
            Execute();

        DrawResizeGripHandle(_consoleRect);

        GUI.DragWindow(new Rect(0, 0, _consoleRect.width, 20));
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

    public static void AppendOutputStatic(string text)
    {
        _instance?.AppendOutput(text);
    }

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
        bool autoScroll = !ConsoleConfig.Initialized || ConsoleConfig.AutoScrollOutput.Value;
        if (autoScroll)
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

    // ── Helpers ───────────────────────────────────────────────
    private string HighlightSearch(string text, string term)
    {
        if (string.IsNullOrEmpty(term) || string.IsNullOrEmpty(text))
        {
            _searchMatchCount = 0;
            return text;
        }

        var accentHex = ConsoleConfig.Initialized
            ? ConsoleConfig.AccentColor.Value.TrimStart('#')
            : "66FF66";
        string tagOpen = $"<color=#{accentHex}><b>";
        string tagClose = "</b></color>";

        var sb = new StringBuilder(text.Length + term.Length * 40);
        int idx = 0;
        int count = 0;

        while (idx < text.Length)
        {
            int found = text.IndexOf(term, idx, StringComparison.OrdinalIgnoreCase);
            if (found < 0)
            {
                sb.Append(text, idx, text.Length - idx);
                break;
            }
            sb.Append(text, idx, found - idx);
            sb.Append(tagOpen);
            sb.Append(text, found, term.Length);
            sb.Append(tagClose);
            count++;
            idx = found + term.Length;
        }

        _searchMatchCount = count;
        return sb.ToString();
    }

    private void AppendOutput(string text)
    {
        _output += text;
        int maxLen = ConsoleConfig.Initialized ? ConsoleConfig.OutputBufferSize.Value : 32000;
        if (_output.Length > maxLen)
            _output = _output[(_output.Length - maxLen)..];
    }

    private void EnsureCursorUnlocked()
    {
        if (!_hasStoredCursorState)
        {
            _storedCursorLockMode = Cursor.lockState;
            _storedCursorVisible = Cursor.visible;
            _hasStoredCursorState = true;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void RestoreCursorState()
    {
        if (!_hasStoredCursorState)
            return;

        Cursor.lockState = _storedCursorLockMode;
        Cursor.visible = _storedCursorVisible;
        _hasStoredCursorState = false;
        _consoleResizing = false;
        _cheatResizing = false;
        _settingsResizing = false;
    }

    private void ConsumeGameMouseInput()
    {
        var mousePos = Input.mousePosition;
        mousePos.y = Screen.height - mousePos.y;

        bool mouseOverConsole = _consoleVisible && _consoleRect.Contains(mousePos);
        bool mouseOverCheat = _cheatVisible && _cheatRect.Contains(mousePos);
        bool mouseOverSettings = _settingsVisible && _settingsRect.Contains(mousePos);
        bool interactingWithGui =
            mouseOverConsole
            || mouseOverCheat
            || mouseOverSettings
            || _consoleResizing
            || _cheatResizing
            || _settingsResizing;

        if (interactingWithGui)
            Input.ResetInputAxes();
    }

    // Draws the resize grip texture in window-local coordinates.
    // Must be called inside a GUI.Window callback.
    private void DrawResizeGripHandle(Rect windowRect)
    {
        var gripRect = new Rect(
            windowRect.width - RESIZE_GRIP_SIZE - 2f,
            windowRect.height - RESIZE_GRIP_SIZE - 2f,
            RESIZE_GRIP_SIZE,
            RESIZE_GRIP_SIZE
        );
        GUI.DrawTexture(gripRect, Tex(new Color(0.35f, 0.35f, 0.35f, 0.95f)));
    }

    // Handles resize grip events using Event.current and an anchor-offset approach.
    // Must be called BEFORE the corresponding GUI.Window call in OnGUI() so that
    // Event.current is not yet consumed or modified by the window.
    //
    // Anchor approach: on drag-start the initial mouse position and window size are
    // recorded.  Every subsequent frame the resize is applied as a delta so:
    //   - clicking the grip causes zero size change (delta = 0)
    //   - dragging gives a smooth, correctly-directional resize
    //
    // Event.current is used for start/stop detection instead of Input.GetMouseButton
    // because ConsumeGameMouseInput() calls Input.ResetInputAxes() which zeros out
    // Input.GetMouseButton mid-frame, causing the resize state to be lost.
    private void HandleResizeGripEvents(
        ref Rect windowRect,
        ref bool isResizing,
        ref Vector2 anchorMouse,
        ref Vector2 anchorSize,
        float minWidth,
        float minHeight,
        bool autoHeight
    )
    {
        // Input.mousePosition: bottom-left origin → convert to GUI top-left origin.
        // Input.mousePosition is NOT zeroed by Input.ResetInputAxes(), making it
        // reliable even after ConsumeGameMouseInput() runs.
        var mouseGui = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);

        var evt = Event.current;

        // Grip hit-area in screen space.
        var gripScreen = new Rect(
            windowRect.x + windowRect.width - RESIZE_GRIP_SIZE - 2f,
            windowRect.y + windowRect.height - RESIZE_GRIP_SIZE - 2f,
            RESIZE_GRIP_SIZE,
            RESIZE_GRIP_SIZE
        );

        // Start: record anchor on the MouseDown event so we can compute a delta later.
        if (evt.type == EventType.MouseDown && evt.button == 0 && gripScreen.Contains(mouseGui))
        {
            isResizing = true;
            anchorMouse = mouseGui;
            anchorSize = new Vector2(windowRect.width, windowRect.height);
        }

        // Stop: rawType catches MouseUp even if a GUI.Window already Used the event.
        if (evt.rawType == EventType.MouseUp)
        {
            isResizing = false;
            return;
        }

        if (!isResizing)
            return;

        // New size = size at drag-start + how far the mouse has moved since then.
        Vector2 delta = mouseGui - anchorMouse;

        float maxWidth = Mathf.Max(minWidth, Screen.width - windowRect.x - 8f);
        windowRect.width = Mathf.Clamp(anchorSize.x + delta.x, minWidth, maxWidth);

        if (!autoHeight)
        {
            float maxHeight = Mathf.Max(minHeight, Screen.height - windowRect.y - 8f);
            windowRect.height = Mathf.Clamp(anchorSize.y + delta.y, minHeight, maxHeight);
        }
    }
}
