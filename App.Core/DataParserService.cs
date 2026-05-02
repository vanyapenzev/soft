namespace App.Core;

using App.Models;
using Microsoft.Extensions.Logging;

/// <summary>
/// Сервис для парсинга данных из дампа EEPROM
/// </summary>
public class DataParserService
{
    private readonly ILogger<DataParserService>? _logger;

    public DataParserService(ILogger<DataParserService>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Парсинг всех доступных данных из дампа
    /// </summary>
    public DashboardData ParseDashboardData(byte[] dump)
    {
        var data = new DashboardData
        {
            DumpSize = dump.Length,
            IsValidDump = false
        };

        try
        {
            // Извлечение номера детали
            data.PartNumber = ExtractPartNumber(dump);
            
            // Извлечение версий
            data.HardwareVersion = ExtractHardwareVersion(dump);
            data.SoftwareVersion = ExtractSoftwareVersion(dump);
            
            // Извлечение пробега
            data.Mileage = ParseMileage(dump);
            
            // Извлечение VIN
            data.VIN = ExtractVin(dump);
            
            // Попытка извлечения IMMO данных (может быть зашифровано)
            ParseImmoData(dump, data);
            
            data.IsValidDump = true;
            
            _logger?.LogInformation("Данные успешно распарсены. Пробег: {Mileage} км, VIN: {VIN}", 
                data.Mileage, data.VIN);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка при парсинге данных дампа");
            data.ValidationError = $"Ошибка парсинга: {ex.Message}";
        }

        return data;
    }

    /// <summary>
    /// Извлечение номера детали
    /// </summary>
    private string? ExtractPartNumber(byte[] dump)
    {
        if (dump.Length < MemoryMap.OFFSET_PART_NUMBER + 16)
            return null;

        var bytes = dump
            .Skip(MemoryMap.OFFSET_PART_NUMBER)
            .Take(16)
            .TakeWhile(b => b != 0)
            .ToArray();

        if (bytes.Length == 0)
            return null;

        string pn = System.Text.Encoding.ASCII.GetString(bytes).Trim();
        return string.Join("", pn.Where(c => char.IsLetterOrDigit(c) || c == '-'));
    }

    /// <summary>
    /// Извлечение версии железа
    /// </summary>
    private string? ExtractHardwareVersion(byte[] dump)
    {
        if (dump.Length < MemoryMap.OFFSET_HW_VERSION + 8)
            return null;

        var bytes = dump
            .Skip(MemoryMap.OFFSET_HW_VERSION)
            .Take(8)
            .TakeWhile(b => b != 0)
            .ToArray();

        return bytes.Length > 0 ? System.Text.Encoding.ASCII.GetString(bytes).Trim() : null;
    }

    /// <summary>
    /// Извлечение версии ПО
    /// </summary>
    private string? ExtractSoftwareVersion(byte[] dump)
    {
        if (dump.Length < MemoryMap.OFFSET_SW_VERSION + 8)
            return null;

        var bytes = dump
            .Skip(MemoryMap.OFFSET_SW_VERSION)
            .Take(8)
            .TakeWhile(b => b != 0)
            .ToArray();

        return bytes.Length > 0 ? System.Text.Encoding.ASCII.GetString(bytes).Trim() : null;
    }

    /// <summary>
    /// Парсинг значения одометра
    /// VDO использует несколько копий пробега с XOR контрольной суммой
    /// </summary>
    public uint ParseMileage(byte[] dump)
    {
        if (dump.Length < MemoryMap.OFFSET_MILEAGE_PRIMARY + 4)
            return 0;

        // Чтение основного значения пробега (little-endian или big-endian в зависимости от реализации)
        // VDO обычно хранит пробег в little-endian формате
        byte b0 = dump[MemoryMap.OFFSET_MILEAGE_PRIMARY];
        byte b1 = dump[MemoryMap.OFFSET_MILEAGE_PRIMARY + 1];
        byte b2 = dump[MemoryMap.OFFSET_MILEAGE_PRIMARY + 2];
        byte b3 = dump[MemoryMap.OFFSET_MILEAGE_PRIMARY + 3];

        // Проверка на инвертированное значение (VDO иногда использует инверсию)
        uint mileage = BitConverter.ToUInt32(new[] { b0, b1, b2, b3 }, 0);
        
        // Проверка на инвертированное значение
        uint invertedMileage = ~mileage;
        
        // Выбор более реалистичного значения (пробег не может быть больше 9999999 км)
        if (mileage > 9999999 && invertedMileage <= 9999999)
        {
            mileage = invertedMileage;
        }
        
        // Дополнительная проверка на копию 1
        uint backupMileage = ParseMileageFromOffset(dump, MemoryMap.OFFSET_MILEAGE_BACKUP_1);
        
        // Если основная и резервная копии совпадают - это хороший знак
        if (mileage == backupMileage || backupMileage == 0)
        {
            return mileage;
        }
        
        // Если есть расхождения, возвращаем основную копию с логом предупреждения
        _logger?.LogWarning("Расхождение между основной и резервной копией пробега: {Main} vs {Backup}", 
            mileage, backupMileage);
        
        return mileage;
    }

    /// <summary>
    /// Парсинг пробега по указанному смещению
    /// </summary>
    private uint ParseMileageFromOffset(byte[] dump, int offset)
    {
        if (dump.Length < offset + 4)
            return 0;

        byte b0 = dump[offset];
        byte b1 = dump[offset + 1];
        byte b2 = dump[offset + 2];
        byte b3 = dump[offset + 3];

        uint mileage = BitConverter.ToUInt32(new[] { b0, b1, b2, b3 }, 0);
        uint invertedMileage = ~mileage;

        return mileage > 9999999 && invertedMileage <= 9999999 ? invertedMileage : mileage;
    }

    /// <summary>
    /// Извлечение VIN кода
    /// </summary>
    private string? ExtractVin(byte[] dump)
    {
        if (dump.Length < MemoryMap.OFFSET_VIN + MemoryMap.VIN_LENGTH)
            return null;

        var vinBytes = dump
            .Skip(MemoryMap.OFFSET_VIN)
            .Take(MemoryMap.VIN_LENGTH)
            .ToArray();

        string vin = System.Text.Encoding.ASCII.GetString(vinBytes);
        
        if (vin.All(c => char.IsLetterOrDigit(c)) && vin.Length == MemoryMap.VIN_LENGTH)
        {
            return vin;
        }

        return null;
    }

    /// <summary>
    /// Парсинг данных иммобилайзера
    /// Примечание: данные могут быть зашифрованы и требовать расшифровки через Crypto модуль
    /// </summary>
    private void ParseImmoData(byte[] dump, DashboardData data)
    {
        if (dump.Length < MemoryMap.OFFSET_IMMO_STATUS + 1)
            return;

        // Чтение статуса иммобилайзера
        byte immoStatus = dump[MemoryMap.OFFSET_IMMO_STATUS];
        data.IsImmoEnabled = immoStatus == MemoryMap.IMMO_STATUS_ENABLED;
        
        _logger?.LogInformation("Статус иммобилайзера: {Status} ({Enabled})", 
            immoStatus.ToString("X2"), data.IsImmoEnabled ? "Включен" : "Выключен");

        // Попытка чтения зашифрованных данных
        // Для их расшифровки потребуется отдельный крипто-модуль или внешний файл ImmoData
        data.ComponentSecurity = dump
            .Skip(MemoryMap.OFFSET_IMMO_CS_ENC)
            .Take(7)
            .ToArray();

        data.MAC = dump
            .Skip(MemoryMap.OFFSET_IMMO_MAC_ENC)
            .Take(4)
            .ToArray();

        // PIN код (зашифрованный)
        var pinEncrypted = dump
            .Skip(MemoryMap.OFFSET_IMMO_PIN_ENC)
            .Take(5)
            .ToArray();
        
        // Пока сохраняем как есть - расшифровка будет в Crypto модуле
        data.PIN = BitConverter.ToString(pinEncrypted).Replace("-", "");
        
        // Парсинг ключей
        data.Keys = ParseKeys(dump);
    }

    /// <summary>
    /// Парсинг информации о ключах иммобилайзера
    /// </summary>
    private List<KeyInfo> ParseKeys(byte[] dump)
    {
        var keys = new List<KeyInfo>();
        
        for (int i = 0; i < MemoryMap.MAX_KEY_SLOTS; i++)
        {
            int offset = MemoryMap.OFFSET_IMMO_KEYS + (i * MemoryMap.KEY_SLOT_SIZE);
            
            if (dump.Length < offset + MemoryMap.KEY_SLOT_SIZE)
                break;

            var keyData = dump.Skip(offset).Take(MemoryMap.KEY_SLOT_SIZE).ToArray();
            
            // Проверка на пустой слот (все нули или все FF)
            bool isEmpty = keyData.All(b => b == 0x00) || keyData.All(b => b == 0xFF);
            
            if (!isEmpty)
            {
                keys.Add(new KeyInfo
                {
                    SlotIndex = i,
                    KeyId = keyData.Take(8).ToArray(), // Первые 8 байт - ID ключа
                    IsActive = true
                });
            }
        }
        
        _logger?.LogInformation("Найдено ключей: {Count}", keys.Count);
        
        return keys;
    }

    /// <summary>
    /// Расчет текущей контрольной суммы пробега
    /// </summary>
    public ushort CalculateMileageCRC(byte[] dump)
    {
        if (dump.Length < MemoryMap.OFFSET_MILEAGE_CRC + 2)
            return 0;

        // VDO использует XOR-based CRC для пробега
        // Точный алгоритм зависит от конкретной версии ПО приборки
        byte crc = 0;
        for (int i = MemoryMap.OFFSET_MILEAGE_PRIMARY; i < MemoryMap.OFFSET_MILEAGE_PRIMARY + 4; i++)
        {
            crc ^= dump[i];
        }

        return crc;
    }
}
