using Unity.Mathematics;
using UnityEngine;

namespace MOC
{
    [CreateAssetMenu(menuName = "MOC/MocConfigAsset")]
    public class MocConfigAsset : ScriptableObject
    {
        public int numBinCols = 4;
        public int numBinRows = 1;
        public int depthBufferWidth = 960;
        public int depthBufferHeight = 540;
        public float coverageThreshold = 0.1f;
        public bool asyncRasterizeOccluders;
        public int maxNumRasterizeTris = 6000;
        public int rayMarchingDownSampleCount = 3;

        [Tooltip("StartStep, MaxStep, IncrStep")]
        public float3 rayMarchingStep = new(0.5f, 3.0f, 0.5f);
    }
}