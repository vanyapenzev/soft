using VagImmoEditor.Pro.Core.Crc;
using VagImmoEditor.Pro.Core.Codecs;
using VagImmoEditor.Pro.Data.Maps;
using VagImmoEditor.Pro.Data.Models;

namespace VagImmoEditor.Pro.Services
{
    /// <summary>
    /// Сервис парсинга данных иммобилайзера из EEPROM
    /// </summary>
    public class ImmoParserService
    {
        private readonly EepromDataService _eepromService;

        public ImmoParserService(EepromDataService eepromService)
        {
            _eepromService = eepromService;
        }

        /// <summary>
        /// Автоматическое определение типа иммобилайзера по сигнатурам и паттернам
        /// </summary>
        public ImmoType DetectImmoType()
        {
            var data = _eepromService.Data;

            // Проверка сигнатур VDO
            if (data[0x1E0] != 0xFF && IsValidBcd(data, 0x1E0, 5))
                return ImmoType.IMMO3_VDO;

            // Проверка сигнатур Motorola
            if (data[0x1F0] != 0xFF && IsValidBcd(data, 0x1F0, 5))
                return ImmoType.IMMO3_Motorola;

            // Проверка сигнатур NEC
            if (data[0x1E8] != 0xFF && IsValidBcd(data, 0x1E8, 5))
                return ImmoType.IMMO3_NEC;

            // Проверка IMMO2
            if (data[0x050] != 0xFF && IsValidBcd(data, 0x050, 5))
                return ImmoType.IMMO2;

            return ImmoType.Unknown;
        }

        /// <summary>
        /// Парсинг всех данных иммобилайзера с использованием карты памяти
        /// </summary>
        public ImmoData ParseImmoData(EepromMap? map = null)
        {
            var data = _eepromService.Data;
            
            // Автоопределение карты если не указана
            if (map == null)
            {
                var type = DetectImmoType();
                map = EepromMapService.GetMapByImmoType(type);
            }

            if (map == null)
            {
                return new ImmoData
                {
                    ImmoType = ImmoType.Unknown,
                    Manufacturer = ClusterManufacturer.Unknown,
                    PinCode = "N/A",
                    Mileage = 0,
                    CrcStatus = "UNKNOWN"
                };
            }

            // Определение производителя по имени карты
            var manufacturer = map.Name.Contains("VDO") ? ClusterManufacturer.VDO :
                              map.Name.Contains("Motorola") ? ClusterManufacturer.MagnetiMarelli :
                              map.Name.Contains("NEC") ? ClusterManufacturer.Siemens :
                              ClusterManufacturer.Unknown;

            // Парсинг PIN кода
            string pinCode = "N/A";
            if (map.PinOffset >= 0 && map.PinOffset + map.PinLength <= data.Length)
            {
                try
                {
                    if (map.PinIsBcd)
                        pinCode = BcdCodec.Decode(data, map.PinOffset, map.PinLength);
                    else
                        pinCode = AsciiCodec.Decode(data, map.PinOffset, map.PinLength);
                }
                catch
                {
                    pinCode = "Invalid";
                }
            }

            // Парсинг пробега
            int mileage = 0;
            string mileageUnit = "km";
            if (map.MileageOffset >= 0 && map.MileageOffset + map.MileageLength <= data.Length)
            {
                try
                {
                    if (map.MileageIsBcd)
                        mileage = BcdCodec.DecodeToInt(data, map.MileageOffset, map.MileageLength) * map.MileageMultiplier;
                    else
                    {
                        var mileageBytes = _eepromService.GetRange(map.MileageOffset, map.MileageLength);
                        mileage = BitConverter.ToInt32(mileageBytes, 0) * map.MileageMultiplier;
                    }
                }
                catch
                {
                    mileage = 0;
                }
            }

            // Парсинг VIN
            string? vin = null;
            if (map.VinOffset >= 0 && map.VinOffset + map.VinLength <= data.Length)
            {
                try
                {
                    vin = AsciiCodec.DecodeVin(data, map.VinOffset);
                    if (string.IsNullOrWhiteSpace(vin) || vin.All(c => c == '\0' || c == 0xFF))
                        vin = null;
                }
                catch
                {
                    vin = null;
                }
            }

            // Парсинг SKC
            byte[] skc = Array.Empty<byte>();
            if (map.SkcOffset >= 0 && map.SkcOffset + map.SkcLength <= data.Length)
            {
                skc = _eepromService.GetRange(map.SkcOffset, map.SkcLength);
            }

            // Парсинг количества ключей
            int keyCount = 0;
            if (map.KeyCountOffset >= 0)
            {
                keyCount = data[map.KeyCountOffset];
            }

            // Парсинг кода страны
            short countryCode = 0;
            if (map.CountryCodeOffset >= 0)
            {
                countryCode = (short)data[map.CountryCodeOffset];
            }

            // Парсинг флагов
            bool componentProtection = false;
            bool learningMode = false;
            bool immobilizerActive = true;

            if (map.FlagsOffset >= 0)
            {
                byte flags = data[map.FlagsOffset];
                
                if (map.ComponentProtectionBit >= 0)
                    componentProtection = (flags & (1 << map.ComponentProtectionBit)) != 0;
                
                if (map.LearningModeBit >= 0)
                    learningMode = (flags & (1 << map.LearningModeBit)) != 0;
                
                if (map.ImmobilizerActiveBit >= 0)
                    immobilizerActive = (flags & (1 << map.ImmobilizerActiveBit)) != 0;
            }

            // Расчет CRC
            ushort storedCrc = 0;
            ushort calculatedCrc = 0;
            
            if (map.CrcOffset >= 0 && map.CrcOffset + 2 <= data.Length)
            {
                storedCrc = BitConverter.ToUInt16(data, map.CrcOffset);
                
                if (map.CrcLength > 0)
                {
                    try
                    {
                        calculatedCrc = _eepromService.CalculateCrc(
                            map.CrcAlgorithm, 
                            map.CrcStartOffset, 
                            map.CrcLength);
                    }
                    catch
                    {
                        calculatedCrc = 0xFFFF;
                    }
                }
            }

            return new ImmoData
            {
                ImmoType = map.ImmoType,
                Manufacturer = manufacturer,
                PinCode = pinCode,
                Mileage = mileage,
                MileageUnit = mileageUnit,
                Vin = vin,
                Skc = skc,
                IsImmobilizerActive = immobilizerActive,
                KeyCount = keyCount,
                CountryCode = countryCode,
                ComponentProtection = componentProtection,
                LearningMode = learningMode,
                StoredCrc = storedCrc,
                CalculatedCrc = calculatedCrc
            };
        }

        private static bool IsValidBcd(byte[] data, int offset, int length)
        {
            if (offset + length > data.Length)
                return false;

            for (int i = 0; i < length; i++)
            {
                byte b = data[offset + i];
                int high = (b >> 4) & 0x0F;
                int low = b & 0x0F;
                if (high > 9 || low > 9)
                    return false;
            }
            return true;
        }
    }
}
