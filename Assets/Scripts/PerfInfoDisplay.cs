using MOC;
using TMPro;
using Unity.Burst;
using UnityEngine;
using static Unity.Burst.Intrinsics.Arm.Neon;
using static Unity.Burst.Intrinsics.X86.Sse;

public class PerfInfoDisplay : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI text;
    [SerializeField] private CullingSystem cullingSystem;
    private MaskedOcclusionCulling _msoc;

    private void Update()
    {
        if (_msoc == null && cullingSystem) _msoc = cullingSystem.GetMaskedOcclusionCulling();
        var deltaTime = Time.deltaTime;
        text.text = $"Cost Time: {deltaTime * 1000:F2}ms {1.0f / deltaTime:F2}fps" +
                    (!cullingSystem ? "" :
                        $"\nFC: {cullingSystem.costTimeFrustumCulling:F2}ms" + 
                        $"\nMSOC: {cullingSystem.costTimeMaskedOcclusionCulling:F2}ms") +
                    (_msoc == null ? "" :
                        $"\nOccluders: {_msoc.CostTimeOccluders:F2}ms" + 
                        $"\nOccludees: {_msoc.CostTimeOccludees:F2}ms" +
                        $"\nClear: {_msoc.CostTimeClear:F2}ms") +
                    $"\nSSE Support: {QuerySimdSupport.SupportSse()}" +
                    $"\nNeon Support: {QuerySimdSupport.SupportNeon()}" +
                    $"\nBuffer Size: {Constants.ScreenWidth}x{Constants.ScreenHeight}";
    }
}

[BurstCompile]
public static class QuerySimdSupport
{
    [BurstCompile]
    public static bool SupportSse()
    {
        return IsSseSupported;
    }

    [BurstCompile]
    public static bool SupportNeon()
    {
        return IsNeonSupported;
    }
}