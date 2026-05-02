using System;
using System.Text;

namespace VagImmoEditor.Tools;

/// <summary>
/// Утилита для создания тестового BIN файла EEPROM
/// </summary>
public class TestEepromGenerator
{
    /// <summary>
    /// Создать тестовый дамп EEPROM 24LC64 с данными IMMO3
    /// </summary>
    public static byte[] CreateTestImmo3Dump()
    {
        var data = new byte[8192]; // 24LC64 = 8KB
        
        // Заполняем нулями
        Array.Clear(data, 0, data.Length);
        
        // Сигнатура IMMO3
        data[0x000] = 0x01;
        data[0x001] = 0x01;
        
        // Component ID (пример: 4B0953257K)
        byte[] componentId = Encoding.ASCII.GetBytes("4B0953257K");
        Array.Copy(componentId, 0, data, 0x010, componentId.Length);
        
        // PIN-код в BCD формате (например, PIN: 12345)
        // Каждый байт содержит две цифры
        data[0x1E0] = 0x01; // 01
        data[0x1E1] = 0x23; // 23
        data[0x1E2] = 0x45; // 45
        data[0x1E3] = 0x00; // padding
        data[0x1E4] = 0x00;
        
        // Пробег (например, 123456 км) в little-endian
        int mileage = 123456;
        data[0x1D0] = (byte)(mileage & 0xFF);
        data[0x1D1] = (byte)((mileage >> 8) & 0xFF);
        data[0x1D2] = (byte)((mileage >> 16) & 0xFF);
        data[0x1D3] = (byte)((mileage >> 24) & 0xFF);
        
        // Количество ключей
        data[0x1F8] = 0x03; // 3 ключа
        
        // Флаги настроек
        data[0x1F5] = 0x04; // Бит 2 = иммобилайзер активен
        
        // VIN (примерный)
        byte[] vin = Encoding.ASCII.GetBytes("WVWZZZ3CZWE123456");
        Array.Copy(vin, 0, data, 0x200, vin.Length);
        
        // CRC16 для проверки целостности (упрощенно)
        ushort crc = CalculateCrc16(data, 0, 0x1FE);
        data[0x1FE] = (byte)(crc & 0xFF);
        data[0x1FF] = (byte)((crc >> 8) & 0xFF);
        
        return data;
    }
    
    /// <summary>
    /// Создать тестовый дамп с другим PIN-кодом
    /// </summary>
    public static byte[] CreateDumpWithPin(string pin)
    {
        var data = CreateTestImmo3Dump();
        
        if (pin.Length >= 4 && pin.Length <= 7)
        {
            // Кодируем PIN в BCD
            int offset = 0x1E0;
            Array.Clear(data, offset, 7);
            
            for (int i = 0; i < pin.Length && i < 5; i++)
            {
                if (char.IsDigit(pin[i]))
                {
                    int digit = pin[i] - '0';
                    if (i % 2 == 0)
                        data[offset + i / 2] = (byte)(digit << 4);
                    else
                        data[offset + i / 2] |= (byte)digit;
                }
            }
        }
        
        return data;
    }
    
    /// <summary>
    /// Расчет CRC16 CCITT
    /// </summary>
    private static ushort CalculateCrc16(byte[] data, int offset, int length)
    {
        ushort crc = 0;
        
        for (int i = offset; i < offset + length && i < data.Length; i++)
        {
            crc ^= data[i];
            for (int j = 0; j < 8; j++)
            {
                if ((crc & 1) != 0)
                    crc = (ushort)((crc >> 1) ^ 0x8408);
                else
                    crc >>= 1;
            }
        }
        
        return crc;
    }
}
