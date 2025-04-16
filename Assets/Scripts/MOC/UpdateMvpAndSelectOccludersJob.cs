namespace MOC
{
    using Unity.Collections;
    using Unity.Jobs;
    using Unity.Mathematics;
    using Unity.Burst;
    using UnityEngine;

    [BurstCompile]
    public struct UpdateMvpAndSelectOccludersJob : IJobParallelFor
    {
        /// <summary>
        /// 所有物体的可见性
        /// </summary>
        [ReadOnly] public NativeArray<bool> CullingResults;
        
        /// <summary>
        /// 所有物体的包围盒（local space）
        /// </summary>
        [ReadOnly] public NativeArray<Bounds> Bounds;
        
        /// <summary>
        /// 所有物体的模型矩阵
        /// </summary>
        [ReadOnly] public NativeArray<float4x4> ModelMatrices;
        
        /// <summary>
        /// 当前摄像机的VP矩阵
        /// </summary>
        public float4x4 VpMatrix;
        
        /// <summary>
        /// 覆盖率阈值 (0~1)，超过此阈值的物体会被视为遮挡物
        /// </summary>
        public float CoverageThreshold;
        
        /// <summary>
        /// 更新后的MVP矩阵
        /// </summary>
        [WriteOnly] public NativeArray<float4x4> MvpMatrices;
        
        /// <summary>
        /// 遮挡物判定结果
        /// </summary>
        [WriteOnly] public NativeArray<bool> OccluderFlags;
        
        [WriteOnly] public NativeArray<OccluderSortInfo> OccluderInfos;
        
        /// <summary>
        /// 对于每个遮挡物，计算屏占比如果大于阈值被判定为遮挡物，并更新mvp矩阵
        /// </summary>
        /// <param name="objIdx">物体索引</param>
        public void Execute(int objIdx)
        {
            var occluderInfo = new OccluderSortInfo { Idx = objIdx, Coverage = 0f, NumRasterizeTris = 0 };
            if (CullingResults[objIdx])
            {
                OccluderInfos[objIdx] = occluderInfo;
                return;
            }

            var coverage = CalcCoverage(Bounds[objIdx], VpMatrix);
            var occluderFlag = coverage >= CoverageThreshold;
            // TODO: 写入screen rect
            OccluderFlags[objIdx] = occluderFlag;
            if (occluderFlag)
            {
                occluderInfo.Coverage = coverage;
                // MvpMatrices[i] = VpMatrix * ModelMatrices[i]; // Error!
                MvpMatrices[objIdx] = math.mul(VpMatrix, ModelMatrices[objIdx]);
            }
            OccluderInfos[objIdx] = occluderInfo;
        }

        /// <summary>
        /// 计算包围盒屏幕覆盖率
        /// </summary>
        /// <param name="bounds">物体包围盒（world space）</param>
        /// <param name="vpMatrix">物体的mvp矩阵</param>
        /// <returns>屏幕覆盖率（范围在0~1）</returns>
        private static float CalcCoverage(in Bounds bounds, in float4x4 vpMatrix)
        {
            float3 center = bounds.center;
            float3 extents = bounds.extents;
            
            var points = new NativeArray<float3>(8, Allocator.Temp);
            points[0] = center + new float3(-extents.x, -extents.y, -extents.z); // 000
            points[1] = center + new float3(-extents.x, -extents.y, extents.z); // 001
            points[2] = center + new float3(-extents.x, extents.y, -extents.z); // 010
            points[3] = center + new float3(-extents.x, extents.y, extents.z); // 011
            points[4] = center + new float3(extents.x, -extents.y, -extents.z); // 100
            points[5] = center + new float3(extents.x, -extents.y, extents.z); // 101
            points[6] = center + new float3(extents.x, extents.y, -extents.z); // 110
            points[7] = center + new float3(extents.x, extents.y, extents.z); // 111

            var minX = Mathf.Infinity;
            var minY = Mathf.Infinity;
            var maxX = -Mathf.Infinity;
            var maxY = -Mathf.Infinity;
            for (var i = 0; i < 8; i++)
            {
                var clipSpacePoint = math.mul(vpMatrix, new float4(points[i], 1.0f));
                var ndcPoint = clipSpacePoint.xyz / clipSpacePoint.w;
                if (ndcPoint.z < 0.0f || ndcPoint.z > 1.0f) return 0.0f; // ignore near plane bounds
                ndcPoint = math.clamp(ndcPoint, -1f, 1f);
                maxX = math.max(maxX, ndcPoint.x);
                minX = math.min(minX, ndcPoint.x);
                maxY = math.max(maxY, ndcPoint.y);
                minY = math.min(minY, ndcPoint.y);
            }
            
            var width = maxX - minX;
            var height = maxY - minY;
            return math.max(width / 2f, height / 2f);
        }
    }
}