-- =============================================================
--  AiComi Lua Mod
--  Wird via BepInEx Plugin -> ParameterContainer._luaEnv.DoString() ausgeführt
-- =============================================================

local util = require 'xlua.util'
local MOD_NAME = 'MeinMod v0.1'
-- MOD_PATH wird automatisch von C# gesetzt
function run(filename)
    local f = io.open(MOD_PATH .. filename, "r")
    if not f then print("[ERROR] File not found: " .. filename) return end
    local code = f:read("*a")
    f:close()
    local fn, err = load(code)
    if not fn then print("[ERROR] " .. tostring(err)) return end
    fn()
end
local function log(msg)
    print('[' .. MOD_NAME .. '] ' .. tostring(msg))
end

-- =============================================================
--  EventTable: alle Events freischalten
-- =============================================================

local function unlock_all_events()
    local em = CS.AC.Lua.EventTable.EventMemory

    -- Type 0: Communication (0-6, plus Special 300)
    for i = 0, 6 do em:Add(i) end
    em:Add(300)

    -- Type 1: Heroine (30-69)
    for i = 30, 69 do em:Add(i) end

    -- Type 2: H-Events – jeweils Ende jeder Kette
    -- 100 standalone, 101->102->103, 104->105->106, 107->108->109, 110 standalone
    for i = 100, 110 do em:Add(i) end

    -- Type 4: Field/Date – jeweils Ende jeder Kette
    -- Route A endet bei 230, Route B bei 231, Route C bei 232, 240 standalone
    for i = 200, 206 do em:Add(i) end
    for i = 208, 218 do em:Add(i) end
    for i = 221, 223 do em:Add(i) end
    em:Add(230) em:Add(231) em:Add(232) em:Add(240)

    log('Alle Events freigeschaltet! EventMemory Count: ' .. tostring(em.Count))
end

-- =============================================================
--  Szenenbaum erkunden
-- =============================================================

local function dump_scene()
    local scene = CS.UnityEngine.SceneManagement.SceneManager.GetActiveScene()
    log('Aktive Szene: ' .. scene.name)
    local roots = scene:GetRootGameObjects()
    for i = 0, roots.Length - 1 do
        log('  Root GO: ' .. roots[i].name)
    end
end

-- =============================================================
--  EventMemory Status
-- =============================================================

local function dump_events()
    local em = CS.AC.Lua.EventTable.EventMemory
    log('EventMemory Einträge: ' .. tostring(em.Count))
    local enumerator = em:GetEnumerator()
    while enumerator:MoveNext() do
        log('  EventID: ' .. tostring(enumerator.Current))
    end
end

-- =============================================================
--  Init – hier entscheiden was ausgeführt wird
-- =============================================================

log('Mod geladen!')
dump_scene()
dump_events()

-- ⬇ Auskommentieren um alle Events freizuschalten:
-- unlock_all_events()

log('Bereit.')