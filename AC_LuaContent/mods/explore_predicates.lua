-- explore_predicates.lua - Runtime exploration of _predicates
xlua.private_accessible(typeof(CS.AC.ParameterContainer))

local game = CS.UnityEngine.GameObject.FindObjectOfType(typeof(CS.Manager.Game))
if not game then print("[WARN] Manager.Game not found") return end

local pc = game.ParameterContainer
if not pc then print("[WARN] ParameterContainer is nil") return end

local predicates = pc._predicates
if not predicates then print("[WARN] _predicates is nil") return end

print("=== _predicates (" .. predicates.Count .. ") ===")

local keys = predicates.Keys
local e = keys:GetEnumerator()
local count = 0
local ok_count = 0
local err_count = 0

while e:MoveNext() do
    local key = e.Current
    count = count + 1
    local ok, result = pcall(function()
        return pc:ValidateConditionStraight(key)
    end)
    if ok then
        ok_count = ok_count + 1
        print(string.format("  [%3d] %-40s = %s", count, key, tostring(result)))
    else
        err_count = err_count + 1
        -- Nur kurze Fehlermeldung
        print(string.format("  [%3d] %-40s = [needs context]", count, key))
    end
end

print(string.format("=== End: %d ok, %d need context ===", ok_count, err_count))