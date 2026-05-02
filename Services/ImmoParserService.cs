using System;
using System.Collections.Generic;
using VagImmoEditor.Core.Codecs;
using VagImmoEditor.Data.Maps;
using VagImmoEditor.Data.Models;

namespace VagImmoEditor.Services;

/// <summary>
/// Сервис парсинга данных иммобилайзера из EEPROM
/// </summary>
public class ImmoParserService
{
    private readonly EepromDataService _eepromService;

    public ImmoParserService(EepromDataService eepromService)
    {
        _eepromService = eepromService ?? throw new ArgumentNullException(nameof(eepromService));
    }

    /// <summary>
    /// Парсинг всех данных иммобилайзера
    /// </summary>
    public ImmoData ParseImmoData()
    {
        var map = _eepromService.CurrentMap;
        byte[] data = _eepromService.GetDataCopy();

        // Парсим PIN
        string pin = ParsePin(data, map);

        // Парсим пробег
        uint mileage = ParseMileage(data, map);

        // Парсим VIN
        string vin = ParseVin(data, map);

        // Получаем CRC
        ushort? storedCrc = null;
        ushort? calculatedCrc = null;
        
        if (map.CrcOffset >= 0)
        {
            storedCrc = map.IsBigEndian
                ? (ushort)((data[map.CrcOffset] << 8) | data[map.CrcOffset + 1])
                : (ushort)((data[map.CrcOffset + 1] << 8) | data[map.CrcOffset]);
            
            int crcDataLength = map.CrcOffset;
            calculatedCrc = map.Type switch
            {
                ImmoType.IMMO3_Motorola or ImmoType.IMMO4_Kayaba 
                    => Core.Crc.CrcCalculator.CalculateCrc16Motorola(data, 0, crcDataLength),
                _ => Core.Crc.CrcCalculator.CalculateCrc16Ccitt(data, 0, crcDataLength)
            };
        }

        bool isCrcValid = storedCrc == calculatedCrc;

        // Парсим опции
        List<ImmoOption> options = ParseOptions(data, map);

        return new ImmoData(
            Type: map.Type,
            PinCode: pin,
            Mileage: mileage,
            Vin: vin,
            StoredCrc: storedCrc,
            CalculatedCrc: calculatedCrc,
            IsCrcValid: isCrcValid,
            Options: options
        );
    }

    /// <summary>
    /// Парсинг PIN кода
    /// </summary>
    private string ParsePin(byte[] data, EepromMap map)
    {
        try
        {
            // Для IMMO2 используем ASCII, для остальных BCD
            if (map.Type == ImmoType.IMMO2)
            {
                return AsciiCodec.Decode(data, map.PinOffset, map.PinLength);
            }
            
            // BCD декодирование с сохранением ведущих нулей
            int totalDigits = map.PinLength * 2;
            return BcdCodec.DecodeToString(data, map.PinOffset, map.PinLength, totalDigits);
        }
        catch
        {
            return "?????";
        }
    }

