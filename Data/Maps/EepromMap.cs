namespace VagImmoEditor.Pro.Data.Maps
{
    /// <summary>
    /// Карта памяти EEPROM для конкретного типа комбинации приборов
    /// </summary>
    public record EepromMap
    {
        public string Name { get; init; } = string.Empty;
        public ImmoType ImmoType { get; init; }
        
        // Смещения данных
        public int PinOffset { get; init; }
        public int PinLength { get; init; }
        public bool PinIsBcd { get; init; } = true;
        
        public int MileageOffset { get; init; }
        public int MileageLength { get; init; }
        public bool MileageIsBcd { get; init; } = true;
        public int MileageMultiplier { get; init; } = 1;
        
        public int VinOffset { get; init; } = -1;
        public int VinLength { get; init; } = 17;
        
        public int SkcOffset { get; init; } = -1;
        public int SkcLength { get; init; } = 7;
        
        public int TransponderOffset { get; init; } = -1;
        public int TransponderCount { get; init; } = 4;
        
        public int FlagsOffset { get; init; } = -1;
        public int KeyCountOffset { get; init; } = -1;
        public int CountryCodeOffset { get; init; } = -1;
        
        // CRC параметры
        public int CrcOffset { get; init; } = -1;
        public int CrcStartOffset { get; init; } = 0;
        public int CrcLength { get; init; } = 0;
        public string CrcAlgorithm { get; init; } = "CRC-16/CCITT (VDO)";
        
        // Флаги
        public int ComponentProtectionBit { get; init; } = -1;
        public int LearningModeBit { get; init; } = -1;
        public int ImmobilizerActiveBit { get; init; } = -1;
        
        public override string ToString() => Name;
    }

    /// <summary>
    /// Сервис карт памяти EEPROM
    /// </summary>
    public static class EepromMapService
    {
        private static readonly Dictionary<string, EepromMap> _maps = new();

        static EepromMapService()
        {
            // IMMO3 VDO - наиболее распространенный тип для 24LC64
            Register(new EepromMap
            {
                Name = "IMMO3 VDO (Golf IV, Passat B5, Audi A4/A6)",
                ImmoType = ImmoType.IMMO3_VDO,
                PinOffset = 0x1E0,
                PinLength = 5,
                PinIsBcd = true,
                MileageOffset = 0x1D0,
                MileageLength = 3,
                MileageIsBcd = true,
                MileageMultiplier = 10,
                VinOffset = 0x1F0,
                VinLength = 17,
                SkcOffset = 0x1E8,
                SkcLength = 7,
                TransponderOffset = 0x1A0,
                TransponderCount = 4,
                FlagsOffset = 0x1DF,
                KeyCountOffset = 0x1DE,
                CountryCodeOffset = 0x1DD,
                CrcOffset = 0x1FC,
                CrcStartOffset = 0x000,
                CrcLength = 0x1FC,
                CrcAlgorithm = "CRC-16/CCITT (VDO)",
                ComponentProtectionBit = 7,
                LearningModeBit = 6,
                ImmobilizerActiveBit = 0
            });

            // IMMO3 Motorola
            Register(new EepromMap
            {
                Name = "IMMO3 Motorola (Octavia, Fabia, Ibiza)",
                ImmoType = ImmoType.IMMO3_Motorola,
                PinOffset = 0x1F0,
                PinLength = 5,
                PinIsBcd = true,
                MileageOffset = 0x1C0,
                MileageLength = 3,
                MileageIsBcd = true,
                MileageMultiplier = 10,
                VinOffset = 0x1D0,
                VinLength = 17,
                SkcOffset = 0x1F8,
                SkcLength = 7,
                TransponderOffset = 0x190,
                TransponderCount = 4,
                FlagsOffset = 0x1EF,
                KeyCountOffset = 0x1EE,
                CountryCodeOffset = 0x1ED,
                CrcOffset = 0x1FE,
                CrcStartOffset = 0x000,
                CrcLength = 0x1FE,
                CrcAlgorithm = "CRC-16/Motorola (IMMO3)",
                ComponentProtectionBit = 7,
                LearningModeBit = 6,
                ImmobilizerActiveBit = 0
            });

            // IMMO3 NEC
            Register(new EepromMap
            {
                Name = "IMMO3 NEC (Polo, Cordoba, Toledo)",
                ImmoType = ImmoType.IMMO3_NEC,
                PinOffset = 0x1E8,
                PinLength = 5,
                PinIsBcd = true,
                MileageOffset = 0x1D8,
                MileageLength = 3,
                MileageIsBcd = true,
                MileageMultiplier = 10,
                VinOffset = 0x1F8,
                VinLength = 17,
                SkcOffset = 0x1F0,
                SkcLength = 7,
                TransponderOffset = 0x1A8,
                TransponderCount = 4,
                FlagsOffset = 0x1E7,
                KeyCountOffset = 0x1E6,
                CountryCodeOffset = 0x1E5,
                CrcOffset = 0x1FD,
                CrcStartOffset = 0x000,
                CrcLength = 0x1FD,
                CrcAlgorithm = "CRC-16/CCITT (VDO)",
                ComponentProtectionBit = 7,
                LearningModeBit = 6,
                ImmobilizerActiveBit = 0
            });

            // IMMO2
            Register(new EepromMap
            {
                Name = "IMMO2 (Early Golf III, Passat B4)",
                ImmoType = ImmoType.IMMO2,
                PinOffset = 0x050,
                PinLength = 5,
                PinIsBcd = true,
                MileageOffset = 0x040,
                MileageLength = 3,
                MileageIsBcd = true,
                MileageMultiplier = 10,
                VinOffset = -1,
                SkcOffset = 0x058,
                SkcLength = 7,
                TransponderOffset = 0x030,
                TransponderCount = 4,
                FlagsOffset = 0x05F,
                KeyCountOffset = 0x05E,
                CountryCodeOffset = -1,
                CrcOffset = -1,
                CrcAlgorithm = "CRC-8 (IMMO2)",
                ComponentProtectionBit = -1,
                LearningModeBit = 5,
                ImmobilizerActiveBit = 0
            });
        }

        public static void Register(EepromMap map)
        {
            _maps[map.Name] = map;
        }

        public static IEnumerable<EepromMap> GetAllMaps() => _maps.Values;

        public static EepromMap? GetMapByName(string name) 
            => _maps.TryGetValue(name, out var map) ? map : null;

        public static EepromMap? GetMapByImmoType(ImmoType type)
            => _maps.Values.FirstOrDefault(m => m.ImmoType == type);

        public static IEnumerable<string> GetMapNames() => _maps.Keys;
    }
}
