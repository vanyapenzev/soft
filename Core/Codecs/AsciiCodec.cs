namespace VagImmoEditor.Pro.Core.Codecs
{
    /// <summary>
    /// Кодер/декодер ASCII с поддержкой различных кодировок VAG
    /// </summary>
    public static class AsciiCodec
    {
        private static readonly System.Text.Encoding VagEncoding = System.Text.Encoding.GetEncoding(1252);

        /// <summary>
        /// Декодирование ASCII строки из байтов с обрезкой нулей и пробелов
        /// </summary>
        public static string Decode(byte[] data, int offset, int length)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (offset < 0 || length <= 0 || offset + length > data.Length)
                throw new ArgumentOutOfRangeException();

            // Находим конец строки (первый нулевой байт или конец диапазона)
            int actualLength = length;
            for (int i = offset; i < offset + length; i++)
            {
                if (data[i] == 0x00)
                {
                    actualLength = i - offset;
                    break;
                }
            }

            var bytes = new byte[actualLength];
            Array.Copy(data, offset, bytes, 0, actualLength);
            
            return VagEncoding.GetString(bytes).TrimEnd('\0', ' ', '\xFF');
        }

        /// <summary>
        /// Кодирование строки в ASCII байты с фиксированной длиной
        /// </summary>
        public static byte[] Encode(string value, int fixedLength = 0)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));

            var bytes = VagEncoding.GetBytes(value);
            
            if (fixedLength > 0)
            {
                if (bytes.Length > fixedLength)
                {
                    // Обрезаем до максимальной длины
                    var truncated = new byte[fixedLength];
                    Array.Copy(bytes, truncated, fixedLength);
                    return truncated;
                }
                else if (bytes.Length < fixedLength)
                {
                    // Дополняем нулями
                    var padded = new byte[fixedLength];
                    Array.Copy(bytes, padded, bytes.Length);
                    return padded;
                }
            }
            
            return bytes;
        }

        /// <summary>
        /// Декодирование VIN номера (17 символов)
        /// </summary>
        public static string DecodeVin(byte[] data, int offset)
        {
            return Decode(data, offset, 17);
        }

        /// <summary>
        /// Кодирование VIN номера
        /// </summary>
        public static byte[] EncodeVin(string vin)
        {
            if (string.IsNullOrEmpty(vin))
                throw new ArgumentException("VIN cannot be empty");
            
            if (vin.Length != 17)
                throw new ArgumentException("VIN must be exactly 17 characters");
            
            return Encode(vin.ToUpperInvariant(), 17);
        }
    }
}
