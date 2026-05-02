namespace App.Crypto;

using App.Models;
using App.Core;
using Microsoft.Extensions.Logging;

/// <summary>
/// Сервис для расшифровки данных иммобилайзера VDO/Continental IMMO4
/// Использует алгоритмы декриптования для NEC/Renesas MCU
/// </summary>
public interface IImmoCryptoService
{
    /// <summary>
    /// Расшифровка PIN кода из зашифрованного блока
    /// </summary>
    string? DecryptPin(byte[] encryptedPin, byte[] seedKey);
    
    /// <summary>
    /// Расшифровка Component Security (CS) данных
    /// </summary>
    byte[]? DecryptCs(byte[] encryptedCs, byte[] seedKey);
    
    /// <summary>
    /// Расшифровка MAC адреса для синхронизации с ECU
    /// </summary>
    byte[]? DecryptMac(byte[] encryptedMac, byte[] seedKey);
    
    /// <summary>
    /// Полная расшифровка блока ImmoData
    /// </summary>
    DecryptedImmoData? DecryptImmoData(byte[] encryptedData, byte[] cryptoKey);
}

/// <summary>
/// Результат расшифровки IMMO данных
/// </summary>
public class DecryptedImmoData
{
    public string? Pin { get; set; }
    public byte[]? ComponentSecurity { get; set; }
    public byte[]? Mac { get; set; }
    public List<KeyInfo>? Keys { get; set; }
    public bool IsDecryptionSuccessful { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Реализация крипто-сервиса для VDO NEC IMMO4
/// Примечание: реальные алгоритмы требуют реверс-инжиниринга конкретной версии ПО
/// </summary>
public class VdoNecCryptoService : IImmoCryptoService
{
    private readonly ILogger<VdoNecCryptoService>? _logger;

    public VdoNecCryptoService(ILogger<VdoNecCryptoService>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Расшифровка PIN кода
    /// VDO использует XOR-шифрование с ключом, привязанным к процессору NEC
    /// </summary>
    public string? DecryptPin(byte[] encryptedPin, byte[] seedKey)
    {
        if (encryptedPin == null || encryptedPin.Length < 4)
        {
            _logger?.LogWarning("Некорректные данные для расшифровки PIN");
            return null;
        }

        try
        {
            // Алгоритм расшифровки зависит от версии крипто-маски
            // Базовый подход: XOR с ключом или инверсия битов
            
            byte[] decrypted;
            
            if (seedKey != null && seedKey.Length >= encryptedPin.Length)
            {
                // XOR расшифровка с использованием seed key
                decrypted = XorDecrypt(encryptedPin, seedKey);
            }
            else
            {
                // Простая инверсия (используется в некоторых версиях)
                decrypted = encryptedPin.Select(b => (byte)~b).ToArray();
            }
            
            // Попытка интерпретации как ASCII цифр
            string pinStr = System.Text.Encoding.ASCII.GetString(decrypted.Take(5).ToArray());
            
            // Проверка на валидный PIN (4-5 цифр)
            if (pinStr.All(char.IsDigit) && pinStr.Length >= 4)
            {
                _logger?.LogInformation("PIN успешно расшифрован");
                return pinStr.TrimStart('0');
            }
            
            // Альтернативная интерпретация - BCD кодирование
            int pin = DecodeBcdPin(decrypted);
            if (pin > 0 && pin <= 99999)
            {
                _logger?.LogInformation("PIN успешно расшифрован (BCD): {PIN}", pin);
                return pin.ToString();
            }
            
            _logger?.LogWarning("Не удалось расшифровать PIN корректно");
            return BitConverter.ToString(decrypted.Take(5).ToArray()).Replace("-", "");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка при расшифровке PIN");
            return null;
        }
    }

    /// <summary>
    /// Расшифровка Component Security данных
    /// </summary>
    public byte[]? DecryptCs(byte[] encryptedCs, byte[] seedKey)
    {
        if (encryptedCs == null || encryptedCs.Length != 7)
        {
            _logger?.LogWarning("Некорректные данные CS для расшифровки");
            return null;
        }

        try
        {
            if (seedKey != null && seedKey.Length >= encryptedCs.Length)
            {
                return XorDecrypt(encryptedCs, seedKey);
            }
            
            // Без ключа возвращаем как есть (не расшифровано)
            return (byte[])encryptedCs.Clone();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка при расшифровке CS");
            return null;
        }
    }

    /// <summary>
    /// Расшифровка MAC адреса
    /// </summary>
    public byte[]? DecryptMac(byte[] encryptedMac, byte[] seedKey)
    {
        if (encryptedMac == null || encryptedMac.Length != 4)
        {
            _logger?.LogWarning("Некорректные данные MAC для расшифровки");
            return null;
        }

        try
        {
            if (seedKey != null && seedKey.Length >= encryptedMac.Length)
            {
                return XorDecrypt(encryptedMac, seedKey);
            }
            
            return (byte[])encryptedMac.Clone();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка при расшифровке MAC");
            return null;
        }
    }

    /// <summary>
    /// Полная расшифровка блока ImmoData
    /// </summary>
    public DecryptedImmoData? DecryptImmoData(byte[] encryptedData, byte[] cryptoKey)
    {
        if (encryptedData == null || encryptedData.Length < 64)
        {
            _logger?.LogWarning("Некорректный размер блока ImmoData");
            return null;
        }

        var result = new DecryptedImmoData();
        
        try
        {
            // Извлечение зашифрованных компонентов из блока
            byte[] encryptedPin = encryptedData.Skip(0x10).Take(5).ToArray();
            byte[] encryptedCs = encryptedData.Skip(0x20).Take(7).ToArray();
            byte[] encryptedMac = encryptedData.Skip(0x30).Take(4).ToArray();
            
            // Расшифровка каждого компонента
            result.Pin = DecryptPin(encryptedPin, cryptoKey);
            result.ComponentSecurity = DecryptCs(encryptedCs, cryptoKey);
            result.Mac = DecryptMac(encryptedMac, cryptoKey);
            
            // Расшифровка ключей
            result.Keys = DecryptKeys(encryptedData, cryptoKey);
            
            result.IsDecryptionSuccessful = result.Pin != null || result.ComponentSecurity != null;
            
            _logger?.LogInformation("Блок ImmoData успешно расшифрован");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка при расшифровке ImmoData");
            result.ErrorMessage = ex.Message;
            result.IsDecryptionSuccessful = false;
        }
        
        return result;
    }

    /// <summary>
    /// XOR расшифровка массива байт
    /// </summary>
    private byte[] XorDecrypt(byte[] data, byte[] key)
    {
        var result = new byte[data.Length];
        
        for (int i = 0; i < data.Length; i++)
        {
            result[i] = (byte)(data[i] ^ key[i % key.Length]);
        }
        
        return result;
    }

    /// <summary>
    /// Декодирование PIN из BCD формата
    /// </summary>
    private int DecodeBcdPin(byte[] bcdData)
    {
        if (bcdData.Length < 3)
            return 0;
        
        int pin = 0;
        int multiplier = 1;
        
        for (int i = 0; i < 3 && i < bcdData.Length; i++)
        {
            int digit1 = bcdData[i] & 0x0F;
            int digit2 = (bcdData[i] >> 4) & 0x0F;
            
            if (digit1 <= 9)
            {
                pin += digit1 * multiplier;
                multiplier *= 10;
            }
            
            if (digit2 <= 9)
            {
                pin += digit2 * multiplier;
                multiplier *= 10;
            }
        }
        
        return pin;
    }

    /// <summary>
    /// Расшифровка ключей иммобилайзера
    /// </summary>
    private List<KeyInfo> DecryptKeys(byte[] encryptedData, byte[] cryptoKey)
    {
        var keys = new List<KeyInfo>();
        
        // Ключи обычно начинаются со смещения 0x70
        int keyOffset = 0x70;
        
        for (int i = 0; i < MemoryMap.MAX_KEY_SLOTS; i++)
        {
            int offset = keyOffset + (i * MemoryMap.KEY_SLOT_SIZE);
            
            if (encryptedData.Length < offset + MemoryMap.KEY_SLOT_SIZE)
                break;
            
            var encryptedKeyData = encryptedData.Skip(offset).Take(MemoryMap.KEY_SLOT_SIZE).ToArray();
            
            // Проверка на пустой слот
            bool isEmpty = encryptedKeyData.All(b => b == 0x00) || encryptedKeyData.All(b => b == 0xFF);
            
            if (!isEmpty)
            {
                byte[]? decryptedKeyId = cryptoKey != null 
                    ? XorDecrypt(encryptedKeyData.Take(8).ToArray(), cryptoKey)
                    : encryptedKeyData.Take(8).ToArray();
                
                keys.Add(new KeyInfo
                {
                    SlotIndex = i,
                    KeyId = decryptedKeyId,
                    IsActive = true
                });
            }
        }
        
        return keys;
    }

    /// <summary>
    /// Генерация Seed Key из данных процессора NEC
    /// Это упрощенная реализация - в реальности алгоритм сложнее
    /// </summary>
    public static byte[] GenerateSeedKeyFromNecData(byte[] necFlashData)
    {
        if (necFlashData == null || necFlashData.Length < 256)
        {
            // Возвращаем ключ по умолчанию если данных нет
            return new byte[] { 0x5A, 0x3C, 0x7E, 0x91, 0x2F, 0x84, 0xB6, 0xD0 };
        }
        
        // Генерация ключа на основе уникальных данных процессора
        var key = new byte[8];
        
        for (int i = 0; i < 8; i++)
        {
            key[i] = (byte)(necFlashData[i * 32] ^ necFlashData[necFlashData.Length - 1 - (i * 32)]);
        }
        
        return key;
    }
}

/// <summary>
/// Сервис для работы с внешними файлами ImmoData (полученными через OBD2)
/// </summary>
public class ImmoDataFileService
{
    private readonly ILogger<ImmoDataFileService>? _logger;

    public ImmoDataFileService(ILogger<ImmoDataFileService>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Загрузка расшифрованного блока ImmoData из файла
    /// Формат файла: бинарный дамп расшифрованных данных (минимум 256 байт)
    /// </summary>
    public async Task<byte[]?> LoadImmoDataFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            _logger?.LogWarning("Файл ImmoData не найден: {FilePath}", filePath);
            return null;
        }

        try
        {
            await using var fileStream = new FileStream(
                filePath, 
                FileMode.Open, 
                FileAccess.Read, 
                FileShare.Read);
            
            if (fileStream.Length < 256)
            {
                _logger?.LogWarning("Файл ImmoData слишком мал: {Size} байт", fileStream.Length);
                return null;
            }
            
            var buffer = new byte[fileStream.Length];
            await fileStream.ReadAsync(buffer.AsMemory(0, (int)fileStream.Length), cancellationToken);
            
            _logger?.LogInformation("Файл ImmoData загружен. Размер: {Size} байт", buffer.Length);
            
            return buffer;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка при загрузке файла ImmoData");
            return null;
        }
    }

    /// <summary>
    /// Сохранение расшифрованного блока ImmoData в файл
    /// </summary>
    public async Task SaveImmoDataFileAsync(string filePath, byte[] data, CancellationToken cancellationToken = default)
    {
        await using var fileStream = new FileStream(
            filePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None);
        
        await fileStream.WriteAsync(data.AsMemory(0, data.Length), cancellationToken);
        
        _logger?.LogInformation("Файл ImmoData сохранен: {FilePath}", filePath);
    }

    /// <summary>
    /// Экспорт расшифрованных IMMO данных в текстовый формат
    /// </summary>
    public string ExportImmoDataToText(DecryptedImmoData data)
    {
        var sb = new System.Text.StringBuilder();
        
        sb.AppendLine("=== IMMO4 Decrypted Data ===");
        sb.AppendLine($"PIN: {data.Pin ?? "N/A"}");
        
        if (data.ComponentSecurity != null)
        {
            sb.AppendLine($"CS: {BitConverter.ToString(data.ComponentSecurity).Replace("-", " ")}");
        }
        
        if (data.Mac != null)
        {
            sb.AppendLine($"MAC: {BitConverter.ToString(data.Mac).Replace("-", " ")}");
        }
        
        if (data.Keys != null && data.Keys.Count > 0)
        {
            sb.AppendLine($"\nKeys ({data.Keys.Count}):");
            foreach (var key in data.Keys)
            {
                sb.AppendLine($"  Slot {key.SlotIndex}: {BitConverter.ToString(key.KeyId!).Replace("-", " ")}");
            }
        }
        
        return sb.ToString();
    }
}
