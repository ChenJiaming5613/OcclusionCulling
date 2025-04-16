using System.Collections.Generic;

namespace MOC
{
    public struct OccluderSortInfo
    {
        public int Idx;
        public float Coverage;
        public int NumRasterizeTris;
    }

    public struct OccluderSortInfoComparer : IComparer<OccluderSortInfo>
    {
        public int Compare(OccluderSortInfo x, OccluderSortInfo y)
        {
            return y.Coverage.CompareTo(x.Coverage); // coverage 从大到小
        }
    }
}