-- mods/touch_items_dump.lua
local GO = CS.UnityEngine.GameObject
local SceneManager = CS.UnityEngine.SceneManagement.SceneManager

local function dump_items(tag)
    print("[TouchItems] === Dump: " .. tag .. " ===")

    local scene = SceneManager.GetActiveScene()
    print(string.format("[TouchItems] ActiveScene: %s (handle=%d, rootCount=%d)",
        tostring(scene.name), scene.handle, scene.rootCount))

    -- Touch/Root finden
    local roots = scene:GetRootGameObjects()
    local touchRoot = nil
    for i = 0, roots.Length - 1 do
        local root = roots[i]
        if root ~= nil and root.name == "Touch" then
            local tRoot = root.transform:Find("Root")
            if tRoot ~= nil then
                touchRoot = tRoot.gameObject
                break
            end
        end
    end

    if touchRoot == nil then
        print("[TouchItems] Touch/Root not found")
        return
    end

    print(string.format("[TouchItems] Touch/Root active=%s activeInHierarchy=%s childCount=%d",
        tostring(touchRoot.activeSelf),
        tostring(touchRoot.activeInHierarchy),
        touchRoot.transform.childCount))

    for i = 0, touchRoot.transform.childCount - 1 do
        local child = touchRoot.transform:GetChild(i)
        if child ~= nil then
            local go = child.gameObject
            local name = go.name or "<nil>"
            if name:find("p_item_") == 1 then
                print(string.format(
                    "[TouchItems] %s active=%s activeSelf=%s activeInHierarchy=%s layer=%d",
                    name,
                    tostring(go.active),
                    tostring(go.activeSelf),
                    tostring(go.activeInHierarchy),
                    go.layer
                ))
            end
        end
    end
end

function TouchItemsDump(tag)
    dump_items(tag or "now")
end

print("[TouchItems] touch_items_dump.lua geladen")
