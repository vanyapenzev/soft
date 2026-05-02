using System;

namespace VagImmoEditor.Core.Crc;

/// <summary>
/// Калькулятор CRC для различных алгоритмов используемых в VAG
/// </summary>
public static class CrcCalculator
{
    /// <summary>
    /// CRC-16/CCITT (XModem) - используется в VDO
    /// Poly: 0x1021, Init: 0x0000, RefIn: false, RefOut: false, XorOut: 0x0000
    /// </summary>
    public static ushort CalculateCrc16Ccitt(byte[] data, int offset, int length)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (offset < 0 || length < 0 || offset + length > data.Length)
            throw new ArgumentOutOfRangeException();

        ushort crc = 0x0000;
        
        for (int i = offset; i < offset + length; i++)
        {
            crc ^= (ushort)(data[i] << 8);
            for (int j = 0; j < 8; j++)
            {
                if ((crc & 0x8000) != 0)
                    crc = (ushort)((crc << 1) ^ 0x1021);
                else
                    crc = (ushort)(crc << 1);
            }
        }
        
        return crc;
    }

    /// <summary>
    /// CRC-16/Motorola (IBM) - используется в Motorola IMMO
    /// Poly: 0x8005, Init: 0x0000, RefIn: false, RefOut: false, XorOut: 0x0000
    /// </summary>
    public static ushort CalculateCrc16Motorola(byte[] data, int offset, int length)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (offset < 0 || length < 0 || offset + length > data.Length)
            throw new ArgumentOutOfRangeException();

        ushort crc = 0x0000;
        
        for (int i = offset; i < offset + length; i++)
        {
            crc ^= (ushort)(data[i] << 8);
            for (int j = 0; j < 8; j++)
            {
                if ((crc & 0x8000) != 0)
                    crc = (ushort)((crc << 1) ^ 0x8005);
                else
                    crc = (ushort)(crc << 1);
            }
        }
        
        return crc;
    }

    /// <summary>
    /// CRC-8 для некоторых блоков VAG
    /// Poly: 0x0D, Init: 0x00
    /// </summary>
    public static byte CalculateCrc8(byte[] data, int offset, int length)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (offset < 0 || length < 0 || offset + length > data.Length)
            throw new ArgumentOutOfRangeException();

        byte crc = 0x00;
        
        for (int i = offset; i < offset + length; i++)
        {
            crc ^= data[i];
            for (int j = 0; j < 8; j++)
            {
                if ((crc & 0x01) != 0)
                    crc = (byte)((crc >> 1) ^ 0x8C); // 0x8C = reverse(0x0D)
                else
                    crc = (byte)(crc >> 1);
            }
        }
        
        return crc;
    }

    /// <summary>
    /// Проверка CRC для блока данных
    /// </summary>
    public static bool VerifyCrc16Ccitt(byte[] data, int offset, int length, ushort expectedCrc)
    {
        ushort calculated = CalculateCrc16Ccitt(data, offset, length);
        return calculated == expectedCrc;
    }

    /// <summary>
    /// Проверка CRC для Motorola
    /// </summary>
    public static bool VerifyCrc16Motorola(byte[] data, int offset, int length, ushort expectedCrc)
    {
        ushort calculated = CalculateCrc16Motorola(data, offset, length);
        return calculated == expectedCrc;
    }
}
