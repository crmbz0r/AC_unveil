-- ═════════════════════════════════════════════════════════════════════════════
--  touch_monitor.lua  v3  (C# hook-backed, IL2CPP-safe value unboxing)
--
--  USAGE:
--    dofile(MOD_PATH .. "touch_monitor.lua")
--
--  Commands while in the Touch scene:
--    ttm()             -- full snapshot of all captured values
--    ttm_diff()        -- print ONLY values that changed since last call
--    ttm_diff_reset()  -- reset the diff baseline
--    ttm_info()        -- SystemData: vigilance / probability tables
--    ttm_history()     -- _contactHistory: recent hit log
-- ═════════════════════════════════════════════════════════════════════════════

local H = CS.AiComi_LuaMod.TouchMonitorHooks

-- ── IL2CPP-safe unboxing helpers ──────────────────────────────────────────────
-- xLua IL2CPP returns C# value-type properties as boxed userdata, not Lua primitives.
-- Always go through tostring() first, then convert.
local function n(v)  return tonumber(tostring(v)) or 0  end  -- C# numeric  → Lua number
local function b(v)  return tostring(v) == "True"       end  -- C# bool     → Lua bool
local function ss(v) return tostring(v)                 end  -- any         → Lua string

-- ── Format helpers ────────────────────────────────────────────────────────────
local function pct(v)
    local nv = n(v)
    if nv == -1 then return "n/a" end
    return string.format("%5.1f%%", nv * 100)
end
local function f4(v)   return string.format("%.4f", n(v)) end

-- Enum label: tbl[int] = "Name"
local function el(tbl, raw)
    local ni = n(raw)
    return string.format("%s(%d)", tbl[ni] or "?", ni)
end

-- ── Enum label tables ─────────────────────────────────────────────────────────
local AREA_KIND = {
    [-1] = "None",
    [0]  = "BigSuccess",
    [1]  = "Success",
    [2]  = "Failure",
    [3]  = "BigFailure",
}
local PARTS_KIND = {
    [0]  = "None",
    [1]  = "Hit_Mouth",
    [2]  = "Hit_MuneL",
    [3]  = "Hit_MuneR",
    [4]  = "Hit_Kokan",
    [5]  = "Hit_Anal",
    [6]  = "Hit_SiriL",
    [7]  = "Hit_SiriR",
    [8]  = "Hit_ShoulderL",
    [9]  = "Hit_ShoulderR",
    [10] = "Reaction_Head",
    [11] = "Reaction_BodyUp",
    [12] = "Reaction_BodyDown",
    [13] = "Reaction_ArmL",
    [14] = "Reaction_ArmR",
    [15] = "Reaction_LegL",
    [16] = "Reaction_LegR",
}
local NO_TOUCH_KIND = { [0] = "None", [1] = "Kiss", [2] = "NoTouch" }

-- Decode pipe-separated AreaKind string e.g. "1|2|0|-1"
local function decode_areas(str)
    if str == "" or str == nil then return "(empty)" end
    local s = tostring(str)
    if s == "" then return "(empty)" end
    local parts = {}
    local i = 0
    for v in s:gmatch("[^|]+") do
        local ni = tonumber(v)
        parts[#parts+1] = string.format("[%d]%s", i, AREA_KIND[ni] or v)
        i = i + 1
    end
    return table.concat(parts, "  ")
end

-- ── State ─────────────────────────────────────────────────────────────────────
local _prev = {}

-- ── ttm – full snapshot ───────────────────────────────────────────────────────
function ttm()
    if not b(H.Captured) then
        print("[TTM] No data yet - touch the character to trigger the first capture.")
        print("      (Every touch/drag/miss update fires the C# patch)")
        return
    end
    print("==== Touch Snapshot  [snap#" .. n(H.SnapCount) .. "] ====")
    print(string.format("  GAUGE Miss : %-8s   Pleasure : %s", pct(H.MissGauge), pct(H.PleasureGauge)))
    print(string.format("  MissOver   : %s   GaugeUp : %s   GaugeDown : %s",
        ss(H.MissOver), f4(H.GaugeUpSpeed), f4(H.GaugeDownSpeed)))
    print("  ------------------------------------------------")
    print(string.format("  IsHit  : %-6s   IsDrag : %-6s   IsKiss : %s",
        ss(H.IsHit), ss(H.IsDrag), ss(H.IsKiss)))
    print(string.format("  HitPart: %-22s  NoTouch : %s",
        el(PARTS_KIND, H.NowHitParts), el(NO_TOUCH_KIND, H.NoTouch)))
    print(string.format("  MainPt : %d", n(H.MainPoint)))
    print("  ------------------------------------------------")
    print("  AreaKind    : " .. decode_areas(H.AreaKindStr))
    print("  AreaProb    : " .. ss(H.AreaProbStr))
    print("  Touched     : " .. ss(H.TouchedAreaStr))
    print("  ------------------------------------------------")
    print(string.format("  Last AddMissGauge  area=%d  returned=%s",
        n(H.LastMissArea), ss(H.LastMissHit)))
    print("  ------------------------------------------------")
    print(string.format("  MotionParam : %s   SpeedBody : %s   SpeedHand : %s",
        f4(H.MotionParam), f4(H.SpeedBody), f4(H.SpeedHand)))
    print(string.format("  Corrections : %s | %s | %s | %s",
        f4(H.Correction0), f4(H.Correction1), f4(H.Correction2), f4(H.Correction3)))
    print(string.format("  IsOrgasm : %-6s   OrgasmCnt : %d",
        ss(H.IsOrgasm), n(H.OrgasmCount)))
    print("================================================")
end

-- ── ttm_diff – only changed values ───────────────────────────────────────────
function ttm_diff()
    if not b(H.Captured) then
        print("[TTM] No data yet - touch the character first.") return
    end

    local changed = false
    local snap = n(H.SnapCount)

    local function chk(key, cur)
        local cs = tostring(cur)
        if _prev[key] ~= cs then
            print(string.format("  D %-22s  %s  ->  %s", key, _prev[key] or "?", cs))
            _prev[key] = cs
            changed = true
        end
    end

    chk("MissGauge",    pct(H.MissGauge))
    chk("PleasureGauge",pct(H.PleasureGauge))
    chk("MissOver",     ss(H.MissOver))
    chk("IsHit",        ss(H.IsHit))
    chk("IsDrag",       ss(H.IsDrag))
    chk("IsKiss",       ss(H.IsKiss))
    chk("NowHitParts",  el(PARTS_KIND,    H.NowHitParts))
    chk("NoTouch",      el(NO_TOUCH_KIND, H.NoTouch))
    chk("MainPoint",    n(H.MainPoint))
    chk("AreaKind",     ss(H.AreaKindStr))
    chk("TouchedArea",  ss(H.TouchedAreaStr))
    chk("LastMissArea", n(H.LastMissArea))
    chk("LastMissHit",  ss(H.LastMissHit))
    chk("MotionParam",  f4(H.MotionParam))
    chk("IsOrgasm",     ss(H.IsOrgasm))
    chk("OrgasmCount",  n(H.OrgasmCount))

    if changed then
        print(string.format("  -> snap#%d  Miss=%s  Pleasure=%s",
            snap, pct(H.MissGauge), pct(H.PleasureGauge)))
    end
end

function ttm_diff_reset()
    _prev = {}
    print("[TTM] Diff baseline reset  (snap#" .. n(H.SnapCount) .. ")")
end

-- ── ttm_info - SystemData via FindObjectOfType ────────────────────────────────
function ttm_info()
    local ok, err = pcall(function()
        local tc = CS.UnityEngine.Object.FindObjectOfType(
            typeof(CS.AC.Scene.Touch.TouchController))
        if tc == nil then print("[TTM] TouchController not found") return end

        local sd = tc._systemData
        if sd == nil then print("[TTM] _systemData is nil") return end

        print("==== SystemData ====")
        print("  CharaProbability  : " .. tostring(sd.CharaProbability))
        print("  VigilanceProbMin  : " .. tostring(sd.VigilanceProbabilityMin))
        print("  VigilanceProbMax  : " .. tostring(sd.VigilanceProbabilityMax))
        print("  RateClickGauge    : " .. tostring(sd.RateClickGauge))
        print("  RateDragGauge     : " .. tostring(sd.RateDragGauge))
        print("  MultiCorrection   : " .. tostring(sd.MultiCorrection))

        local function tryArr(label, arr)
            if arr == nil then print("  " .. label .. " : nil") return end
            local parts = {}
            for i = 0, arr.Length - 1 do parts[i+1] = tostring(arr[i]) end
            print("  " .. label .. " : {" .. table.concat(parts, ", ") .. "}")
        end
        tryArr("VigilanceProbArr  ", sd.VigilanceProbability)
        tryArr("StartVigilance    ", sd.StartVigilance)
        tryArr("BaseProbability   ", sd.BaseProbability)
        tryArr("CorrectionProb    ", sd.CorrectionProbability)
        tryArr("AreaCorrection    ", sd.AreaCorrection)

        local pp = sd.PartsProbability
        if pp ~= nil then
            print("  PartsProbability  : (row=point, col=area zone)")
            for i = 0, pp.Length - 1 do
                local row = pp[i]
                if row ~= nil then
                    local p = {}
                    for j = 0, row.Length - 1 do p[j+1] = tostring(row[j]) end
                    print(string.format("    row[%d]: {%s}", i, table.concat(p, ", ")))
                end
            end
        end
        print("====================")
    end)
    if not ok then print("[TTM] Error: " .. tostring(err)) end
end

-- ── ttm_history - _contactHistory log ────────────────────────────────────────
function ttm_history()
    local ok, err = pcall(function()
        local tc = CS.UnityEngine.Object.FindObjectOfType(
            typeof(CS.AC.Scene.Touch.TouchController))
        if tc == nil then print("[TTM] TouchController not found") return end

        local hist = tc._contactHistory
        if hist == nil then print("[TTM] _contactHistory is nil") return end

        local cnt = tostring(hist.Count)
        print("==== ContactHistory (" .. cnt .. " entries) ====")
        local e = hist:GetEnumerator()
        local idx = 0
        while e:MoveNext() do
            local t = e.Current
            -- ValueTuple<bool,int,int>: Item1=wasHit, Item2=point, Item3=area/value
            print(string.format("  [%02d]  hit=%-5s  point=%-3s  area=%s",
                idx, tostring(t.Item1), tostring(t.Item2), tostring(t.Item3)))
            idx = idx + 1
            if idx >= 50 then print("  ... (capped at 50)") break end
        end
        print("============================================")
    end)
    if not ok then print("[TTM] Error: " .. tostring(err)) end
end

-- ── Ready ─────────────────────────────────────────────────────────────────────
print("============================================================")
print("[TTM] touch_monitor.lua v3 loaded  (C# hook-backed)")
print("  ttm()             full snapshot (fires after first touch)")
print("  ttm_diff()        show only changed values")
print("  ttm_diff_reset()  reset diff baseline")
print("  ttm_info()        SystemData (vigilance / probabilities)")
print("  ttm_history()     _contactHistory hit log")
print("============================================================")
print("[TTM] snap#" .. n(H.SnapCount) .. "  captured=" .. tostring(b(H.Captured)))

function ttm_force()
    local ok, tc = pcall(function()
        return CS.UnityEngine.Object.FindObjectOfType(typeof(CS.AC.Scene.Touch.TouchController))
    end)
    if not ok or tc == nil then
        print("[TTM] TouchController nicht gefunden")
        return
    end

    xlua.private_accessible(typeof(CS.AC.Scene.Touch.TouchController))

    print("[TTM] TouchController direkt ausgelesen:")
    local fields = {
        "_missOver","_gaugeUpSpeed","_gaugeDownSpeed","_motionParam",
        "_speedBody","_speedHand","_isHit","_isDrag","_isKiss",
        "_nowHitParts","_noTouch","_mainPoint","_isOrgasm","_orgasmCount",
    }
    for _, f in ipairs(fields) do
        local ok2, v = pcall(function() return tc[f] end)
        if ok2 and v ~= nil then print("  ." .. f .. " = " .. tostring(v)) end
    end

    -- Slider values
    local ok3, sm = pcall(function() return tc._sliderMiss end)
    if ok3 and sm then print("  ._sliderMiss.value = " .. tostring(sm.value)) end
    local ok4, sf = pcall(function() return tc._sliderF end)
    if ok4 and sf then print("  ._sliderF.value = " .. tostring(sf.value)) end
end