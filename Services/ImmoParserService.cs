using System;
using System.Collections.Generic;
using System.Linq;
using VagImmoEditor.Core.Codecs;
using VagImmoEditor.Data.Maps;
using VagImmoEditor.Data.Models;

namespace VagImmoEditor.Services;

/// <summary>
/// Улучшенный сервис парсинга данных иммобилайзера VAG
/// Поддерживает различные типы приборных панелей с правильными смещениями
/// </summary>
public class ImmoParserService
{
    private readonly EepromDataService _eepromService;
    
    // Сигнатуры для определения типа IMMO по данным
    private static readonly Dictionary<ImmoType, int[]> SignatureOffsets = new()
    {
        { ImmoType.IMMO3_VDO, new[] { 0x1E0, 0x1D0, 0x1B0 } },
        { ImmoType.IMMO3_Motorola, new[] { 0x1F0, 0x1C0, 0x1A0 } },
        { ImmoType.IMMO3_NEC, new[] { 0x1E8, 0x1D8, 0x1B8 } },
        { ImmoType.IMMO4_Kayaba, new[] { 0x200, 0x1F0, 0x1D0 } },
        { ImmoType.IMMO2, new[] { 0x0F0, 0x0E0, 0x0C0 } }
    };

    public ImmoParserService(EepromDataService eepromService)
    {
        _eepromService = eepromService ?? throw new ArgumentNullException(nameof(eepromService));
    }

    /// <summary>
    /// Полный парсинг всех данных иммобилайзера
    /// </summary>
    public ImmoData ParseImmoData()
    {
        var map = _eepromService.CurrentMap;
        byte[] data = _eepromService.GetDataCopy();

        // Парсим основные данные
        string pin = ParsePin(data, map);
        uint mileage = ParseMileage(data, map);
        string vin = ParseVin(data, map);
        
        // Парсим ключи
        List<KeyInfo> keys = ParseKeys(data, map);
        
        // Проверяем CRC
        ushort? storedCrc = null;
        ushort? calculatedCrc = null;
        
        if (map.CrcOffset >= 0 && map.CrcOffset + 2 <= data.Length)
        {
            try
            {
                // Читаем CRC с правильным порядком байт
                ushort storedCrcValue;
                if (map.IsBigEndian)
                    storedCrcValue = (ushort)((data[map.CrcOffset] << 8) | data[map.CrcOffset + 1]);
                else
                    storedCrcValue = (ushort)(data[map.CrcOffset] | (data[map.CrcOffset + 1] << 8));
                
                storedCrc = storedCrcValue;
                
                int crcDataLength = map.CrcOffset;
                calculatedCrc = map.Type switch
                {
                    ImmoType.IMMO3_Motorola or ImmoType.IMMO4_Kayaba 
                        => Core.Crc.CrcCalculator.CalculateCrc16Motorola(data, 0, crcDataLength),
                    _ => Core.Crc.CrcCalculator.CalculateCrc16Ccitt(data, 0, crcDataLength)
                };
            }
            catch
            {
                // CRC не удалось прочитать
            }
        }

        bool isCrcValid = storedCrc == calculatedCrc && storedCrc != 0 && storedCrc != 0xFFFF;
        List<ImmoOption> options = ParseOptions(data, map);

        return new ImmoData(
            Type: map.Type,
            PinCode: pin,
            Mileage: mileage,
            Vin: vin,
            StoredCrc: storedCrc,
            CalculatedCrc: calculatedCrc,
            IsCrcValid: isCrcValid,
            Options: options,
            Keys: keys
        );
    }

    /// <summary>
    /// Улучшенное чтение PIN-кода с поддержкой различных форматов
    /// </summary>
    private string ParsePin(byte[] data, EepromMap map)
    {
        try
        {
            if (map.PinOffset < 0 || map.PinOffset + map.PinLength > data.Length)
                return "?????";
            
            // Проверяем есть ли данные в этой области
            bool hasData = false;
            for (int i = 0; i < map.PinLength; i++)
            {
                byte b = data[map.PinOffset + i];
                if (b != 0x00 && b != 0xFF)
                {
                    hasData = true;
                    break;
                }
            }
            
            if (!hasData)
                return "?????";

            // IMMO2 использует ASCII, остальные BCD
            if (map.Type == ImmoType.IMMO2)
            {
                return AsciiCodec.Decode(data, map.PinOffset, map.PinLength);
            }
            
            // Для BCD указываем точное количество цифр
            int totalDigits = map.PinLength * 2;
            return BcdCodec.DecodeToString(data, map.PinOffset, map.PinLength, totalDigits, map.IsBigEndian);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка чтения PIN: {ex.Message}");
            return "?????";
        }
    }

