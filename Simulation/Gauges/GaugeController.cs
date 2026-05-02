using System;

namespace VagImmoEditor.Pro.Simulation.Gauges
{
    /// <summary>
    /// Контроллер приборов (спидометр, тахометр, температура, топливо)
    /// </summary>
    public class GaugeController
    {
        private readonly AnalogGauge _speedGauge;
        private readonly AnalogGauge _rpmGauge;
        private readonly AnalogGauge _tempGauge;
        private readonly AnalogGauge _fuelGauge;

        public AnalogGauge SpeedGauge => _speedGauge;
        public AnalogGauge RpmGauge => _rpmGauge;
        public AnalogGauge TempGauge => _tempGauge;
        public AnalogGauge FuelGauge => _fuelGauge;

        public GaugeController()
        {
            // Инициализация приборов с параметрами для VAG
            _speedGauge = new AnalogGauge(0, 280, 0, 270, GaugeStyle.VagClassic);
            _rpmGauge = new AnalogGauge(0, 8000, 0, 270, GaugeStyle.VagSport);
            _tempGauge = new AnalogGauge(-40, 150, 45, 135, GaugeStyle.VagSimple);
            _fuelGauge = new AnalogGauge(0, 100, 45, 135, GaugeStyle.VagSimple);
        }

        public void Update(DashboardState state)
        {
            // Плавное движение стрелок
            _speedGauge.TargetValue = state.Speed;
            _rpmGauge.TargetValue = state.Rpm;
            _tempGauge.TargetValue = state.CoolantTemp;
            _fuelGauge.TargetValue = state.FuelLevel;

            // Обновление углов стрелок
            state.NeedleAngleSpeed = _speedGauge.CurrentAngle;
            state.NeedleAngleRpm = _rpmGauge.CurrentAngle;
            state.NeedleAngleTemp = _tempGauge.CurrentAngle;
            state.NeedleAngleFuel = _fuelGauge.CurrentAngle;
        }

        public void Reset()
        {
            _speedGauge.Reset();
            _rpmGauge.Reset();
            _tempGauge.Reset();
            _fuelGauge.Reset();
        }

        public void SweepAll()
        {
            // Тестовый проход всех стрелок (при включении зажигания)
            _speedGauge.Sweep();
            _rpmGauge.Sweep();
            _tempGauge.Sweep();
            _fuelGauge.Sweep();
        }
    }

    /// <summary>
    /// Аналоговый прибор со стрелкой
    /// </summary>
    public class AnalogGauge
    {
        private double _currentValue;
        private double _targetValue;
        private readonly double _minValue;
        private readonly double _maxValue;
        private readonly double _minAngle;
        private readonly double _maxAngle;
        private readonly GaugeStyle _style;
        private readonly double _smoothingFactor;

        public double CurrentValue => _currentValue;
        public double TargetValue 
        { 
            get => _targetValue;
            set => _targetValue = Math.Max(_minValue, Math.Min(_maxValue, value));
        }

        public double CurrentAngle { get; private set; }
        public bool IsSweeping { get; private set; }

        public AnalogGauge(double minValue, double maxValue, double minAngle, double maxAngle, GaugeStyle style)
        {
            _minValue = minValue;
            _maxValue = maxValue;
            _minAngle = minAngle;
            _maxAngle = maxAngle;
            _style = style;
            
            // Коэффициент сглаживания (разный для разных типов приборов)
            _smoothingFactor = style switch
            {
                GaugeStyle.VagSport => 0.15,   // Быстрая реакция
                GaugeStyle.VagClassic => 0.1,  // Средняя
                GaugeStyle.VagSimple => 0.08,  // Медленная
                _ => 0.1
            };
            
            CurrentAngle = _minAngle;
            _currentValue = minValue;
            _targetValue = minValue;
        }

        public void Update()
        {
            if (IsSweeping)
            {
                // Режим тестового прохода
                CurrentAngle += 5;
                if (CurrentAngle >= _maxAngle)
                {
                    CurrentAngle = _maxAngle;
                    IsSweeping = false;
                }
                return;
            }

            // Плавное движение к целевому значению
            double targetAngle = ValueToAngle(_targetValue);
            
            // Интерполяция для плавности
            CurrentAngle = CurrentAngle + (targetAngle - CurrentAngle) * _smoothingFactor;
            
            // Округление для предотвращения дрожания
            if (Math.Abs(targetAngle - CurrentAngle) < 0.1)
                CurrentAngle = targetAngle;
        }

        public double ValueToAngle(double value)
        {
            value = Math.Max(_minValue, Math.Min(_maxValue, value));
            double normalized = (value - _minValue) / (_maxValue - _minValue);
            return _minAngle + normalized * (_maxAngle - _minAngle);
        }

        public double AngleToValue(double angle)
        {
            angle = Math.Max(_minAngle, Math.Min(_maxAngle, angle));
            double normalized = (angle - _minAngle) / (_maxAngle - _minAngle);
            return _minValue + normalized * (_maxValue - _minValue);
        }

        public void Reset()
        {
            _currentValue = _minValue;
            _targetValue = _minValue;
            CurrentAngle = _minAngle;
            IsSweeping = false;
        }

        public void Sweep()
        {
            IsSweeping = true;
            CurrentAngle = _minAngle;
        }
    }

    /// <summary>
    /// Стили приборов VAG
    /// </summary>
    public enum GaugeStyle
    {
        VagClassic,  // Классические приборы (Golf III/IV)
        VagSport,    // Спортивные приборы (GTI, R32)
        VagSimple    // Упрощенные (температура, топливо)
    }

    /// <summary>
    /// Конфигурации шкал для разных моделей
    /// </summary>
    public static class GaugeConfigs
    {
        // Golf IV / Passat B5 / Audi A3/A4
        public static readonly AnalogGaugeConfig GolfIV_Speed = new(0, 280, 0, 270, "km/h");
        public static readonly AnalogGaugeConfig GolfIV_Rpm = new(0, 8000, 0, 270, "x1000/min");
        
        // US версии (мили)
        public static readonly AnalogGaugeConfig GolfIV_Speed_MPH = new(0, 180, 0, 270, "MPH");
        
        // Коммерческие автомобили
        public static readonly AnalogGaugeConfig Transporter_Speed = new(0, 180, 0, 270, "km/h");
        public static readonly AnalogGaugeConfig Transporter_Rpm = new(0, 6000, 0, 270, "x1000/min");
    }

    public record AnalogGaugeConfig(double Min, double Max, double MinAngle, double MaxAngle, string Unit);
}
