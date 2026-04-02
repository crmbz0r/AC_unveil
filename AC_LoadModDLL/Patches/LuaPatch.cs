using System;
using System.IO;
using AC;
using BepInEx;
using HarmonyLib;
using XLua;

namespace AiComi_LuaMod;

// ─────────────────────────────────────────────────────────────
//  Harmony: grab LuaEnv once available
// ─────────────────────────────────────────────────────────────
[HarmonyPatch(typeof(ParameterContainer))]
internal static class LuaPatch
{
    // ── Why pure Lua for R? ──────────────────────────────────────────────────────
    // xLua IL2CPP can only call static methods on types in its LuaCallCSharp list.
    // Plugin types are NOT in that list → "No such type: MyClass.Method" error.
    //
    // What IS proven to work (from LuaNoDislike):
    //   xlua.private_accessible(t)   → unlocks private members for a type
    //   obj._privateField            → direct field read/write after unlock
    //   t:GetFields(flags)           → xLua uses System.Reflection internally → in LuaCallCSharp
    //   CS.UnityEngine.Object.FindObjectsOfType(t)  → always in LuaCallCSharp
    //
    // R is a pure Lua table using only those proven entry points.
    // ────────────────────────────────────────────────────────────────────────────

    private const string LuaBridge =
        @"
R = {}

-- BindingFlags (numeric — avoids CS.System.Reflection.BindingFlags lookup)
-- Public=16, NonPublic=32, Instance=4, Static=8, DeclaredOnly=2
local BF_INST  = 52  -- Public|NonPublic|Instance
local BF_DECL  = 54  -- Public|NonPublic|Instance|DeclaredOnly
local BF_STAT  = 60  -- Public|NonPublic|Instance|Static

local _typeCache = {}

-- ─── internal: find a System.Type by name across all assemblies ────────────
function R._type(name)
    if _typeCache[name] then return _typeCache[name] end

    -- Try direct lookup first (needs fully qualified name)
    local ok, t = pcall(function() return CS.System.Type.GetType(name) end)
    if ok and t ~= nil then _typeCache[name] = t; return t end

    -- Search all loaded assemblies by short or full name
    local okD, domain = pcall(function() return CS.System.AppDomain.CurrentDomain end)
    if not okD then return nil end

    local asms = domain:GetAssemblies()
    for i = 0, asms.Length - 1 do
        local asm = asms[i]
        local ok2, types = pcall(function() return asm:GetTypes() end)
        if ok2 and types then
            for j = 0, types.Length - 1 do
                local tp = types[j]
                if tp ~= nil and (tp.Name == name or tp.FullName == name) then
                    _typeCache[name] = tp
                    return tp
                end
            end
        end
    end
    return nil
end

-- ─── R.Find('ClassName') ────────────────────────────────────────────────────
-- Returns all live scene instances.  Result is 0-indexed.
--   local tc = R.Find('TouchController')[0]
function R.Find(typeName)
    local t = R._type(typeName)
    if t == nil then
        print('[R.Find] Type not found: ' .. typeName)
        print(""  Hint: R.Types('AC') to discover available types"")
        return nil
    end
    local ok, arr = pcall(function()
        return CS.UnityEngine.Object.FindObjectsOfType(t)
    end)
    if not ok or arr == nil then
        print('[R.Find] FindObjectsOfType failed')
        return nil
    end
    print('[R.Find] ' .. typeName .. ' — ' .. arr.Length .. ' instance(s)')
    return arr
end

-- ─── R.Get(obj, 'fieldName') ────────────────────────────────────────────────
-- Read any private or public field.
--   print(R.Get(tc, '_missOver'))
function R.Get(obj, fieldName)
    if obj == nil then print('[R.Get] obj is nil'); return nil end
    xlua.private_accessible(obj:GetType())
    local ok, v = pcall(function() return obj[fieldName] end)
    if ok then return v end
    -- Walk base types if not found on declared type
    local cur = obj:GetType().BaseType
    while cur ~= nil and cur.FullName ~= 'System.Object' do
        xlua.private_accessible(cur)
        local ok2, v2 = pcall(function() return obj[fieldName] end)
        if ok2 then return v2 end
        cur = cur.BaseType
    end
    print('[R.Get] Field not found: ' .. fieldName)
    return nil
end

-- ─── R.Set(obj, 'fieldName', val) ───────────────────────────────────────────
-- Write any private or public field.
--   R.Set(tc, '_gaugeUpSpeed', 0.1)
function R.Set(obj, fieldName, val)
    if obj == nil then print('[R.Set] obj is nil'); return end
    xlua.private_accessible(obj:GetType())
    local ok, err = pcall(function() obj[fieldName] = val end)
    if not ok then print('[R.Set] ' .. fieldName .. ': ' .. tostring(err)) end
end

-- ─── R.Inspect(obj) ─────────────────────────────────────────────────────────
-- Dump all fields + current values (incl. private) up the full type chain.
--   R.Inspect(tc)
function R.Inspect(obj, showStatic)
    if obj == nil then print('(null)'); return end
    local t = obj:GetType()
    xlua.private_accessible(t)

