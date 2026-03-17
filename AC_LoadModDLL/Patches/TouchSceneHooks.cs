using System;
using AC.Scene.Touch;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace AiComi_LuaMod;

// ─────────────────────────────────────────────────────────────
//  Touch Scene Hooks
//  - Unlock All Touch Items
//  - No Dislike
//  - Show Next H Button
// ─────────────────────────────────────────────────────────────
[HarmonyPatch(typeof(TouchController))]
public static class TouchSceneHooks
{
    public static bool UnlockItems = false;
    public static bool NoDislike = false;
    public static bool ShowNextH = false;

    // ── Lua-Code für No Dislike ───────────────────────────────
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

    // ── Helper ────────────────────────────────────────────────
    private static Il2CppReferenceArray<Il2CppSystem.ValueTuple<int, int>> MakeItems(
        params (int, int)[] tuples)
    {
        var arr = new Il2CppReferenceArray<Il2CppSystem.ValueTuple<int, int>>(tuples.Length);
        for (int i = 0; i < tuples.Length; i++)
            arr[i] = new Il2CppSystem.ValueTuple<int, int>(tuples[i].Item1, tuples[i].Item2);
        return arr;
    }

    // ── Setup Postfix ─────────────────────────────────────────
    [HarmonyPostfix]
    [HarmonyPatch(nameof(TouchController.Setup))]
    private static void OnSetup(TouchController __instance)
    {
        // Unlock Items
        if (UnlockItems)
        {
            try
            {
                var partsInfos = __instance._partsInfos;
                if (partsInfos != null)
                {
                    partsInfos[0].Items = MakeItems((0, 1), (1, 2), (2, 2));
                    partsInfos[1].Items = MakeItems((0, 1), (1, 2), (2, 2));
                    partsInfos[2].Items = MakeItems((0, 3), (1, 3), (2, 3), (3, 3));
                    partsInfos[3].Items = MakeItems((0, 5), (1, 5), (4, 5));
                    partsInfos[4].Items = MakeItems((0, 4));
                    partsInfos[5].Items = MakeItems((0, 4));
                    partsInfos[6].Items = MakeItems((0, 6));
                    partsInfos[7].Items = MakeItems((0, 6));
                    Plugin.Log.LogInfo("[TouchScene] Items freigeschaltet!");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("[TouchScene] UnlockItems: " + ex.Message);
            }
        }

        // No Dislike
        if (NoDislike)
        {
            try
            {
                LuaConsole.RunLuaStatic(LuaNoDislike);
                Plugin.Log.LogInfo("[TouchScene] NoDislike angewendet!");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("[TouchScene] NoDislike: " + ex.Message);
            }
        }

        // Show Next H
        if (ShowNextH)
            ApplyShowNextH(__instance);
    }

    // ── Show Next H ───────────────────────────────────────────
    public static void ApplyShowNextH(TouchController? tc = null)
    {
        tc ??= UnityEngine.Object.FindObjectOfType(Il2CppType.Of<TouchController>())
            ?.TryCast<TouchController>();

        if (tc == null)
        {
            Plugin.Log.LogWarning("[TouchScene] ShowNextH: TouchController nicht gefunden");
            return;
        }

        try
        {
            var btn = tc._buttonH;
            if (btn != null && !btn.gameObject.activeSelf)
            {
                btn.gameObject.SetActive(true);
                Plugin.Log.LogInfo("[TouchScene] ShowNextH: _buttonH aktiviert");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning("[TouchScene] ShowNextH: " + ex.Message);
        }
    }
}