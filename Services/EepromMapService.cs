using System;
using System.Collections.Generic;

namespace VagImmoEditor.Services;

/// <summary>
/// Сервис карт памяти для различных типов комбинаций приборов VAG
/// Содержит реальные смещения для разных типов IMMO
/// </summary>
public class EepromMapService
{
    private static readonly Dictionary<string, EepromMap> _maps = new();
    
    static EepromMapService()
    {
        InitializeMaps();
    }
    
    private static void InitializeMaps()
    {
        // IMMO3 VDO (Siemens) - наиболее распространенный
        _maps["IMMO3_VDO"] = new EepromMap
        {
            Name = "IMMO3 VDO",
            Description = "VDO/Siemens комбинации приборов (Audi A4/A6, VW Passat B5)",
            PinOffset = 0x1E0,
            PinLength = 5,
            PinFormat = PinFormat.Bcd,
            MileageOffset = 0x1D0,
            MileageLength = 4,
            MileageMultiplier = 1,
            MileageEndian = Endian.Little,
            KeyCountOffset = 0x1F8,
            ComponentIdOffset = 0x010,
            ComponentIdLength = 16,
            VinOffset = 0x200,
            VinLength = 17,
            OptionsOffset = 0x1F5,
            CrcOffset = 0x1FFC,
            HasCrc = true
        };
        
        // IMMO3 Motorola
        _maps["IMMO3_MOTOROLA"] = new EepromMap
        {
            Name = "IMMO3 Motorola",
            Description = "Motorola комбинации приборов (Audi A3, VW Golf IV)",
            PinOffset = 0x1F0,
            PinLength = 5,
            PinFormat = PinFormat.Bcd,
            MileageOffset = 0x1C0,
            MileageLength = 4,
            MileageMultiplier = 1,
            MileageEndian = Endian.Big,
            KeyCountOffset = 0x1F9,
            ComponentIdOffset = 0x020,
            ComponentIdLength = 16,
            VinOffset = 0x210,
            VinLength = 17,
            OptionsOffset = 0x1F6,
            CrcOffset = -1,
            HasCrc = false
        };
        
        // IMMO3 NEC
        _maps["IMMO3_NEC"] = new EepromMap
        {
            Name = "IMMO3 NEC",
            Description = "NEC комбинации приборов (Skoda Octavia, Seat Leon)",
            PinOffset = 0x1E0,
            PinLength = 7,
            PinFormat = PinFormat.Bcd,
            MileageOffset = 0x1B0,
            MileageLength = 4,
            MileageMultiplier = 10,
            MileageEndian = Endian.Little,
            KeyCountOffset = 0x1FA,
            ComponentIdOffset = 0x030,
            ComponentIdLength = 16,
            VinOffset = 0x220,
            VinLength = 17,
            OptionsOffset = 0x1F7,
            CrcOffset = 0x1FFE,
            HasCrc = true
        };
        
        // IMMO2 (старые системы)
        _maps["IMMO2"] = new EepromMap
        {
            Name = "IMMO2",
            Description = "Старые системы IMMO2 (VW Golf III, Passat B3/B4)",
            PinOffset = 0x100,
            PinLength = 4,
            PinFormat = PinFormat.Ascii,
            MileageOffset = 0x0F0,
            MileageLength = 3,
            MileageMultiplier = 10,
            MileageEndian = Endian.Little,
            KeyCountOffset = 0x110,
            ComponentIdOffset = -1,
            ComponentIdLength = 0,
            VinOffset = -1,
            VinLength = 0,
            OptionsOffset = 0x108,
            CrcOffset = -1,
            HasCrc = false
        };
        
        // VDO High Line
        _maps["VDO_HIGH"] = new EepromMap
        {
            Name = "VDO High Line",
            Description = "VDO High Line (Audi A8, VW Phaeton)",
            PinOffset = 0x1E0,
            PinLength = 7,
            PinFormat = PinFormat.Bcd,
            MileageOffset = 0x1D0,
            MileageLength = 4,
            MileageMultiplier = 1,
            MileageEndian = Endian.Little,
            KeyCountOffset = 0x1F8,
            ComponentIdOffset = 0x010,
            ComponentIdLength = 20,
            VinOffset = 0x250,
            VinLength = 17,
            OptionsOffset = 0x1F5,
            CrcOffset = 0x1FFC,
            HasCrc = true
        };
        
        // IMMO4 Kayaba (начальная поддержка)
        _maps["IMMO4_KAYABA"] = new EepromMap
        {
            Name = "IMMO4 Kayaba",
            Description = "IMMO4 системы с шифрованием (Audi A4 B7, A6 C6 после 2005)",
            PinOffset = 0x400, // Смещено из-за шифрования
            PinLength = 7,
            PinFormat = PinFormat.Encrypted,
            MileageOffset = 0x3F0,
            MileageLength = 4,
            MileageMultiplier = 1,
            MileageEndian = Endian.Little,
            KeyCountOffset = 0x410,
            ComponentIdOffset = 0x010,
            ComponentIdLength = 20,
            VinOffset = 0x500,
            VinLength = 17,
            OptionsOffset = 0x408,
            CrcOffset = 0x1FFC,
            HasCrc = true,
            IsEncrypted = true
        };
    }
    
