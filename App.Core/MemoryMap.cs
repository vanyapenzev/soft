namespace App.Core;

/// <summary>
/// Карта памяти EEPROM для приборной панели VDO 5JA920810B VD1
/// Содержит смещения (offsets) всех значимых данных в дампе
/// </summary>
public static class MemoryMap
{
    // Размеры дампов
    public const int SIZE_24C32 = 4096;   // 4KB
    public const int SIZE_24C64 = 8192;   // 8KB
    
    // Сигнатуры и идентификаторы
    public const int OFFSET_PART_NUMBER = 0x01F0;    // Номер детали (ASCII, ~10 байт)
    public const int OFFSET_HW_VERSION = 0x0200;     // Версия железа
    public const int OFFSET_SW_VERSION = 0x0210;     // Версия ПО
    
    // Данные одометра (VDO использует несколько копий для надежности)
    public const int OFFSET_MILEAGE_PRIMARY = 0x0A00;    // Основное значение пробега
    public const int OFFSET_MILEAGE_BACKUP_1 = 0x0A10;   // Резервная копия 1
    public const int OFFSET_MILEAGE_BACKUP_2 = 0x0A20;   // Резервная копия 2
    public const int OFFSET_MILEAGE_CRC = 0x0A30;        // CRC пробега
    
    // VIN код (17 байт ASCII + возможные дополнения)
    public const int OFFSET_VIN = 0x0500;
    public const int VIN_LENGTH = 17;
    
    // IMMO4 данные (зашифрованный блок)
    public const int OFFSET_IMMO_DATA = 0x0600;
    public const int OFFSET_IMMO_STATUS = 0x0600;      // Байт состояния иммобилайзера
    public const int OFFSET_IMMO_PIN_ENC = 0x0610;     // Зашифрованный PIN
    public const int OFFSET_IMMO_CS_ENC = 0x0620;      // Зашифрованный CS (7 байт)
    public const int OFFSET_IMMO_MAC_ENC = 0x0630;     // Зашифрованный MAC (4 байта)
    public const int OFFSET_IMMO_KEYS = 0x0700;        // Данные ключей (до 8 ключей)
    
    // Ключи иммобилайзера (каждый ключ занимает 16 байт)
    public const int KEY_SLOT_SIZE = 16;
    public const int MAX_KEY_SLOTS = 8;
    
    // Контрольные суммы
    public const int OFFSET_GLOBAL_CRC = 0x1FE0;       // Общая CRC всего дампа
    public const int OFFSET_BLOCK_CRC_START = 0x1FF0;  // Начало блока CRC
    
    // Флаги и статусы
    public const byte IMMO_STATUS_ENABLED = 0x06;
    public const byte IMMO_STATUS_DISABLED = 0x04;
    
    /// <summary>
    /// Проверка размера дампа на соответствие известным типам
    /// </summary>
    public static bool IsValidDumpSize(int size)
    {
        return size == SIZE_24C32 || size == SIZE_24C64;
    }
    
    /// <summary>
    /// Получение типа дампа по размеру
    /// </summary>
    public static string GetDumpType(int size)
    {
        return size switch
        {
            SIZE_24C32 => "24C32 (4KB)",
            SIZE_24C64 => "24C64 (8KB)",
            _ => "Unknown"
        };
    }
}
