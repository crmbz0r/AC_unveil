using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace AiComi_LuaMod;

[BepInPlugin("aicomi.luamod", "AiComi LuaMod", "1.0.0")]
public class Plugin : BasePlugin
{
    internal static new ManualLogSource Log = null!;

    public override void Load()
    {
        Log = base.Log;
        Harmony.CreateAndPatchAll(typeof(LuaPatch));
        Harmony.CreateAndPatchAll(typeof(TouchSceneHooks));
        Harmony.CreateAndPatchAll(typeof(DialogSceneHooks));
        Harmony.CreateAndPatchAll(typeof(TouchMonitorHooks));
        Harmony.CreateAndPatchAll(typeof(ExplorationSceneHooks));
        AddComponent<NoclipBehaviour>();
        LuaConsole.InitConfig(Config);
        LuaConsole.ApplySavedCheatStates();
        Log.LogWarning("AiComi LuaMod loaded -- waiting for BuildConditionsFromLua...");
    }
}
