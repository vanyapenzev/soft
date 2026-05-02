using System;
using System.Threading.Tasks;

namespace VagImmoEditor.Pro.Simulation.Scenarios
{
    /// <summary>
    /// Интерфейс сценария работы приборной панели
    /// </summary>
    public interface IDashboardScenario
    {
        string Name { get; }
        Task InitializeAsync(DashboardSimulator simulator);
        void Update(DashboardSimulator simulator, DashboardState state);
        Task CleanupAsync(DashboardSimulator simulator);
    }

    /// <summary>
    /// Сценарий запуска (при включении зажигания)
    /// </summary>
    public class StartupSequence : IDashboardScenario
    {
        private int _step;
        private int _frameCount;

        public string Name => "Startup Sequence";

        public async Task InitializeAsync(DashboardSimulator simulator)
        {
            _step = 0;
            _frameCount = 0;
            
            simulator.SetDisplayMessage("VAG SIMULATOR", DisplayMessageType.Info);
            await Task.Delay(100);
        }

        public void Update(DashboardSimulator simulator, DashboardState state)
        {
            _frameCount++;

            // Шаг 1: Тест ламп (2 секунды)
            if (_frameCount < 120)
            {
                state.IgnitionOn = true;
                
                // Включить все лампы предупреждения
                foreach (WarningLight light in Enum.GetValues(typeof(WarningLight)))
                {
                    simulator.SetWarningLight(light, true);
                }
                
                // Тестовый проход стрелок
                if (_frameCount == 1)
                {
                    simulator.GaugeController.SweepAll();
                }
                
                simulator.SetDisplayMessage("SYSTEM CHECK", DisplayMessageType.Info);
            }
            // Шаг 2: Выключение ламп, кроме важных (1 секунда)
            else if (_frameCount < 180)
            {
                // Оставить только важные предупреждения
                simulator.SetWarningLight(WarningLight.Seatbelt, state.ImmoActive);
                simulator.SetWarningLight(WarningLight.Immobilizer, state.ImmoActive);
                
                if (state.ComponentProtection)
                {
                    simulator.SetWarningLight(WarningLight.CheckEngine, true);
                    simulator.SetDisplayMessage("COMPONENT PROTECTED", DisplayMessageType.Warning);
                }
                else
                {
                    simulator.SetDisplayMessage($"ODO: {state.Mileage:F0} km", DisplayMessageType.Info);
                }
            }
            // Шаг 3: Рабочий режим
            else
            {
                // Показать текущее состояние
                if (!state.ImmoActive)
                {
                    simulator.SetWarningLight(WarningLight.Immobilizer, false);
                    simulator.SetDisplayMessage("IMMO OFF", DisplayMessageType.Warning);
                }
                else if (state.EngineRunning)
                {
                    simulator.SetWarningLight(WarningLight.Immobilizer, false);
                    simulator.SetWarningLight(WarningLight.Seatbelt, false);
                }
            }
        }

        public async Task CleanupAsync(DashboardSimulator simulator)
        {
            await Task.Delay(100);
        }
    }

    /// <summary>
    /// Сценарий диагностики иммобилайзера
    /// </summary>
    public class ImmoDiagnosticScenario : IDashboardScenario
    {
        private int _blinkCount;
        private bool _ledState;
        private int _frameCounter;

        public string Name => "IMMO Diagnostic";

        public async Task InitializeAsync(DashboardSimulator simulator)
        {
            _blinkCount = 0;
            _ledState = false;
            _frameCounter = 0;
            
            simulator.SetDisplayMessage("IMMO DIAGNOSTIC", DisplayMessageType.Info);
            simulator.SetWarningLight(WarningLight.Immobilizer, true);
            
            await Task.Delay(500);
        }

        public void Update(DashboardSimulator simulator, DashboardState state)
        {
            _frameCounter++;

            // Мигание лампы иммобилайзера каждые 500мс
            if (_frameCounter % 30 == 0)
            {
                _ledState = !_ledState;
                simulator.SetWarningLight(WarningLight.Immobilizer, _ledState);
                
                if (_ledState)
                    _blinkCount++;
            }

            // После серии миганий показать код ошибки
            if (_frameCounter > 180 && _frameCounter % 120 < 60)
            {
                // Показать PIN или код ошибки
                var pin = state.PinCode;
                if (!string.IsNullOrEmpty(pin))
                {
                    simulator.SetDisplayMessage($"PIN: {pin}", DisplayMessageType.Info);
                }
            }
            else if (_frameCounter > 180)
            {
                simulator.SetDisplayMessage("IMMO OK", DisplayMessageType.Info);
            }
        }

        public async Task CleanupAsync(DashboardSimulator simulator)
        {
            simulator.SetWarningLight(WarningLight.Immobilizer, state.ImmoActive);
            await Task.Delay(100);
        }
    }

