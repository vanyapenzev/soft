using System;
using System.Collections.Generic;
using System.Linq;

namespace VagImmoEditor.Models;

/// <summary>
/// Модель данных для EEPROM 24LC64 (8KB = 8192 байта)
/// Хранит структуру прошивки иммобилайзера VAG
/// </summary>
public class EepromData
{
    public const int EepromSize = 8192; // 24LC64 = 8KB
    public const int MinValidFileSize = 256; // Минимальный разумный размер файла
    
    private readonly byte[] _data;
    private readonly List<ChangeRecord> _changeHistory;
    private int _currentHistoryIndex = -1;
    private bool _isCrcValid;
    private ushort _storedCrc;
    
    public EepromData()
    {
        _data = new byte[EepromSize];
        _changeHistory = new List<ChangeRecord>();
        _isCrcValid = true;
    }
    
    public EepromData(byte[] data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
            
        if (data.Length > EepromSize)
            throw new ArgumentException($"Data size exceeds EEPROM capacity. Max: {EepromSize} bytes, got: {data.Length}");
        
        if (data.Length < MinValidFileSize)
            throw new ArgumentException($"Data size too small. Min: {MinValidFileSize} bytes, got: {data.Length}");
        
        _data = new byte[EepromSize];
        Buffer.BlockCopy(data, 0, _data, 0, data.Length);
        _changeHistory = new List<ChangeRecord>();
        
        // Проверяем CRC при загрузке
        ValidateCrc();
    }
    
    /// <summary>
    /// Проверка CRC при загрузке файла
    /// </summary>
    private void ValidateCrc()
    {
        // Попытка найти сохраненный CRC в последних 2 байтах
        _storedCrc = BitConverter.ToUInt16(_data, EepromSize - 2);
        ushort calculatedCrc = CalculateCrc16(0, EepromSize - 2);
        _isCrcValid = (_storedCrc == 0xFFFF) || (_storedCrc == calculatedCrc);
    }
    
    public byte[] Data => _data.ToArray(); // Возвращаем копию для безопасности
    
    public byte this[int index]
    {
        get
        {
            if (index < 0 || index >= EepromSize)
                throw new ArgumentOutOfRangeException(nameof(index), $"Index must be between 0 and {EepromSize - 1}");
            return _data[index];
        }
        set
        {
            if (index < 0 || index >= EepromSize)
                throw new ArgumentOutOfRangeException(nameof(index), $"Index must be between 0 and {EepromSize - 1}");
            
            RecordChange(index, _data[index], value);
            _data[index] = value;
            _isCrcValid = false;
        }
    }
    
    public int Length => _data.Length;
    
    public bool IsCrcValid => _isCrcValid;
    
    public IReadOnlyList<ChangeRecord> ChangeHistory => _changeHistory.AsReadOnly();
    
    /// <summary>
    /// Получить массив байтов из указанного диапазона
    /// </summary>
    public byte[] GetRange(int offset, int length)
    {
        if (offset < 0 || length < 0)
            throw new ArgumentOutOfRangeException("Offset and length must be non-negative");
            
        if (offset + length > EepromSize)
            throw new ArgumentOutOfRangeException($"Range [{offset}, {offset + length}) exceeds EEPROM size {EepromSize}");
        
        var result = new byte[length];
        Buffer.BlockCopy(_data, offset, result, 0, length);
        return result;
    }
    
