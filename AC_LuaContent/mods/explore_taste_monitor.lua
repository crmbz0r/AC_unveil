-- explore_taste_monitor.lua
-- Monitors ALL parameter changes during conversations in real-time.
-- KEY INSIGHT: Player taste progress is on SaveData.PlayerData._tastes (byte[], 0-20)
--              NPC taste thresholds are on NPCData._tastes (int[], static per girl)
--
-- Usage:
--   tm_start()   - begin monitoring (take initial snapshot)
--   tm_tick()    - poll for changes (call between conversation steps)
--   tm_stop()    - stop and print full change log
--   tm_snap()    - print current snapshot without stopping
--   tm_probe()   - deep-probe all data fields
--   tm_clear()   - reset log

---------------------------------------------------------------------------
-- Helpers
---------------------------------------------------------------------------

local typeof = typeof or CS.System.Type.GetType
local _poll_co = nil   -- coroutine handle
local _log = {}        -- {timestamp, npcName, field, oldVal, newVal}
local _prev = {}       -- previous snapshot per NPC guid
local _running = false
local _start_time = 0

local function safe(fn)
    local ok, v = pcall(fn)
    return ok and v or nil
end

local function get_comm()
    local go = safe(function()
        return CS.UnityEngine.GameObject.Find("ExploreScene/Communication")
    end)
    if not go then
        go = safe(function()
            return CS.UnityEngine.GameObject.Find("Communication")
        end)
    end
    if not go then return nil end
    return safe(function() return go:GetComponent("CommunicationUI") end)
end

local function get_targets()
    local comm = get_comm()
    if not comm then return nil end
    xlua.private_accessible(typeof(CS.AC.Scene.Explore.Communication.CommunicationUI))
    return safe(function() return comm._targets end)
end

---------------------------------------------------------------------------
-- Read PlayerData._tastes  (Il2CppStructArray<byte>, range 0-20)
-- THIS is the player's conversation progress per taste category!
---------------------------------------------------------------------------
local _player_taste_debug = true  -- print debug info once

local function read_player_tastes()
    -- Try multiple paths to get SaveData
    local saveData = nil
    local path = "?"

    local ok1, sd1 = pcall(function() return CS.Manager.Game.Instance.SaveData end)
    if ok1 and sd1 then
        saveData = sd1
        path = "Manager.Game"
    end
    if not saveData then
        local ok2, sd2 = pcall(function() return CS.AC.Lua.EventTable.SaveData end)
        if ok2 and sd2 then saveData = sd2; path = "EventTable" end
    end
    if not saveData then return nil, nil, nil end

    local ok3, pd = pcall(function() return saveData.PlayerData end)
    if not ok3 or not pd then return nil, nil, nil end

    xlua.private_accessible(typeof(CS.AC.User.PlayerData))

    local t0, t1, t2 = nil, nil, nil
    local strategy = "none"

    -- Strategy 1: Direct indexer on _tastes (works for int arrays, not byte)
    local arr = safe(function() return pd._tastes end)
    if arr then
        t0 = safe(function() return arr[0] end)
        if t0 ~= nil then
            t1 = safe(function() return arr[1] end)
            t2 = safe(function() return arr[2] end)
            strategy = "direct_index"
        end
    end

    -- Strategy 2: ArrayStartPointer + Marshal.ReadByte (points directly at data!)
    -- ArrayStartPointer = Pointer + 32 on 64-bit, directly at byte[0]
    if t0 == nil and arr then
        local dataPtr = safe(function() return arr.ArrayStartPointer end)
        if dataPtr then
            local Marshal = CS.System.Runtime.InteropServices.Marshal
            t0 = safe(function() return Marshal.ReadByte(dataPtr, 0) end)
            t1 = safe(function() return Marshal.ReadByte(dataPtr, 1) end)
            t2 = safe(function() return Marshal.ReadByte(dataPtr, 2) end)
            if t0 ~= nil then strategy = "ArrayStartPointer+Marshal" end
        end
    end

    -- Strategy 3: Pointer + offset 32 + Marshal.ReadByte (fallback)
    if t0 == nil and arr then
        local ptr = safe(function() return arr.Pointer end)
        if ptr then
            local Marshal = CS.System.Runtime.InteropServices.Marshal
            -- Data at offset 4 * sizeof(IntPtr) = 32 on 64-bit
            t0 = safe(function() return Marshal.ReadByte(ptr, 32) end)
            t1 = safe(function() return Marshal.ReadByte(ptr, 33) end)
            t2 = safe(function() return Marshal.ReadByte(ptr, 34) end)
            if t0 ~= nil then strategy = "Pointer+Marshal(offset=32)" end
        end
    end

    -- Strategy 4: arr:get_Item(i)
    if t0 == nil and arr then
        t0 = safe(function() return arr:get_Item(0) end)
        if t0 ~= nil then
            t1 = safe(function() return arr:get_Item(1) end)
            t2 = safe(function() return arr:get_Item(2) end)
            strategy = "get_Item"
        end
    end

    if _player_taste_debug then
        print("[TM] DEBUG PlayerTaste: strategy=" .. strategy ..
              " values=[" .. tostring(t0) .. "," .. tostring(t1) .. "," .. tostring(t2) .. "]")
        -- Extra pointer debug
        if arr then
            local ptr = safe(function() return arr.Pointer end)
            print("[TM] DEBUG: arr.Pointer=" .. tostring(ptr))
            if ptr then
                local ptrType = safe(function() return ptr:GetType().FullName end)
                print("[TM] DEBUG: Pointer type=" .. tostring(ptrType))
            end
        end
        _player_taste_debug = false
    end

    return t0, t1, t2