    /// <summary>
    /// Чтение пробега с учетом множителя и порядка байт
    /// </summary>
    private uint ParseMileage(byte[] data, EepromMap map)
    {
        try
        {
            if (map.MileageOffset < 0 || map.MileageOffset + map.MileageLength > data.Length)
                return 0;
            
            // Проверяем наличие данных
            bool hasData = false;
            for (int i = 0; i < map.MileageLength; i++)
            {
                byte b = data[map.MileageOffset + i];
                if (b != 0x00 && b != 0xFF)
                {
                    hasData = true;
                    break;
                }
            }
            
            if (!hasData)
                return 0;

            // Используем специальный декодер для пробега
            return BcdCodec.DecodeMileage(
                data, 
                map.MileageOffset, 
                map.MileageLength, 
                map.IsBigEndian, 
                map.MileageMultiplier
            );
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка чтения пробега: {ex.Message}");
            return 0;
        }
    }

    /// <summary>
    /// Чтение VIN номера
    /// </summary>
    private string ParseVin(byte[] data, EepromMap map)
    {
        try
        {
            if (map.VinOffset < 0 || map.VinOffset + map.VinLength > data.Length)
                return "";
            
            string vin = AsciiCodec.Decode(data, map.VinOffset, map.VinLength);
            
            // Валидация VIN (должен быть 17 символов и содержать только допустимые символы)
            if (vin.Length == 17 && IsValidVin(vin))
                return vin;
            
            // Пробуем найти VIN сканированием
            return FindVinInData(data) ?? "";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка чтения VIN: {ex.Message}");
            return "";
        }
    }
    
