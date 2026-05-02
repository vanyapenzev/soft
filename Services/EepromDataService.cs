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
            
            // Копируем данные в буфер
            Array.Clear(_data, 0, _data.Length);
            Buffer.BlockCopy(fileData, 0, _data, 0, fileData.Length);
            
            // Очищаем историю при загрузке нового файла
            _undoStack.Clear();
            _redoStack.Clear();
            _isDirty = false;
            _lastError = null;

            // Автоопределение типа IMMO и проверка CRC
            AutoDetectImmoType();
            var crcValid = ValidateCrc();
            
            _operationLog.Add($"Загружен файл: {Path.GetFileName(filePath)}, Тип: {_currentMap.Name}, CRC: {(crcValid ? "OK" : "INVALID")}");

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
    public byte ReadByte(int offset)
    {
        if (offset < 0 || offset >= _data.Length)
            throw new ArgumentOutOfRangeException(nameof(offset), $"Смещение {offset} вне диапазона [0, {_data.Length - 1}]");
        
        return _data[offset];
    }

    /// <summary>
    /// Запись байта с сохранением в историю для Undo
    /// </summary>
    public void WriteByte(int offset, byte value)
    {
        if (offset < 0 || offset >= _data.Length)
            throw new ArgumentOutOfRangeException(nameof(offset));

        byte oldValue = _data[offset];
        if (oldValue == value) return;

        _data[offset] = value;
        _undoStack.Push(new ChangeRecord(offset, oldValue, value, DateTime.Now));
        _redoStack.Clear();
        _isDirty = true;
    }

    /// <summary>
    /// Запись диапазона байтов
    /// </summary>
    public void WriteRange(int offset, byte[] data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (offset < 0 || offset + data.Length > _data.Length)
            throw new ArgumentOutOfRangeException(nameof(offset), $"Диапазон [{offset}, {offset + data.Length}) вне границ EEPROM");

        for (int i = 0; i < data.Length; i++)
        {
            byte oldValue = _data[offset + i];
            if (oldValue != data[i])
            {
                _undoStack.Push(new ChangeRecord(offset + i, oldValue, data[i], DateTime.Now));
                _data[offset + i] = data[i];
            }
        }
        
        if (_undoStack.Count > 0)
            _redoStack.Clear();
        _isDirty = true;
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
            ushort storedCrc = _currentMap.IsBigEndian
                ? (ushort)((_data[_currentMap.CrcOffset] << 8) | _data[_currentMap.CrcOffset + 1])
                : (ushort)((_data[_currentMap.CrcOffset + 1] << 8) | _data[_currentMap.CrcOffset]);

            // Вычисляем CRC для данных до CRC поля
            int crcDataLength = _currentMap.CrcOffset;
            ushort calculatedCrc;

            switch (_currentMap.Type)
            {
                case ImmoType.IMMO3_Motorola:
                case ImmoType.IMMO4_Kayaba:
                    calculatedCrc = CrcCalculator.CalculateCrc16Motorola(_data, 0, crcDataLength);
                    break;
                default:
                    calculatedCrc = CrcCalculator.CalculateCrc16Ccitt(_data, 0, crcDataLength);
                    break;
            }

            return storedCrc == calculatedCrc;
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
        ushort newCrc;

        switch (_currentMap.Type)
        {
            case ImmoType.IMMO3_Motorola:
            case ImmoType.IMMO4_Kayaba:
                newCrc = CrcCalculator.CalculateCrc16Motorola(_data, 0, crcDataLength);
                break;
            default:
                newCrc = CrcCalculator.CalculateCrc16Ccitt(_data, 0, crcDataLength);
                break;
        }

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
        
        // VDO: PIN обычно в 0x1E0
        bool hasVdoPattern = IsDataPresent(0x1E0, 5) && IsDataPresent(0x1D0, 4);
        
        // Motorola: PIN обычно в 0x1F0
        bool hasMotorolaPattern = IsDataPresent(0x1F0, 7) && IsDataPresent(0x1C0, 4);
        
        // NEC: PIN обычно в 0x1E8
        bool hasNecPattern = IsDataPresent(0x1E8, 5) && IsDataPresent(0x1D8, 4);
        
        // IMMO4: данные в области 0x200+
        bool hasKayabaPattern = IsDataPresent(0x200, 7) && IsDataPresent(0x1F0, 4);

        if (hasKayabaPattern && !hasVdoPattern && !hasMotorolaPattern)
            _currentMap = EepromMap.Maps.IMMO4_KAYABA;
        else if (hasMotorolaPattern && !hasVdoPattern)
            _currentMap = EepromMap.Maps.IMMO3_MOTOROLA;
        else if (hasNecPattern && !hasVdoPattern)
            _currentMap = EepromMap.Maps.IMMO3_NEC;
        else if (hasVdoPattern)
            _currentMap = EepromMap.Maps.IMMO3_VDO;
        else
        {
            // Если ничего не найдено, используем эвристику по первому байту
            if (_data[0x1E0] != 0x00 && _data[0x1E0] != 0xFF)
                _currentMap = EepromMap.Maps.IMMO3_VDO;
            else if (_data[0x1F0] != 0x00 && _data[0x1F0] != 0xFF)
                _currentMap = EepromMap.Maps.IMMO3_MOTOROLA;
            else
                _currentMap = EepromMap.Maps.IMMO3_VDO; // Default
        }
        
        _operationLog.Add($"Автоопределен тип IMMO: {_currentMap.Name}");
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
