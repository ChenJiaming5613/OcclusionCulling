using Unity.Mathematics;

namespace StatUtils
{
    public struct Counter
    {
        private float _avgCount;
        private int _maxCount;
        private int _minCount;
        private int _currCount;
        private int _count;
    
        public void Tick(int currCount)
        {
            _currCount = currCount;
            if (_count == 0)
            {
                _avgCount = _maxCount = _minCount = currCount;
                _count = 1;
                return;
            }
            _maxCount = math.max(_maxCount, currCount);
            _minCount = math.min(_minCount, currCount);
            _avgCount = (_avgCount * _count + currCount) / (_count + 1);
            _count++;
        }

        public string GetStatusStr()
        {
            return $"Curr:{_currCount} Avg:{_avgCount:F2} Max:{_maxCount} Min:{_minCount}";
        }
    }
}