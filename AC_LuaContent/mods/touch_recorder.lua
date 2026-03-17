-- mods/touch_recorder.lua – Pure Cursor/TouchState Dumps

local TM        = CS.AiComi_LuaMod.TouchMonitorHooks
local Resources = CS.UnityEngine.Resources
local GameCursorType = typeof(CS.ILLGAMES.Unity.Component.GameCursor)

local function dump_cursors(where)
    print(string.format("[TouchRecLua] --- Cursor Dump (%s) ---", where))
    local all = Resources.FindObjectsOfTypeAll(GameCursorType)
    if all == nil or all.Length == 0 then
        print("[TouchRecLua] Keine GameCursor gefunden")
        return
    end

    print(string.format("[TouchRecLua] GameCursor Count = %d", all.Length))
    for i = 0, all.Length - 1 do
        local gc = all[i]:TryCast(CS.ILLGAMES.Unity.Component.GameCursor)
        if gc ~= nil then
            local mode = tostring(gc._mode)
            local arr  = gc._anameTex
            local len  = arr ~= nil and arr.Length or 0
            print(string.format("[TouchRecLua] #%d mode=%s anameTex len=%d", i, mode, len))
            if arr ~= nil then
                for j = 0, arr.Length - 1 do
                    print(string.format("  [%02d] %s", j, tostring(arr[j])))
                end
            end
        end
    end
end

local function snapshot_touch_state(tag)
    if not TM.Captured then
        print(string.format("[TouchRecLua] [%s] Noch kein TouchController-Snapshot vorhanden", tag))
        return
    end

    local miss  = tonumber(TM.MissGauge)      or -1
    local pleas = tonumber(TM.PleasureGauge)  or -1
    local org   = tostring(TM.IsOrgasm)
    local oc    = tonumber(TM.OrgasmCount)    or -1

    print(string.format(
        "[TouchRecLua] [%s] Miss=%.3f Pleas=%.3f Orgasm=%s (%d)",
        tag, miss, pleas, org, oc
    ))
end


function TouchRecDump(tag)
    tag = tag or "manual"
    print("[TouchRecLua] === Dump: " .. tag .. " ===")
    snapshot_touch_state(tag)
    dump_cursors(tag)
end

print("[TouchRecLua] touch_recorder (manual dumps) geladen")