    /// <summary>
    /// Получить карту памяти по типу IMMO
    /// </summary>
    public static EepromMap? GetMap(string immoType)
    {
        if (string.IsNullOrEmpty(immoType))
            return null;
            
        // Нормализуем имя типа
        string normalizedType = immoType.ToUpper().Replace(" ", "_");
        
        if (_maps.TryGetValue(normalizedType, out var map))
            return map;
        
        // Попытка найти по частичному совпадению
        foreach (var kvp in _maps)
        {
            if (normalizedType.Contains(kvp.Key) || kvp.Key.Contains(normalizedType))
                return kvp.Value;
        }
        
        // Возвращаем карту по умолчанию (IMMO3 VDO)
        return _maps["IMMO3_VDO"];
    }
    
    /// <summary>
    /// Получить все доступные карты
    /// </summary>
    public static IEnumerable<EepromMap> GetAllMaps()
    {
        return _maps.Values;
    }
    
    /// <summary>
    /// Автоопределение типа IMMO по данным EEPROM
    /// </summary>
    public static string DetectImmoType(byte[] data)
    {
        if (data == null || data.Length < 256)
            return "Unknown";
        
        // Проверка сигнатур
        
        // VDO: обычно начинается с 0x01 0x01 или имеет паттерн VDO
        if ((data[0] == 0x01 && data[1] == 0x01) || 
            (data[0] == 0x56 && data[1] == 0x44)) // "VD"
            return "IMMO3_VDO";
        
        // Motorola: сигнатура MO
        if (data[0] == 0x4D && data[1] == 0x4F) // "MO"
            return "IMMO3_MOTOROLA";
        
        // NEC: сигнатура NE
        if (data[0] == 0x4E && data[1] == 0x45) // "NE"
            return "IMMO3_NEC";
        
        // IMMO2: часто заполнен FF в начале
        if (data[0] == 0xFF && data[1] == 0xFF)
            return "IMMO2";
        
        // Эвристическая проверка по наличию VIN
        for (int i = 0x200; i < 0x300; i++)
        {
            if (i + 17 < data.Length && IsValidVinPattern(data, i))
                return "IMMO3_VDO";
        }
        
        return "IMMO3_VDO"; // По умолчанию
    }
    
    private static bool IsValidVinPattern(byte[] data, int offset)
    {
        int validChars = 0;
        for (int i = 0; i < 17; i++)
        {
            byte b = data[offset + i];
            if ((b >= 'A' && b <= 'Z') || (b >= '0' && b <= '9'))
            {
                // VIN не содержит I, O, Q
                if (b != 'I' && b != 'O' && b != 'Q')
                    validChars++;
            }
        }
        return validChars >= 15; // Минимум 15 валидных символов
    }
}

/// <summary>
/// Карта памяти EEPROM для конкретного типа комбинации приборов
/// </summary>
public class EepromMap
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    // PIN код
    public int PinOffset { get; set; }
    public int PinLength { get; set; }
    public PinFormat PinFormat { get; set; }
    
    // Пробег
    public int MileageOffset { get; set; }
    public int MileageLength { get; set; }
    public int MileageMultiplier { get; set; }
    public Endian MileageEndian { get; set; }
    
    // Ключи
    public int KeyCountOffset { get; set; }
    
    // Component ID
    public int ComponentIdOffset { get; set; }
    public int ComponentIdLength { get; set; }
    
    // VIN
    public int VinOffset { get; set; }
    public int VinLength { get; set; }
    
    // Опции
    public int OptionsOffset { get; set; }
    
    // CRC
    public int CrcOffset { get; set; }
    public bool HasCrc { get; set; }
    
    // Шифрование
    public bool IsEncrypted { get; set; }
}

/// <summary>
/// Формат хранения PIN кода
/// </summary>
public enum PinFormat
{
    Bcd,        // BCD кодирование (каждый байт = 2 цифры)
    Ascii,      // ASCII строка
    Encrypted   // Зашифрованные данные
}

/// <summary>
/// Порядок байтов (Endianness)
/// </summary>
public enum Endian
{
    Little,     // Little-endian (Intel)
    Big         // Big-endian (Motorola)
}