    local lines = {'=== ' .. t.FullName .. ' ==='}
    local cur = t
    while cur ~= nil do
        local fn = cur.FullName or ''
        if fn == 'System.Object' or fn == 'Il2CppSystem.Object' then break end

        local ok, fields = pcall(function()
            return cur:GetFields(showStatic and BF_STAT or BF_DECL)
        end)
        if ok and fields ~= nil and fields.Length > 0 then
            table.insert(lines, '  ── ' .. cur.Name .. ' ──')
            for i = 0, fields.Length - 1 do
                local f = fields[i]
                local ok2, v = pcall(function() return f:GetValue(obj) end)
                local vs = ok2 and tostring(v) or '<err>'
                if #vs > 90 then vs = vs:sub(1, 90) .. '…' end
                local acc = f.IsPublic and 'pub' or 'prv'
                local st  = f.IsStatic  and ' stt' or '    '
                table.insert(lines, string.format(
                    '  %s%s  %-22s %s = %s', acc, st, f.FieldType.Name, f.Name, vs))
            end
        end
        cur = cur.BaseType
    end
    print(table.concat(lines, '\n'))
end

-- ─── R.Fields('ClassName') ──────────────────────────────────────────────────
-- List all fields of a type — no instance needed.
--   R.Fields('TouchController')
function R.Fields(typeName)
    local t = (type(typeName) == 'string') and R._type(typeName) or typeName
    if t == nil then print('Type not found: ' .. tostring(typeName)); return end
    xlua.private_accessible(t)

    local lines = {'[' .. t.FullName .. '] Fields:'}
    local cur = t
    while cur ~= nil do
        local fn = cur.FullName or ''
        if fn == 'System.Object' or fn == 'Il2CppSystem.Object' then break end
        local ok, fields = pcall(function() return cur:GetFields(BF_DECL) end)
        if ok and fields then
            for i = 0, fields.Length - 1 do
                local f = fields[i]
                table.insert(lines, string.format('  %s%s  %-24s %s',
                    f.IsPublic and 'pub' or 'prv',
                    f.IsStatic and ' stt' or '    ',
                    f.FieldType.Name, f.Name))
            end
        end
        cur = cur.BaseType
    end
    print(table.concat(lines, '\n'))
end

-- ─── R.Methods(obj or 'ClassName') ──────────────────────────────────────────
-- List all methods.
--   R.Methods(tc)
--   R.Methods('TouchController')
function R.Methods(objOrName)
    local t
    if type(objOrName) == 'string' then
        t = R._type(objOrName)
    elseif objOrName ~= nil then
        t = objOrName:GetType()
    end
    if t == nil then print('[R.Methods] type/object not found'); return end

