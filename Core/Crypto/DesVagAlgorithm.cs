using System.Security.Cryptography;

namespace VagImmoEditor.Pro.Core.Crypto
{
    /// <summary>
    /// Реализация DES шифрования для IMMO3 и старых систем VAG (VDO, Motorola)
    /// </summary>
    public class DesVagAlgorithm : IVagCryptoAlgorithm
    {
        public string Name => "DES-ECB (VAG IMMO3)";

        public byte[] Encrypt(byte[] data, byte[] key)
        {
            if (!IsValidKey(key))
                throw new ArgumentException("Invalid key length. Must be 8 bytes for DES");

            using var des = DES.Create();
            des.Mode = CipherMode.ECB;
            des.Padding = PaddingMode.None;
            des.Key = key.Take(8).ToArray();

            using var transform = des.CreateEncryptor();
            return TransformBlocks(transform, data);
        }

        public byte[] Decrypt(byte[] data, byte[] key)
        {
            if (!IsValidKey(key))
                throw new ArgumentException("Invalid key length. Must be 8 bytes for DES");

            using var des = DES.Create();
            des.Mode = CipherMode.ECB;
            des.Padding = PaddingMode.None;
            des.Key = key.Take(8).ToArray();

            using var transform = des.CreateDecryptor();
            return TransformBlocks(transform, data);
        }

        private static byte[] TransformBlocks(ICryptoTransform transform, byte[] data)
        {
            var output = new byte[data.Length];
            var offset = 0;

            while (offset < data.Length)
            {
                var blockSize = Math.Min(8, data.Length - offset);
                var block = new byte[8];
                Array.Copy(data, offset, block, 0, blockSize);

                transform.TransformBlock(block, 0, 8, output, offset);
                offset += blockSize;
            }

            return output;
        }

        public bool IsValidKey(byte[] key)
        {
            return key != null && key.Length >= 8;
        }
    }
}
