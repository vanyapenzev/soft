namespace VagImmoEditor.Pro.Core.Codecs
{
    /// <summary>
    /// Кодер/декодер BCD (Binary Coded Decimal) для PIN кодов и пробега
    /// </summary>
    public static class BcdCodec
    {
        /// <summary>
        /// Декодирование BCD в строку с сохранением ведущих нулей
        /// </summary>
        public static string Decode(byte[] data, int offset, int length, bool preserveLeadingZeros = true)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (offset < 0 || length <= 0 || offset + length > data.Length)
                throw new ArgumentOutOfRangeException();

            var result = new System.Text.StringBuilder();
            
            for (int i = 0; i < length; i++)
            {
                byte b = data[offset + i];
                int high = (b >> 4) & 0x0F;
                int low = b & 0x0F;
                
                // Проверка на корректность BCD
                if (high > 9 || low > 9)
                    throw new FormatException($"Invalid BCD value at offset {offset + i}: 0x{b:X2}");
                
                // Первый байт - обработка ведущего нуля
                if (i == 0 && !preserveLeadingZeros)
                {
                    if (high != 0) result.Append(high);
                    if (low != 0 || length == 1) result.Append(low);
                }
                else
                {
                    result.Append(high);
                    result.Append(low);
                }
            }
            
            // Гарантируем минимальную длину для PIN (5 знаков)
            if (preserveLeadingZeros && result.Length < 5)
            {
                while (result.Length < 5)
                    result.Insert(0, '0');
            }
            
            return result.ToString();
        }

        /// <summary>
        /// Кодирование строки в BCD формат
        /// </summary>
        public static byte[] Encode(string value, int minLength = 0)
        {
            if (string.IsNullOrEmpty(value))
                throw new ArgumentException("Value cannot be empty");
            
            // Проверка что все символы цифры
            foreach (char c in value)
            {
                if (!char.IsDigit(c))
                    throw new FormatException($"Invalid character '{c}' in BCD value");
            }
            
            // Дополняем до четной длины если нужно
            string paddedValue = value;
            if (minLength > 0 && value.Length < minLength)
            {
                paddedValue = value.PadLeft(minLength, '0');
            }
            else if (value.Length % 2 != 0)
            {
                paddedValue = "0" + value;
            }
            
            var result = new byte[paddedValue.Length / 2];
            for (int i = 0; i < result.Length; i++)
            {
                int high = paddedValue[i * 2] - '0';
                int low = paddedValue[i * 2 + 1] - '0';
                result[i] = (byte)((high << 4) | low);
            }
            
            return result;
        }

        /// <summary>
        /// Декодирование BCD в integer (для пробега)
        /// </summary>
        public static int DecodeToInt(byte[] data, int offset, int length)
        {
            string str = Decode(data, offset, length, false);
            return int.TryParse(str, out var result) ? result : 0;
        }

        /// <summary>
        /// Кодирование integer в BCD
        /// </summary>
        public static byte[] EncodeFromInt(int value, int minLength = 6)
        {
            return Encode(value.ToString(), minLength);
        }
    }
}