end

---------------------------------------------------------------------------
-- Read _tastes array  (Il2CppStructArray<int>)
---------------------------------------------------------------------------
local function read_tastes(npcdata)
    -- npcdata._tastes is Il2CppStructArray<int>, index 0-based
    local ok, arr = pcall(function()
        xlua.private_accessible(typeof(CS.AC.User.NPCData))
        return npcdata._tastes
    end)
    if not ok or arr == nil then return nil, nil, nil end
    local t0 = safe(function() return arr[0] end)
    local t1 = safe(function() return arr[1] end)
    local t2 = safe(function() return arr[2] end)
    return t0, t1, t2
end

---------------------------------------------------------------------------
-- Read _tasteFlags array  (Il2CppStructArray<bool>)
---------------------------------------------------------------------------
local function read_taste_flags(npcdata)
    local ok, arr = pcall(function()
        xlua.private_accessible(typeof(CS.AC.User.NPCData))
        return npcdata._tasteFlags
    end)
    if not ok or arr == nil then return nil, nil, nil end
    local f0 = safe(function() return arr[0] end)
    local f1 = safe(function() return arr[1] end)
    local f2 = safe(function() return arr[2] end)
    return f0, f1, f2
end

---------------------------------------------------------------------------
-- Read Desire entries from DesireTable on NPCData
---------------------------------------------------------------------------
local _desire_debug = true

