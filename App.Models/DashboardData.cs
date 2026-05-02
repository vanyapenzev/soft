namespace App.Models;

/// <summary>
/// Структура данных для хранения информации о приборной панели VAG IMMO4
/// </summary>
public class DashboardData
{
    // Идентификация
    public string? PartNumber { get; set; }       // Номер детали (например, 5JA920810B)
    public string? HardwareVersion { get; set; }  // Версия железа
    public string? SoftwareVersion { get; set; }  // Версия ПО
    
    // Основные данные
    public uint Mileage { get; set; }             // Пробег в км
    public string? VIN { get; set; }              // VIN-код (17 символов)
    
    // IMMO4 данные (расшифрованные)
    public string? PIN { get; set; }              // PIN-код (4-5 цифр)
    public byte[]? ComponentSecurity { get; set; }// CS - 7 байт для IMMO4
    public byte[]? MAC { get; set; }              // MAC - 4 байта синхронизации с ECU
    public List<KeyInfo>? Keys { get; set; }      // Информация о прописанных ключах
    
    // Состояние
    public bool IsImmoEnabled { get; set; } = true;
    public bool IsValidDump { get; set; } = false;
    public string? ValidationError { get; set; }
    
    // Метаданные дампа
    public int DumpSize { get; set; }
    public DumpType DetectedType { get; set; }
}

/// <summary>
/// Тип дампа EEPROM
/// </summary>
public enum DumpType
{
    Unknown = 0,
    VDO_5JA920810B_24C64,    // 8KB дамп
    VDO_5JA920810B_24C32,    // 4KB дамп
    Continental_NEC          // Другие варианты Continental
}

/// <summary>
/// Информация о ключе иммобилайзера
/// </summary>
public class KeyInfo
{
    public int SlotIndex { get; set; }      // Индекс слота (0-7)
    public byte[]? KeyId { get; set; }      // ID ключа (обычно 4-8 байт)
    public bool IsActive { get; set; }      // Активен ли ключ
    public DateTime? LastUsed { get; set; } // Последнее использование (если доступно)
}

/// <summary>
/// Результат операции редактирования
/// </summary>
public class EditResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public byte[]? ModifiedData { get; set; }
    public EditOperation Operation { get; set; }
}

/// <summary>
/// Тип операции редактирования
/// </summary>
public enum EditOperation
{
    None = 0,
    MileageChange,
    ImmoOff,
    VinChange,
    CustomEdit
}
