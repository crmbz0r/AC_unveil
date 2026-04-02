using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AiComi_LuaMod;

public partial class LuaConsole
{
    // Cheat panel
    private Rect _cheatRect = new Rect(810, 80, 280, 420);
    private float _cheatContentHeight = 0f;
    private bool _cheatsDirty = false;

    private GUIStyle? _styleToggleOn,
        _styleToggleOff;

    // ── Event Snapshot ────────────────────────────────────────
    private HashSet<int>? _eventSnapshot;

    private bool DrawToggle(bool value, string label)
    {
        var cheatOnCol = ConsoleConfig.Initialized
            ? ConsoleConfig.GetAccentColor()
            : new Color(0.4f, 1f, 0.4f);

        if (_styleToggleOn == null)
        {
            _styleToggleOn = new GUIStyle(_styleBtn!)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
            };
            _styleToggleOn.normal.background = Tex(new Color(
                cheatOnCol.r * 0.25f, cheatOnCol.g * 0.28f, cheatOnCol.b * 0.25f, 1f));
            _styleToggleOn.hover.background = Tex(new Color(
                cheatOnCol.r * 0.33f, cheatOnCol.g * 0.35f, cheatOnCol.b * 0.33f, 1f));
            _styleToggleOn.normal.textColor = cheatOnCol;
            _styleToggleOn.hover.textColor = new Color(
                Mathf.Min(1f, cheatOnCol.r + 0.1f),
                Mathf.Min(1f, cheatOnCol.g + 0.1f),
                Mathf.Min(1f, cheatOnCol.b + 0.1f));

            _styleToggleOff = new GUIStyle(_styleBtn!)
            {
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
            };
            _styleToggleOff.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
            _styleToggleOff.hover.textColor = new Color(0.85f, 0.85f, 0.85f);
        }

        var style = value ? _styleToggleOn! : _styleToggleOff!;
        var symbol = value ? "✓ [ON]" : "╳ [OFF]";
        bool newVal = value;
        if (GUILayout.Button($"{symbol}  {label}", style))
            newVal = !value;

        if (newVal != value && ConsoleConfig.Initialized && ConsoleConfig.AutoRestoreCheats.Value)
            _cheatsDirty = true;

        return newVal;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "CodeQuality",
        "IDE0060:Remove unused parameter",
        Justification = "Required by GUI.Window callback signature"
    )]
    private void DrawCheatPanel(int id)
    {
        GUILayout.Space(4);

        // ── Exploration  stuff ──
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("~ Exploration Mode", GUI.skin.label);

        ExplorationSceneHooks.NoclipEnabled = DrawToggle(
            ExplorationSceneHooks.NoclipEnabled,
            "Noclip"
        );

        if (ExplorationSceneHooks.NoclipEnabled)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Noclip Speed: {NoclipBehaviour.Speed:F1}", GUILayout.Width(130));
            NoclipBehaviour.Speed = GUILayout.HorizontalSlider(NoclipBehaviour.Speed, 1f, 20f);
            GUILayout.EndHorizontal();
        }

        bool prevGhostMode = ExplorationSceneHooks.GhostModeEnabled;
        ExplorationSceneHooks.GhostModeEnabled = DrawToggle(
            ExplorationSceneHooks.GhostModeEnabled,
            "No Area Restrictions"
        );
        if (ExplorationSceneHooks.GhostModeEnabled != prevGhostMode)
            ExplorationSceneHooks.ApplyBlindness();

        GUILayout.EndVertical();

        GUILayout.Space(5);

        // ── Dialog / Communication / Talk stuff ──
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("~ Dialog / Conversation", GUI.skin.label);

        var prevNoTalkTime = DialogSceneHooks.NoTalkTimeReduction;
        DialogSceneHooks.NoTalkTimeReduction = DrawToggle(
            DialogSceneHooks.NoTalkTimeReduction,
            "No Talk Time Reduction"
        );
        if (DialogSceneHooks.NoTalkTimeReduction != prevNoTalkTime)
        {
            DialogSceneHooks.ApplyNoTalkTimeReduction();
        }
        DialogSceneHooks.NoFavorLoss = DrawToggle(DialogSceneHooks.NoFavorLoss, "No Favor Loss");
        DialogSceneHooks.NoMoodLoss = DrawToggle(DialogSceneHooks.NoMoodLoss, "No Mood Loss");
        DialogSceneHooks.ForcePositiveChoice = DrawToggle(
            DialogSceneHooks.ForcePositiveChoice,
            "Force Positive Choice"
        );
        DialogSceneHooks.AlwaysAcceptTouch = DrawToggle(
            DialogSceneHooks.AlwaysAcceptTouch,
            "Always Accept Massage"
        );
        GUILayout.EndVertical();

        GUILayout.Space(5);

        // ── Touch Scene ──
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("~ Touch (Massage) Scene", GUI.skin.label);

        TouchSceneHooks.UnlockItems = DrawToggle(
            TouchSceneHooks.UnlockItems,
            "Unlock all massage items"
        );

        var prevNoDislike = TouchSceneHooks.NoDislike;
        TouchSceneHooks.NoDislike = DrawToggle(TouchSceneHooks.NoDislike, "No Dislike React");
        if (TouchSceneHooks.NoDislike && !prevNoDislike)
        {
            RunLua(TouchSceneHooks.LuaNoDislike);
        }

        TouchSceneHooks.NoTouchInterruption = DrawToggle(
            TouchSceneHooks.NoTouchInterruption,
            "No 1/3 & 2/3 Interruption"
        );

        var prevShowNextH = TouchSceneHooks.ShowNextH;
        TouchSceneHooks.ShowNextH = DrawToggle(TouchSceneHooks.ShowNextH, "Show Next H Button");
        if (TouchSceneHooks.ShowNextH && !prevShowNextH)
        {
            TouchSceneHooks.ApplyShowNextH();
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

        if (GUILayout.Button("  [+] Unlock All Events (w/ backup)", _styleBtn!))
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

        // Track content height for auto-sizing
        if (Event.current.type == EventType.Repaint)
        {
            float contentBottom = GUILayoutUtility.GetLastRect().yMax;
            float maxHeight = Mathf.Max(MIN_CHEAT_HEIGHT, Screen.height - _cheatRect.y - 8f);
            float desiredHeight = contentBottom + 30f; // padding for title bar + bottom margin
            _cheatContentHeight = Mathf.Clamp(desiredHeight, MIN_CHEAT_HEIGHT, maxHeight);
        }

        DrawResizeGripHandle(_cheatRect);

        // Deferred save: all cheat statics are assigned by now
        if (_cheatsDirty)
        {
            _cheatsDirty = false;
            ConsoleConfig.SaveCheatStates();
        }

        GUI.DragWindow(new Rect(0, 0, _cheatRect.width, 20));
    }

    private void TakeEventSnapshot()
    {
        if (_luaEnv is null)
            return;
        _eventSnapshot = new HashSet<int>();
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
            foreach (var r in results.Where(static r => r != null))
                try
                {
                    _eventSnapshot.Add(Convert.ToInt32(r));
                }
                catch
                {
                    // Ignore non-integer entries returned by Lua when rebuilding the snapshot list.
                }
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
            print('Events restored: ' .. em.Count)
        "
        );
    }
}