local function read_desires(npcdata)
    local desires = {}
    local ok, dt = pcall(function()
        xlua.private_accessible(typeof(CS.AC.User.NPCData))
        return npcdata.DesireStats
    end)
    if not ok or dt == nil then return desires end

    -- DesireTable wraps IntKeyDictionary<Desire>
    -- Try accessing the internal _instance dictionary directly
    local ok_inst, dict = pcall(function()
        xlua.private_accessible(typeof(CS.AC.Scene.Explore.DesireTable))
        return dt._instance
    end)

    if ok_inst and dict then
        -- IntKeyDictionary<Desire>.GetEnumerator() returns KVP enumerator
        local ok2, enumerator = pcall(function() return dict:GetEnumerator() end)
        if not ok2 or enumerator == nil then
            if _desire_debug then
                print("[TM] DEBUG: dict:GetEnumerator() failed")
                _desire_debug = false
            end
            return desires
        end

        local idx = 0
        while true do
            local mv_ok, has_next = pcall(function() return enumerator:MoveNext() end)
            if not mv_ok or not has_next then break end
            -- Current is KeyValuePair<int, Desire>
            local kvp = safe(function() return enumerator.Current end)
            if kvp then
                -- Try .Value for the Desire, .Key for the int key
                local d = safe(function() return kvp.Value end)
                local key = safe(function() return kvp.Key end)
                if d then
                    xlua.private_accessible(typeof(CS.AC.Scene.Explore.Desire))
                    local id  = safe(function() return d.ID end)
                    local cur = safe(function() return d.Current end)
                    local pri = safe(function() return d.Prioritizes end)
                    local ord = safe(function() return d.Order end)
                    local tag = safe(function() return d.Tag end)
                    if _desire_debug and idx == 0 then
                        print(string.format("[TM] DEBUG: First desire: key=%s id=%s cur=%s pri=%s tag=%s",
                            tostring(key), tostring(id), tostring(cur), tostring(pri), tostring(tag)))
                    end
                    desires[idx] = {id=id or key, current=cur, prioritizes=pri, order=ord, tag=tag}
                else
                    -- Maybe kvp itself IS the Desire (non-generic enumerator)
                    xlua.private_accessible(typeof(CS.AC.Scene.Explore.Desire))
                    local id  = safe(function() return kvp.ID end)
                    local cur = safe(function() return kvp.Current end)
                    if id or cur then
                        desires[idx] = {
                            id = id, current = cur,
                            prioritizes = safe(function() return kvp.Prioritizes end),
                            order = safe(function() return kvp.Order end),
                            tag = safe(function() return kvp.Tag end)
                        }
                    elseif _desire_debug then
                        print("[TM] DEBUG: kvp has no .Value and no .ID - type: " .. tostring(safe(function() return kvp:GetType().FullName end)))
                    end
                end
                idx = idx + 1
            end
        end
        _desire_debug = false
    else
        -- Fallback: try DesireTable's own GetEnumerator
        local ok2, enumerator = pcall(function() return dt:GetEnumerator() end)
        if not ok2 or enumerator == nil then return desires end

        local idx = 0
        while true do
            local mv_ok, has_next = pcall(function() return enumerator:MoveNext() end)
            if not mv_ok or not has_next then break end
            local kvp = safe(function() return enumerator.Current end)
            if kvp then
                -- Same KVP handling
                local d = safe(function() return kvp.Value end)
                if d then
                    xlua.private_accessible(typeof(CS.AC.Scene.Explore.Desire))
                    desires[idx] = {
                        id = safe(function() return d.ID end),
                        current = safe(function() return d.Current end),
                        prioritizes = safe(function() return d.Prioritizes end),
                        order = safe(function() return d.Order end),
                        tag = safe(function() return d.Tag end)
                    }
                end
                idx = idx + 1
            end
        end
    end
    return desires
end

---------------------------------------------------------------------------
-- Read TalkStats
---------------------------------------------------------------------------
local _ts_debug = true

local function read_talkstats(npcdata)
    local ts = safe(function()
        xlua.private_accessible(typeof(CS.AC.User.NPCData))
        return npcdata.TalkStats
    end)
    if not ts then
        ts = safe(function() return npcdata.talkStats end)
    end
    if not ts then return nil end

    xlua.private_accessible(typeof(CS.AC.User.TalkStats))

    -- Read all available fields with debug
    local result = {
        time          = safe(function() return ts.Time end),
        maxTime       = safe(function() return ts.MaxTime end),
        acquiredPoint = safe(function() return ts.AcquiredPoint end),
        asked         = safe(function() return ts.Asked end),
        invitedLunch  = safe(function() return ts.InvitedLunch end),
    }

    -- Also try TalkStrikeZone - this might be the taste-related data
    local tsz = safe(function() return ts.TalkStrikeZone end)
    if tsz then
        -- TalkStrikeZone is a Span<byte> or similar - try indexed access
        result.strikeZone0 = safe(function() return tsz[0] end)
        result.strikeZone1 = safe(function() return tsz[1] end)
        result.strikeZone2 = safe(function() return tsz[2] end)
    end

    -- Try TopicListen queue
    local tl = safe(function() return ts.TopicListen end)
    if tl then
        result.topicListenCount = safe(function() return tl.Count end)
    end

    if _ts_debug then
        print("[TM] DEBUG TalkStats: time=" .. tostring(result.time) ..
              " max=" .. tostring(result.maxTime) ..
              " acquired=" .. tostring(result.acquiredPoint) ..
              " sz=[" .. tostring(result.strikeZone0) .. "," .. tostring(result.strikeZone1) .. "," .. tostring(result.strikeZone2) .. "]" ..
              " topicQ=" .. tostring(result.topicListenCount))
        _ts_debug = false
    end

    return result