    /// <summary>
    /// Сценарий адаптации ключей
    /// </summary>
    public class KeyLearningScenario : IDashboardScenario
    {
        private int _keysLearned;
        private readonly int _totalKeys;

        public string Name => "Key Learning";

        public KeyLearningScenario(int totalKeys = 2)
        {
            _totalKeys = totalKeys;
        }

        public async Task InitializeAsync(DashboardSimulator simulator)
        {
            _keysLearned = 0;
            
            simulator.SetDisplayMessage("KEY ADAPTATION", DisplayMessageType.Info);
            simulator.SetWarningLight(WarningLight.Immobilizer, true);
            
            await Task.Delay(1000);
        }

        public void Update(DashboardSimulator simulator, DashboardState state)
        {
            // Симуляция процесса обучения ключей
            // В реальной реализации ждет ввода ключа
            
            if (_keysLearned < _totalKeys)
            {
                simulator.SetDisplayMessage($"KEY {_keysLearned + 1}/{_totalKeys}", DisplayMessageType.Info);
                
                // Имитация задержки на "вставку ключа"
                // В реальности здесь был бы ввод от пользователя
            }
            else
            {
                simulator.SetDisplayMessage("ADAPTATION OK", DisplayMessageType.Info);
                simulator.SetWarningLight(WarningLight.Immobilizer, false);
            }
        }

        public async Task CleanupAsync(DashboardSimulator simulator)
        {
            simulator.SetWarningLight(WarningLight.Immobilizer, state.ImmoActive);
            await Task.Delay(100);
        }
    }

    /// <summary>
    /// Сценарий проверки защиты компонентов
    /// </summary>
    public class ComponentProtectionCheck : IDashboardScenario
    {
        public string Name => "Component Protection Check";

        public async Task InitializeAsync(DashboardSimulator simulator)
        {
            simulator.SetDisplayMessage("CHECKING CP...", DisplayMessageType.Info);
            await Task.Delay(2000);
        }

        public void Update(DashboardSimulator simulator, DashboardState state)
        {
            if (state.ComponentProtection)
            {
                simulator.SetWarningLight(WarningLight.CheckEngine, true);
                simulator.SetDisplayMessage("COMPONENT PROTECTED", DisplayMessageType.Error);
                
                // Блокировка некоторых функций
                simulator.SetSpeed(0);
            }
            else
            {
                simulator.SetDisplayMessage("CP: OK", DisplayMessageType.Info);
            }
        }

        public async Task CleanupAsync(DashboardSimulator simulator)
        {
            await Task.Delay(100);
        }
    }

    /// <summary>
    /// Динамический сценарий езды
    /// </summary>
    public class DrivingScenario : IDashboardScenario
    {
        private double _time;

        public string Name => "Driving Simulation";

        public async Task InitializeAsync(DashboardSimulator simulator)
        {
            _time = 0;
            simulator.SetDisplayMessage("DRIVING MODE", DisplayMessageType.Info);
            await Task.Delay(100);
        }

        public void Update(DashboardSimulator simulator, DashboardState state)
        {
            _time += 0.016; // ~60 FPS

            // Симуляция разгона/торможения
            double targetSpeed = 60 + 30 * Math.Sin(_time * 0.5);
            simulator.SetSpeed(targetSpeed);

            // Обороты двигателя зависят от скорости и "переключения передач"
            int targetRpm = (int)(2000 + 1500 * (targetSpeed / 100) + 500 * Math.Sin(_time * 2));
            simulator.SetRpm(targetRpm);

            // Температура постепенно растет
            double targetTemp = 85 + 10 * Math.Sin(_time * 0.1);
            simulator.SetCoolantTemp(targetTemp);

            // Топливо медленно уменьшается
            simulator.SetFuelLevel(Math.Max(0, state.FuelLevel - 0.001));

            // Обновление времени
            state.CurrentTime = state.CurrentTime.AddSeconds(0.016);

            // Проверка предупреждений
            if (state.FuelLevel < 10)
            {
                simulator.SetWarningLight(WarningLight.WasherFluid, true); // Используем как индикатор низкого топлива
                simulator.SetDisplayMessage("LOW FUEL", DisplayMessageType.Warning);
            }

            if (state.CoolantTemp > 110)
            {
                simulator.SetWarningLight(WarningLight.OilPressure, true); // Предупреждение о температуре
                simulator.SetDisplayMessage("OVERHEAT", DisplayMessageType.Critical);
            }
        }

        public async Task CleanupAsync(DashboardSimulator simulator)
        {
            simulator.SetSpeed(0);
            simulator.SetRpm(0);
            await Task.Delay(100);
        }
    }
}
