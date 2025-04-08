using System;
using Unity.Mathematics;

namespace MOC
{
    [Serializable]
    public struct Tile // contain 1x4 SubTiles
    {
        public float4 z;
        public uint4 bitmask;
    }
}