    /// <summary>
    /// Парсинг пробега
    /// </summary>
    private uint ParseMileage(byte[] data, EepromMap map)
    {
        try
        {
            uint rawValue;
            
            if (map.IsBigEndian)
            {
                rawValue = (uint)((data[map.MileageOffset] << 24) |
                                  (data[map.MileageOffset + 1] << 16) |
                                  (data[map.MileageOffset + 2] << 8) |
                                  data[map.MileageOffset + 3]);
            }
            else
            {
                rawValue = (uint)(data[map.MileageOffset] |
                                  (data[map.MileageOffset + 1] << 8) |
                                  (data[map.MileageOffset + 2] << 16) |
                                  (data[map.MileageOffset + 3] << 24));
            }

            // BCD декодирование
            string bcdStr = BcdCodec.DecodeToString(data, map.MileageOffset, map.MileageLength);
            if (uint.TryParse(bcdStr, out uint bcdValue))
            {
                return bcdValue * (uint)map.MileageMultiplier;
            }

            return rawValue * (uint)map.MileageMultiplier;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Парсинг VIN номера
    /// </summary>
    private string ParseVin(byte[] data, EepromMap map)
    {
        try
        {
            return AsciiCodec.Decode(data, map.VinOffset, map.VinLength);
        }
        catch
        {
            return "";
        }
    }

    /// <summary>
    /// Парсинг опций иммобилайзера
    /// </summary>
    private List<ImmoOption> ParseOptions(byte[] data, EepromMap map)
    {
        var options = new List<ImmoOption>();

        // Определяем смещение байта опций в зависимости от типа
        int optionsOffset = map.Type switch
        {
            ImmoType.IMMO3_VDO => 0x1F8,
            ImmoType.IMMO3_Motorola => 0x1F9,
            ImmoType.IMMO3_NEC => 0x1F8,
            ImmoType.IMMO4_Kayaba => 0x2F0,
            _ => 0x1F8
        };

        if (optionsOffset + 1 >= data.Length)
            return options;

        byte optionsByte = data[optionsOffset];

        // Component Protection (бит 0)
        options.Add(new ImmoOption(
            Name: "Component Protection",
            Description: "Защита компонентов (транспондер)",
            Offset: optionsOffset,
            BitIndex: 0,
            Value: (optionsByte & 0x01) != 0
        ));

        // Learning Mode (бит 1)
        options.Add(new ImmoOption(
            Name: "Learning Mode",
            Description: "Режим обучения ключей",
            Offset: optionsOffset,
            BitIndex: 1,
            Value: (optionsByte & 0x02) != 0
        ));

        // Immobilizer Active (бит 2)
        options.Add(new ImmoOption(
            Name: "Immobilizer Active",
            Description: "Иммобилайзер активен",
            Offset: optionsOffset,
            BitIndex: 2,
            Value: (optionsByte & 0x04) != 0
        ));

        // Key Count (биты 4-5)
        int keyCount = (optionsByte >> 4) & 0x03;
        options.Add(new ImmoOption(
            Name: "Key Count",
            Description: $"Количество обученных ключей: {keyCount + 1}",
            Offset: optionsOffset,
            BitIndex: 4,
            Value: keyCount > 0
        ));

        // Country Code (биты 6-7)
        int countryCode = (optionsByte >> 6) & 0x03;
        string countryStr = countryCode switch
        {
            0 => "Германия",
            1 => "США",
            2 => "Великобритания",
            3 => "Япония",
            _ => "Неизвестно"
        };
        options.Add(new ImmoOption(
            Name: "Country Code",
            Description: $"Код страны: {countryStr}",
            Offset: optionsOffset,
            BitIndex: 6,
            Value: countryCode > 0
        ));

        return options;
    }

    /// <summary>
    /// Обновление PIN кода
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
                : Core.Codecs.BcdCodec.Encode(newPin, map.PinLength);

            _eepromService.WriteRange(map.PinOffset, encoded);
            return true;
        }
        catch
        {
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
            
            // Делим на множитель для получения значения для записи
            uint valueToWrite = newMileage / (uint)map.MileageMultiplier;
            
            byte[] encoded = Core.Codecs.BcdCodec.Encode(valueToWrite, map.MileageLength);
            
            // Учитываем порядок байт
            if (map.IsBigEndian)
            {
                Array.Reverse(encoded);
            }

            _eepromService.WriteRange(map.MileageOffset, encoded);
            return true;
        }
        catch
        {
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
        catch
        {
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

            if (option.BitIndex >= 4)
            {
                // Для многобитовых опций (Key Count, Country Code)
                int mask = option.BitIndex == 4 ? 0x30 : 0xC0;
                int shift = option.BitIndex;
                int bitValue = newValue ? 1 : 0;
                
                newValueByte = (byte)((currentValue & ~mask) | (bitValue << shift));
            }
            else
            {
                // Для одиночных битов
                if (newValue)
                    newValueByte = (byte)(currentValue | option.Mask);
                else
                    newValueByte = (byte)(currentValue & ~option.Mask);
            }

            _eepromService.WriteByte(option.Offset, newValueByte);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
