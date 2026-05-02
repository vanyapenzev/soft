using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using VagImmoEditor.Pro.Data.Models;
using VagImmoEditor.Pro.Services;
using VagImmoEditor.Pro.Simulation.Core;
using VagImmoEditor.Pro.Simulation.Scenarios;

namespace VagImmoEditor.Pro.UI.ViewModels
{
    /// <summary>
    /// ViewModel для 3D симуляции приборной панели
    /// </summary>
    public partial class DashboardSimulationViewModel : ObservableObject
    {
        private readonly DashboardSimulator _simulator;
        private readonly EepromDataService _eepromService;
        private readonly ImmoParserService _parserService;

        [ObservableProperty]
        private bool _isSimulationRunning;

        [ObservableProperty]
        private string _simulationStatus = "Готов к запуску";

        [ObservableProperty]
        private double _currentMileage;

        [ObservableProperty]
        private string _currentPin = string.Empty;

        [ObservableProperty]
        private string _immoType = "Не определен";

        [ObservableProperty]
        private ObservableCollection<WarningLightItem> _activeWarnings = new();

        [ObservableProperty]
        private IDashboardScenario? _selectedScenario;

        [ObservableProperty]
        private DisplayType _selectedDisplayType;

        public ObservableCollection<IDashboardScenario> AvailableScenarios { get; } = new();
        public ObservableCollection<DisplayType> DisplayTypes { get; } = new();

        public DashboardSimulationViewModel()
        {
            _simulator = new DashboardSimulator();
            _eepromService = new EepromDataService();
            _parserService = new ImmoParserService();

            // Инициализация сценариев
            AvailableScenarios.Add(new StartupSequence());
            AvailableScenarios.Add(new ImmoDiagnosticScenario());
            AvailableScenarios.Add(new KeyLearningScenario());
            AvailableScenarios.Add(new ComponentProtectionCheck());
            AvailableScenarios.Add(new DrivingScenario());
            SelectedScenario = AvailableScenarios[0];

            // Инициализация типов дисплеев
            foreach (DisplayType type in Enum.GetValues(typeof(DisplayType)))
            {
                DisplayTypes.Add(type);
            }
            SelectedDisplayType = DisplayType.Fishtank;

            // Подписка на события симулятора
            _simulator.StateChanged += OnStateChanged;
            _simulator.MessageLogged += OnMessageLogged;
            _simulator.ErrorOccurred += OnErrorOccurred;
        }

        [RelayCommand]
        private async Task StartSimulationAsync()
        {
            if (IsSimulationRunning) return;

            try
            {
                SimulationStatus = "Запуск симуляции...";
                await _simulator.StartAsync(SelectedScenario);
                IsSimulationRunning = true;
                SimulationStatus = "Симуляция запущена";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка запуска симуляции: {ex.Message}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                SimulationStatus = "Ошибка запуска";
            }
        }

        [RelayCommand]
        private async Task StopSimulationAsync()
        {
            if (!IsSimulationRunning) return;

            try
            {
                await _simulator.StopAsync();
                IsSimulationRunning = false;
                SimulationStatus = "Симуляция остановлена";
                ActiveWarnings.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка остановки: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Инициализация симуляции с данными из EEPROM
        /// </summary>
        public void InitializeFromEeprom(byte[] eepromData)
        {
            try
            {
                var eepromMap = _parserService.DetectImmoType(eepromData);
                var immoData = _parserService.ParseImmoData(eepromData, eepromMap);

                _simulator.Initialize(immoData, eepromMap);

                CurrentMileage = immoData.Mileage;
                CurrentPin = immoData.PinCode;
                ImmoType = eepromMap.ImmoType.ToString();

                // Выбор типа дисплея на основе типа IMMO
                SelectedDisplayType = immoData.ImmoType switch
                {
                    ImmoType.Immo2 => DisplayType.Segment,
                    ImmoType.Immo3Vdo => DisplayType.Fishtank,
                    ImmoType.Immo3Motorola => DisplayType.DotMatrix,
                    ImmoType.Immo3Nec => DisplayType.DotMatrix,
                    ImmoType.Immo4 => DisplayType.ColorTft,
                    _ => DisplayType.Fishtank
                };

                SimulationStatus = $"Инициализировано: {ImmoType}, Пробег: {CurrentMileage} км";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка инициализации: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnStateChanged(object? sender, DashboardState state)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                CurrentMileage = state.Mileage;
                
                // Обновление списка активных предупреждений
                ActiveWarnings.Clear();
                foreach (var light in state.ActiveWarnings)
                {
                    ActiveWarnings.Add(new WarningLightItem 
                    { 
                        Light = light, 
                        Name = light.ToString(),
                        IsOn = true 
                    });
                }
            });
        }

        private void OnMessageLogged(object? sender, string message)
        {
            // Логирование сообщений (можно вывести в UI)
            System.Diagnostics.Debug.WriteLine(message);
        }

        private void OnErrorOccurred(object? sender, Exception ex)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show($"Ошибка симуляции: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                SimulationStatus = "Ошибка";
            });
        }

        public void Dispose()
        {
            _simulator.Dispose();
        }
    }

    /// <summary>
    /// Элемент списка предупреждений
    /// </summary>
    public partial class WarningLightItem : ObservableObject
    {
        [ObservableProperty]
        private WarningLight _light;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private bool _isOn;
    }
}
