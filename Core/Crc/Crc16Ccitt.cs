namespace VagImmoEditor.Pro.Core.Crc
{
    /// <summary>
    /// CRC16 CCITT для VDO и большинства комбинаций приборов VAG
    /// Polynomial: 0x1021, Initial: 0xFFFF
    /// </summary>
    public class Crc16Ccitt : ICrcAlgorithm
    {
        public string Name => "CRC-16/CCITT (VDO)";
        
        private const ushort Polynomial = 0x1021;
        private const ushort InitialValue = 0xFFFF;

        public ushort Calculate(byte[] data, int offset, int length)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (offset < 0 || length < 0 || offset + length > data.Length)
                throw new ArgumentOutOfRangeException();

            ushort crc = InitialValue;

            for (int i = offset; i < offset + length; i++)
            {
                crc ^= (ushort)(data[i] << 8);
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 0x8000) != 0)
                        crc = (ushort)((crc << 1) ^ Polynomial);
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
