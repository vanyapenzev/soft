namespace VagImmoEditor.Pro.Core.Crc
{
    /// <summary>
    /// CRC8 для простых проверок в IMMO2
    /// Polynomial: 0x07, Initial: 0x00
    /// </summary>
    public class Crc8 : ICrcAlgorithm
    {
        public string Name => "CRC-8 (IMMO2)";
        
        private const byte Polynomial = 0x07;
        private const byte InitialValue = 0x00;

        public ushort Calculate(byte[] data, int offset, int length)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (offset < 0 || length < 0 || offset + length > data.Length)
                throw new ArgumentOutOfRangeException();

            byte crc = InitialValue;

            for (int i = offset; i < offset + length; i++)
            {
                crc ^= data[i];
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 0x80) != 0)
                        crc = (byte)((crc << 1) ^ Polynomial);
                    else
                        crc <<= 1;
                }
            }

            return crc;
        }

        public bool Verify(byte[] data, int offset, int length, ushort expectedCrc)
        {
            var calculated = Calculate(data, offset, length);
            return calculated == expectedCrc;
        }
    }
}
