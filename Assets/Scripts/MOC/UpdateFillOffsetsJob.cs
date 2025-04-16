namespace MOC
{
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Jobs;

    [BurstCompile]
    public struct UpdateFillOffsetsJob : IJob
    {
        public int NumOccluders;
        public int MaxNumRasterizeTris;
        // [ReadOnly] public NativeArray<bool> CullingResults;
        // [ReadOnly] public NativeArray<bool> OccluderFlags;
        [ReadOnly] public NativeArray<int> OccluderNumTri;
        [WriteOnly] public NativeArray<int> FillOffsets;
        [WriteOnly] public NativeArray<int> NumRasterizeTris;
        public NativeArray<OccluderSortInfo> OccluderInfos;

        public void Execute()
        {
            OccluderInfos.Sort(new OccluderSortInfoComparer());
            
            var numRasterizeTris = 0;
            for (var i = 0; i < NumOccluders; i++)
            {
                var occluderInfo = OccluderInfos[i];
                if (occluderInfo.Coverage == 0f) break; // 因为已经排序了
                // if (!OccluderFlags[i] || CullingResults[i]) continue;
                var idx = occluderInfo.Idx;
                FillOffsets[idx] = numRasterizeTris;
                var numTri = OccluderNumTri[idx];
                if (numRasterizeTris + numTri > MaxNumRasterizeTris)
                {
                    numTri = MaxNumRasterizeTris - numRasterizeTris;
                }
                numRasterizeTris += numTri;
                occluderInfo.NumRasterizeTris = numTri;
                OccluderInfos[i] = occluderInfo;
            }
            NumRasterizeTris[0] = numRasterizeTris;
        }
    }
}