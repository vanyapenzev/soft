using System.Text;
using VagImmoEditor.Models;

namespace VagImmoEditor.Parsers;

/// <summary>
/// Парсер для EEPROM иммобилайзеров VAG (24LC64)
/// Поддерживает различные типы прошивок VAG Group (VW, Audi, Skoda, Seat)
/// </summary>
public class VagEepromParser
{
    private readonly EepromData _eeprom;
    
    public VagEepromParser(EepromData eeprom)
    {
        _eeprom = eeprom ?? throw new ArgumentNullException(nameof(eeprom));
    }
    
    /// <summary>
    /// Распарсить все доступные данные из EEPROM
    /// </summary>
    public ImmoData Parse()
    {
        var result = new ImmoData();
        
        // Попытка определить тип иммобилайзера
        result.ImmoType = DetectImmoType();
        
        // Извлечение PIN-кода
        result.PinCode = ExtractPinCode();
        
        // Извлечение пробега
        var mileageData = ExtractMileage();
        result.Mileage = mileageData.Item1;
        result.MileageUnit = mileageData.Item2;
        
        // Component ID
        result.ComponentId = ExtractComponentId();
        
        // VIN (если доступен)
        result.Vin = ExtractVin();
        
        // Количество ключей
        result.KeyCount = ExtractKeyCount();
        
        // Дополнительная информация
        result.AdditionalInfo = BuildAdditionalInfo();
        
        return result;
    }
    
    /// <summary>
    /// Определение типа иммобилайзера по сигнатурам
    /// </summary>
    private string? DetectImmoType()
    {
        var data = _eeprom.Data;
        
        // Проверка на IMMO3 (наиболее распространенный для 24LC64)
        // Сигнатура обычно находится по адресу 0x000 или 0x100
        if (data[0] == 0x01 && data[1] == 0x01)
            return "IMMO3";
        
        // Проверка на IMMO2
        if (data[0] == 0xFF && data[1] == 0xFF)
            return "IMMO2";
        
        // Проверка на VDO (Siemens)
        if (data[0] == 0x56 && data[1] == 0x44) // "VD"
            return "VDO";
        
        // Проверка на Motorola
        if (data[0] == 0x4D && data[1] == 0x4F) // "MO"
            return "Motorola";
        
        // Проверка на NEC
        if (data[0] == 0x4E && data[1] == 0x45) // "NE"
            return "NEC";
        
        return "Unknown";
    }
    
    /// <summary>
    /// Извлечение PIN-кода иммобилайзера
    /// PIN может быть 5 или 7 знаков
    /// </summary>
    private string? ExtractPinCode()
    {
        var data = _eeprom.Data;
        
        // Метод 1: Поиск по известным смещениям для IMMO3
        // Обычно PIN хранится по адресу 0x1E0 - 0x1E4 (5 знаков) или 0x1E0 - 0x1E6 (7 знаков)
        int pinOffset = FindPinOffset();
        if (pinOffset >= 0)
        {
            return DecodePin(data, pinOffset);
        }
        
        // Метод 2: Поиск паттерна PIN в EEPROM
        return SearchForPinPattern();
    }
    
    /// <summary>
    /// Поиск стандартного смещения PIN-кода
    /// </summary>
    private int FindPinOffset()
    {
        var data = _eeprom.Data;
        
        // Стандартные смещения для различных типов
        int[] commonOffsets = { 0x1E0, 0x1F0, 0x200, 0x210, 0x100, 0x110 };
        
        foreach (int offset in commonOffsets)
        {
            if (offset + 7 <= data.Length)
            {
                // Проверка на валидный PIN (BCD или ASCII)
                if (IsValidPinLocation(data, offset))
                    return offset;
            }
        }
        
        return -1;
    }
    
    /// <summary>
    /// Проверка, является ли область потенциальным хранением PIN
    /// </summary>
    private bool IsValidPinLocation(byte[] data, int offset)
    {
        // PIN в BCD формате: каждый байт содержит две цифры (0x00-0x99)
        for (int i = 0; i < 5; i++)
        {
            byte b = data[offset + i];
            if (b > 0x99 || (b & 0x0F) > 9 || ((b >> 4) & 0x0F) > 9)
            {
                // Не BCD, проверяем ASCII
                if (!IsValidAsciiPinChar(data[offset + i]))
                    return false;
            }
        }
        
        return true;
    }
    
