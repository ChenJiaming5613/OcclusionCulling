using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace MOC
{
    [BurstCompile]
    public struct UpdateMatricesJob : IJob
    {
        public int NumOccluders;
        public float4x4 VpMatrix;
        [ReadOnly] public NativeSlice<bool> OccluderCullingResults;
        [ReadOnly] public NativeArray<int> OccluderNumTri;
        [ReadOnly] public NativeArray<float4x4> ModelMatrices;
        [WriteOnly] public NativeArray<int> FillOffsets;
        [WriteOnly] public NativeArray<float4x4> MvpMatrices;
        [WriteOnly] public NativeArray<int> NumTotalTris;

        public void Execute()
        {
            var numTotalTris = 0;
            for (var i = 0; i < NumOccluders; i++)
            {
                if (OccluderCullingResults[i]) continue;
                FillOffsets[i] = numTotalTris;
                numTotalTris += OccluderNumTri[i];
                MvpMatrices[i] = math.mul(VpMatrix, ModelMatrices[i]);
                // MvpMatrices[i] = VpMatrix * ModelMatrices[i]; // Error!
            }
            NumTotalTris[0] = numTotalTris;
        }
    }
}