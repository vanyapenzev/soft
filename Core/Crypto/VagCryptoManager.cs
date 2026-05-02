namespace VagImmoEditor.Pro.Core.Crypto
{
    /// <summary>
    /// Менеджер криптографических алгоритмов VAG
    /// </summary>
    public static class VagCryptoManager
    {
        private static readonly Dictionary<string, IVagCryptoAlgorithm> _algorithms = new();

        static VagCryptoManager()
        {
            RegisterAlgorithm(new XorVagAlgorithm());
            RegisterAlgorithm(new DesVagAlgorithm());
            RegisterAlgorithm(new AesVagAlgorithm());
        }

        public static void RegisterAlgorithm(IVagCryptoAlgorithm algorithm)
        {
            _algorithms[algorithm.Name] = algorithm;
        }

        public static IVagCryptoAlgorithm? GetAlgorithm(string name)
        {
            return _algorithms.TryGetValue(name, out var algo) ? algo : null;
        }

        public static IEnumerable<string> GetAvailableAlgorithms()
        {
            return _algorithms.Keys;
        }

        public static byte[] DecryptData(string algorithmName, byte[] data, byte[] key)
        {
            var algo = GetAlgorithm(algorithmName) 
                ?? throw new InvalidOperationException($"Algorithm '{algorithmName}' not found");
            
            return algo.Decrypt(data, key);
        }

        public static byte[] EncryptData(string algorithmName, byte[] data, byte[] key)
        {
            var algo = GetAlgorithm(algorithmName)
                ?? throw new InvalidOperationException($"Algorithm '{algorithmName}' not found");
            
            return algo.Encrypt(data, key);
        }
    }
}