    /// <summary>
    /// Декодирование PIN-кода из байтов с правильной обработкой ведущих нулей
    /// </summary>
    private string DecodePin(byte[] data, int offset)
    {
        // BCD формат: каждый байт = две цифры
        StringBuilder pin = new StringBuilder();
        
        bool isBcd = true;
        for (int i = 0; i < 5; i++)
        {
            byte b = data[offset + i];
            if (b > 0x99 || (b & 0x0F) > 9 || ((b >> 4) & 0x0F) > 9)
            {
                isBcd = false;
                break;
            }
        }
        
        if (isBcd)
        {
            // BCD формат: каждый байт содержит две цифры
            // Важно: сохраняем ВСЕ цифры включая ведущие нули для 5-значного PIN
            for (int i = 0; i < 5; i++)
            {
                byte b = data[offset + i];
                int high = (b >> 4) & 0x0F;
                int low = b & 0x0F;
                
                // Для первого байта проверяем special case
                if (i == 0)
                {
                    // Если оба ниббла нулевые - пропускаем
                    if (high == 0 && low == 0)
                        continue;
                    
                    // Если high ноль, но low не ноль - добавляем только low
                    // Это正确处理 для PIN типа "01234" -> первый байт 0x01
                    if (high == 0)
                    {
                        pin.Append(low);
                    }
                    else
                    {
                        pin.Append(high);
                        pin.Append(low);
                    }
                }
                else
                {
                    // Для остальных байтов всегда добавляем обе цифры
                    pin.Append(high);
                    pin.Append(low);
                }
            }
            
            // Нормализуем длину PIN
            // PIN должен быть 4-7 знаков
            if (pin.Length >= 4 && pin.Length <= 7)
                return pin.ToString();
            
            // Если PIN короче 4 знаков, возможно это 5-значный PIN с ведущими нулями
            // Пробуем альтернативный подход: читаем все 5 байт как 10 цифр и берем последние 5
            if (pin.Length < 4)
            {
                pin.Clear();
                for (int i = 0; i < 5; i++)
                {
                    byte b = data[offset + i];
                    int high = (b >> 4) & 0x0F;
                    int low = b & 0x0F;
                    
                    // Добавляем обе цифры если они не 0xFF (пустое значение)
                    if (high != 0xF) pin.Append(high);
                    if (low != 0xF) pin.Append(low);
                }
                
                // Берем последние 5 цифр если их больше
                if (pin.Length > 5)
                    return pin.ToString(pin.Length - 5, 5);
                
                if (pin.Length >= 4)
                    return pin.ToString();
            }
        }
        
        // ASCII формат - резервный вариант
        pin.Clear();
        for (int i = 0; i < 7; i++)
        {
            byte b = data[offset + i];
            if (IsValidAsciiPinChar(b))
                pin.Append((char)b);
            else if (pin.Length > 0)
                break;
        }
        
        if (pin.Length >= 4 && pin.Length <= 7)
            return pin.ToString();
        
        return null;
    }
    