    local lines = {'[' .. t.FullName .. '] Methods:'}
    local cur = t
    while cur ~= nil do
        local fn = cur.FullName or ''
        if fn == 'System.Object' or fn == 'Il2CppSystem.Object' then break end
        local ok, methods = pcall(function() return cur:GetMethods(BF_DECL) end)
        if ok and methods then
            for i = 0, methods.Length - 1 do
                local m = methods[i]
                if not m.IsSpecialName then
                    local ok2, params = pcall(function() return m:GetParameters() end)
                    local plist = {}
                    if ok2 and params then
                        for j = 0, params.Length - 1 do
                            plist[#plist+1] = params[j].ParameterType.Name
                        end
                    end
                    table.insert(lines, string.format('  %s%s  %-14s %s(%s)',
                        m.IsPublic and 'pub' or 'prv',
                        m.IsStatic and ' stt' or '    ',
                        m.ReturnType.Name, m.Name,
                        table.concat(plist, ', ')))
                end
            end
        end
        cur = cur.BaseType
    end
    print(table.concat(lines, '\n'))
end

-- ─── R.Call(obj, 'methodName', ...) ─────────────────────────────────────────
-- Call any method including private ones.
--   R.Call(tc, 'SubMisssGauge')
--   local hit = R.Call(tc, 'AddMissGauge', 2)
function R.Call(obj, methodName, ...)
    if obj == nil then print('[R.Call] obj is nil'); return end
    xlua.private_accessible(obj:GetType())
    local args = {...}
    local ok, result = pcall(function()
        -- Lua: obj[name](obj, ...) is equivalent to obj:name(...)
        local fn = obj[methodName]
        if fn == nil then error('method not found: ' .. methodName) end
        return fn(obj, table.unpack(args))
    end)
    if not ok then print('[R.Call] ' .. methodName .. ': ' .. tostring(result)); return nil end
    return result
end

-- ─── R.Types('prefix') ──────────────────────────────────────────────────────
-- Discover all types whose name starts with prefix.
--   R.Types('AC.Scene.Touch')
--   R.Types('AC')
function R.Types(prefix)
    prefix = prefix or 'AC'
    local okD, domain = pcall(function() return CS.System.AppDomain.CurrentDomain end)
    if not okD then print('[R.Types] AppDomain not accessible'); return end

    local asms = domain:GetAssemblies()
    local found = {}
    for i = 0, asms.Length - 1 do
        local asm = asms[i]
        local ok2, types = pcall(function() return asm:GetTypes() end)
        if ok2 and types then
            for j = 0, types.Length - 1 do
                local tp = types[j]
                if tp ~= nil then
                    local fn = tp.FullName or tp.Name or ''
                    if fn:sub(1, #prefix) == prefix then
                        found[#found+1] = fn
                    end
                end
            end
        end
    end
    table.sort(found)
    print(table.concat(found, '\n'))
    print('[' .. #found .. ' types matching ' .. prefix .. ']')
end

-- ─── R.Summary(obj) ─────────────────────────────────────────────────────────
-- One-line overview: type name + first few field values.
function R.Summary(obj)
    if obj == nil then print('(null)'); return end
    local t = obj:GetType()
    xlua.private_accessible(t)
    local ok, fields = pcall(function() return t:GetFields(BF_DECL) end)
    if not ok or not fields then print(t.Name .. ' {}'); return end
    local parts = {}
    for i = 0, math.min(4, fields.Length - 1) do
        local f = fields[i]
        local ok2, v = pcall(function() return f:GetValue(obj) end)
        parts[#parts+1] = f.Name .. '=' .. (ok2 and tostring(v) or '?')
    end
    local suffix = fields.Length > 5 and ', ...' or ''
    print(t.Name .. ' { ' .. table.concat(parts, ', ') .. suffix .. ' }')
end

print('[R] Ready - type rhelp() for usage')
";

    private const string LuaHelpers =
        @"
function UnlockItems(v)
    CS.AiComi_LuaMod.TouchSceneHooks.UnlockItems = v
    print('[TouchScene] UnlockItems = ' .. tostring(v))
end

function rhelp()
    print([[
══ R — Lua Exploration Bridge ══════════════════════════════════
  R.Types('AC')                   discover types in namespace
  R.Fields('ClassName')           list all fields  (no instance needed)
  R.Methods(obj)                  list all methods of an object
  R.Methods('ClassName')          list all methods by type name
  R.Find('ClassName')             all live scene instances  [0]-indexed
  R.Inspect(obj)                  dump ALL fields + values (incl. private)
  R.Get(obj, '_fieldName')        read any private field
  R.Set(obj, '_fieldName', val)   write any private field
  R.Call(obj, 'methodName', ...)  call any method (incl. private)
  R.Summary(obj)                  quick one-line field overview

Quick example:
  local tc = R.Find('TouchController')[0]
  R.Inspect(tc)
  print(R.Get(tc, '_missOver'))
  R.Set(tc, '_gaugeUpSpeed', 0.1)
  R.Call(tc, 'SubMisssGauge')
]])
end
";

    [HarmonyPostfix, HarmonyPatch(nameof(ParameterContainer.BuildConditionsFromLua))]
    private static void AfterBuildConditions(ParameterContainer __instance)
    {
        if (DialogSceneHooks.NoFavorLoss)
            Plugin.Log.LogWarning("[Dialog] NoFavorLoss active");
        if (DialogSceneHooks.NoMoodLoss)
            Plugin.Log.LogWarning("[Dialog] NoMoodLoss active");
        if (DialogSceneHooks.ForcePositiveChoice)
            Plugin.Log.LogWarning("[Dialog] ForcePositiveChoice active");
        if (DialogSceneHooks.AlwaysAcceptTouch)
            Plugin.Log.LogWarning("Always Accept Massage active");

        var luaEnv = __instance._luaEnv;
        if (luaEnv is null)
        {
            Plugin.Log.LogError("LuaEnv ist null!");
            return;
        }

        Plugin.Log.LogWarning("LuaEnv found! Initializing xLua plugin + console...");
        LuaConsole.Initialize(luaEnv);

        // ── R bridge (pure Lua) ──────────────────────────────────────────────────
        try
        {
            luaEnv.DoString(LuaBridge, "R_bridge");
            Plugin.Log.LogWarning("[R] Reflection bridge active");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[R] Bridge load failed: {ex.Message}");
        }

        // ── MOD_PATH ─────────────────────────────────────────────────────────────
        var modsPath =
            Path.Combine(Paths.PluginPath, "lua_scripts", "mods").Replace("\\", "/") + "/";
        luaEnv.Global.Set("MOD_PATH", modsPath);

        // ── AiComi_LuaMod.lua ────────────────────────────────────────────────────
        try
        {
            var path = Path.Combine(BepInEx.Paths.PluginPath, "AiComi_LuaMod.lua");
            if (File.Exists(path))
            {
                luaEnv.DoString(File.ReadAllText(path), "AiComi_LuaMod");
                Plugin.Log.LogWarning($"Lua-Mod loaded: {path}");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"Lua-Mod error: {ex}");
        }

        // ── Helpers ──────────────────────────────────────────────────────────────
        luaEnv.DoString(LuaHelpers, "helpers");
    }
}
