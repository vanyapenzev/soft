namespace VagImmoEditor.Pro.Core.Crypto
{
    /// <summary>
    /// Интерфейс для алгоритмов шифрования/дешифрования используемых в иммобилайзерах VAG
    /// </summary>
    public interface IVagCryptoAlgorithm
    {
        string Name { get; }
        byte[] Encrypt(byte[] data, byte[] key);
        byte[] Decrypt(byte[] data, byte[] key);
        bool IsValidKey(byte[] key);
    }
}
