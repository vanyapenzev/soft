using System;

namespace VagImmoEditor.Data.Maps;

/// <summary>
/// Типы иммобилайзеров VAG
/// </summary>
public enum ImmoType
{
    Unknown = 0,
    IMMO2 = 1,
    IMMO3_VDO = 2,
    IMMO3_Motorola = 3,
    IMMO3_NEC = 4,
    IMMO4_Kayaba = 5,
    IMMO4_Denso = 6
}

/// <summary>
/// Карта памяти EEPROM для различных типов комбинаций приборов VAG
/// </summary>
public record EepromMap(
    ImmoType Type,
    string Name,
    int PinOffset,
    int PinLength,
    int MileageOffset,
    int MileageLength,
    int VinOffset,
    int VinLength,
    int CrcOffset,
    int CrcLength,
    int CryptoOffset = -1,
    int CryptoLength = 0,
    bool IsBigEndian = false,
    int MileageMultiplier = 10
)
{
    public static class Maps
    {
        public static readonly EepromMap IMMO3_VDO = new(
            Type: ImmoType.IMMO3_VDO,
            Name: "IMMO3 VDO/Siemens",
            PinOffset: 0x1E0,
            PinLength: 5,
            MileageOffset: 0x1D0,
            MileageLength: 4,
            VinOffset: 0x1B0,
            VinLength: 17,
            CrcOffset: 0x1FC,
            CrcLength: 2,
            IsBigEndian: false,
            MileageMultiplier: 10
        );

        public static readonly EepromMap IMMO3_MOTOROLA = new(
            Type: ImmoType.IMMO3_Motorola,
            Name: "IMMO3 Motorola",
            PinOffset: 0x1F0,
            PinLength: 7,
            MileageOffset: 0x1C0,
            MileageLength: 4,
            VinOffset: 0x1A0,
            VinLength: 17,
            CrcOffset: 0x1FE,
            CrcLength: 2,
            IsBigEndian: true,
            MileageMultiplier: 10
        );

        public static readonly EepromMap IMMO3_NEC = new(
            Type: ImmoType.IMMO3_NEC,
            Name: "IMMO3 NEC",
            PinOffset: 0x1E8,
            PinLength: 5,
            MileageOffset: 0x1D8,
            MileageLength: 4,
            VinOffset: 0x1B8,
            VinLength: 17,
            CrcOffset: 0x1FA,
            CrcLength: 2,
            IsBigEndian: false,
            MileageMultiplier: 10
        );

        public static readonly EepromMap IMMO2 = new(
            Type: ImmoType.IMMO2,
            Name: "IMMO2 (Early)",
            PinOffset: 0x0F0,
            PinLength: 4,
            MileageOffset: 0x0E0,
            MileageLength: 3,
            VinOffset: 0x0C0,
            VinLength: 17,
            CrcOffset: -1,
            CrcLength: 0,
            IsBigEndian: false,
            MileageMultiplier: 10
        );

        public static readonly EepromMap IMMO4_KAYABA = new(
            Type: ImmoType.IMMO4_Kayaba,
            Name: "IMMO4 Kayaba",
            PinOffset: 0x200,
            PinLength: 7,
            MileageOffset: 0x1F0,
            MileageLength: 4,
            VinOffset: 0x1D0,
            VinLength: 17,
            CrcOffset: 0x1FF,
            CrcLength: 2,
            CryptoOffset: 0x300,
            CryptoLength: 256,
            IsBigEndian: true,
            MileageMultiplier: 1
        );

        public static EepromMap[] AllMaps => new[]
        {
            IMMO2,
            IMMO3_VDO,
            IMMO3_MOTOROLA,
            IMMO3_NEC,
            IMMO4_KAYABA
        };
    }
}