end

---------------------------------------------------------------------------
-- Read ParameterContainer modifications (SetupParameterModification lambdas)
---------------------------------------------------------------------------
local function read_param_container(npcdata)
    local pc = safe(function()
        xlua.private_accessible(typeof(CS.AC.User.NPCData))
        return npcdata.ParameterContainer
    end)
    if pc == nil then
        pc = safe(function() return npcdata.parameterContainer end)
    end
    if pc == nil then return nil end

    xlua.private_accessible(typeof(CS.AC.ParameterContainer))

    -- Try reading relevant fields
    local result = {}
    for _, field in ipairs({"_favorAdder","_moodAdder","_lewdnessAdder","_desireAdder",
                            "_favorModifier","_moodModifier","_lewdnessModifier",
                            "_tasteAdder","_tasteMod",
                            "FavorModification","MoodModification","LewdnessModification",
                            "TasteModification"}) do
        local v = safe(function() return pc[field] end)
        if v ~= nil then
            result[field] = v
        end
    end
    return result
end

---------------------------------------------------------------------------
-- Find NPCData via SaveData.NPCDataList  (same as existing rec script)
---------------------------------------------------------------------------
local function find_npcdata(actor)
    local saveData = safe(function() return CS.AC.Lua.EventTable.SaveData end)
    if not saveData then
        saveData = safe(function() return CS.Manager.Game.Instance.SaveData end)
    end
    if not saveData then return nil end
    local list = safe(function() return saveData.NPCDataList end)
    if not list then return nil end

    -- Try matching by BaseData identity first
    local actorBase = safe(function() return actor.BaseData end)
    -- Also get name for fallback matching
    local actorName = safe(function() return actor.BaseData.HumanData.Parameter.fullname end)
    if actorName then actorName = tostring(actorName) end

    local outer = list:GetEnumerator()
    while outer:MoveNext() do
        local inner = outer.Current
        if inner then
            local ie = inner:GetEnumerator()
            while ie:MoveNext() do
                local nd = ie.Current
                if nd then
                    -- Try identity match
                    if actorBase then
                        local base = safe(function() return nd.NPCInstance.BaseData end)
                        if base == actorBase then return nd end
                    end
                    -- Try name match as fallback
                    if actorName then
                        local ndName = safe(function() return nd.HumanData.GetCharaName(true) end)
                        if ndName and tostring(ndName) == actorName then return nd end
                        -- Also try Parameter.fullname
                        ndName = safe(function() return nd.HumanData.Parameter.fullname end)
                        if ndName and tostring(ndName) == actorName then return nd end
                    end
                end
            end
        end
    end
    return nil
end

---------------------------------------------------------------------------
-- Build full snapshot for one NPC
---------------------------------------------------------------------------
local function snapshot_npc(actor)
    local name = "?"
    local n = safe(function() return actor.BaseData.HumanData.Parameter.fullname end)
    if n then name = tostring(n) end

    local base = actor.BaseData
    local function g(f) return safe(function() return base[f] end) end

    local nd = find_npcdata(actor)
    local t0, t1, t2 = nil, nil, nil
    local tf0, tf1, tf2 = nil, nil, nil
    local desires = {}
    local talkstats = nil
    local paramcont = nil

    if nd then
        t0, t1, t2 = read_tastes(nd)
        tf0, tf1, tf2 = read_taste_flags(nd)
        desires = read_desires(nd)
        talkstats = read_talkstats(nd)
        paramcont = read_param_container(nd)
    end

    -- Also try reading _tastes directly from NPC object
    if t0 == nil then
        local npc_t0, npc_t1, npc_t2 = nil, nil, nil
        pcall(function()
            xlua.private_accessible(typeof(CS.AC.Scene.Explore.NPC))
            local arr = actor._tastes
            if arr then
                npc_t0 = arr[0]
                npc_t1 = arr[1]
                npc_t2 = arr[2]
            end
        end)
        if npc_t0 ~= nil then
            t0, t1, t2 = npc_t0, npc_t1, npc_t2
        end
    end

    -- Read PLAYER taste progress (the values that change during conversation)
    local pt0, pt1, pt2 = read_player_tastes()

    return {
        name     = name,
        favor    = g("FavorValue"),
        relation = g("RelationValue"),
        mood     = g("Mood"),
        intimacy = g("Intimacy"),
        lewdness = g("LewdnessValue"),
        -- NPC thresholds (static per girl)
        npc_taste0 = t0,  -- Study threshold
        npc_taste1 = t1,  -- Sport threshold
        npc_taste2 = t2,  -- Fashion threshold
        tf0      = tf0,
        tf1      = tf1,
        tf2      = tf2,
        -- Player progress (changes during conversation!)
        player_taste0 = pt0,  -- Study progress
        player_taste1 = pt1,  -- Sport progress
        player_taste2 = pt2,  -- Fashion progress
        desires  = desires,
        talkstats = talkstats,
        paramcont = paramcont,
    }
