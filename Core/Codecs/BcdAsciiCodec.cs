using System;
using System.Text;

namespace VagImmoEditor.Core.Codecs;

/// <summary>
/// Кодер/декодер BCD (Binary Coded Decimal)
/// Используется в VAG для хранения PIN, пробега и других числовых значений
/// </summary>
public static class BcdCodec
{
    /// <summary>
    /// Декодирование BCD в строку с сохранением ведущих нулей
    /// </summary>
    public static string DecodeToString(byte[] data, int offset, int length, int totalDigits = -1)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (offset < 0 || length < 0 || offset + length > data.Length)
            throw new ArgumentOutOfRangeException();

        StringBuilder result = new StringBuilder();
        
        for (int i = 0; i < length; i++)
        {
            byte b = data[offset + i];
            int high = (b >> 4) & 0x0F;
            int low = b & 0x0F;
            
            // Проверка на заполнитель 0xF или 0x0
            if (high != 0xF && high <= 9)
                result.Append(high);
            if (low != 0xF && low <= 9)
                result.Append(low);
        }
        
        // Добавляем ведущие нули если указано totalDigits
        if (totalDigits > 0 && result.Length < totalDigits)
        {
            while (result.Length < totalDigits)
                result.Insert(0, '0');
        }
        
        return result.ToString();
    }

    /// <summary>
    /// Декодирование BCD в число (uint)
    /// </summary>
    public static uint DecodeToUint(byte[] data, int offset, int length)
    {
        string str = DecodeToString(data, offset, length);
        return uint.TryParse(str, out var value) ? value : 0;
    }

    /// <summary>
    /// Кодирование строки в BCD
    /// </summary>
    public static byte[] Encode(string value, int byteLength)
    {
        if (string.IsNullOrEmpty(value))
            throw new ArgumentException("Value cannot be null or empty", nameof(value));
        
        // Удаляем нецифровые символы
        string digits = new string(Array.FindAll(value.ToCharArray(), char.IsDigit));
        
        byte[] result = new byte[byteLength];
        Array.Fill(result, 0xFF); // Заполняем 0xFF как padding
        
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
        
        return result;
    }

    /// <summary>
    /// Кодирование числа в BCD
    /// </summary>
    public static byte[] Encode(uint value, int byteLength)
    {
        return Encode(value.ToString(), byteLength);
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
        Array.Fill(result, 0x00); // Заполняем нулями
        
        byte[] encoded = Encoding.ASCII.GetBytes(value);
        int copyLength = Math.Min(encoded.Length, length);
        Array.Copy(encoded, 0, result, 0, copyLength);
        
        return result;
    }
}
