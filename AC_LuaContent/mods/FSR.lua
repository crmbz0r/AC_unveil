print("=== FSR SHADER SCAN ===")
local shaders = CS.UnityEngine.Resources.FindObjectsOfTypeAll(CS.UnityEngine.Shader)
for i=0,shaders.Length-1 do
    local name = shaders[i].name:lower()
    if name:find("fsr") or name:find("fidelity") or name:find("upscale") then
        print("SHADER:", shaders[i].name)
    end
end

print("=== FSR MATERIAL SCAN ===")
local mats = CS.UnityEngine.Resources.FindObjectsOfTypeAll(CS.UnityEngine.Material)
for i=0,mats.Length-1 do
    local name = mats[i].name:lower()
    if name:find("fsr") or mats[i].shader.name:lower():find("fsr") then
        print("MAT:", mats[i].name, "Shader:", mats[i].shader.name)
        -- Test Hack
        mats[i]:SetFloat("_RenderScale", 0.5)
        mats[i]:SetFloat("_Sharpness", 0.8)
        print("  → HACKED!")
    end
end