end

---------------------------------------------------------------------------
-- Compare two snapshots and log differences
---------------------------------------------------------------------------
local TRACKED_SCALARS = {
    "favor","relation","mood","intimacy","lewdness",
    "npc_taste0","npc_taste1","npc_taste2",
    "tf0","tf1","tf2",
    "player_taste0","player_taste1","player_taste2",
}

local function compare_and_log(name, prev, curr, t)
    local changes = {}

    -- Scalar fields
    for _, f in ipairs(TRACKED_SCALARS) do
        local pv, cv = prev[f], curr[f]
        if pv ~= nil and cv ~= nil and pv ~= cv then
            table.insert(_log, {t=t, name=name, field=f, old=pv, new=cv})
            table.insert(changes, string.format("  %s: %s -> %s", f, tostring(pv), tostring(cv)))
        end
    end

    -- Desire changes
    for idx, cd in pairs(curr.desires) do
        local pd = prev.desires and prev.desires[idx]
        if pd and pd.current ~= cd.current then
            local label = "desire["..tostring(cd.id or idx).."]"
            if cd.tag then label = label .. "(" .. tostring(cd.tag) .. ")" end
            table.insert(_log, {t=t, name=name, field=label, old=pd.current, new=cd.current})
            table.insert(changes, string.format("  %s: %s -> %s", label, tostring(pd.current), tostring(cd.current)))
        end
    end

    -- TalkStats changes
    if prev.talkstats and curr.talkstats then
        for _, f in ipairs({"time","maxTime","acquiredPoint","asked","invitedLunch",
                            "strikeZone0","strikeZone1","strikeZone2","topicListenCount"}) do
            local pv, cv = prev.talkstats[f], curr.talkstats[f]
            if pv ~= nil and cv ~= nil and pv ~= cv then
                local label = "talk." .. f
                table.insert(_log, {t=t, name=name, field=label, old=pv, new=cv})
                table.insert(changes, string.format("  %s: %s -> %s", label, tostring(pv), tostring(cv)))
            end
        end
    end

    -- Print changes live
    if #changes > 0 then
        print(string.format("[TM] %.1fs  %s:", t, name))
        for _, c in ipairs(changes) do print(c) end
    end
end

---------------------------------------------------------------------------
-- Full snapshot of all conversation targets
---------------------------------------------------------------------------
local function full_snapshot()
    local targets = get_targets()
    if not targets then return nil end
    local npcs = {}
    local e = targets:GetEnumerator()
    while e:MoveNext() do
        local actor = e.Current
        if actor then
            local snap = snapshot_npc(actor)
            npcs[snap.name] = snap
        end
    end
    return npcs
end

