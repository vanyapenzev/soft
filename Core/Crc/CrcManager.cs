namespace VagImmoEditor.Pro.Core.Crc
{
    /// <summary>
    /// Менеджер алгоритмов контрольной суммы
    /// </summary>
    public static class CrcManager
    {
        private static readonly Dictionary<string, ICrcAlgorithm> _algorithms = new();

        static CrcManager()
        {
            RegisterAlgorithm(new Crc8());
            RegisterAlgorithm(new Crc16Ccitt());
            RegisterAlgorithm(new Crc16Motorola());
        }

        public static void RegisterAlgorithm(ICrcAlgorithm algorithm)
        {
            _algorithms[algorithm.Name] = algorithm;
        }

        public static ICrcAlgorithm? GetAlgorithm(string name)
        {
            return _algorithms.TryGetValue(name, out var algo) ? algo : null;
        }

        public static IEnumerable<string> GetAvailableAlgorithms()
        {
            return _algorithms.Keys;
        }

        public static ushort CalculateCrc(string algorithmName, byte[] data, int offset, int length)
        {
            var algo = GetAlgorithm(algorithmName)
                ?? throw new InvalidOperationException($"CRC algorithm '{algorithmName}' not found");
            
            return algo.Calculate(data, offset, length);
        }

        public static bool VerifyCrc(string algorithmName, byte[] data, int offset, int length, ushort expectedCrc)
        {
            var algo = GetAlgorithm(algorithmName)
                ?? throw new InvalidOperationException($"CRC algorithm '{algorithmName}' not found");
            
            return algo.Verify(data, offset, length, expectedCrc);
        }
    }
}
