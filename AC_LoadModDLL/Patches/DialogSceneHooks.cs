using System;
using AC;
using HarmonyLib;

namespace AiComi_LuaMod;

// ─────────────────────────────────────────────────────────────
//  Dialog Scene Hooks
//  - No Favor Loss
//  - No Mood Loss
//  - Force Positive Choice
// ─────────────────────────────────────────────────────────────
public static class DialogSceneHooks
{
    public static bool NoFavorLoss = false;
    public static bool NoMoodLoss = false;
    public static bool ForcePositiveChoice = false;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(AC.User.NPCData), "_SetupParameterModification_b__105_0")]
    private static void NoFavorLossHook(ref int add)
    {
        if (NoFavorLoss && add < 0)
            add = 0;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(AC.User.NPCData), "_SetupParameterModification_b__105_1")]
    private static void NoMoodLossHook(ref int add)
    {
        if (NoMoodLoss && add < 0)
            add = 0;
    }

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
                    Plugin.Log.LogInfo($"[Dialog] ForcePositive: '{tag}' → '{siblingTag}'");
                    choice.TagToJump = siblingTag;
                    if (choice._outPositiveState != null)
                        choice._outNegativeState = choice._outPositiveState;
                    return;
                }
            }
            Plugin.Log.LogWarning($"[Dialog] ForcePositive: kein positiver Sibling für '{tag}'");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning("[Dialog] ForcePositive: " + ex.Message);
        }
    }
}
