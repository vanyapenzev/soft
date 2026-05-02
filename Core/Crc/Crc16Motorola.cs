namespace VagImmoEditor.Pro.Core.Crc
{
    /// <summary>
    /// CRC16 для Motorola HC05/HC08 (IMMO3)
    /// Polynomial: 0x8005, Initial: 0x0000, RefIn: true, RefOut: true, XorOut: 0x0000
    /// </summary>
    public class Crc16Motorola : ICrcAlgorithm
    {
        public string Name => "CRC-16/Motorola (IMMO3)";
        
        private const ushort Polynomial = 0x8005;
        private const ushort InitialValue = 0x0000;

        public ushort Calculate(byte[] data, int offset, int length)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (offset < 0 || length < 0 || offset + length > data.Length)
                throw new ArgumentOutOfRangeException();

            ushort crc = InitialValue;

            for (int i = offset; i < offset + length; i++)
            {
                byte b = data[i];
                // Reflect input byte
                b = ReflectByte(b);
                crc ^= b;
                
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 0x0001) != 0)
                        crc = (ushort)((crc >> 1) ^ Polynomial);
                    else
                        crc >>= 1;
                }
            }

            // Reflect output
            return ReflectShort(crc);
        }

        public bool Verify(byte[] data, int offset, int length, ushort expectedCrc)
        {
            var calculated = Calculate(data, offset, length);
            return calculated == expectedCrc;
        }

        private static byte ReflectByte(byte b)
        {
            byte result = 0;
            for (int i = 0; i < 8; i++)
            {
                if ((b & (1 << i)) != 0)
                    result |= (byte)(1 << (7 - i));
            }
            return result;
        }

        private static ushort ReflectShort(ushort v)
        {
            ushort result = 0;
            for (int i = 0; i < 16; i++)
            {
                if ((v & (1 << i)) != 0)
                    result |= (ushort)(1 << (15 - i));
            }
            return result;
        }
    }
}
