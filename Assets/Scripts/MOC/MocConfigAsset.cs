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
    }
}