---------------------------------------------------------------------------
-- Pretty format a snapshot
---------------------------------------------------------------------------
local function fmt_snapshot(npcs)
    local lines = {}
    for name, s in pairs(npcs) do
        table.insert(lines, string.format(
            "[TM] %-16s fav=%-4s mood=%-3s lewd=%-3s int=%-3s",
            name,
            tostring(s.favor), tostring(s.mood), tostring(s.lewdness), tostring(s.intimacy)
        ))
        table.insert(lines, string.format(
            "       NPC thresholds:  study=%s  sport=%s  fashion=%s  flags=[%s,%s,%s]",
            tostring(s.npc_taste0), tostring(s.npc_taste1), tostring(s.npc_taste2),
            tostring(s.tf0), tostring(s.tf1), tostring(s.tf2)
        ))
        table.insert(lines, string.format(
            "       PLAYER progress: study=%s  sport=%s  fashion=%s  (0-20, these change!)",
            tostring(s.player_taste0), tostring(s.player_taste1), tostring(s.player_taste2)
        ))
        -- Print desires
        if s.desires then
            for idx, d in pairs(s.desires) do
                table.insert(lines, string.format(
                    "       desire[%s] id=%s cur=%s pri=%s ord=%s tag=%s",
                    tostring(idx), tostring(d.id), tostring(d.current),
                    tostring(d.prioritizes), tostring(d.order), tostring(d.tag or "?")))
            end
        end
        -- Print talkstats
        if s.talkstats then
            local ts = s.talkstats
            table.insert(lines, string.format(
                "       talkstats: time=%s/%s acquired=%s asked=%s lunch=%s strikeZone=[%s,%s,%s] topicQ=%s",
                tostring(ts.time), tostring(ts.maxTime),
                tostring(ts.acquiredPoint), tostring(ts.asked), tostring(ts.invitedLunch),
                tostring(ts.strikeZone0), tostring(ts.strikeZone1), tostring(ts.strikeZone2),
                tostring(ts.topicListenCount)))
        end
        -- Print paramcont fields that were found
        if s.paramcont and next(s.paramcont) then
            local parts = {}
            for k, v in pairs(s.paramcont) do
                table.insert(parts, k .. "=" .. tostring(v))
            end
            table.insert(lines, "       paramcont: " .. table.concat(parts, ", "))
        end
    end
    return table.concat(lines, "\n")
end

---------------------------------------------------------------------------
-- Polling via LateUpdate hook (auto-polls every ~0.5s real time)
---------------------------------------------------------------------------
local _poll_go = nil
local _poll_interval = 0.5
local _last_poll = 0

local function do_poll()
    if not _running then return end
    local now_real = CS.UnityEngine.Time.realtimeSinceStartup
    if now_real - _last_poll < _poll_interval then return end
    _last_poll = now_real

    local t = now_real - _start_time
    local curr = full_snapshot()
    if curr then
        for name, cs in pairs(curr) do
            if _prev[name] then
                compare_and_log(name, _prev[name], cs, t)
            end
        end
        _prev = curr
    end
end

-- We'll call do_poll() manually from tm_tick(), or the user can
-- set up an Update hook if available. For simplicity, use manual polling.

