using System.Windows;
using VagImmoEditor.Models;
using VagImmoEditor.Parsers;
using VagImmoEditor.Services;

namespace VagImmoEditor.ViewModels;

/// <summary>
/// Главная ViewModel приложения с поддержкой Undo/Redo
/// </summary>
public class MainViewModel : CommunityToolkit.Mvvm.ObservableObject
{
    private EepromData? _eepromData;
    private ImmoData? _immoData;
    private VagEepromParser? _parser;
    private ImmoOptionsService? _optionsService;
    private ChangeHistoryService? _historyService;
    
    private string _statusMessage = "Готов к работе";
    private string _fileName = "Нет файла";
    private bool _isFileLoaded;
    private bool _canUndo;
    private bool _canRedo;
    
    public MainViewModel()
    {
        LoadFileCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(LoadFile);
        SaveFileCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(SaveFile, CanSaveFile);
        ExitCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(ExitApplication);
        RefreshDataCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(RefreshData, CanRefreshData);
        ApplyOptionCommand = new CommunityToolkit.Mvvm.Input.RelayCommand<ImmoOption>(ApplyOption, CanApplyOption);
        UndoCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(Undo, () => _canUndo);
        RedoCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(Redo, () => _canRedo);
    }
    
    #region Properties
    
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }
    
    public string FileName
    {
        get => _fileName;
        set => SetProperty(ref _fileName, value);
    }
    
    public bool IsFileLoaded
    {
        get => _isFileLoaded;
        set => SetProperty(ref _isFileLoaded, value);
    }
    
    public bool CanUndo
    {
        get => _canUndo;
        set => SetProperty(ref _canUndo, value);
    }
    
    public bool CanRedo
    {
        get => _canRedo;
        set => SetProperty(ref _canRedo, value);
    }
    
    public ImmoData? ImmoData
    {
        get => _immoData;
        set => SetProperty(ref _immoData, value);
    }
    
    public string PinCode => ImmoData?.PinCode ?? "N/A";
    public int Mileage => ImmoData?.Mileage ?? 0;
    public string MileageUnit => ImmoData?.MileageUnit ?? "km";
    public string ImmoType => ImmoData?.ImmoType ?? "Unknown";
    public string ComponentId => ImmoData?.ComponentId ?? "N/A";
    public int KeyCount => ImmoData?.KeyCount ?? 0;
    public string? Vin => ImmoData?.Vin;
    public bool IsCrcValid => _eepromData?.IsCrcValid ?? false;
    
    #endregion
    
    #region Commands
    
    public CommunityToolkit.Mvvm.Input.IRelayCommand LoadFileCommand { get; }
    public CommunityToolkit.Mvvm.Input.IRelayCommand SaveFileCommand { get; }
    public CommunityToolkit.Mvvm.Input.IRelayCommand ExitCommand { get; }
    public CommunityToolkit.Mvvm.Input.IRelayCommand RefreshDataCommand { get; }
    public CommunityToolkit.Mvvm.Input.IRelayCommand<ImmoOption?> ApplyOptionCommand { get; }
    public CommunityToolkit.Mvvm.Input.IRelayCommand UndoCommand { get; }
    public CommunityToolkit.Mvvm.Input.IRelayCommand RedoCommand { get; }
    
    #endregion
    
    #region Command Methods
    
    private void LoadFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "BIN файлы (*.bin)|*.bin|Все файлы (*.*)|*.*",
            Title = "Открыть файл прошивки EEPROM"
        };
        
        if (dialog.ShowDialog() == true)
        {
            try
            {
                _eepromData = EepromData.LoadFromFile(dialog.FileName);
                _parser = new VagEepromParser(_eepromData);
                _optionsService = new ImmoOptionsService(_eepromData);
                _historyService = new ChangeHistoryService(_eepromData);
                
                // Подписка на события истории
                _historyService.HistoryChanged += OnHistoryChanged;
                
                _immoData = _parser.Parse();
                
                FileName = System.IO.Path.GetFileName(dialog.FileName);
                IsFileLoaded = true;
                StatusMessage = $"Файл загружен: {FileName} | CRC: {(IsCrcValid ? "OK" : "INVALID")}";
                
                OnPropertyChanged(nameof(PinCode));
                OnPropertyChanged(nameof(Mileage));
                OnPropertyChanged(nameof(MileageUnit));
                OnPropertyChanged(nameof(ImmoType));
                OnPropertyChanged(nameof(ComponentId));
                OnPropertyChanged(nameof(KeyCount));
                OnPropertyChanged(nameof(Vin));
                OnPropertyChanged(nameof(IsCrcValid));
                
                RefreshOptions();
                UpdateUndoRedoState();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ошибка загрузки: {ex.Message}";
                MessageBox.Show($"Ошибка при загрузке файла:\n{ex.Message}", 
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
    
    private void SaveFile()
    {
        if (_eepromData == null) return;
        
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "BIN файлы (*.bin)|*.bin|Все файлы (*.*)|*.*",
            Title = "Сохранить файл прошивки",
            FileName = FileName
        };
        
        if (dialog.ShowDialog() == true)
        {
            try
            {
                _eepromData.SaveToFile(dialog.FileName, updateCrc: true);
                StatusMessage = $"Файл сохранен: {System.IO.Path.GetFileName(dialog.FileName)} | CRC обновлен";
                MessageBox.Show("Файл успешно сохранен!\nCRC контрольная сумма обновлена.", 
                    "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ошибка сохранения: {ex.Message}";
                MessageBox.Show($"Ошибка при сохранении файла:\n{ex.Message}", 
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
    
    private bool CanSaveFile() => _eepromData != null;
    
    private void ExitApplication()
    {
        Application.Current.Shutdown();
    }
    
    private void RefreshData()
    {
        if (_parser == null || _eepromData == null) return;
        
        _immoData = _parser.Parse();
        
        OnPropertyChanged(nameof(PinCode));
        OnPropertyChanged(nameof(Mileage));
        OnPropertyChanged(nameof(MileageUnit));
        OnPropertyChanged(nameof(ImmoType));
        OnPropertyChanged(nameof(ComponentId));
        OnPropertyChanged(nameof(KeyCount));
        OnPropertyChanged(nameof(Vin));
        OnPropertyChanged(nameof(IsCrcValid));
        
        StatusMessage = $"Данные обновлены | CRC: {(IsCrcValid ? "OK" : "INVALID")}";
        RefreshOptions();
    }
    
    private bool CanRefreshData() => _parser != null;
    
    private void ApplyOption(ImmoOption? option)
    {
        if (option == null || _optionsService == null) return;
        
        try
        {
            // Сохраняем старые значения для истории
            var oldValues = new Dictionary<int, byte>();
            if (option.Type == OptionType.Boolean && option.BitPosition.HasValue)
            {
                oldValues[option.Offset] = _eepromData![option.Offset];
            }
            else if (option.Value != null)
            {
                for (int i = 0; i < option.Value.Length; i++)
                {
                    oldValues[option.Offset + i] = _eepromData![option.Offset + i];
                }
            }
            
            if (_optionsService.ApplyOptionChange(option))
            {
                // Записываем изменения в историю
                if (_historyService != null)
                {
                    foreach (var kvp in oldValues)
                    {
                        byte newValue = _eepromData![kvp.Key];
                        if (oldValues[kvp.Key] != newValue)
                        {
                            _historyService.RecordChange(kvp.Key, kvp.Value, newValue, option.Name);
                        }
                    }
                }
                
                StatusMessage = $"Опция '{option.Name}' применена";
                
                // Перечитываем данные после изменения
                RefreshData();
                UpdateUndoRedoState();
            }
            else
            {
                StatusMessage = $"Не удалось применить опцию '{option.Name}'";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка: {ex.Message}";
            MessageBox.Show($"Ошибка при применении опции:\n{ex.Message}", 
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private bool CanApplyOption(ImmoOption? option) => 
        option != null && !option.IsReadOnly && _optionsService != null;
    
    /// <summary>
    /// Отменить последнее изменение
    /// </summary>
    private void Undo()
    {
        if (_historyService?.Undo() == true)
        {
            StatusMessage = "Изменение отменено";
            RefreshData();
            UpdateUndoRedoState();
        }
    }
    
    /// <summary>
    /// Повторить отмененное изменение
    /// </summary>
    private void Redo()
    {
        if (_historyService?.Redo() == true)
        {
            StatusMessage = "Изменение повторено";
            RefreshData();
            UpdateUndoRedoState();
        }
    }
    
    #endregion
    
    #region Helper Methods
    
    private void OnHistoryChanged(object? sender, EventArgs e)
    {
        UpdateUndoRedoState();
    }
    
    private void UpdateUndoRedoState()
    {
        if (_historyService != null)
        {
            CanUndo = _historyService.CanUndo;
            CanRedo = _historyService.CanRedo;
            
            // Обновляем команды
            ((CommunityToolkit.Mvvm.Input.RelayCommand)UndoCommand).NotifyCanExecuteChanged();
            ((CommunityToolkit.Mvvm.Input.RelayCommand)RedoCommand).NotifyCanExecuteChanged();
        }
    }
    
    public IReadOnlyList<ImmoOption> GetOptions()
    {
        return _optionsService?.GetAllOptions() ?? new List<ImmoOption>().AsReadOnly();
    }
    
    public List<OptionGroup> GetOptionGroups()
    {
        return _optionsService?.GetOptionGroups() ?? new List<OptionGroup>();
    }
    
    private void RefreshOptions()
    {
        OnPropertyChanged(nameof(OptionsList));
    }
    
    public IEnumerable<ImmoOption> OptionsList => GetOptions();
    
    /// <summary>
    /// Экспорт истории изменений
    /// </summary>
    public string ExportChangeHistory()
    {
        return _historyService?.ExportToJson() ?? "[]";
    }
    
    #endregion
}
