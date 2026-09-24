using AutoOC.Monitors;

namespace AutoOC.Controllers
{
    public class AdaptiveUndervoltController : IDisposable
    {
        private readonly InstabilityMonitor _monitor;
        private readonly int _minOffset;
        private readonly int _stepSize;
        private readonly int _stableThreshold;
        private readonly int _cooldownThreshold;
        private readonly bool _isIgpu;
        private int _currentOffset;
        private int _stableCount;
        private int _cooldownCount;

        public AdaptiveUndervoltController(
            InstabilityMonitor monitor,
            int minOffset,
            int stepSize,
            int stableThreshold,
            int cooldownThreshold,
            bool isIgpu)
        {
            _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
            _minOffset = minOffset;
            _stepSize = stepSize;
            _stableThreshold = stableThreshold;
            _cooldownThreshold = cooldownThreshold;
            _isIgpu = isIgpu;
            _currentOffset = 0;
            _stableCount = 0;
            _cooldownCount = 0;
        }

        public int UpdateOffset()
        {
            var isStable = _monitor == null || _monitor.IsStable();
            if (isStable)
            {
                _stableCount++;
                _cooldownCount = 0;
                if (_stableCount >= _stableThreshold)
                {
                    _stableCount = 0;
                    if (_currentOffset > _minOffset)
                    {
                        _currentOffset -= _stepSize;
                    }
                }
            }
            else
            {
                _stableCount = 0;
                _cooldownCount++;
                if (_cooldownCount >= _cooldownThreshold)
                {
                    _cooldownCount = 0;
                    if (_currentOffset < 0)
                    {
                        _currentOffset += _stepSize;
                    }
                }
            }

            return _currentOffset;
        }

        public void RecordAppliedOffset(int offset)
        {
            _currentOffset = offset;
        }

        public void Dispose()
        {
        }
    }
}

namespace AutoOC.Monitors
{
    public class InstabilityMonitor
    {
        public bool IsStable()
        {
            return true;
        }

        public void Stop()
        {
        }
    }
}
