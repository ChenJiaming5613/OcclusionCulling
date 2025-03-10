using MOC;
using TMPro;
using Unity.Burst;
using UnityEngine;
using static Unity.Burst.Intrinsics.Arm.Neon;
using static Unity.Burst.Intrinsics.X86.Sse;

public class PerfInfoDisplay : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI text;
    [SerializeField] private MaskedOcclusionCulling msoc;

    private void Update()
    {
        text.text = $"Cost Time: {Time.deltaTime * 1000:F2}ms" +
                    (msoc ? $"\nRaster Time: {msoc.rasterizeCostTime:F2}ms" : "") +
                    $"\nSSE Support: {QuerySimdSupport.SupportSse()}" +
                    $"\nNeon Support: {QuerySimdSupport.SupportNeon()}";
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