using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VagImmoEditor.Core.Crc;
using VagImmoEditor.Data.Maps;
using VagImmoEditor.Data.Models;

namespace VagImmoEditor.Services;

/// <summary>
/// Сервис для безопасной работы с EEPROM 24LC64 (8KB)
/// Поддерживает Undo/Redo, валидацию CRC и защиту от повреждения данных
/// </summary>
public class EepromDataService
{
    private const int EepromSize = 8192; // 24LC64 = 8KB
    private const int MinValidFileSize = 256;
    
    private readonly byte[] _data;
    private readonly Stack<ChangeRecord> _undoStack = new();
    private readonly Stack<ChangeRecord> _redoStack = new();
    private EepromMap _currentMap;
    private bool _isDirty;
    private string? _lastError;
    private readonly List<string> _operationLog = new();

    public EepromDataService()
    {
        _data = new byte[EepromSize];
        _currentMap = EepromMap.Maps.IMMO3_VDO;
        _isDirty = false;
        _lastError = null;
        _operationLog.Add("Сервис инициализирован");
    }

    /// <summary>
    /// Загрузка BIN файла с проверкой размера и CRC
    /// </summary>
    public EepromResult LoadFromFile(string filePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                _lastError = "Путь к файлу не указан";
                return new EepromResult(false, _lastError);
            }
            
            if (!File.Exists(filePath))
            {
                _lastError = "Файл не найден";
                return new EepromResult(false, _lastError);
            }

            var fileInfo = new FileInfo(filePath);
            
            // Валидация размера
            if (fileInfo.Length < MinValidFileSize || fileInfo.Length > EepromSize)
            {
                _lastError = $"Неверный размер файла: {fileInfo.Length} байт. Ожидается {MinValidFileSize}-{EepromSize} байт";
                return new EepromResult(false, _lastError);
            }

            byte[] fileData = File.ReadAllBytes(filePath);
            
            // Копируем данные в буфер с защитой от переполнения
            Array.Clear(_data, 0, _data.Length);
            int copyLength = Math.Min(fileData.Length, EepromSize);
            Buffer.BlockCopy(fileData, 0, _data, 0, copyLength);
            
            // Очищаем историю при загрузке нового файла
            _undoStack.Clear();
            _redoStack.Clear();
            _isDirty = false;
            _lastError = null;

            // Автоопределение типа IMMO и проверка CRC
            AutoDetectImmoType();
            var crcValid = ValidateCrc();
            
            _operationLog.Add($"Загружен файл: {Path.GetFileName(filePath)}, Размер: {fileInfo.Length} байт, Тип: {_currentMap.Name}, CRC: {(crcValid ? "OK" : "INVALID")}");

