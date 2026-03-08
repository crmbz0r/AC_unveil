-- explore_record_conversation.lua
-- rec_start() | rec_stop() | rec_dump()

local function get_comm()
    local go = CS.UnityEngine.GameObject.Find("ExploreScene/Communication")
    if not go then go = CS.UnityEngine.GameObject.Find("Communication") end
    if not go then return nil end
    return go:GetComponent("CommunicationUI")
end

local function get_targets()
    local comm = get_comm()
    if not comm then return nil, "CommunicationUI nicht gefunden" end
    xlua.private_accessible(typeof(CS.AC.Scene.Explore.Communication.CommunicationUI))
    local ok, t = pcall(function() return comm._targets end)
    if not ok or t == nil then return nil, "_targets: " .. tostring(t) end
    return t, nil
end

-- Debug: alle bekannten Felder auf einem Objekt testen
local function probe(obj, label)
    local fields = {
        "FavorValue","RelationValue","Mood","Intimacy","LewdnessValue","Lewdness",
        "Sexperience","HCountValue","HCount","IsVirgin",
        "StudyApproachValue","SportApproachValue","FashionApproachValue",
        "Taste1","Taste2","Taste3","TasteValue",
        "TasteStudy","TasteSport","TasteFashion",
        "Study","Sport","Fashion",
        "Param1","Param2","Param3",
        "Name","CharaName","UniqueID","ID",
    }
    print("[PROBE] " .. label)
    for _, f in ipairs(fields) do
        local ok, v = pcall(function() return obj[f] end)
        if ok and v ~= nil then
            print("  ." .. f .. " = " .. tostring(v))
        end
    end
end

function rec_debug()
    local targets, err = get_targets()
    if not targets then print("[REC] " .. tostring(err)) return end
    local e = targets:GetEnumerator()
    if e:MoveNext() then
        local npc = e.Current
        print("[REC] type: " .. tostring(npc:GetType().FullName))
        probe(npc, "npc (Actor)")
        probe(npc.BaseData, "npc.BaseData")
        -- Sub-Objekte von BaseData nach Taste-Feldern
        for _, f in ipairs({"TasteData","Taste","ApproachData","Status","Param","Extra","UniqueData","SubData"}) do
            local ok, sub = pcall(function() return npc.BaseData[f] end)
            if ok and sub ~= nil then probe(sub, "npc.BaseData."..f) end
        end
        -- direkt Taste1/2/3 auf BaseData testen
        print("[PROBE] Taste candidates on BaseData:")
        for _, f in ipairs({"Taste1","Taste2","Taste3","TasteStudy","TasteSport","TasteFashion",
                            "StudyApproachValue","SportApproachValue","FashionApproachValue",
                            "ApproachValue","TasteValues","ApproachValues","_taste1","_taste2","_taste3"}) do
            local ok, v = pcall(function() return npc.BaseData[f] end)
            if ok and v ~= nil then print("  .BaseData."..f.." = "..tostring(v)) end
        end
    end
end

-- Snapshot
local _fields = nil  -- {favor, relation, mood, intimacy, lewdness, study, sport, fashion, src}

local function init_fields(npc)
    if _fields then return end
    _fields = {}
    -- BaseData ist die bestätigte Quelle für FavorValue etc.
    local src = npc.BaseData
    _fields.src = src
    -- Taste-Felder suchen
    for i, key in ipairs({"study","sport","fashion"}) do
        local suffixes = {"study","sport","fashion"}
        local capkey = suffixes[i]:sub(1,1):upper()..suffixes[i]:sub(2)
        local candidates = {
            "Taste"..i, "TasteValue"..i, "Taste"..i.."Value",
            capkey.."ApproachValue", "_taste"..i,
        }
        for _, f in ipairs(candidates) do
            local ok, val = pcall(function() return src[f] end)
            if ok and type(val)=="number" then
                print("[REC] "..key.." -> BaseData."..f.." = "..val)
                _fields[key]=f; break
            end
        end
        if not _fields[key] then print("[REC] WARN: "..key.." nicht gefunden") end
    end
end