    /// <summary>
    /// Записать массив байтов по указанному смещению с записью в историю
    /// </summary>
    public void SetRange(int offset, byte[] data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
            
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset), "Offset must be non-negative");
            
        if (offset + data.Length > EepromSize)
            throw new ArgumentOutOfRangeException($"Range [{offset}, {offset + data.Length}) exceeds EEPROM size {EepromSize}");
        
        // Записываем изменения в историю
        for (int i = 0; i < data.Length; i++)
        {
            RecordChange(offset + i, _data[offset + i], data[i]);
        }
        
        // Исправленный Buffer.BlockCopy - был баг с лишним параметром 0
        Buffer.BlockCopy(data, 0, _data, offset, data.Length);
        _isCrcValid = false;
    }
    
    /// <summary>
    /// Запись изменения в историю для Undo/Redo
    /// </summary>
    private void RecordChange(int offset, byte oldValue, byte newValue)
    {
        if (oldValue == newValue)
            return;
        
        // Удаляем будущую историю если мы сделали undo и затем изменили данные
        if (_currentHistoryIndex < _changeHistory.Count - 1)
        {
            _changeHistory.RemoveRange(_currentHistoryIndex + 1, _changeHistory.Count - _currentHistoryIndex - 1);
        }
        
        _changeHistory.Add(new ChangeRecord(offset, oldValue, newValue, DateTime.Now));
        _currentHistoryIndex = _changeHistory.Count - 1;
        
        // Ограничиваем историю последними 100 изменениями
        if (_changeHistory.Count > 100)
        {
            _changeHistory.RemoveAt(0);
            _currentHistoryIndex--;
        }
    }
    
    /// <summary>
    /// Отменить последнее изменение (Undo)
    /// </summary>
    public bool Undo()
    {
        if (_currentHistoryIndex < 0)
            return false;
        
        var change = _changeHistory[_currentHistoryIndex];
        _data[change.Offset] = change.OldValue;
        _currentHistoryIndex--;
        return true;
    }
    
    /// <summary>
    /// Повторить отмененное изменение (Redo)
    /// </summary>
    public bool Redo()
    {
        if (_currentHistoryIndex >= _changeHistory.Count - 1)
            return false;
        
        _currentHistoryIndex++;
        var change = _changeHistory[_currentHistoryIndex];
        _data[change.Offset] = change.NewValue;
        return true;
    }
    
    /// <summary>
    /// Получить CRC всего дампа
    /// </summary>
    public ushort CalculateCrc16(int offset = 0, int? length = null)
    {
        int len = length ?? (EepromSize - offset);
        ushort crc = 0;
        
        for (int i = offset; i < offset + len && i < EepromSize; i++)
        {
            crc ^= _data[i];
            for (int j = 0; j < 8; j++)
            {
                if ((crc & 1) != 0)
                    crc = (ushort)((crc >> 1) ^ 0x8408); // Полином CCITT
                else
                    crc >>= 1;
            }
        }
        
        return crc;
    }
    
    /// <summary>
    /// Обновить CRC в файле
    /// </summary>
    public void UpdateCrc()
    {
        ushort crc = CalculateCrc16(0, EepromSize - 2);
        byte[] crcBytes = BitConverter.GetBytes(crc);
        _data[EepromSize - 2] = crcBytes[0];
        _data[EepromSize - 1] = crcBytes[1];
        _isCrcValid = true;
    }
    
    /// <summary>
    /// Сохранить в файл с обновлением CRC
    /// </summary>
    public void SaveToFile(string path, bool updateCrc = true)
    {
        if (updateCrc)
            UpdateCrc();
        
        File.WriteAllBytes(path, _data);
    }
    
    /// <summary>
    /// Загрузить из файла с валидацией
    /// </summary>
    public static EepromData LoadFromFile(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"File not found: {path}");
        
        var fileInfo = new FileInfo(path);
        if (fileInfo.Length > EepromSize)
            throw new ArgumentException($"File too large. Max: {EepromSize} bytes, got: {fileInfo.Length}");
        
        if (fileInfo.Length < MinValidFileSize)
            throw new ArgumentException($"File too small. Min: {MinValidFileSize} bytes, got: {fileInfo.Length}");
        
        var data = File.ReadAllBytes(path);
        return new EepromData(data);
    }
    
    /// <summary>
    /// Экспорт изменений в JSON формат
    /// </summary>
    public string ExportChangesToJson()
    {
        var json = new System.Text.StringBuilder();
        json.AppendLine("{");
        json.AppendLine("  \"changes\": [");
        
        for (int i = 0; i < _changeHistory.Count; i++)
        {
            var change = _changeHistory[i];
            json.Append($"    {{ \"offset\": {change.Offset}, \"oldValue\": {change.OldValue}, \"newValue\": {change.NewValue}, \"timestamp\": \"{change.Timestamp:O}\" }}");
            if (i < _changeHistory.Count - 1)
                json.AppendLine(",");
            else
                json.AppendLine();
        }
        
        json.AppendLine("  ]");
        json.AppendLine("}");
        
        return json.ToString();
    }
}

/// <summary>
/// Запись об изменении для истории Undo/Redo
/// </summary>
public record ChangeRecord(int Offset, byte OldValue, byte NewValue, DateTime Timestamp);
