namespace MOC
{
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Jobs;

    [BurstCompile]
    public struct UpdateFillOffsetsJob : IJob
    {
        public int NumOccluders;
        [ReadOnly] public NativeArray<bool> CullingResults;
        [ReadOnly] public NativeArray<bool> OccluderFlags;
        [ReadOnly] public NativeArray<int> OccluderNumTri;
        [WriteOnly] public NativeArray<int> FillOffsets;
        [WriteOnly] public NativeArray<int> NumRasterizeTris;

        public void Execute()
        {
            var numRasterizeTris = 0;
            for (var i = 0; i < NumOccluders; i++)
            {
                if (!OccluderFlags[i] || CullingResults[i]) continue;
                FillOffsets[i] = numRasterizeTris;
                numRasterizeTris += OccluderNumTri[i];
            }
            NumRasterizeTris[0] = numRasterizeTris;
        }
    }
}