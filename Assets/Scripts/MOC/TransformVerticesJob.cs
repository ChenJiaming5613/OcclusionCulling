﻿using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace MOC
{
    [BurstCompile]
    public struct TransformVerticesJob : IJobParallelFor
    {
        public int DepthBufferWidth;
        public int DepthBufferHeight;
        [ReadOnly] public NativeArray<float3> LocalSpaceVertices;
        [ReadOnly] public NativeArray<int> LocalSpaceVertexOffsets;
        [ReadOnly] public NativeArray<int> Indices;
        [ReadOnly] public NativeArray<int> IndexOffsets;
        [ReadOnly] public NativeArray<float4x4> MvpMatrices;
        [ReadOnly] public NativeArray<int> FillOffsets;
        // [ReadOnly] public NativeArray<bool> CullingResults;
        // [ReadOnly] public NativeArray<bool> OccluderFlags;
        [ReadOnly] public NativeArray<OccluderSortInfo> OccluderInfos;

        [NativeDisableParallelForRestriction] [WriteOnly]
        public NativeArray<float3> ScreenSpaceVertices;
        
        public void Execute(int index)
        {
            var occluderInfo = OccluderInfos[index];
            if (occluderInfo.NumRasterizeTris == 0) return;
            // if (!OccluderFlags[objIdx] || CullingResults[objIdx]) return;
            var objIdx = occluderInfo.Idx;
            var mvpMatrix = MvpMatrices[objIdx];
            var fillOffset = FillOffsets[objIdx] * 3;
            var vertexOffset = LocalSpaceVertexOffsets[objIdx];
            var indexStart = IndexOffsets[objIdx];
            // var indexEnd = objIdx + 1 < IndexOffsets.Length ? IndexOffsets[objIdx + 1] : Indices.Length;
            
            for (var i = indexStart; i < indexStart + occluderInfo.NumRasterizeTris * 3; i++)
            {
                var localSpaceVertex = new float4(LocalSpaceVertices[Indices[i] + vertexOffset], 1.0f);
                var clipSpaceVertex = math.mul(mvpMatrix, localSpaceVertex);
                var ndcSpaceVertex = clipSpaceVertex.xyz / clipSpaceVertex.w;
                var screenSpaceVertex = new float3(
                    ndcSpaceVertex.xy * 0.5f + 0.5f,
                    #if MOC_REVERSED_Z
                    ndcSpaceVertex.z // TODO: z in [0, 1]
                    #else
                    ndcSpaceVertex.z * 0.5f + 0.5f
                    #endif
                );
                screenSpaceVertex.x *= DepthBufferWidth;
                screenSpaceVertex.y *= DepthBufferHeight;
                ScreenSpaceVertices[fillOffset + i - indexStart] = screenSpaceVertex;
            }
        }
    }
}