-- AC.User.NPCData via SaveData.NPCDataList finden (wie CheatTools)
local function find_npcdata(actor)
    local ok, saveData = pcall(function() return CS.AC.Lua.EventTable.SaveData end)
    if not ok or saveData == nil then return nil end
    local ok2, list = pcall(function() return saveData.NPCDataList end)
    if not ok2 or list == nil then return nil end
    local actorBase = actor.BaseData
    local outer = list:GetEnumerator()
    while outer:MoveNext() do
        local inner = outer.Current
        if inner ~= nil then
            local ie = inner:GetEnumerator()
            while ie:MoveNext() do
                local nd = ie.Current
                if nd ~= nil then
                    local ok3, base = pcall(function() return nd.NPCInstance.BaseData end)
                    if ok3 and base == actorBase then return nd end
                end
            end
        end
    end
    return nil
end

local function snap_npc(npc)
    local name = "?"
    local ok, v = pcall(function() return npc.BaseData.HumanData.Parameter.fullname end)
    if ok and v then name = tostring(v) end
    local s = npc.BaseData
    local function g(f) local ok,v=pcall(function()return s[f]end); return ok and v or "?" end

    -- Taste-Werte via SaveData.NPCDataList
    local t1, t2, t3 = "?", "?", "?"
    local nd = find_npcdata(npc)
    if nd then
        local function gnd(f) local ok,v=pcall(function()return nd[f]end); return ok and v or "?" end
        t1 = gnd("StudyApproachValue")
        t2 = gnd("SportApproachValue")
        t3 = gnd("FashionApproachValue")
        if t1 == "?" then t1 = gnd("Taste1") end
        if t2 == "?" then t2 = gnd("Taste2") end
        if t3 == "?" then t3 = gnd("Taste3") end
    end

    return {
        name=name,
        favor   = g("FavorValue"),
        relation= g("RelationValue"),
        mood    = g("Mood"),
        intimacy= g("Intimacy"),
        lewdness= g("LewdnessValue"),
        study=t1, sport=t2, fashion=t3,
        _ref=s, _nd=nd,
    }
end

local function snapshot()
    local targets, err = get_targets()
    if not targets then return nil, err end
    local npcs = {}
    local e = targets:GetEnumerator()
    while e:MoveNext() do
        local t = e.Current
        if t ~= nil then table.insert(npcs, snap_npc(t)) end
    end
    return {npcs=npcs}
end

local function fmt(snap)
    if #snap.npcs==0 then return "  (keine NPCs)" end
    local lines = {}
    for _, n in ipairs(snap.npcs) do
        table.insert(lines, string.format(
            "  %-14s favor=%-4s rel=%-3s mood=%-3s study=%-4s sport=%-4s fashion=%-4s int=%-3s lewd=%-3s",
            n.name, tostring(n.favor), tostring(n.relation), tostring(n.mood),
            tostring(n.study), tostring(n.sport), tostring(n.fashion),
            tostring(n.intimacy), tostring(n.lewdness)))
    end
    return table.concat(lines, "\n")
end

local function diff(b, a)
    local out = {}
    for i, nb in ipairs(b.npcs) do
        local na = a.npcs[i]
        if na then
            for _, f in ipairs({"favor","relation","mood","study","sport","fashion","intimacy","lewdness"}) do
                if type(nb[f])=="number" and type(na[f])=="number" and na[f]~=nb[f] then
                    table.insert(out, string.format("  %-14s %-12s %+d  (%d -> %d)",
                        nb.name, f, na[f]-nb[f], nb[f], na[f]))
                end
            end
        end
    end
    return #out>0 and table.concat(out,"\n") or "  (keine Änderungen)"
end

_rec_before=nil; _rec_after=nil; _fields=nil

function rec_start()
    local s, err = snapshot()
    if not s then print("[REC] Fehler: "..tostring(err)) return end
    _rec_before = s
    print("[REC] ▶ "..#s.npcs.." NPC(s)"); print(fmt(s))
end

function rec_stop()
    if not _rec_before then print("[REC] rec_start() zuerst!") return end
    local s, err = snapshot()
    if not s then print("[REC] Fehler: "..tostring(err)) return end
    print("[REC] ■ DIFF:"); print(diff(_rec_before, s))
    print("NACHHER:"); print(fmt(s))
    _rec_after=s; _rec_before=nil
end

function rec_dump()
    if _rec_after then print(fmt(_rec_after)) else print("[REC] Kein Snapshot.") end
end

print("[REC] Geladen!  rec_start() | rec_stop() | rec_dump() | rec_debug()")