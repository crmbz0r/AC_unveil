-- mods/comm_cursor_dump.lua
-- Dump CommunicationUI cursor types (_defaultCursor, _cursorTypes)

local GO          = CS.UnityEngine.GameObject
local Resources   = CS.UnityEngine.Resources
local CommUIType  = typeof(CS.AC.Scene.Explore.Communication.CommunicationUI)
local CursorTypeSO = CS.CodeMonkey.CursorSystemPro.CursorTypeSO

local function dump_comm_cursor(tag)
    tag = tag or "now"
    print("[CommCursor] === Dump: " .. tag .. " ===")

    -- CommunicationUI suchen
    local comm = Resources.FindObjectsOfTypeAll(CommUIType)
    if comm == nil or comm.Length == 0 then
        print("[CommCursor] CommunicationUI not found")
        return
    end
    local ui = comm[0]
    if ui == nil then
        print("[CommCursor] ui[0] is nil")
        return
    end

    -- intern zugreifen
    xlua.private_accessible(CommUIType)

    -- _defaultCursor
    local ok, defCur = pcall(function() return ui._defaultCursor end)
    if ok and defCur ~= nil then
        print(string.format("[CommCursor] defaultCursor: %s", tostring(defCur.name)))
    else
        print("[CommCursor] defaultCursor: <nil or inaccessible>")
    end

    -- _cursorTypes
    local ok2, arr = pcall(function() return ui._cursorTypes end)
    if not ok2 or arr == nil then
        print("[CommCursor] _cursorTypes: <nil or inaccessible>")
        return
    end

    print(string.format("[CommCursor] _cursorTypes Length = %d", arr.Length))
    for i = 0, arr.Length - 1 do
        local ct = arr[i]
        if ct ~= nil then
            local n = ct.name or ("<no name #" .. i .. ">")
            local valid = false
            local frames = -1
            local okv, isValid = pcall(function() return ct:IsValid() end)
            if okv and isValid then
                valid = true
                local okf, fc = pcall(function() return ct:GetFrameCount() end)
                if okf then frames = fc end
            end
            print(string.format(
                "[CommCursor] [%02d] %s valid=%s frames=%d",
                i, n, tostring(valid), frames
            ))
        else
            print(string.format("[CommCursor] [%02d] <nil>", i))
        end
    end
end

function CommCursorDump(tag)
    dump_comm_cursor(tag or "now")
end

print("[CommCursor] comm_cursor_dump.lua geladen")