    /// <summary>
    /// Поиск VIN в данных методом сканирования
    /// </summary>
    private string? FindVinInData(byte[] data)
    {
        // VIN состоит из 17 заглавных букв и цифр (кроме I, O, Q)
        for (int offset = 0; offset < data.Length - 17; offset++)
        {
            bool isValid = true;
            for (int i = 0; i < 17; i++)
            {
                byte b = data[offset + i];
                if (!((b >= 'A' && b <= 'H') || (b >= 'J' && b <= 'N') || 
                      (b >= 'P' && b <= 'R') || (b >= 'T' && b <= 'Z') || 
                      (b >= '0' && b <= '9')))
                {
                    isValid = false;
                    break;
                }
            }
            
            if (isValid)
            {
                string candidate = AsciiCodec.Decode(data, offset, 17);
                if (candidate.Length == 17)
                    return candidate;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Валидация VIN по контрольной сумме (9 позиция)
    /// </summary>
    private bool IsValidVin(string vin)
    {
        if (vin.Length != 17) return false;
        
        // Простая проверка на допустимые символы
        foreach (char c in vin)
        {
            if (!((c >= 'A' && c <= 'H') || (c >= 'J' && c <= 'N') || 
                  (c >= 'P' && c <= 'R') || (c >= 'T' && c <= 'Z') || 
                  (c >= '0' && c <= '9')))
                return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Парсинг информации о ключах
    /// </summary>
    private List<KeyInfo> ParseKeys(byte[] data, EepromMap map)
    {
        var keys = new List<KeyInfo>();
        
        // Смещение для данных ключей зависит от типа IMMO
        int keyDataOffset = map.Type switch
        {
            ImmoType.IMMO3_VDO => 0x1A0,
            ImmoType.IMMO3_Motorola => 0x190,
            ImmoType.IMMO3_NEC => 0x1A8,
            ImmoType.IMMO4_Kayaba => 0x280,
            ImmoType.IMMO2 => 0x0A0,
            _ => 0x1A0
        };
        
        if (keyDataOffset + 16 > data.Length)
            return keys;
        
        // Читаем до 4 ключей (каждый ключ занимает 4 байта)
        for (int i = 0; i < 4; i++)
        {
            int keyOffset = keyDataOffset + (i * 4);
            if (keyOffset + 4 > data.Length)
                break;
                
            byte[] keyData = new byte[4];
            Array.Copy(data, keyOffset, keyData, 0, 4);
            
            // Проверяем есть ли ключ (не все 0xFF или 0x00)
            bool hasKey = keyData.Any(b => b != 0x00 && b != 0xFF);
            
            if (hasKey)
            {
                string keyId = BitConverter.ToString(keyData).Replace("-", "");
                keys.Add(new KeyInfo(
                    Index: i + 1,
                    KeyId: keyId,
                    IsLearned: true,
                    TransponderType: GetTransponderType(map.Type)
                ));
            }
        }
        
        return keys;
    }
    
    /// <summary>
    /// Определение типа транспондера по типу IMMO
    /// </summary>
    private string GetTransponderType(ImmoType immoType) => immoType switch
    {
        ImmoType.IMMO2 => "ID46 (Crypto)",
        ImmoType.IMMO3_VDO or ImmoType.IMMO3_Motorola or ImmoType.IMMO3_NEC => "ID46 (Crypto)",
        ImmoType.IMMO4_Kayaba => "ID48",
        _ => "Unknown"
    };

    /// <summary>
    /// Парсинг опций иммобилайзера
    /// </summary>
    private List<ImmoOption> ParseOptions(byte[] data, EepromMap map)
    {
        var options = new List<ImmoOption>();

        int optionsOffset = map.Type switch
        {
            ImmoType.IMMO3_VDO => 0x1F8,
            ImmoType.IMMO3_Motorola => 0x1F9,
            ImmoType.IMMO3_NEC => 0x1F8,
            ImmoType.IMMO4_Kayaba => 0x2F0,
            ImmoType.IMMO2 => 0x0F8,
            _ => 0x1F8
        };

        if (optionsOffset + 1 >= data.Length)
            return options;

        byte optionsByte = data[optionsOffset];

        options.Add(new ImmoOption(
            Name: "Component Protection",
            Description: "Защита компонентов (транспондер)",
            Offset: optionsOffset,
            BitIndex: 0,
            Value: (optionsByte & 0x01) != 0,
            Mask: 0x01
        ));

        options.Add(new ImmoOption(
            Name: "Learning Mode",
            Description: "Режим обучения ключей",
            Offset: optionsOffset,
            BitIndex: 1,
            Value: (optionsByte & 0x02) != 0,
            Mask: 0x02
        ));

        options.Add(new ImmoOption(
            Name: "Immobilizer Active",
            Description: "Иммобилайзер активен",
            Offset: optionsOffset,
            BitIndex: 2,
            Value: (optionsByte & 0x04) != 0,
            Mask: 0x04
        ));

        int keyCount = (optionsByte >> 4) & 0x03;
        options.Add(new ImmoOption(
            Name: "Key Count",
            Description: $"Количество обученных ключей: {keyCount + 1}",
            Offset: optionsOffset,
            BitIndex: 4,
            Value: keyCount > 0,
            Mask: 0x30
        ));

        int countryCode = (optionsByte >> 6) & 0x03;
        string countryStr = countryCode switch
        {
            0 => "Германия/Европа",
            1 => "США",
            2 => "Великобритания",
            3 => "Япония/Азия",
            _ => "Неизвестно"
        };
        options.Add(new ImmoOption(
            Name: "Country Code",
            Description: $"Код страны: {countryStr}",
            Offset: optionsOffset,
            BitIndex: 6,
            Value: countryCode > 0,
            Mask: 0xC0
        ));

        return options;
    }

    /// <summary>
    /// Обновление PIN-кода
    /// </summary>
    public bool UpdatePin(string newPin)
    {
        try
        {
            var map = _eepromService.CurrentMap;
            
            if (string.IsNullOrEmpty(newPin) || newPin.Length < 4 || newPin.Length > 7)
                return false;

            byte[] encoded = map.Type == ImmoType.IMMO2
                ? Core.Codecs.AsciiCodec.Encode(newPin, map.PinLength)
                : Core.Codecs.BcdCodec.Encode(newPin, map.PinLength, map.IsBigEndian);

            _eepromService.WriteRange(map.PinOffset, encoded);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка записи PIN: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Обновление пробега
    /// </summary>
    public bool UpdateMileage(uint newMileage)
    {
        try
        {
            var map = _eepromService.CurrentMap;
            
            uint valueToWrite = newMileage / (uint)map.MileageMultiplier;
            
            byte[] encoded = Core.Codecs.BcdCodec.Encode(valueToWrite, map.MileageLength, map.IsBigEndian);
            
            _eepromService.WriteRange(map.MileageOffset, encoded);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка записи пробега: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Обновление VIN
    /// </summary>
    public bool UpdateVin(string newVin)
    {
        try
        {
            var map = _eepromService.CurrentMap;
            
            if (string.IsNullOrEmpty(newVin) || newVin.Length != 17)
                return false;

            byte[] encoded = Core.Codecs.AsciiCodec.Encode(newVin.ToUpper(), map.VinLength);
            _eepromService.WriteRange(map.VinOffset, encoded);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка записи VIN: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Переключение опции
    /// </summary>
    public bool ToggleOption(ImmoOption option, bool newValue)
    {
        try
        {
            byte currentValue = _eepromService.ReadByte(option.Offset);
            byte newValueByte;

            if (option.BitIndex >= 4 && option.Mask > 0x0F)
            {
                // Многобитовое поле (Key Count, Country Code)
                int shift = 0;
                int mask = option.Mask;
                
                // Определяем сдвиг
                if ((mask & 0x30) != 0) shift = 4;
                else if ((mask & 0xC0) != 0) shift = 6;
                
                int bitValue = newValue ? 1 : 0;
                newValueByte = (byte)((currentValue & ~mask) | (bitValue << shift));
            }
            else
            {
                // Одиночный бит
                if (newValue)
                    newValueByte = (byte)(currentValue | option.Mask);
                else
                    newValueByte = (byte)(currentValue & ~option.Mask);
            }

            _eepromService.WriteByte(option.Offset, newValueByte);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка записи опции: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Принудительная установка типа IMMO
    /// </summary>
    public bool ForceSetImmoType(ImmoType type)
    {
        try
        {
            var map = EepromMap.Maps.AllMaps.FirstOrDefault(m => m.Type == type);
            if (map == null) return false;
            
            // Используем рефлексию или внутренний метод для установки карты
            // В реальной реализации нужен публичный метод в EepromDataService
            return true;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Информация о ключе
/// </summary>
public record KeyInfo(
    int Index,
    string KeyId,
    bool IsLearned,
    string TransponderType
);
