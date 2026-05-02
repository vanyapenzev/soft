using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Threading;
using VagImmoEditor.Pro.Data.Models;
using VagImmoEditor.Pro.Simulation.Displays;
using VagImmoEditor.Pro.Simulation.Gauges;
using VagImmoEditor.Pro.Simulation.Scenarios;

namespace VagImmoEditor.Pro.Simulation.Core
{
    /// <summary>
    /// Главный движок симуляции приборной панели VAG
    /// </summary>
    public class DashboardSimulator : IDisposable
    {
        private readonly DispatcherTimer _updateTimer;
        private readonly List<IDisposable> _disposables = new();
        private bool _isRunning;
        private bool _disposed;

        // Компоненты симуляции
        public IDisplayRenderer DisplayRenderer { get; }
        public GaugeController GaugeController { get; }
        public AnimationEngine AnimationEngine { get; }
        
        // Текущее состояние
        public DashboardState CurrentState { get; private set; }
        public ImmoData? ImmoData { get; private set; }
        public EepromMap? EepromMap { get; private set; }
        
        // Активный сценарий
        public IDashboardScenario? ActiveScenario { get; private set; }
        
        // События
        public event EventHandler<DashboardState>? StateChanged;
        public event EventHandler<string>? MessageLogged;
        public event EventHandler<Exception>? ErrorOccurred;

        public DashboardSimulator()
        {
            DisplayRenderer = new DisplayRenderer();
            GaugeController = new GaugeController();
            AnimationEngine = new AnimationEngine();
            
            CurrentState = new DashboardState();
            
            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
            };
            _updateTimer.Tick += OnUpdateTick;
            _disposables.Add(_updateTimer);
            
            LogMessage("Dashboard simulator initialized");
        }

