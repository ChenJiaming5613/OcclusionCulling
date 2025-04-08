using Unity.Burst;
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
        [ReadOnly] public NativeArray<bool> CullingResults;
        [ReadOnly] public NativeArray<bool> OccluderFlags;

        [NativeDisableParallelForRestriction] [WriteOnly]
        public NativeArray<float3> ScreenSpaceVertices;
        
        public void Execute(int objIdx)
        {
            if (!OccluderFlags[objIdx] || CullingResults[objIdx]) return;
            var mvpMatrix = MvpMatrices[objIdx];
            var fillOffset = FillOffsets[objIdx] * 3;
            var vertexOffset = LocalSpaceVertexOffsets[objIdx];
            var indexStart = IndexOffsets[objIdx];
            var indexEnd = objIdx + 1 < IndexOffsets.Length ? IndexOffsets[objIdx + 1] : Indices.Length;
            
            for (var i = indexStart; i < indexEnd; i++)
            {
                var localSpaceVertex = new float4(LocalSpaceVertices[Indices[i] + vertexOffset], 1.0f);
                var clipSpaceVertex = math.mul(mvpMatrix, localSpaceVertex);
                var ndcSpaceVertex = clipSpaceVertex.xyz / clipSpaceVertex.w;
                var screenSpaceVertex = ndcSpaceVertex * 0.5f + 0.5f;
                screenSpaceVertex.x *= DepthBufferWidth;
                screenSpaceVertex.y *= DepthBufferHeight;
                ScreenSpaceVertices[fillOffset + i - indexStart] = screenSpaceVertex;
            }
        }
    }
}