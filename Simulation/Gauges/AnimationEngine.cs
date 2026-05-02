using System;

namespace VagImmoEditor.Pro.Simulation.Gauges
{
    /// <summary>
    /// Движок анимаций для приборной панели
    /// </summary>
    public class AnimationEngine : IDisposable
    {
        private readonly System.Windows.Threading.DispatcherTimer _timer;
        private bool _isRunning;
        private bool _disposed;

        public double FrameRate { get; set; } = 60.0;
        public int FrameCount { get; private set; }
        public TimeSpan TotalTime { get; private set; }

        public event EventHandler? FrameUpdated;

        public AnimationEngine()
        {
            _timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1000.0 / FrameRate)
            };
            _timer.Tick += OnFrameTick;
        }

        public void Start()
        {
            if (_disposed || _isRunning) return;
            
            FrameCount = 0;
            TotalTime = TimeSpan.Zero;
            _isRunning = true;
            _timer.Start();
        }

        public void Stop()
        {
            if (!_isRunning) return;
            
            _isRunning = false;
            _timer.Stop();
        }

        public void Update(DashboardState state)
        {
            // Обновление всех анимаций состояния
            UpdateNeedleAnimations(state);
            UpdateWarningLightAnimations(state);
            UpdateDisplayAnimations(state);
        }

        private void UpdateNeedleAnimations(DashboardState state)
        {
            // Плавное движение стрелок уже реализовано в GaugeController
            // Здесь можно добавить дополнительные эффекты (дрожание на холостых и т.д.)
            
            if (state.EngineRunning && state.Rpm > 0)
            {
                // Небольшое дрожание стрелки тахометра на холостых
                double jitter = Math.Sin(FrameCount * 0.5) * 20;
                state.NeedleAngleRpm += jitter * 0.01;
            }
        }

        private void UpdateWarningLightAnimations(DashboardState state)
        {
            // Анимация мигания предупреждений
            bool blinkPhase = (FrameCount / 30) % 2 == 0; // Мигание каждые 0.5 сек
            
            // Проверка критических предупреждений (мигают)
            if (state.ActiveWarnings.Contains(WarningLight.OilPressure) ||
                state.ActiveWarnings.Contains(WarningLight.BrakeSystem))
            {
                // Критические предупреждения мигают быстрее
                bool fastBlink = (FrameCount / 15) % 2 == 0;
            }
        }

        private void UpdateDisplayAnimations(DashboardState state)
        {
            // Анимация перехода сообщений на дисплее
            // Плавное появление/исчезновение текста
        }

        private void OnFrameTick(object? sender, EventArgs e)
        {
            FrameCount++;
            TotalTime += TimeSpan.FromSeconds(1.0 / FrameRate);
            
            FrameUpdated?.Invoke(this, EventArgs.Empty);
        }

        public void Reset()
        {
            FrameCount = 0;
            TotalTime = TimeSpan.Zero;
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            Stop();
            _timer.Tick -= OnFrameTick;
            _disposed = true;
        }
    }

    /// <summary>
    /// Класс для плавных переходов значений
    /// </summary>
    public class SmoothTransition
    {
        private double _currentValue;
        private double _targetValue;
        private readonly double _speed;

        public double Value => _currentValue;
        public bool IsComplete => Math.Abs(_currentValue - _targetValue) < 0.001;

        public SmoothTransition(double initialValue, double targetValue, double speed = 0.1)
        {
            _currentValue = initialValue;
            _targetValue = targetValue;
            _speed = Math.Max(0.01, Math.Min(1.0, speed));
        }

        public void Update()
        {
            _currentValue = _currentValue + (_targetValue - _currentValue) * _speed;
        }

        public void SetTarget(double newTarget)
        {
            _targetValue = newTarget;
        }
    }

    /// <summary>
    /// Анимация мигания
    /// </summary>
    public class BlinkAnimation
    {
        private int _frameCount;
        private readonly int _onFrames;
        private readonly int _offFrames;

        public bool IsOn { get; private set; } = true;

        public BlinkAnimation(int onFrames = 30, int offFrames = 30)
        {
            _onFrames = onFrames;
            _offFrames = offFrames;
        }

        public void Update()
        {
            _frameCount++;
            
            int totalCycle = _onFrames + _offFrames;
            int position = _frameCount % totalCycle;
            
            IsOn = position < _onFrames;
        }

        public void Reset()
        {
            _frameCount = 0;
            IsOn = true;
        }
    }

    /// <summary>
    /// Анимация прогресс-бара
    /// </summary>
    public class ProgressBarAnimation
    {
        private double _currentProgress;
        private readonly double _speed;

        public double Progress => _currentProgress;
        public bool IsComplete => _currentProgress >= 1.0;

        public ProgressBarAnimation(double speed = 0.02)
        {
            _speed = speed;
            _currentProgress = 0.0;
        }

        public void Update()
        {
            if (!IsComplete)
            {
                _currentProgress = Math.Min(1.0, _currentProgress + _speed);
            }
        }

        public void Reset()
        {
            _currentProgress = 0.0;
        }
    }
}
