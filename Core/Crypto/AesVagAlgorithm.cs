using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Modes;

namespace VagImmoEditor.Pro.Core.Crypto
{
    /// <summary>
    /// Реализация AES шифрования для IMMO4 и современных систем VAG
    /// </summary>
    public class AesVagAlgorithm : IVagCryptoAlgorithm
    {
        public string Name => "AES-128-CBC (VAG IMMO4)";

        public byte[] Encrypt(byte[] data, byte[] key)
        {
            if (!IsValidKey(key))
                throw new ArgumentException("Invalid key length. Must be 16 bytes for AES-128");

            var engine = new AesEngine();
            var cipher = new CbcBlockCipher(engine);
            var iv = new byte[16]; // Zero IV для совместимости со старыми системами
            
            var parameters = new ParametersWithIV(new KeyParameter(key), iv);
            cipher.Init(true, parameters);

            var output = new byte[data.Length];
            var offset = 0;
            
            while (offset < data.Length)
            {
                var blockSize = Math.Min(16, data.Length - offset);
                var block = new byte[16];
                Array.Copy(data, offset, block, 0, blockSize);
                
                cipher.ProcessBlock(block, 0, output, offset);
                offset += blockSize;
            }

            return output;
        }

        public byte[] Decrypt(byte[] data, byte[] key)
        {
            if (!IsValidKey(key))
                throw new ArgumentException("Invalid key length. Must be 16 bytes for AES-128");

            var engine = new AesEngine();
            var cipher = new CbcBlockCipher(engine);
            var iv = new byte[16];
            
            var parameters = new ParametersWithIV(new KeyParameter(key), iv);
            cipher.Init(false, parameters);

            var output = new byte[data.Length];
            var offset = 0;
            
            while (offset < data.Length)
            {
                var blockSize = Math.Min(16, data.Length - offset);
                var block = new byte[16];
                Array.Copy(data, offset, block, 0, blockSize);
                
                cipher.ProcessBlock(block, 0, output, offset);
                offset += blockSize;
            }

            return output;
        }

        public bool IsValidKey(byte[] key)
        {
            return key != null && (key.Length == 16 || key.Length == 24 || key.Length == 32);
        }
    }
}
