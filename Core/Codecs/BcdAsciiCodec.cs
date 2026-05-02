using System;
using System.Text;

namespace VagImmoEditor.Core.Codecs;

/// <summary>
/// Кодер/декодер BCD (Binary Coded Decimal)
/// Используется в VAG для хранения PIN, пробега и других числовых значений
/// Поддерживает различные форматы: прямой BCD, обратный BCD, с заполнителем 0xF/0x0
/// </summary>
public static class BcdCodec
{
    /// <summary>
    /// Декодирование BCD в строку с сохранением ведущих нулей
    /// Поддерживает различные типы заполнителей и порядки байт
    /// </summary>
    public static string DecodeToString(byte[] data, int offset, int length, int totalDigits = -1, bool isBigEndian = false)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (offset < 0 || length < 0 || offset + length > data.Length)
            throw new ArgumentOutOfRangeException();

        StringBuilder result = new StringBuilder();
        
        // Создаем временный буфер для правильного порядка байт
        byte[] buffer = new byte[length];
        if (isBigEndian)
        {
            for (int i = 0; i < length; i++)
                buffer[i] = data[offset + length - 1 - i];
        }
        else
        {
            Array.Copy(data, offset, buffer, 0, length);
        }
        
        for (int i = 0; i < length; i++)
        {
            byte b = buffer[i];
            int high = (b >> 4) & 0x0F;
            int low = b & 0x0F;
            
            // Проверка на заполнитель 0xF или 0x0
            // В VAG обычно используется 0xF как заполнитель, но иногда 0x0
            if (high != 0xF && high <= 9)
                result.Append(high);
            else if (high == 0x0 && i == 0)
                ; // Пропускаем ведущий 0x0 только если это первый байт
            
            if (low != 0xF && low <= 9)
                result.Append(low);
            else if (low == 0x0 && i == length - 1 && result.Length == 0)
                ; // Сохраняем хотя бы один ноль если все остальные заполнители
        }
        
        // Добавляем ведущие нули если указано totalDigits
        if (totalDigits > 0 && result.Length < totalDigits)
        {
            while (result.Length < totalDigits)
                result.Insert(0, '0');
        }
        
        // Если результат пустой, возвращаем нули
        if (result.Length == 0 && totalDigits > 0)
        {
            return new string('0', totalDigits);
        }
        
        return result.ToString();
    }

    /// <summary>
    /// Декодирование BCD в число (uint) с множителем
    /// </summary>
    public static uint DecodeToUint(byte[] data, int offset, int length, bool isBigEndian = false)
    {
        string str = DecodeToString(data, offset, length, -1, isBigEndian);
        return uint.TryParse(str, out var value) ? value : 0;
    }

    /// <summary>
    /// Кодирование строки в BCD
    /// </summary>
    public static byte[] Encode(string value, int byteLength, bool isBigEndian = false)
    {
        if (string.IsNullOrEmpty(value))
            throw new ArgumentException("Value cannot be null or empty", nameof(value));
        
        // Удаляем нецифровые символы
        string digits = new string(Array.FindAll(value.ToCharArray(), char.IsDigit));
        
        byte[] result = new byte[byteLength];
        Array.Fill<byte>(result, 0xFF); // Заполняем 0xFF как padding
        
        int digitIndex = digits.Length - 1;
        for (int i = byteLength - 1; i >= 0 && digitIndex >= 0; i--)
        {
            byte low = (byte)(digits[digitIndex] - '0');
            digitIndex--;
            
            byte high = 0xF;
            if (digitIndex >= 0)
            {
                high = (byte)(digits[digitIndex] - '0');
                digitIndex--;
            }
            
            result[i] = (byte)((high << 4) | low);
        }
        
        // Меняем порядок байт если нужно
        if (isBigEndian)
        {
            Array.Reverse(result);
        }
        
        return result;
    }

    /// <summary>
    /// Кодирование числа в BCD
    /// </summary>
    public static byte[] Encode(uint value, int byteLength, bool isBigEndian = false)
    {
        return Encode(value.ToString(), byteLength, isBigEndian);
    }
    
    /// <summary>
    /// Специальный декодер для пробега VAG (с множителем x10)
    /// </summary>
    public static uint DecodeMileage(byte[] data, int offset, int length, bool isBigEndian = false, int multiplier = 10)
    {
        uint rawValue = DecodeToUint(data, offset, length, isBigEndian);
        return rawValue * (uint)multiplier;
    }
}

/// <summary>
/// Кодер/декодер ASCII строк
/// </summary>
public static class AsciiCodec
{
    /// <summary>
    /// Декодирование ASCII строки с обрезкой нулей и пробелов
    /// </summary>
    public static string Decode(byte[] data, int offset, int length)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (offset < 0 || length < 0 || offset + length > data.Length)
            throw new ArgumentOutOfRangeException();

        string result = Encoding.ASCII.GetString(data, offset, length);
        
        // Обрезаем terminating null и пробелы
        int nullIndex = result.IndexOf('\0');
        if (nullIndex >= 0)
            result = result.Substring(0, nullIndex);
        
        return result.Trim();
    }

    /// <summary>
    /// Кодирование строки в ASCII с фиксированной длиной
    /// </summary>
    public static byte[] Encode(string value, int length)
    {
        if (string.IsNullOrEmpty(value))
            throw new ArgumentException("Value cannot be null or empty", nameof(value));
        
        byte[] result = new byte[length];
        Array.Fill<byte>(result, 0x00); // Заполняем нулями
        
        byte[] encoded = Encoding.ASCII.GetBytes(value);
        int copyLength = Math.Min(encoded.Length, length);
        Array.Copy(encoded, 0, result, 0, copyLength);
        
        return result;
    }
}
