namespace App.UI;

using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using Microsoft.Extensions.Logging;
using AppCore = App.Core;
using AppModels = App.Models;
using AppCrypto = App.Crypto;

/// <summary>
/// Главное окно приложения VAG EEPROM Editor
/// </summary>
public partial class MainWindow : Window
{
    // Сервисы
    private readonly AppCore.DumpLoaderService _dumpLoader;
    private readonly AppCore.DataParserService _dataParser;
    private readonly AppCore.EditCommandService _editCommandService;
    private readonly AppCore.VdoCrcCalculator _crcCalculator;
    private readonly AppCrypto.VdoNecCryptoService _cryptoService;

    // Текущие данные
    private byte[]? _currentDump;
    private AppModels.DashboardData? _currentData;
    private bool _isModified;

    public MainWindow()
    {
        InitializeComponent();
        
        // Инициализация сервисов (без DI контейнера для простоты)
        _crcCalculator = new VdoCrcCalculator();
        _dumpLoader = new DumpLoaderService();
        _dataParser = new DataParserService();
        _editCommandService = new EditCommandService(_crcCalculator);
        _cryptoService = new VdoNecCryptoService();
        
        Log("Приложение запущено. Ожидание загрузки дампа...");
        UpdateUiState(false);
    }

    /// <summary>
    /// Загрузка дампа из файла
    /// </summary>
    private async void LoadDump_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "Binary files (*.bin)|*.bin|All files (*.*)|*.*",
            Title = "Выберите файл дампа EEPROM"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            try
            {
                Log($"Загрузка файла: {openFileDialog.FileName}");
                
                _currentDump = await _dumpLoader.LoadDumpAsync(openFileDialog.FileName);
                
                Log($"Файл загружен. Размер: {_currentDump.Length} байт");
                
                // Валидация размера
                var sizeValidation = _dumpLoader.ValidateSize(_currentDump);
                if (!sizeValidation.IsValid)
                {
                    MessageBox.Show(this, sizeValidation.Message, "Ошибка валидации", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    Log($"⚠ Ошибка валидации: {sizeValidation.Message}");
                    return;
                }

                // Идентификация приборной панели
                var identification = _dumpLoader.IdentifyDashboard(_currentDump);
                
                if (identification.IsValid)
                {
                    Log($"✓ Приборная панель идентифицирована: {identification.PartNumber ?? "Неизвестно"}");
                }
                else
                {
                    Log($"⚠ Предупреждение: {identification.Error}");
                }

                // Парсинг данных
                _currentData = _dataParser.ParseDashboardData(_currentDump);
                
                // Обновление UI
                UpdateUiWithData(_currentData, identification);
                UpdateHexView(_currentDump);
                UpdateUiState(true);
                
                // Проверка доступности операций
                CheckOperationAvailability();
                
                Log("Дамп успешно загружен и проанализирован");
            }
            catch (Exception ex)
            {
                Log($"❌ Ошибка при загрузке: {ex.Message}");
                MessageBox.Show(this, $"Ошибка при загрузке файла:\n{ex.Message}", 
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    /// <summary>
    /// Применение нового значения пробега
    /// </summary>
    private void ApplyMileage_Click(object sender, RoutedEventArgs e)
    {
        if (_currentDump == null || _currentData == null)
        {
            MessageBox.Show(this, "Сначала загрузите дамп", "Ошибка", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!uint.TryParse(TxtNewMileage.Text, out uint newMileage))
        {
            MessageBox.Show(this, "Введите корректное значение пробега", "Ошибка ввода", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            Log($"Попытка изменения пробега: {_currentData.Mileage} -> {newMileage} км");
            
            var command = _editCommandService.CreateMileageCommand(newMileage);
            var result = _editCommandService.ExecuteCommand(command, _currentDump, _currentData);
            
            if (result.Success && result.ModifiedData != null)
            {
                _currentDump = result.ModifiedData;
                _currentData.Mileage = newMileage;
                _isModified = true;
                
                TxtMileage.Text = newMileage.ToString();
                UpdateHexView(_currentDump);
                
                Log($"✓ Пробег успешно изменен: {result.Message}");
                MessageBox.Show(this, result.Message, "Успех", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                Log($"❌ Ошибка изменения пробега: {result.Message}");
                MessageBox.Show(this, result.Message, "Ошибка операции", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            Log($"❌ Критическая ошибка: {ex.Message}");
            MessageBox.Show(this, $"Произошла ошибка:\n{ex.Message}", 
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Отключение иммобилайзера (IMMO OFF)
    /// </summary>
    private void ImmoOff_Click(object sender, RoutedEventArgs e)
    {
        if (_currentDump == null || _currentData == null)
        {
            MessageBox.Show(this, "Сначала загрузите дамп", "Ошибка", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!_currentData.IsImmoEnabled)
        {
            MessageBox.Show(this, "Иммобилайзер уже отключен", "Информация", 
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirmResult = MessageBox.Show(this, 
            "Вы уверены, что хотите отключить иммобилайзер?\n\n" +
            "⚠ ВНИМАНИЕ: Эта операция может привести к неработоспособности приборной панели,\n" +
            "если версия крипто-маски не поддерживается.\n\n" +
            "Рекомендуется создать резервную копию оригинального дампа перед продолжением.",
            "Подтверждение IMMO OFF",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmResult != MessageBoxResult.Yes)
            return;

        try
        {
            Log("Попытка выполнения IMMO OFF...");
            
            var command = _editCommandService.CreateImmoOffCommand();
            var result = _editCommandService.ExecuteCommand(command, _currentDump, _currentData);
            
            if (result.Success && result.ModifiedData != null)
            {
                _currentDump = result.ModifiedData;
                _currentData.IsImmoEnabled = false;
                _isModified = true;
                
                UpdateImmoStatusDisplay();
                UpdateHexView(_currentDump);
                
                Log($"✓ IMMO OFF выполнен успешно: {result.Message}");
                MessageBox.Show(this, result.Message, "Успех", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                Log($"❌ Ошибка IMMO OFF: {result.Message}");
                MessageBox.Show(this, result.Message, "Ошибка операции", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            Log($"❌ Критическая ошибка: {ex.Message}");
            MessageBox.Show(this, $"Произошла ошибка:\n{ex.Message}", 
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Обновление состояния UI
    /// </summary>
    private void UpdateUiState(bool isDumpLoaded)
    {
        BtnApplyMileage.IsEnabled = isDumpLoaded;
        BtnImmoOff.IsEnabled = isDumpLoaded;
        
        if (!isDumpLoaded)
        {
            ClearDataFields();
            HexEditor.Text = string.Empty;
            TxtDumpStatus.Text = "Дамп не загружен";
            TxtDumpSize.Text = "Размер: -";
        }
    }

    /// <summary>
    /// Обновление полей данными из дампа
    /// </summary>
    private void UpdateUiWithData(AppModels.DashboardData data, AppCore.DashboardIdentificationResult identification)
    {
        TxtPartNumber.Text = data.PartNumber ?? "Не найден";
        TxtHwVersion.Text = data.HardwareVersion ?? "-";
        TxtSwVersion.Text = data.SoftwareVersion ?? "-";
        TxtVin.Text = data.VIN ?? "Не найден";
        TxtMileage.Text = data.Mileage.ToString();
        TxtPin.Text = data.PIN ?? "Зашифрован (требуется ключ)";
        TxtCs.Text = data.ComponentSecurity != null 
            ? BitConverter.ToString(data.ComponentSecurity).Replace("-", " ") 
            : "-";
        TxtMac.Text = data.MAC != null 
            ? BitConverter.ToString(data.MAC).Replace("-", " ") 
            : "-";

        TxtDumpStatus.Text = identification.IsValid 
            ? $"✓ Дамп корректен\nТип: {identification.DetectedType}" 
            : $"⚠ {identification.Error}";
        TxtDumpSize.Text = $"Размер: {data.DumpSize} байт ({MemoryMap.GetDumpType(data.DumpSize)})";

        UpdateImmoStatusDisplay();
        
        if (data.Keys != null)
        {
            TxtKeyCount.Text = $"Ключей прописано: {data.Keys.Count}";
        }
    }

    /// <summary>
    /// Обновление отображения статуса иммобилайзера
    /// </summary>
    private void UpdateImmoStatusDisplay()
    {
        if (_currentData == null)
        {
            TxtImmoStatus.Text = "Статус: Не загружено";
            EllImmoIndicator.Fill = System.Windows.Media.Brushes.Gray;
            return;
        }

        if (_currentData.IsImmoEnabled)
        {
            TxtImmoStatus.Text = "Статус: ВКЛЮЧЕН";
            EllImmoIndicator.Fill = System.Windows.Media.Brushes.Red;
        }
        else
        {
            TxtImmoStatus.Text = "Статус: ВЫКЛЮЧЕН";
            EllImmoIndicator.Fill = System.Windows.Media.Brushes.Green;
        }
    }

    /// <summary>
    /// Обновление Hex представления
    /// </summary>
    private void UpdateHexView(byte[] data)
    {
        if (data == null || data.Length == 0)
        {
            HexEditor.Text = string.Empty;
            return;
        }

        var sb = new StringBuilder();
        int linesToShow = Math.Min(data.Length / 16 + 1, 500); // Ограничение для производительности
        
        for (int i = 0; i < linesToShow * 16 && i < data.Length; i += 16)
        {
            // Адрес строки
            sb.Append($"{i:X8}  ");
            
            // Hex байты
            for (int j = 0; j < 16; j++)
            {
                if (i + j < data.Length)
                {
                    sb.Append($"{data[i + j]:X2} ");
                    if (j == 7) sb.Append(" "); // Разделитель посередине
                }
                else
                {
                    sb.Append("   ");
                }
            }
            
            sb.Append(" |");
            
            // ASCII представление
            for (int j = 0; j < 16 && i + j < data.Length; j++)
            {
                char c = (char)data[i + j];
                sb.Append(char.IsControl(c) ? '.' : c);
            }
            
            sb.AppendLine("|");
        }
        
        if (data.Length > linesToShow * 16)
        {
            sb.AppendLine($"\n... еще {(data.Length - linesToShow * 16)} байт ...");
        }
        
        HexEditor.Text = sb.ToString();
    }

    /// <summary>
    /// Проверка доступности операций редактирования
    /// </summary>
    private void CheckOperationAvailability()
    {
        if (_currentData == null || _currentDump == null)
            return;

        // Проверка доступности IMMO OFF
        var immoCommand = _editCommandService.CreateImmoOffCommand();
        bool canImmoOff = immoCommand.CanExecute(_currentDump, _currentData);
        
        BtnImmoOff.IsEnabled = canImmoOff;
        TxtImmoWarning.Visibility = canImmoOff ? Visibility.Collapsed : Visibility.Visible;
        
        if (!canImmoOff && _currentData.IsImmoEnabled)
        {
            Log("⚠ IMMO OFF недоступен: неизвестная версия крипто-маски или структура дампа");
        }
    }

    /// <summary>
    /// Очистка полей данных
    /// </summary>
    private void ClearDataFields()
    {
        TxtPartNumber.Text = string.Empty;
        TxtHwVersion.Text = string.Empty;
        TxtSwVersion.Text = string.Empty;
        TxtVin.Text = string.Empty;
        TxtMileage.Text = string.Empty;
        TxtPin.Text = string.Empty;
        TxtCs.Text = string.Empty;
        TxtMac.Text = string.Empty;
        TxtImmoStatus.Text = "Статус: Не загружено";
        TxtKeyCount.Text = "Ключей прописано: 0";
        EllImmoIndicator.Fill = System.Windows.Media.Brushes.Gray;
    }

    /// <summary>
    /// Логирование в консоль приложения
    /// </summary>
    private void Log(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        LogConsole.AppendText($"[{timestamp}] {message}\n");
        LogConsole.ScrollToEnd();
    }

    /// <summary>
    /// Обработка закрытия окна с проверкой несохраненных изменений
    /// </summary>
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (_isModified)
        {
            var result = MessageBox.Show(this,
                "Есть несохраненные изменения в дампе.\nСохранить перед выходом?",
                "Несохраненные изменения",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Cancel)
            {
                e.Cancel = true;
                return;
            }
            
            if (result == MessageBoxResult.Yes)
            {
                SaveCurrentDump();
            }
        }
        
        base.OnClosing(e);
    }

    /// <summary>
    /// Сохранение текущего дампа в файл
    /// </summary>
    private async void SaveCurrentDump()
    {
        if (_currentDump == null)
            return;

        var saveDialog = new SaveFileDialog
        {
            Filter = "Binary files (*.bin)|*.bin|All files (*.*)|*.*",
            Title = "Сохранить модифицированный дамп",
            FileName = "modified_dump.bin"
        };

        if (saveDialog.ShowDialog() == true)
        {
            try
            {
                await _dumpLoader.SaveDumpAsync(saveDialog.FileName, _currentDump);
                Log($"✓ Дамп сохранен: {saveDialog.FileName}");
                _isModified = false;
            }
            catch (Exception ex)
            {
                Log($"❌ Ошибка сохранения: {ex.Message}");
                MessageBox.Show(this, $"Ошибка при сохранении:\n{ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