    /// <summary>
    /// Поиск PIN по паттерну в EEPROM
    /// </summary>
    private string? SearchForPinPattern()
    {
        var data = _eeprom.Data;
        
        // Поиск последовательности, похожей на PIN
        for (int i = 0; i < data.Length - 5; i++)
        {
            // Проверка на BCD PIN
            bool isBcdPin = true;
            for (int j = 0; j < 5; j++)
            {
                byte b = data[i + j];
                if ((b & 0x0F) > 9 || ((b >> 4) & 0x0F) > 9)
                {
                    isBcdPin = false;
                    break;
                }
            }
            
            if (isBcdPin)
            {
                string? pin = DecodePin(data, i);
                if (!string.IsNullOrEmpty(pin) && pin.Length >= 4)
                    return pin;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Проверка символа на принадлежность к PIN-коду
    /// </summary>
    private bool IsValidAsciiPinChar(byte b)
    {
        return (b >= '0' && b <= '9') || (b >= 'A' && b <= 'Z');
    }
    
    /// <summary>
    /// Извлечение данных о пробеге
    /// </summary>
    private Tuple<int, string> ExtractMileage()
    {
        var data = _eeprom.Data;
        
        // Стандартные смещения для пробега в IMMO3
        int[] mileageOffsets = { 0x1D0, 0x1C0, 0x1B0, 0x1A0 };
        
        foreach (int offset in mileageOffsets)
        {
            if (offset + 4 <= data.Length)
            {
                int mileage = DecodeMileage(data, offset);
                if (mileage > 0 && mileage < 10000000) // Разумный диапазон
                {
                    return new Tuple<int, string>(mileage, "km");
                }
            }
        }
        
        return new Tuple<int, string>(0, "km");
    }
    
    /// <summary>
    /// Декодирование значения пробега
    /// </summary>
    private int DecodeMileage(byte[] data, int offset)
    {
        // Пробег обычно хранится в little-endian или big-endian формате
        // Иногда с множителем (например, x10 или x100)
        
        // Little-endian
        int mileageLe = BitConverter.ToInt32(data, offset);
        if (mileageLe > 0 && mileageLe < 10000000)
            return mileageLe;
        
        // Big-endian
        byte[] reversed = new byte[4];
        for (int i = 0; i < 4; i++)
            reversed[i] = data[offset + 3 - i];
        
        int mileageBe = BitConverter.ToInt32(reversed, 0);
        if (mileageBe > 0 && mileageBe < 10000000)
            return mileageBe;
        
        // С множителем 10
        if (mileageLe > 0 && mileageLe % 10 == 0 && mileageLe / 10 < 10000000)
            return mileageLe / 10;
        
        return 0;
    }
    
    /// <summary>
    /// Извлечение Component ID
    /// </summary>
    private string? ExtractComponentId()
    {
        var data = _eeprom.Data;
        
        // Component ID обычно ASCII строка по определенным смещениям
        int[] idOffsets = { 0x010, 0x020, 0x030, 0x100 };
        
        foreach (int offset in idOffsets)
        {
            if (offset + 16 <= data.Length)
            {
                string id = Encoding.ASCII.GetString(data, offset, 16).TrimEnd('\0');
                if (!string.IsNullOrEmpty(id) && IsValidComponentId(id))
                    return id;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Проверка валидности Component ID
    /// </summary>
    private bool IsValidComponentId(string id)
    {
        // Component ID обычно содержит буквы и цифры, может иметь формат типа "4B0953257"
        if (id.Length < 8 || id.Length > 16)
            return false;
        
        foreach (char c in id)
        {
            if (!char.IsLetterOrDigit(c) && c != ' ')
                return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Извлечение VIN (если доступен)
    /// </summary>
    private string? ExtractVin()
    {
        var data = _eeprom.Data;
        
        // VIN может храниться в разных местах, длина 17 символов
        for (int i = 0; i < data.Length - 17; i++)
        {
            bool isValidVin = true;
            for (int j = 0; j < 17; j++)
            {
                byte b = data[i + j];
                if (!((b >= 'A' && b <= 'Z') || (b >= '0' && b <= '9')))
                {
                    isValidVin = false;
                    break;
                }
            }
            
            if (isValidVin)
            {
                string vin = Encoding.ASCII.GetString(data, i, 17);
                if (IsValidVinFormat(vin))
                    return vin;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Базовая проверка формата VIN
    /// </summary>
    private bool IsValidVinFormat(string vin)
    {
        if (vin.Length != 17)
            return false;
        
        // VIN не должен содержать букв I, O, Q
        if (vin.Contains('I') || vin.Contains('O') || vin.Contains('Q'))
            return false;
        
        return true;
    }
    
    /// <summary>
    /// Извлечение количества ключей
    /// </summary>
    private int ExtractKeyCount()
    {
        var data = _eeprom.Data;
        
        // Количество ключей обычно хранится в одном байте
        int[] keyCountOffsets = { 0x1F8, 0x1F9, 0x1FA, 0x200 };
        
        foreach (int offset in keyCountOffsets)
        {
            if (offset < data.Length)
            {
                int count = data[offset];
                if (count >= 1 && count <= 8) // Разумное количество ключей
                    return count;
            }
        }
        
        return 0;
    }
    
    /// <summary>
    /// Построение дополнительной информации
    /// </summary>
    private string BuildAdditionalInfo()
    {
        var info = new StringBuilder();
        
        info.AppendLine($"CRC16: 0x{_eeprom.CalculateCrc16():X4}");
        info.AppendLine($"Size: {_eeprom.Length} bytes");
        
        return info.ToString();
    }
}
