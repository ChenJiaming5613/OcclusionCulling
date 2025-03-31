using MOC;
using TMPro;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Burst.Intrinsics.Arm.Neon;
using static Unity.Burst.Intrinsics.X86.Sse;

public class PerfInfoDisplay : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI text;
    [SerializeField] private CullingSystem cullingSystem;
    [SerializeField] private int maxCount = -1;
    private MaskedOcclusionCulling _msoc;
    private int _count;
    private Indicator _fcIndicator;
    private Indicator _msocIndicator;
    private Indicator _occludersIndicator;
    private Indicator _occludeesIndicator;
    private Indicator _clearIndicator;
    private Counter _fcCounter;
    private Counter _msocCounter;
    // private Counter _totalTriCounter;
    // private Counter _clippedTriCounter;

    private void Update()
    {
        if (_msoc == null && cullingSystem) _msoc = cullingSystem.GetMaskedOcclusionCulling();
        if (_msoc != null && (_count < maxCount || maxCount == -1))
        {
            var statData = cullingSystem.StatData;
            if (statData.FrustumCullingCount == -1) return;
            var msocStatData = _msoc.StatData;
            _fcIndicator.Tick(statData.CostTimeFrustumCulling);
            _msocIndicator.Tick(statData.CostTimeMaskedOcclusionCulling);
            _occludersIndicator.Tick(msocStatData.CostTimeOccluders);
            _occludeesIndicator.Tick(msocStatData.CostTimeOccludees);
            _clearIndicator.Tick(msocStatData.CostTimeClear);
            _fcCounter.Tick(statData.FrustumCullingCount);
            _msocCounter.Tick(statData.MaskedOcclusionCullingCount);
            // _totalTriCounter.Tick(msocStatData.TotalTriCount);
            // _clippedTriCounter.Tick(msocStatData.ClippedTriCount);
            _count++;
        }
        
        var deltaTime = Time.deltaTime;
        text.text = $"Cost Time: {deltaTime * 1000:F2}ms {1.0f / deltaTime:F2}fps" +
                    $"\nSSE Support: {QuerySimdSupport.SupportSse()}" +
                    $"\nNeon Support: {QuerySimdSupport.SupportNeon()}" +
                    $"\nBuffer Size: {Constants.ScreenWidth}x{Constants.ScreenHeight}" +
                    "\n==========================" +
                    $"{GetFCStatStr()}" +
                    "\n==========================" +
                    $"{GetMsocStatStr()}";
    }

    private string GetFCStatStr()
    {
        if (!cullingSystem) return "";
        return $"\nTotal Instance: {cullingSystem.StatData.TotalObjectCount}" +
               $"\nFC: {_fcIndicator.GetStatusStr()}" +
               $"\nFC Culled: {_fcCounter.GetStatusStr()}";
    }

    private string GetMsocStatStr()
    {
        if (_msoc == null) return "";
        return $"\nMSOC: {_msocIndicator.GetStatusStr()}" +
               $"\nOccluders: {_occludersIndicator.GetStatusStr()}" +
               $"\nOccludees: {_occludeesIndicator.GetStatusStr()}" +
               $"\nClear: {_clearIndicator.GetStatusStr()}" +
               $"\nMSOC Culled: {_msocCounter.GetStatusStr()}" +
               // $"\nMSOC Total Tri: {_totalTriCounter.GetStatusStr()}" +
               // $"\nMSOC Clipped Tri: {_clippedTriCounter.GetStatusStr()}" +
               $"\nCount: {_count}";
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

public struct Indicator
{
    private float _avgTime;
    private float _currTime;
    private int _count;

    public void Tick(float currTime)
    {
        _currTime = currTime;
        if (_count == 0)
        {
            _avgTime = currTime;
            _count = 1;
            return;
        }
        _avgTime = (_avgTime * _count + currTime) / (_count + 1);
        _count++;
    }

    public string GetStatusStr()
    {
        return $"Curr:{_currTime:F2}ms Avg:{_avgTime:F2}ms";
    }
}

public struct Counter
{
    private float _avgCount;
    private int _maxCount;
    private int _minCount;
    private int _currCount;
    private int _count;
    
    public void Tick(int currCount)
    {
        _currCount = currCount;
        if (_count == 0)
        {
            _avgCount = _maxCount = _minCount = currCount;
            _count = 1;
            return;
        }
        _maxCount = math.max(_maxCount, currCount);
        _minCount = math.min(_minCount, currCount);
        _avgCount = (_avgCount * _count + currCount) / (_count + 1);
        _count++;
    }

    public string GetStatusStr()
    {
        return $"Curr:{_currCount} Avg:{_avgCount:F2} Max:{_maxCount} Min:{_minCount}";
    }
}