---------------------------------------------------------------------------
-- Also try deep-probing: iterate ALL fields on NPCData to find unknowns
---------------------------------------------------------------------------
local function deep_probe_npcdata(actor)
    local nd = find_npcdata(actor)
    if not nd then
        print("[TM] Could not find NPCData for actor")
        return
    end

    print("[TM] Deep-probing NPCData fields...")
    xlua.private_accessible(typeof(CS.AC.User.NPCData))

    -- Probe all known numeric/bool fields
    local probe_fields = {
        "FavorValue", "Mood", "Intimacy", "LewdnessValue", "HCountValue",
        "RelationValue", "VisitNumber", "PeriodStartDay", "PeriodDay",
        "BehaviorType", "TouchCount", "Sexperience", "DateCount",
        "Plan", "PromisedFestival", "ArrangedDate", "ArrangedShoppingDate",
        "IsVirginFlag", "IsAnalVirginFlag", "IsRunning", "InvokedPairEvent",
        "InvokedNorokeEvent", "AppliedShyParameter",
    }

    for _, f in ipairs(probe_fields) do
        local v = safe(function() return nd[f] end)
        if v ~= nil then
            print(string.format("  .%s = %s (%s)", f, tostring(v), type(v)))
        end
    end

    -- Try _tastes (NPC thresholds)
    local t0, t1, t2 = read_tastes(nd)
    print(string.format("  ._tastes (NPC thresholds) = [%s, %s, %s]", tostring(t0), tostring(t1), tostring(t2)))

    -- Try _tasteFlags
    local tf0, tf1, tf2 = read_taste_flags(nd)
    print(string.format("  ._tasteFlags = [%s, %s, %s]", tostring(tf0), tostring(tf1), tostring(tf2)))

    -- Player taste progress
    local pt0, pt1, pt2 = read_player_tastes()
    print(string.format("  PlayerData._tastes (PROGRESS) = [%s, %s, %s]  (0-20, these change!)", tostring(pt0), tostring(pt1), tostring(pt2)))

    -- DesireStats
    local desires = read_desires(nd)
    print("  DesireStats:")
    for idx, d in pairs(desires) do
        print(string.format("    [%d] id=%s cur=%s pri=%s ord=%s tag=%s",
            idx, tostring(d.id), tostring(d.current), tostring(d.prioritizes),
            tostring(d.order), tostring(d.tag or "?")))
    end

    -- TalkStats
    local ts = read_talkstats(nd)
    if ts then
        print(string.format("  TalkStats: time=%s/%s acquired=%s asked=%s lunch=%s",
            tostring(ts.time), tostring(ts.maxTime), tostring(ts.acquiredPoint),
            tostring(ts.asked), tostring(ts.invitedLunch)))
    end

    -- ParameterContainer
    local pc = read_param_container(nd)
    if pc and next(pc) then
        print("  ParameterContainer fields found:")
        for k, v in pairs(pc) do
            print(string.format("    .%s = %s", k, tostring(v)))
        end
    else
        print("  ParameterContainer: no matching fields found")
    end

    -- Try additional fields that might exist
    print("  Probing extra fields...")
    local extra = {
        "DesireStats", "TalkStats", "ParameterContainer",
        "UrgentAction", "ActionState",
        -- ParameterContainer sub-fields 
    }
    for _, f in ipairs(extra) do
        local v = safe(function() return nd[f] end)
        if v ~= nil then
            print(string.format("    .%s = %s (%s)", f, tostring(v), tostring(v:GetType().FullName)))
        end
    end
end

---------------------------------------------------------------------------
-- Public API
---------------------------------------------------------------------------

function tm_start()
    _running = true
    _log = {}
    _prev = {}
    _start_time = CS.UnityEngine.Time.realtimeSinceStartup
    _last_poll = _start_time

    -- Take initial snapshot
    local snap = full_snapshot()
    if snap then
        _prev = snap
        print("[TM] === MONITORING STARTED ===")
        print(fmt_snapshot(snap))
        print("[TM] Use tm_tick() to poll, or just talk - call tm_stop() when done.")
    else
        print("[TM] Warning: No conversation targets found yet.")
        print("[TM]   Start a conversation, then call tm_start() again or tm_tick()")
    end
end

function tm_tick()
    do_poll()
end

