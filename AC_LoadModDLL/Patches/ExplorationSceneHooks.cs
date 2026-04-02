using System;
using System.Collections.Generic;
using AC.Scene.Explore;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;

namespace AiComi_LuaMod;

// ─────────────────────────────────────────────────────────────
// Exploration Scene Hooks
// - GhostMode: NPCs blind + cannot enter detection states
// - Noclip: NavMeshAgent disabled, Transform moved directly
// ─────────────────────────────────────────────────────────────

[HarmonyPatch(typeof(AC.Scene.ExploreScene))]
public static class ExplorationSceneHooks
{
    public static bool GhostModeEnabled = false;
    public static bool NoclipEnabled
    {
        get => NoclipBehaviour.Enabled;
        set => NoclipBehaviour.Enabled = value;
    }

    // Detection states to block (Encounter = 5, Shy = 9)
    private static readonly HashSet<int> DetectionStates = new() { 5, 9 };

    // Saved sight radii for restore on toggle-off
    private static readonly Dictionary<int, (float radius, float radiusCrouch)> OriginalSightRadii =
        new();

    // ── Scene accessor ────────────────────────────────────────
    public static AC.Scene.ExploreScene? GetExploreScene()
    {
        return UnityEngine
            .Object.FindObjectOfType(Il2CppInterop.Runtime.Il2CppType.Of<AC.Scene.ExploreScene>())
            ?.TryCast<AC.Scene.ExploreScene>();
    }

    // ── StartAllActors Postfix ────────────────────────────────
    // Apply blindness on scene load if GhostMode is already on

    [HarmonyPostfix]
    [HarmonyPatch("StartAllActors")]
    private static void OnStartAllActors(AC.Scene.ExploreScene __instance)
    {
        if (GhostModeEnabled)
            ApplyBlindness(__instance);
    }

    // ── Apply / Restore Blindness ─────────────────────────────
    public static void ApplyBlindness(AC.Scene.ExploreScene? scene = null)
    {
        scene ??= GetExploreScene();

        if (scene == null)
        {
            Plugin.Log.LogWarning("[ExploreScene] GhostMode: ExploreScene not found!");
            return;
        }

        try
        {
            var npcList = scene._npcListReadOnlyInstance;
            if (npcList == null)
                return;

            var sightField = AccessTools.Field(typeof(NPC), "_sight");
            var sightCrouchField = AccessTools.Field(typeof(NPC), "_sightInCrounching");

            for (int i = 0; i < npcList.Count; i++)
            {
                var npc = npcList[i];
                if (npc == null)
                    continue;

                var sight = (NPC.Sight)sightField.GetValue(npc);
                var sightCrouch = (NPC.Sight)sightCrouchField.GetValue(npc);
                int key = npc.GetHashCode();

                if (GhostModeEnabled)
                {
                    if (!OriginalSightRadii.ContainsKey(key))
                        OriginalSightRadii[key] = (sight.Radius, sightCrouch.Radius);

                    sight.Radius = 0f;
                    sightCrouch.Radius = 0f;
                }
                else
                {
                    if (OriginalSightRadii.TryGetValue(key, out var orig))
                    {
                        sight.Radius = orig.radius;
                        sightCrouch.Radius = orig.radiusCrouch;
                    }
                }
            }

            Plugin.Log.LogWarning(
                $"[ExploreScene] GhostMode blindness {(GhostModeEnabled ? "applied" : "restored")} on {npcList.Count} NPCs."
            );
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning("[ExploreScene] GhostMode blindness: " + ex.Message);
        }
    }

    // ── Block Detection State Transitions ────────────────────
    [HarmonyPatch(typeof(NPC), "ChangeState", new Type[] { typeof(NPC.StateID) })]
    [HarmonyPrefix]
    static bool BlockChangeStateID(NPC __instance, NPC.StateID __0)
    {
        if (!GhostModeEnabled || __instance == null)
            return true;
        return !DetectionStates.Contains((int)__0);
    }

    [HarmonyPatch(typeof(NPC), "ChangeState", new Type[] { typeof(int) })]
    [HarmonyPrefix]
    static bool BlockChangeStateInt(NPC __instance, int __0)
    {
        if (!GhostModeEnabled || __instance == null)
            return true;
        return !DetectionStates.Contains(__0);
    }

    [HarmonyPatch(typeof(NPC), "ChangeStateIfDifferent", new Type[] { typeof(NPC.StateID) })]
    [HarmonyPrefix]
    static bool BlockChangeStateIfDifferentID(NPC __instance, NPC.StateID __0)
    {
        if (!GhostModeEnabled || __instance == null)
            return true;
        return !DetectionStates.Contains((int)__0);
    }

    [HarmonyPatch(typeof(NPC), "ChangeStateIfDifferent", new Type[] { typeof(int) })]
    [HarmonyPrefix]
    static bool BlockChangeStateIfDifferentInt(NPC __instance, int __0)
    {
        if (!GhostModeEnabled || __instance == null)
            return true;
        return !DetectionStates.Contains(__0);
    }
}

// ─────────────────────────────────────────────────────────────
// IL2CPP private field accessor helper
// ─────────────────────────────────────────────────────────────

internal static class AccessHarmony
{
    public static object? GetField(object obj, string fieldName)
    {
        try
        {
            return AccessTools
                .FindIncludingBaseTypes(obj.GetType(), t => AccessTools.Field(t, fieldName))
                ?.GetValue(obj);
        }
        catch
        {
            return null;
        }
    }
}
