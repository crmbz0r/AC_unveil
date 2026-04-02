using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using AC;
using AC.Scene.Explore.Communication;
using HarmonyLib;

namespace AiComi_LuaMod;

// ─────────────────────────────────────────────────────────────
// Dialog Scene Hooks
// - No Favor Loss
// - No Mood Loss
// - Force Positive Choice
// - Always Accept Touch Scene
// - No Talk Time Reduction (AOB patch)
// ─────────────────────────────────────────────────────────────
public static class DialogSceneHooks
{
    public static bool UnlockCommunicationCategories = false;
    public static bool NoFavorLoss = false;
    public static bool NoMoodLoss = false;
    public static bool ForcePositiveChoice = false;
    public static bool AlwaysAcceptTouch = false;
    public static bool NoTalkTimeReduction = false;

    // ── No Talk Time Reduction: AOB Patch Fields ──────────────────────────────

    // Pattern:  test rcx,rcx | je ??+??+??+?? | dec [rcx+3C]
    // Bytes:    48 85 C9     | 0F 84 ?? ?? ?? ?? | FF 49 3C
    private static readonly byte?[] _talkTimePattern =
    {
        0x48, 0x85, 0xC9,                    // test rcx,rcx
        0x0F, 0x84, null, null, null, null,  // je ... (wildcard)
        0xFF, 0x49, 0x3C                     // dec [rcx+3C]  <-- NOP target (+9)
    };

    private static readonly Dictionary<IntPtr, byte[]> _originalBytes = new();
    private static List<IntPtr> _patchedAddresses = new();
    private static bool _patchInitialized = false;

    [DllImport("kernel32.dll")]
    private static extern bool VirtualProtect(
        IntPtr lpAddress,
        UIntPtr dwSize,
        uint flNewProtect,
        out uint lpflOldProtect
    );

    private const uint PAGE_EXECUTE_READWRITE = 0x40;

    // ── Unlock All Categories ──────────────────────────────
    // Alle IDs die wir immer wollen
    private static readonly int[] AllCategoryIDs =
    {
        0,
        2,
        3,
        4,
        5,
        6,
        7,
        8,
        9,
        10,
        11,
        12,
        13,
        14,
        100,
        101,
    };

    [HarmonyPostfix]
    [HarmonyPatch(typeof(AC.Scene.Explore.Communication.CommunicationUI), "RefreshButtons")]
    static void UnlockAllCategories(AC.Scene.Explore.Communication.CommunicationUI __instance)
    {
        if (!UnlockCommunicationCategories)
            return;

        var categoryList = __instance._categoryList;
        if (categoryList == null)
            return;

        // Which IDs are already present?
        var existing = new HashSet<int>();
        for (int i = 0; i < categoryList.Count; i++)
            existing.Add(categoryList[i].ID);

        Plugin.Log.LogWarning($"[CommUI] Existing: {string.Join(",", existing)}");
        // Missing IDs:
        foreach (var id in AllCategoryIDs)
            if (!existing.Contains(id))
                Plugin.Log.LogWarning($"[CommUI] Missing: {id}");
    }

    // ── No Favor Loss ──────────────────────────────
    [HarmonyPrefix]
    [HarmonyPatch(typeof(AC.User.NPCData), "_SetupParameterModification_b__105_0")]
    private static void NoFavorLossHook(ref int add)
    {
        if (NoFavorLoss && add < 0)
            add = 0;
    }

    // ── No Mood Loss ───────────────────────────────
    [HarmonyPrefix]
    [HarmonyPatch(typeof(AC.User.NPCData), "_SetupParameterModification_b__105_1")]
    private static void NoMoodLossHook(ref int add)
    {
        if (NoMoodLoss && add < 0)
            add = 0;
    }

    // ── Force Positive Choice ─────────────────────
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
                    Plugin.Log.LogWarning($"[Dialog] ForcePositive: '{tag}' → '{siblingTag}'");
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

    // ── Always Accept Touch Scene ───────────────────────

