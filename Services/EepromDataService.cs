using System;
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

    public EepromDataService()
    {
        _data = new byte[EepromSize];
        _currentMap = EepromMap.Maps.IMMO3_VDO;
        _isDirty = false;
    }

    /// <summary>
    /// Загрузка BIN файла с проверкой размера и CRC
    /// </summary>
    public EepromResult LoadFromFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return new EepromResult(false, "Файл не найден");

            var fileInfo = new FileInfo(filePath);
            
            // Валидация размера
            if (fileInfo.Length < MinValidFileSize || fileInfo.Length > EepromSize)
                return new EepromResult(false, 
                    $"Неверный размер файла: {fileInfo.Length} байт. Ожидается {MinValidFileSize}-{EepromSize} байт");

            byte[] fileData = File.ReadAllBytes(filePath);
            
            // Копируем данные в буфер
            Array.Clear(_data, 0, _data.Length);
            Buffer.BlockCopy(fileData, 0, _data, 0, fileData.Length);
            
            // Очищаем историю при загрузке нового файла
            _undoStack.Clear();
            _redoStack.Clear();
            _isDirty = false;

            // Автоопределение типа IMMO и проверка CRC
            AutoDetectImmoType();
            var crcValid = ValidateCrc();

            return new EepromResult(true, 
                $"Файл загружен. Тип: {_currentMap.Name}. CRC: {(crcValid ? "OK" : "INVALID")}", 
                GetDataCopy());
        }
        catch (Exception ex)
        {
            return new EepromResult(false, $"Ошибка загрузки: {ex.Message}");
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
            
            return new EepromResult(true, "Файл успешно сохранен");
        }
        catch (Exception ex)
        {
            return new EepromResult(false, $"Ошибка сохранения: {ex.Message}");
        }
    }

    /// <summary>
    /// Чтение байта по смещению с проверкой границ
    /// </summary>
    public byte ReadByte(int offset)
    {
        if (offset < 0 || offset >= _data.Length)
            throw new ArgumentOutOfRangeException(nameof(offset));
        
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
            throw new ArgumentOutOfRangeException(nameof(offset));

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
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Пересчет и запись CRC
    /// </summary>
    public void RecalculateCrc()
    {
        if (_currentMap.CrcOffset < 0) return;

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
    }

    /// <summary>
    /// Автоопределение типа IMMO по сигнатурам и структуре данных
    /// </summary>
    private void AutoDetectImmoType()
    {
        // Простая эвристика по наличию данных в определенных областях
        // В реальной реализации нужно более сложное определение
        
        bool hasVdoPattern = _data[0x1E0] != 0xFF && _data[0x1E0] != 0x00;
        bool hasMotorolaPattern = _data[0x1F0] != 0xFF && _data[0x1F0] != 0x00;
        
        if (hasMotorolaPattern && !hasVdoPattern)
            _currentMap = EepromMap.Maps.IMMO3_MOTOROLA;
        else if (hasVdoPattern)
            _currentMap = EepromMap.Maps.IMMO3_VDO;
        else
            _currentMap = EepromMap.Maps.IMMO3_VDO; // Default
    }

    public EepromMap CurrentMap => _currentMap;
    public bool IsDirty => _isDirty;
    public int UndoCount => _undoStack.Count;
    public int RedoCount => _redoStack.Count;
}
