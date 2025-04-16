namespace StatUtils
{
    public struct Indicator
    {
        private float _avgTime;
        private float _currTime;
        private int _count;

        public void Tick(float currTime)
        {
            _currTime = currTime;
            if (_count == 0)
            {
                _avgTime = currTime;
                _count = 1;
                return;
            }
            _avgTime = (_avgTime * _count + currTime) / (_count + 1);
            _count++;
        }

        public string GetStatusStr()
        {
            return $"Curr:{_currTime:F2}ms Avg:{_avgTime:F2}ms";
        }
    }
}