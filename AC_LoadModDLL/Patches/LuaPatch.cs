using System;
using System.IO;
using AC;
using BepInEx;
using HarmonyLib;
using XLua;

namespace AiComi_LuaMod;

// ─────────────────────────────────────────────────────────────
//  Harmony: LuaEnv abgreifen
// ─────────────────────────────────────────────────────────────
[HarmonyPatch(typeof(ParameterContainer))]
internal static class LuaPatch
{
    [HarmonyPostfix, HarmonyPatch(nameof(ParameterContainer.BuildConditionsFromLua))]
    private static void AfterBuildConditions(ParameterContainer __instance)
    {
        if (DialogSceneHooks.NoFavorLoss)
            Plugin.Log.LogInfo("[Dialog] NoFavorLoss aktiv");
        if (DialogSceneHooks.NoMoodLoss)
            Plugin.Log.LogInfo("[Dialog] NoMoodLoss aktiv");
        if (DialogSceneHooks.ForcePositiveChoice)
            Plugin.Log.LogInfo("[Dialog] ForcePositiveChoice aktiv");

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

        // Register UnlockItems function for Lua access
        luaEnv.Global.Set(
            "UnlockItems",
            (Action<bool>)(
                v =>
                {
                    TouchSceneHooks.UnlockItems = v;
                    Plugin.Log.LogInfo($"[TouchScene] UnlockItems = {v}");
                }
            )
        );
    }
}