    [HarmonyPrefix]
    [HarmonyPatch(
        typeof(CommunicationUI.ProbCalculator),
        "Check",
        new[] { typeof(AC.User.NPCData) }
    )]
    private static bool AlwaysAcceptTouch_NPC(ref bool __result)
    {
        if (!AlwaysAcceptTouch)
            return true; // run original
        __result = true;
        Plugin.Log.LogWarning("[Dialog] AlwaysAcceptTouch: Check(NPCData) forced > true");
        return false; // skip original
    }

    [HarmonyPrefix]
    [HarmonyPatch(
        typeof(CommunicationUI.ProbCalculator),
        "Check",
        new[] { typeof(AC.User.UniqueNPCData) }
    )]
    private static bool AlwaysAcceptTouch_UniqueNPC(ref bool __result)
    {
        if (!AlwaysAcceptTouch)
            return true; // run original
        __result = true;
        Plugin.Log.LogWarning("[Dialog] AlwaysAcceptTouch: Check(UniqueNPCData) forced > true");
        return false; // skip original
    }

    // ── No Talk Time Reduction (AOB Patch) ──────────────────────────────

    private static void InitializeTalkTimePatch()
    {
        if (_patchInitialized)
            return;

        _patchInitialized = true;

        ProcessModule? gameAssembly = null;
        foreach (ProcessModule m in Process.GetCurrentProcess().Modules)
        {
            if (
                m.ModuleName?.Equals("GameAssembly.dll", StringComparison.OrdinalIgnoreCase) == true
            )
            {
                gameAssembly = m;
                break;
            }
        }

        if (gameAssembly == null)
        {
            Plugin.Log.LogError("[Dialog] NoTalkTimeReduction: GameAssembly.dll not found!");
            return;
        }

        var baseAddr = gameAssembly.BaseAddress;
        var size = gameAssembly.ModuleMemorySize;

        Plugin.Log.LogWarning(
            $"[Dialog] NoTalkTimeReduction: Scanning GameAssembly.dll @ 0x{baseAddr:X} size={size}"
        );

        var hits = AobScan(baseAddr, size, _talkTimePattern);
        // Store target addresses (offset +9 = start of "FF 49 3C" dec instruction)
        _patchedAddresses = new List<IntPtr>();
        foreach (var hit in hits)
        {
            _patchedAddresses.Add(hit + 9);
        }
        Plugin.Log.LogWarning($"[Dialog] NoTalkTimeReduction: Found {_patchedAddresses.Count} hit(s)");
    }

    public static void ApplyNoTalkTimeReduction()
    {
        InitializeTalkTimePatch();

        foreach (var addr in _patchedAddresses)
        {
            if (NoTalkTimeReduction)
                NopBytes(addr, 3);
            else
                RestoreBytes(addr);
        }

        Plugin.Log.LogWarning(
            $"[Dialog] NoTalkTimeReduction: {(NoTalkTimeReduction ? "Patched" : "Restored")} {_patchedAddresses.Count} address(es)"
        );
    }

    private static List<IntPtr> AobScan(IntPtr baseAddr, int size, byte?[] pattern)
    {
        var results = new List<IntPtr>();
        var data = new byte[size];

        Marshal.Copy(baseAddr, data, 0, size);

        for (int i = 0; i <= size - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (pattern[j].HasValue && data[i + j] != pattern[j]!.Value)
                {
                    match = false;
                    break;
                }
            }
            if (match)
                results.Add(baseAddr + i);
        }

        return results;
    }

    private static void NopBytes(IntPtr address, int count)
    {
        // Backup original bytes
        var original = new byte[count];
        Marshal.Copy(address, original, 0, count);
        _originalBytes[address] = original;

        VirtualProtect(address, (UIntPtr)count, PAGE_EXECUTE_READWRITE, out uint old);
        var nops = new byte[count];
        for (int i = 0; i < count; i++)
            nops[i] = 0x90;
        Marshal.Copy(nops, 0, address, count);
        VirtualProtect(address, (UIntPtr)count, old, out _);
    }

    private static void RestoreBytes(IntPtr address)
    {
        if (!_originalBytes.TryGetValue(address, out var original))
            return;
        VirtualProtect(address, (UIntPtr)original.Length, PAGE_EXECUTE_READWRITE, out uint old);
        Marshal.Copy(original, 0, address, original.Length);
        VirtualProtect(address, (UIntPtr)original.Length, old, out _);
        _originalBytes.Remove(address);
    }
}
