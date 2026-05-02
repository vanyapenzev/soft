namespace VagImmoEditor.Pro.Data.Models
{
    /// <summary>
    /// Типы иммобилайзеров VAG
    /// </summary>
    public enum ImmoType
    {
        Unknown = 0,
        IMMO2 = 1,           // Ранние системы (1995-1997)
        IMMO3_VDO = 2,       // VDO (1997-2001)
        IMMO3_Motorola = 3,  // Motorola (1997-2001)
        IMMO3_NEC = 4,       // NEC (1997-2001)
        IMMO4_Kayaba = 5,    // Kayaba (2001-2005)
        IMMO4_Delphi = 6,    // Delphi (2001-2005)
        MQB = 7              // Современные MQB платформы (2012+)
    }

    /// <summary>
    /// Производитель комбинации приборов
    /// </summary>
    public enum ClusterManufacturer
    {
        Unknown = 0,
        VDO = 1,
        MagnetiMarelli = 2,
        Bosch = 3,
        Siemens = 4,
        Continental = 5
    }

    /// <summary>
    /// Структура данных EEPROM
    /// </summary>
    public record EepromInfo
    {
        public string ChipType { get; init; } = "24LC64";
        public int SizeBytes { get; init; } = 8192;
        public int PageSize { get; init; } = 32;
        public bool IsProtected { get; init; } = false;
    }

    /// <summary>
    /// Данные иммобилайзера распарсенные из EEPROM
    /// </summary>
    public record ImmoData
    {
        public ImmoType ImmoType { get; init; }
        public ClusterManufacturer Manufacturer { get; init; }
        
        public string PinCode { get; init; } = string.Empty;
        public int Mileage { get; init; }
        public string MileageUnit { get; init; } = "km";
        public string? Vin { get; init; }
        
        public byte[] Skc { get; init; } = Array.Empty<byte>();  // Secret Key Code
        public byte[] TransponderIds { get; init; } = Array.Empty<byte>();
        
        public bool IsImmobilizerActive { get; init; } = true;
        public int KeyCount { get; init; }
        public short CountryCode { get; init; }
        
        public bool ComponentProtection { get; init; }
        public bool LearningMode { get; init; }
        
        public ushort StoredCrc { get; init; }
        public ushort CalculatedCrc { get; init; }
        public bool CrcValid => StoredCrc == CalculatedCrc;
        
        public string CrcStatus => CrcValid ? "VALID" : "INVALID";
        public string ImmoTypeName => ImmoType.ToString();
        public string ManufacturerName => Manufacturer.ToString();
    }

    /// <summary>
    /// Опция иммобилайзера для редактирования
    /// </summary>
    public record ImmoOption
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public bool IsEnabled { get; set; }
        public bool IsReadOnly { get; init; } = false;
        public string? Tooltip { get; init; }
    }

    /// <summary>
    /// Запись изменения для Undo/Redo
    /// </summary>
    public record ChangeRecord
    {
        public int Offset { get; init; }
        public byte OldValue { get; init; }
        public byte NewValue { get; init; }
        public string Description { get; init; } = string.Empty;
        public DateTime Timestamp { get; init; } = DateTime.Now;
    }

    /// <summary>
    /// Результат операции с EEPROM
    /// </summary>
    public record EepromResult
    {
        public bool Success { get; init; }
        public string Message { get; init; } = string.Empty;
        public Exception? Error { get; init; }
        
        public static EepromResult Ok(string message = "Success") 
            => new() { Success = true, Message = message };
        
        public static EepromResult Fail(string message, Exception? error = null) 
            => new() { Success = false, Message = message, Error = error };
    }
}
