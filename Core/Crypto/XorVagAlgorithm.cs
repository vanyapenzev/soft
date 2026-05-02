namespace VagImmoEditor.Pro.Core.Crypto
{
    /// <summary>
    /// Реализация XOR шифрования для простых систем и тестирования
    /// </summary>
    public class XorVagAlgorithm : IVagCryptoAlgorithm
    {
        public string Name => "XOR (Simple VAG)";

        public byte[] Encrypt(byte[] data, byte[] key)
        {
            if (key == null || key.Length == 0)
                throw new ArgumentException("Key cannot be empty");

            var output = new byte[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                output[i] = (byte)(data[i] ^ key[i % key.Length]);
            }
            return output;
        }

        public byte[] Decrypt(byte[] data, byte[] key)
        {
            // XOR симметричен
            return Encrypt(data, key);
        }

        public bool IsValidKey(byte[] key)
        {
            return key != null && key.Length > 0;
        }
    }
}