            return new EepromResult(true, 
                $"Файл загружен. Тип: {_currentMap.Name}. CRC: {(crcValid ? "OK" : "INVALID")}", 
                GetDataCopy());
        }
        catch (IOException ex)
        {
            _lastError = $"Ошибка чтения файла: {ex.Message}";
            _operationLog.Add($"Ошибка загрузки: {_lastError}");
            return new EepromResult(false, _lastError);
        }
        catch (Exception ex)
        {
            _lastError = $"Критическая ошибка: {ex.Message}";
            _operationLog.Add($"Критическая ошибка: {_lastError}");
            return new EepromResult(false, _lastError);
        }
    }

    /// <summary>
    /// Сохранение BIN файла с пересчетом CRC
    /// </summary>
    public EepromResult SaveToFile(string filePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                _lastError = "Путь к файлу не указан";
                return new EepromResult(false, _lastError);
            }
            
            // Проверка данных перед сохранением
            if (_data == null || _data.Length != EepromSize)
            {
                _lastError = "Некорректные данные EEPROM";
                return new EepromResult(false, _lastError);
            }
            
            // Пересчет CRC перед сохранением
            RecalculateCrc();

            File.WriteAllBytes(filePath, GetDataCopy());
            _isDirty = false;
            _operationLog.Add($"Сохранен файл: {Path.GetFileName(filePath)}");
            
            return new EepromResult(true, "Файл успешно сохранен");
        }
        catch (IOException ex)
        {
            _lastError = $"Ошибка записи файла: {ex.Message}";
            _operationLog.Add($"Ошибка сохранения: {_lastError}");
            return new EepromResult(false, _lastError);
        }
        catch (Exception ex)
        {
            _lastError = $"Критическая ошибка при сохранении: {ex.Message}";
            _operationLog.Add($"Критическая ошибка: {_lastError}");
            return new EepromResult(false, _lastError);
        }
    }

    /// <summary>
    /// Чтение байта по смещению с проверкой границ
    /// </summary>
    public byte? ReadByte(int offset)
    {
        if (offset < 0 || offset >= _data.Length)
            return null;
        
        return _data[offset];
    }

    /// <summary>
    /// Запись байта с сохранением в историю для Undo
    /// </summary>
    public bool WriteByte(int offset, byte value)
    {
        if (offset < 0 || offset >= _data.Length)
            return false;

        byte oldValue = _data[offset];
        if (oldValue == value) return true;

        _data[offset] = value;
        _undoStack.Push(new ChangeRecord(offset, oldValue, value, DateTime.Now));
        _redoStack.Clear();
        _isDirty = true;
        
        return true;
    }

    /// <summary>
    /// Запись диапазона байтов
    /// </summary>
    public bool WriteRange(int offset, byte[] data)
    {
        if (data == null) return false;
        if (offset < 0 || offset + data.Length > _data.Length)
            return false;

        bool hasChanges = false;
        for (int i = 0; i < data.Length; i++)
        {
            byte oldValue = _data[offset + i];
            if (oldValue != data[i])
            {
                _undoStack.Push(new ChangeRecord(offset + i, oldValue, data[i], DateTime.Now));
                _data[offset + i] = data[i];
                hasChanges = true;
            }
        }
        
        if (hasChanges)
        {
            _redoStack.Clear();
            _isDirty = true;
        }
        
        return true;
    }

    /// <summary>
    /// Получение копии данных (не ссылка!)
    /// </summary>
    public byte[] GetDataCopy()
    {
        return _data.ToArray();
    }

    /// <summary>
    /// Отмена последнего изменения (Undo)
    /// </summary>
    public bool Undo()
    {
        if (_undoStack.Count == 0) return false;

        var record = _undoStack.Pop();
        _data[record.Offset] = record.OldValue;
        _redoStack.Push(new ChangeRecord(record.Offset, record.NewValue, record.OldValue, DateTime.Now));
        _isDirty = true;
        
        return true;
    }

    /// <summary>
    /// Повтор изменения (Redo)
    /// </summary>
    public bool Redo()
    {
        if (_redoStack.Count == 0) return false;

        var record = _redoStack.Pop();
        _data[record.Offset] = record.NewValue;
        _undoStack.Push(new ChangeRecord(record.Offset, record.OldValue, record.NewValue, DateTime.Now));
        _isDirty = true;
        
        return true;
    }

    /// <summary>
    /// Проверка CRC на основе текущей карты памяти
    /// </summary>
    public bool ValidateCrc()
    {
        if (_currentMap.CrcOffset < 0) return true; // Нет CRC
        
        if (_currentMap.CrcOffset + _currentMap.CrcLength > _data.Length)
        {
            _lastError = "Некорректное смещение CRC";
            return false;
        }

        try
        {
            // Читаем сохраненный CRC
            ushort storedCrc;
            if (_currentMap.IsBigEndian)
                storedCrc = (ushort)((_data[_currentMap.CrcOffset] << 8) | _data[_currentMap.CrcOffset + 1]);
            else
                storedCrc = (ushort)(_data[_currentMap.CrcOffset] | (_data[_currentMap.CrcOffset + 1] << 8));

            // Вычисляем CRC для данных до CRC поля
            int crcDataLength = _currentMap.CrcOffset;
            ushort? calculatedCrc;

            switch (_currentMap.Type)
            {
                case ImmoType.IMMO3_Motorola:
                case ImmoType.IMMO4_Kayaba:
                case ImmoType.IMMO4_Bosch:
                    calculatedCrc = CrcCalculator.CalculateCrc16Motorola(_data, 0, crcDataLength);
                    break;
                default:
                    calculatedCrc = CrcCalculator.CalculateCrc16Ccitt(_data, 0, crcDataLength);
                    break;
            }

            return calculatedCrc.HasValue && storedCrc == calculatedCrc.Value;
        }
        catch (Exception ex)
        {
            _lastError = $"Ошибка проверки CRC: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Пересчет и запись CRC
    /// </summary>
    public void RecalculateCrc()
    {
        if (_currentMap.CrcOffset < 0) return;
        
        if (_currentMap.CrcOffset + _currentMap.CrcLength > _data.Length)
        {
            _lastError = "Некорректное смещение CRC для записи";
            return;
        }

        int crcDataLength = _currentMap.CrcOffset;
        ushort? newCrcNullable;

        switch (_currentMap.Type)
        {
            case ImmoType.IMMO3_Motorola:
            case ImmoType.IMMO4_Kayaba:
            case ImmoType.IMMO4_Bosch:
                newCrcNullable = CrcCalculator.CalculateCrc16Motorola(_data, 0, crcDataLength);
                break;
            default:
                newCrcNullable = CrcCalculator.CalculateCrc16Ccitt(_data, 0, crcDataLength);
                break;
        }

        if (!newCrcNullable.HasValue)
        {
            _lastError = "Не удалось вычислить CRC";
            return;
        }

        ushort newCrc = newCrcNullable.Value;

        // Записываем CRC в правильном порядке байт
        if (_currentMap.IsBigEndian)
        {
            _data[_currentMap.CrcOffset] = (byte)(newCrc >> 8);
            _data[_currentMap.CrcOffset + 1] = (byte)(newCrc & 0xFF);
        }
        else
        {
            _data[_currentMap.CrcOffset] = (byte)(newCrc & 0xFF);
            _data[_currentMap.CrcOffset + 1] = (byte)(newCrc >> 8);
        }
        
        _operationLog.Add($"CRC пересчитан: 0x{newCrc:X4}");
    }

    /// <summary>
    /// Автоопределение типа IMMO по сигнатурам и структуре данных
    /// </summary>
    private void AutoDetectImmoType()
    {
        // Проверяем наличие данных в характерных областях для разных типов
        
        // VDO: PIN обычно в 0x1E0, пробег в 0x1D0
        bool hasVdoPattern = IsDataPresent(0x1E0, 5) && IsDataPresent(0x1D0, 4);
        
        // Motorola: PIN обычно в 0x1F0, пробег в 0x1C0
        bool hasMotorolaPattern = IsDataPresent(0x1F0, 7) && IsDataPresent(0x1C0, 4);
        
        // NEC: PIN обычно в 0x1E8, пробег в 0x1D8
        bool hasNecPattern = IsDataPresent(0x1E8, 5) && IsDataPresent(0x1D8, 4);
        
        // IMMO4: данные в области 0x200+, пробег в 0x1F0
        bool hasKayabaPattern = IsDataPresent(0x200, 7) && IsDataPresent(0x1F0, 4);
        
        // IMMO4 VDO: данные в области 0x280+, пробег в 0x270
        bool hasVdo4Pattern = IsDataPresent(0x280, 7) && IsDataPresent(0x270, 4);
        
        // IMMO4 Bosch: данные в области 0x290+, пробег в 0x280
        bool hasBosch4Pattern = IsDataPresent(0x290, 7) && IsDataPresent(0x280, 4);

        // Подсчитываем количество совпадений для более точного определения
        int vdoScore = (hasVdoPattern ? 2 : 0) + (IsDataPresent(0x1B0, 17) ? 1 : 0);
        int motorolaScore = (hasMotorolaPattern ? 2 : 0) + (IsDataPresent(0x1A0, 17) ? 1 : 0);
        int necScore = (hasNecPattern ? 2 : 0) + (IsDataPresent(0x1B8, 17) ? 1 : 0);
        int kayabaScore = (hasKayabaPattern ? 2 : 0) + (IsDataPresent(0x1D0, 17) ? 1 : 0);
        int vdo4Score = (hasVdo4Pattern ? 2 : 0) + (IsDataPresent(0x250, 17) ? 1 : 0);
        int bosch4Score = (hasBosch4Pattern ? 2 : 0) + (IsDataPresent(0x260, 17) ? 1 : 0);

        // Выбираем тип с наибольшим score
        int maxScore = Math.Max(Math.Max(Math.Max(vdoScore, motorolaScore), Math.Max(necScore, kayabaScore)), Math.Max(vdo4Score, bosch4Score));
        
        if (maxScore == 0)
        {
            // Если ничего не найдено, используем эвристику по наличию данных в характерных областях
            if (_data[0x1E0] != 0x00 && _data[0x1E0] != 0xFF)
                _currentMap = EepromMap.Maps.IMMO3_VDO;
            else if (_data[0x1F0] != 0x00 && _data[0x1F0] != 0xFF)
                _currentMap = EepromMap.Maps.IMMO3_MOTOROLA;
            else if (_data[0x290] != 0x00 && _data[0x290] != 0xFF)
                _currentMap = EepromMap.Maps.IMMO4_BOSCH;
            else if (_data[0x280] != 0x00 && _data[0x280] != 0xFF)
                _currentMap = EepromMap.Maps.IMMO4_VDO;
            else if (_data[0x200] != 0x00 && _data[0x200] != 0xFF)
                _currentMap = EepromMap.Maps.IMMO4_KAYABA;
            else
                _currentMap = EepromMap.Maps.IMMO3_VDO; // Default
        }
        else if (bosch4Score == maxScore && bosch4Score > kayabaScore && bosch4Score > vdo4Score)
            _currentMap = EepromMap.Maps.IMMO4_BOSCH;
        else if (vdo4Score == maxScore && vdo4Score > kayabaScore && vdo4Score > bosch4Score)
            _currentMap = EepromMap.Maps.IMMO4_VDO;
        else if (kayabaScore == maxScore && kayabaScore > vdoScore && kayabaScore > motorolaScore)
            _currentMap = EepromMap.Maps.IMMO4_KAYABA;
        else if (motorolaScore == maxScore && motorolaScore > vdoScore)
            _currentMap = EepromMap.Maps.IMMO3_MOTOROLA;
        else if (necScore == maxScore && necScore > vdoScore)
            _currentMap = EepromMap.Maps.IMMO3_NEC;
        else
            _currentMap = EepromMap.Maps.IMMO3_VDO;
        
        _operationLog.Add($"Автоопределен тип IMMO: {_currentMap.Name} (score: VDO={vdoScore}, Moto={motorolaScore}, NEC={necScore}, Kayaba={kayabaScore}, VDO4={vdo4Score}, Bosch4={bosch4Score})");
    }
    
    /// <summary>
    /// Проверка наличия значимых данных в диапазоне
    /// </summary>
    private bool IsDataPresent(int offset, int length)
    {
        if (offset + length > _data.Length) return false;
        
        int nonZeroCount = 0;
        for (int i = offset; i < offset + length; i++)
        {
            if (_data[i] != 0x00 && _data[i] != 0xFF)
                nonZeroCount++;
        }
        
        // Считаем что данные есть если хотя бы 50% байт не 0x00/0xFF
        return nonZeroCount >= length / 2;
    }

    public EepromMap CurrentMap => _currentMap;
    public bool IsDirty => _isDirty;
    public int UndoCount => _undoStack.Count;
    public int RedoCount => _redoStack.Count;
    public string? LastError => _lastError;
    public IReadOnlyList<string> OperationLog => _operationLog.AsReadOnly();
    
    /// <summary>
    /// Получить статистику использования EEPROM
    /// </summary>
    public EepromStatistics GetStatistics()
    {
        int nonZeroCount = _data.Count(b => b != 0x00 && b != 0xFF);
        double fillPercentage = (double)nonZeroCount / _data.Length * 100;
        
        return new EepromStatistics(
            TotalSize: _data.Length,
            NonZeroBytes: nonZeroCount,
            FillPercentage: Math.Round(fillPercentage, 2),
            ImmoType: _currentMap.Name,
            IsDirty: _isDirty,
            UndoAvailable: _undoStack.Count > 0,
            RedoAvailable: _redoStack.Count > 0
        );
    }
}

/// <summary>
/// Статистика EEPROM
/// </summary>
public record EepromStatistics(
    int TotalSize,
    int NonZeroBytes,
    double FillPercentage,
    string ImmoType,
    bool IsDirty,
    bool UndoAvailable,
    bool RedoAvailable
);