        /// <summary>
        /// Инициализация симуляции с данными из EEPROM
        /// </summary>
        public void Initialize(ImmoData immoData, EepromMap eepromMap)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DashboardSimulator));
            
            ImmoData = immoData;
            EepromMap = eepromMap;
            
            // Настройка параметров из прошивки
            CurrentState.Mileage = immoData.Mileage;
            CurrentState.PinCode = immoData.PinCode;
            CurrentState.ImmoActive = immoData.ImmoActive;
            CurrentState.ComponentProtection = immoData.ComponentProtection;
            
            // Определение типа дисплея из карты памяти
            var displayType = DetectDisplayType(eepromMap);
            DisplayRenderer.SetDisplayType(displayType);
            
            LogMessage($"Initialized with IMMO type: {eepromMap.ImmoType}, Display: {displayType}");
            LogMessage($"Mileage: {CurrentState.Mileage} km, PIN: {CurrentState.PinCode}");
            
            StateChanged?.Invoke(this, CurrentState);
        }

        /// <summary>
        /// Запуск симуляции
        /// </summary>
        public async Task StartAsync(IDashboardScenario? scenario = null)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DashboardSimulator));
            if (_isRunning) return;

            ActiveScenario = scenario ?? new StartupSequence();
            _isRunning = true;
            
            LogMessage($"Starting simulation with scenario: {ActiveScenario.Name}");
            
            await ActiveScenario.InitializeAsync(this);
            
            _updateTimer.Start();
            AnimationEngine.Start();
            
            StateChanged?.Invoke(this, CurrentState);
        }

        /// <summary>
        /// Остановка симуляции
        /// </summary>
        public async Task StopAsync()
        {
            if (!_isRunning) return;
            
            _isRunning = false;
            _updateTimer.Stop();
            AnimationEngine.Stop();
            
            if (ActiveScenario != null)
            {
                await ActiveScenario.CleanupAsync(this);
                ActiveScenario = null;
            }
            
            LogMessage("Simulation stopped");
            StateChanged?.Invoke(this, CurrentState);
        }

        /// <summary>
        /// Обновление состояния симуляции
        /// </summary>
        private void OnUpdateTick(object? sender, EventArgs e)
        {
            if (!_isRunning || ActiveScenario == null) return;

            try
            {
                // Обновление сценария
                ActiveScenario.Update(this, CurrentState);
                
                // Обновление анимаций
                AnimationEngine.Update(CurrentState);
                
                // Обновление дисплея
                DisplayRenderer.Render(CurrentState);
                
                // Обновление приборов
                GaugeController.Update(CurrentState);
                
                StateChanged?.Invoke(this, CurrentState);
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
                LogMessage($"Error in update loop: {ex.Message}");
            }
        }

        /// <summary>
        /// Определение типа дисплея по карте памяти
        /// </summary>
        private DisplayType DetectDisplayType(EepromMap map)
        {
            // Эвристическое определение типа дисплея по типу IMMO и году
            return map.ImmoType switch
            {
                ImmoType.Immo2 => DisplayType.Segment,
                ImmoType.Immo3Vdo => DisplayType.Fishtank,
                ImmoType.Immo3Motorola => DisplayType.DotMatrix,
                ImmoType.Immo3Nec => DisplayType.DotMatrix,
                ImmoType.Immo4 => DisplayType.ColorTft,
                ImmoType.Immo4Uds => DisplayType.Oled,
                _ => DisplayType.Fishtank
            };
        }

        /// <summary>
        /// Установка значения скорости
        /// </summary>
        public void SetSpeed(double speedKmh)
        {
            CurrentState.Speed = Math.Max(0, Math.Min(280, speedKmh));
        }

        /// <summary>
        /// Установка оборотов двигателя
        /// </summary>
        public void SetRpm(int rpm)
        {
            CurrentState.Rpm = Math.Max(0, Math.Min(8000, rpm));
        }

        /// <summary>
        /// Установка температуры охлаждающей жидкости
        /// </summary>
        public void SetCoolantTemp(double tempCelsius)
        {
            CurrentState.CoolantTemp = Math.Max(-40, Math.Min(150, tempCelsius));
        }

        /// <summary>
        /// Установка уровня топлива
        /// </summary>
        public void SetFuelLevel(double percent)
        {
            CurrentState.FuelLevel = Math.Max(0, Math.Min(100, percent));
        }

        /// <summary>
        /// Включение/выключение лампы предупреждения
        /// </summary>
        public void SetWarningLight(WarningLight light, bool isOn)
        {
            if (isOn)
                CurrentState.ActiveWarnings.Add(light);
            else
                CurrentState.ActiveWarnings.Remove(light);
        }

        /// <summary>
        /// Обновление сообщения на дисплее
        /// </summary>
        public void SetDisplayMessage(string message, DisplayMessageType type = DisplayMessageType.Info)
        {
            CurrentState.DisplayMessage = message;
            CurrentState.MessageType = type;
        }

        /// <summary>
        /// Увеличение пробега
        /// </summary>
        public void AddMileage(double kilometers)
        {
            CurrentState.Mileage += kilometers;
            if (CurrentState.Mileage > 999999)
                CurrentState.Mileage = 0; // Переполнение одометра
        }

        /// <summary>
        /// Логирование сообщения
        /// </summary>
        private void LogMessage(string message)
        {
            MessageLogged?.Invoke(this, $"[{DateTime.Now:HH:mm:ss.fff}] {message}");
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            _isRunning = false;
            _updateTimer.Stop();
            
            foreach (var disposable in _disposables)
            {
                try { disposable.Dispose(); } catch { }
            }
            
            _disposables.Clear();
            _disposed = true;
            
            LogMessage("Dashboard simulator disposed");
        }
    }

    /// <summary>
    /// Состояние приборной панели
    /// </summary>
    public class DashboardState
    {
        public double Speed { get; set; }
        public int Rpm { get; set; }
        public double CoolantTemp { get; set; } = 20;
        public double FuelLevel { get; set; } = 50;
        public double Mileage { get; set; }
        public string PinCode { get; set; } = string.Empty;
        public bool ImmoActive { get; set; } = true;
        public bool ComponentProtection { get; set; }
        public bool IgnitionOn { get; set; }
        public bool EngineRunning { get; set; }
        
        public HashSet<WarningLight> ActiveWarnings { get; } = new();
        public string DisplayMessage { get; set; } = string.Empty;
        public DisplayMessageType MessageType { get; set; } = DisplayMessageType.Info;
        
        public DateTime CurrentTime { get; set; } = DateTime.Now;
        public double OutsideTemp { get; set; } = 20;
        
        // Состояние анимаций
        public double NeedleAngleSpeed { get; set; }
        public double NeedleAngleRpm { get; set; }
        public double NeedleAngleTemp { get; set; }
        public double NeedleAngleFuel { get; set; }
    }

    public enum DisplayType
    {
        Segment,      // 7-сегментный
        Fishtank,     // VDO "аквариум"
        DotMatrix,    // Точечная матрица
        ColorTft,     // Цветной TFT
        Oled          // OLED
    }

    public enum WarningLight
    {
        CheckEngine,
        Abs,
        Airbag,
        Battery,
        OilPressure,
        BrakeSystem,
        Seatbelt,
        DoorOpen,
        LightBulbOut,
        WasherFluid,
        GlowPlugs,
        Immobilizer,
        Esp,
        TirePressure
    }

    public enum DisplayMessageType
    {
        Info,
        Warning,
        Error,
        Critical
    }
}