function tm_stop()
    -- Do one final poll to catch last changes
    if _running then
        do_poll()
    end
    _running = false
    print("[TM] === MONITORING STOPPED ===")

    -- Print final snapshot
    local snap = full_snapshot()
    if snap then
        print("[TM] Final state:")
        print(fmt_snapshot(snap))
    end

    -- Print full change log
    if #_log > 0 then
        print(string.format("[TM] === CHANGE LOG (%d entries) ===", #_log))
        for _, e in ipairs(_log) do
            local delta = ""
            if type(e.old) == "number" and type(e.new) == "number" then
                delta = string.format(" (%+d)", e.new - e.old)
            end
            print(string.format("  [%.1fs] %-14s %-20s %s -> %s%s",
                e.t, e.name, e.field, tostring(e.old), tostring(e.new), delta))
        end
    else
        print("[TM] No changes detected.")
    end
end

function tm_snap()
    local snap = full_snapshot()
    if snap then
        print("[TM] Current snapshot:")
        print(fmt_snapshot(snap))
    else
        print("[TM] No conversation targets found.")
    end
end

function tm_clear()
    _log = {}
    _prev = {}
    print("[TM] Log und prev cleared.")
end

function tm_probe()
    local targets = get_targets()
    if not targets then
        print("[TM] No conversation targets found")
        return
    end
    local e = targets:GetEnumerator()
    if e:MoveNext() then
        deep_probe_npcdata(e.Current)
    end
end

-- Interactive debug for PlayerData byte array access
function tm_debug_player()
    print("[TM] === DEEP DEBUG: PlayerData._tastes ===")

    local sd = safe(function() return CS.Manager.Game.Instance.SaveData end)
    if not sd then
        print("  SaveData: nil")
        return
    end
    print("  SaveData: OK (" .. tostring(safe(function() return sd:GetType().FullName end)) .. ")")

    local pd = safe(function() return sd.PlayerData end)
    if not pd then
        print("  PlayerData: nil")
        return
    end
    print("  PlayerData: OK (" .. tostring(safe(function() return pd:GetType().FullName end)) .. ")")

    xlua.private_accessible(typeof(CS.AC.User.PlayerData))

    -- Check _tastes field
    local arr = safe(function() return pd._tastes end)
    if not arr then
        print("  _tastes: nil")
    else
        print("  _tastes: OK (object exists)")
        print("    type: " .. tostring(safe(function() return arr:GetType().FullName end)))
        print("    Length: " .. tostring(safe(function() return arr.Length end)))
        print("    arr[0]: " .. tostring(safe(function() return arr[0] end)))
        print("    arr:get_Item(0): " .. tostring(safe(function() return arr:get_Item(0) end)))

        -- Try Pointer + Marshal approach (raw IL2CPP memory!)
        print("  --- Trying Pointer + Marshal.ReadByte ---")
        local ptr = safe(function() return arr.Pointer end)
        print("    arr.Pointer: " .. tostring(ptr))
        if ptr then
            print("    Pointer type: " .. tostring(safe(function() return ptr:GetType().FullName end)))
            local Marshal = CS.System.Runtime.InteropServices.Marshal
            local ptrSize = safe(function() return Marshal.SizeOf(typeof(CS.System.IntPtr)) end)
            print("    IntPtr.Size: " .. tostring(ptrSize))

            -- Try various offsets to find where data starts
            print("    Scanning byte values at different offsets:")
            for offset = 0, 48, 4 do
                local b0 = safe(function() return Marshal.ReadByte(ptr, offset) end)
                local b1 = safe(function() return Marshal.ReadByte(ptr, offset+1) end)
                local b2 = safe(function() return Marshal.ReadByte(ptr, offset+2) end)
                local b3 = safe(function() return Marshal.ReadByte(ptr, offset+3) end)
                print(string.format("      offset %2d: [%s, %s, %s, %s]",
                    offset, tostring(b0), tostring(b1), tostring(b2), tostring(b3)))
            end

            -- Try ReadInt32 for array length at common offsets
            print("    Scanning int32 values (looking for length=3):")
            for offset = 0, 32, 4 do
                local v = safe(function() return Marshal.ReadInt32(ptr, offset) end)
                print(string.format("      int32 at %2d: %s", offset, tostring(v)))
            end
        else
            print("    Pointer not accessible from Lua")
        end
    end

    -- Also try .Tastes property (Span<byte>)
    print("  --- Trying .Tastes property (Span<byte>) ---")
    local span = safe(function() return pd.Tastes end)
    if span then
        print("    .Tastes type: " .. tostring(safe(function() return span:GetType().FullName end)))
        print("    span.Length: " .. tostring(safe(function() return span.Length end)))
        print("    span[0]: " .. tostring(safe(function() return span[0] end)))
        print("    span[1]: " .. tostring(safe(function() return span[1] end)))
        print("    span[2]: " .. tostring(safe(function() return span[2] end)))
    else
        print("    .Tastes: nil")
    end

    print("[TM] === END DEBUG ===")
end

print("[TM] Taste Monitor geladen!")
print("  tm_start()        - Start monitoring (take initial snapshot)")
print("  tm_tick()         - Poll for changes (call between conversation steps)")
print("  tm_stop()         - Stop and print full change log (auto-polls)")
print("  tm_snap()         - Print current snapshot")
print("  tm_probe()        - Deep-probe NPCData fields")
print("  tm_debug_player() - Debug PlayerData._tastes byte array access")
print("  tm_clear()        - Reset log")
