using System;
using System.Collections.ObjectModel;
using System.Windows;
using Microsoft.Win32;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VagImmoEditor.Services;
using VagImmoEditor.Data.Models;

namespace VagImmoEditor.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly EepromDataService _eepromService;
    private readonly ImmoParserService _immoParser;

    [ObservableProperty]
    private string _pinCode = "-----";

    [ObservableProperty]
    private uint _mileage;

    [ObservableProperty]
    private string _vin = "";

    [ObservableProperty]
    private string _immoType = "Не определен";

    [ObservableProperty]
    private string _crcStatus = "N/A";

    [ObservableProperty]
    private string _statusMessage = "Готов к работе";

    [ObservableProperty]
    private bool _isFileLoaded;

    [ObservableProperty]
    private ObservableCollection<ImmoOptionItem> _options = new();

    public MainViewModel()
    {
        _eepromService = new EepromDataService();
        _immoParser = new ImmoParserService(_eepromService);
    }

    [RelayCommand]
    private void LoadFile()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "BIN файлы|*.bin|Все файлы|*.*",
            Title = "Открыть дамп EEPROM"
        };

        if (dialog.ShowDialog() == true)
        {
            var result = _eepromService.LoadFromFile(dialog.FileName);
            
            if (result.Success)
            {
                RefreshData();
                StatusMessage = result.Message;
                IsFileLoaded = true;
            }
            else
            {
                StatusMessage = $"Ошибка: {result.Message}";
                IsFileLoaded = false;
            }
        }
    }

    [RelayCommand]
    private void SaveFile()
    {
        if (!IsFileLoaded)
        {
            StatusMessage = "Сначала загрузите файл";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "BIN файлы|*.bin|Все файлы|*.*",
            Title = "Сохранить дамп EEPROM",
            DefaultExt = "bin"
        };

        if (dialog.ShowDialog() == true)
        {
            var result = _eepromService.SaveToFile(dialog.FileName);
            StatusMessage = result.Message;
            
            if (result.Success)
            {
                RefreshData();
            }
        }
    }

    [RelayCommand]
    private void Undo()
    {
        if (_eepromService.Undo())
        {
            RefreshData();
            StatusMessage = "Отменено последнее изменение";
        }
    }

    [RelayCommand]
    private void Redo()
    {
        if (_eepromService.Redo())
        {
            RefreshData();
            StatusMessage = "Повторено изменение";
        }
    }

    [RelayCommand]
    private void UpdatePin(string newPin)
    {
        if (!IsFileLoaded) return;

        if (_immoParser.UpdatePin(newPin))
        {
            RefreshData();
            StatusMessage = $"PIN обновлен: {newPin}";
        }
        else
        {
            StatusMessage = "Ошибка обновления PIN. Проверьте формат (4-7 цифр)";
        }
    }

    [RelayCommand]
    private void UpdateMileage(uint newMileage)
    {
        if (!IsFileLoaded) return;

        if (_immoParser.UpdateMileage(newMileage))
        {
            RefreshData();
            StatusMessage = $"Пробег обновлен: {newMileage} км";
        }
        else
        {
            StatusMessage = "Ошибка обновления пробега";
        }
    }

    [RelayCommand]
    private void ToggleOption(ImmoOptionItem optionItem)
    {
        if (!IsFileLoaded || optionItem.Option == null) return;

        var option = optionItem.Option;
        if (_immoParser.ToggleOption(option, optionItem.IsEnabled))
        {
            RefreshData();
            StatusMessage = $"Опция '{option.Name}' изменена";
        }
    }

    private void RefreshData()
    {
        try
        {
            var immoData = _immoParser.ParseImmoData();

            PinCode = immoData.PinCode;
            Mileage = immoData.Mileage;
            Vin = immoData.Vin;
            ImmoType = _eepromService.CurrentMap.Name;
            
            CrcStatus = immoData.IsCrcValid ? "VALID ✓" : "INVALID ✗";

            Options.Clear();
            foreach (var opt in immoData.Options)
            {
                Options.Add(new ImmoOptionItem(opt));
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка парсинга: {ex.Message}";
        }
    }
}

public partial class ImmoOptionItem : ObservableObject
{
    public ImmoOption? Option { get; }

    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _description = "";

    public ImmoOptionItem(ImmoOption option)
    {
        Option = option;
        IsEnabled = option.Value;
        Name = option.Name;
        Description = option.Description;
    }
}
