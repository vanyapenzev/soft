# 3D Симуляция приборной панели VAG

## Обзор

Модуль 3D симуляции позволяет визуализировать работу приборной панели VAG на основе данных из EEPROM прошивки. Поддерживаются различные типы дисплеев и сценарии работы.

## Архитектура

### Core (Ядро симуляции)
- **DashboardSimulator** - главный движок симуляции
- **DashboardState** - состояние приборной панели в реальном времени
- **AnimationEngine** - система анимаций (60 FPS)

### Displays (Дисплеи)
Поддержка 5 типов дисплеев:
1. **Segment** - 7-сегментный (IMMO2, старые Golf/Bora)
2. **Fishtank** - VDO "аквариум" (Golf IV, Passat B5, Audi A3/A4)
3. **DotMatrix** - точечная матрица (Motorola/NEC)
4. **ColorTft** - цветной TFT (IMMO4+)
5. **Oled** - OLED (новейшие модели)

### Gauges (Приборы)
- **GaugeController** - управление стрелочными приборами
- **AnalogGauge** - модель аналогового прибора
- Стили: VagClassic, VagSport, VagSimple

### Scenarios (Сценарии)
1. **StartupSequence** - последовательность запуска (тест ламп, проход стрелок)
2. **ImmoDiagnosticScenario** - диагностика иммобилайзера
3. **KeyLearningScenario** - адаптация ключей
4. **ComponentProtectionCheck** - проверка защиты компонентов
5. **DrivingScenario** - динамическая симуляция езды

## Использование

### Инициализация
```csharp
var simulator = new DashboardSimulator();
simulator.Initialize(immoData, eepromMap);
```

### Запуск сценария
```csharp
await simulator.StartAsync(new StartupSequence());
```

### Управление параметрами
```csharp
simulator.SetSpeed(80);        // Установка скорости
simulator.SetRpm(3000);        // Обороты двигателя
simulator.SetCoolantTemp(90);  // Температура
simulator.SetFuelLevel(75);    // Уровень топлива
simulator.SetWarningLight(WarningLight.CheckEngine, true);
```

## Интеграция с UI

### XAML (3D Viewport)
```xml
<helix:HelixViewport3D>
    <helix:CubeVisual3D Center="0,0,0" Size="700,400,50"/>
    <helix:DiscVisual3D Center="-150,-50,30" Radius="120"/>
    <helix:ArrowVisual3D x:Name="SpeedNeedle"/>
</helix:HelixViewport3D>
```

### ViewModel
```csharp
public partial class DashboardSimulationViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isSimulationRunning;
    
    [RelayCommand]
    private async Task StartSimulationAsync() { ... }
}
```

## Особенности реализации

### Реалистичное поведение
- Плавное движение стрелок с инерцией
- Дрожание тахометра на холостых оборотах
- Мигание предупреждающих ламп
- Анимация перехода сообщений на дисплее

### Определение типа дисплея
Автоматическое определение по типу IMMO:
- IMMO2 → Segment
- IMMO3 VDO → Fishtank (голубая подсветка)
- IMMO3 Motorola/NEC → DotMatrix
- IMMO4+ → ColorTft/Oled

### Сценарии работы
Каждый сценарий реализует интерфейс `IDashboardScenario`:
- `InitializeAsync()` - инициализация
- `Update()` - обновление каждый кадр
- `CleanupAsync()` - очистка

## Зависимости
- HelixToolkit.Wpf 2.24.0 - 3D рендеринг
- CommunityToolkit.Mvvm - MVVM инфраструктура
- .NET 8.0 Windows

## Расширение

### Добавление нового сценария
```csharp
public class CustomScenario : IDashboardScenario
{
    public string Name => "Custom";
    
    public Task InitializeAsync(DashboardSimulator sim) { ... }
    public void Update(DashboardSimulator sim, DashboardState state) { ... }
    public Task CleanupAsync(DashboardSimulator sim) { ... }
}
```

### Новый тип дисплея
1. Добавить enum значение в `DisplayType`
2. Реализовать метод рендеринга в `DisplayRenderer`
3. Добавить цветовую палитру в `DisplayPalettes`

## Отладка

Включите логирование событий:
```csharp
simulator.MessageLogged += (s, msg) => Debug.WriteLine(msg);
simulator.ErrorOccurred += (s, ex) => Debug.WriteLine(ex);
```

## Производительность
- Целевая частота кадров: 60 FPS
- Интервал обновления: 16 мс
- Оптимизировано для реального времени

## Будущие улучшения
- Поддержка скинов приборных панелей
- Экспорт видео симуляции
- Сетевой режим (удаленная демонстрация)
- VR поддержка
