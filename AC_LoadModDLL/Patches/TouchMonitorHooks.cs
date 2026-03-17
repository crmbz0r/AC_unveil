using System.Text;
using AC.Scene.Touch;
using HarmonyLib;
using UnityEngine;

namespace AiComi_LuaMod;

// ─────────────────────────────────────────────────────────────
//  Touch Monitor Hooks
//  Captures private TouchController fields into public statics
//  so Lua can read them via CS.AiComi_LuaMod.TouchMonitorHooks
// ─────────────────────────────────────────────────────────────
[HarmonyPatch]
public static class TouchMonitorHooks
{
    // ── Snapshot metadata ─────────────────────────────────────
    public static bool Captured { get; private set; }
    public static int SnapCount { get; private set; }

    // ── Gauges ────────────────────────────────────────────────
    public static float MissGauge { get; private set; }
    public static float PleasureGauge { get; private set; }
    public static bool MissOver { get; private set; }
    public static float GaugeUpSpeed { get; private set; }
    public static float GaugeDownSpeed { get; private set; }

    // ── Touch state ───────────────────────────────────────────
    public static bool IsHit { get; private set; }
    public static bool IsDrag { get; private set; }
    public static bool IsKiss { get; private set; }
    public static int NowHitParts { get; private set; }
    public static int NoTouch { get; private set; }
    public static int MainPoint { get; private set; }

    // ── Motion ────────────────────────────────────────────────
    public static float MotionParam { get; private set; }
    public static float SpeedBody { get; private set; }
    public static float SpeedHand { get; private set; }

    // ── Correction values ─────────────────────────────────────
    public static float Correction0 { get; private set; }
    public static float Correction1 { get; private set; }
    public static float Correction2 { get; private set; }
    public static float Correction3 { get; private set; }

    // ── Other ─────────────────────────────────────────────────
    public static bool IsOrgasm { get; private set; }
    public static int OrgasmCount { get; private set; }

    // ── Last AddMissGauge call ────────────────────────────────
    public static int LastMissArea { get; private set; } = -99;
    public static bool LastMissHit { get; private set; }

    // ── Arrays as pipe-separated strings ─────────────────────
    // AreaKind values: None=-1, BigSuccess=0, Success=1, Failure=2, BigFailure=3
    public static string AreaKindStr { get; private set; } = "";
    public static string AreaProbStr { get; private set; } = "";
    public static string TouchedAreaStr { get; private set; } = "";

    // ── Capture ───────────────────────────────────────────────
    private static void CaptureState(TouchController tc)
    {
        if (tc == null)
            return;
        Captured = true;
        MissGauge = tc._sliderMiss != null ? tc._sliderMiss.value : -1f;
        PleasureGauge = tc._sliderF != null ? tc._sliderF.value : -1f;
        MissOver = tc._missOver;
        GaugeUpSpeed = tc._gaugeUpSpeed;
        GaugeDownSpeed = tc._gaugeDownSpeed;
        IsHit = tc._isHit;
        IsDrag = tc._isDrag;
        IsKiss = tc._isKiss;
        NowHitParts = (int)tc._nowHitParts;
        NoTouch = (int)tc._noTouch;
        MainPoint = tc._mainPoint;
        MotionParam = tc._motionParam;
        SpeedBody = tc._speedBodyParam;
        SpeedHand = tc._speedHandParam;
        Correction0 = tc._correctionValue0;
        Correction1 = tc._correctionValue1;
        Correction2 = tc._correctionValue2;
        Correction3 = tc._correctionValue3;
        IsOrgasm = tc._isOrgasm;
        OrgasmCount = tc._orgasmCount;

        var ak = tc._areaKind;
        if (ak != null)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < ak.Length; i++)
            {
                if (i > 0)
                    sb.Append('|');
                sb.Append((int)ak[i]);
            }
            AreaKindStr = sb.ToString();
        }
        var ap = tc._areaProbability;
        if (ap != null)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < ap.Length; i++)
            {
                if (i > 0)
                    sb.Append('|');
                sb.Append(ap[i]);
            }
            AreaProbStr = sb.ToString();
        }
        var ta = tc._touchedArea;
        if (ta != null)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < ta.Length; i++)
            {
                if (i > 0)
                    sb.Append('|');
                sb.Append(ta[i] ? 'T' : '.');
            }
            TouchedAreaStr = sb.ToString();
        }
        SnapCount++;
    }

    // ── Patches ───────────────────────────────────────────────
    [HarmonyPostfix, HarmonyPatch(typeof(TouchController), nameof(TouchController.AddMissGauge))]
    private static void OnAddMiss(TouchController __instance, int area, bool __result)
    {
        LastMissArea = area;
        LastMissHit = __result;
        CaptureState(__instance);
    }

    [HarmonyPostfix, HarmonyPatch(typeof(TouchController), nameof(TouchController.SubMisssGauge))]
    private static void OnSubMiss(TouchController __instance) => CaptureState(__instance);

    [HarmonyPostfix, HarmonyPatch(typeof(TouchController), nameof(TouchController.TouchPart))]
    private static void OnTouchPart(TouchController __instance) => CaptureState(__instance);

    [HarmonyPostfix, HarmonyPatch(typeof(TouchController), "Update")]
    private static void OnUpdate(TouchController __instance) => CaptureState(__instance);

    public static void ForceCapture()
    {
        var tc = UnityEngine.Object.FindObjectOfType<TouchController>();
        if (tc != null)
            CaptureState(tc);
        else
            Plugin.Log.LogWarning("[TouchMonitor] ForceCapture: TouchController not found");
    }
}
