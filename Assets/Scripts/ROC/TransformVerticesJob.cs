using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace ROC
{
    [BurstCompile]
    public struct TransformVerticesJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Vector3> LocalSpaceVertices;
        public float4x4 MvpMatrix;

        [WriteOnly] public NativeArray<float3> ScreenSpaceVertices;
        
        public void Execute(int vertexIdx)
        {
            var clipSpaceVertex = math.mul(MvpMatrix, new float4(
                LocalSpaceVertices[vertexIdx].x,
                LocalSpaceVertices[vertexIdx].y,
                LocalSpaceVertices[vertexIdx].z,
                1.0f)
            );
            var ndcSpaceVertex = clipSpaceVertex.xyz / clipSpaceVertex.w;
            var screenSpaceVertex = ndcSpaceVertex * 0.5f + 0.5f;
            screenSpaceVertex.x *= Constants.ScreenWidth;
            screenSpaceVertex.y *= Constants.ScreenHeight;
            ScreenSpaceVertices[vertexIdx] = screenSpaceVertex;
        }
    }
}