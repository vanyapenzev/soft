namespace App.Core;

using App.Models;

/// <summary>
/// Сервис для загрузки и валидации бинарных дампов EEPROM
/// </summary>
public class DumpLoaderService
{
    public DumpLoaderService()
    {
    }

    /// <summary>
    /// Загрузка дампа из файла
    /// </summary>
    public async Task<byte[]> LoadDumpAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Файл дампа не найден: {filePath}");
        }

        await using var fileStream = new FileStream(
            filePath, 
            FileMode.Open, 
            FileAccess.Read, 
            FileShare.Read);
        
        var buffer = new byte[fileStream.Length];
        await fileStream.ReadAsync(buffer.AsMemory(0, (int)fileStream.Length), cancellationToken);
        
        _logger?.LogInformation("Файл загружен. Размер: {Size} байт", buffer.Length);
        
        return buffer;
    }

    /// <summary>
    /// Валидация размера дампа
    /// </summary>
    public ValidationResult ValidateSize(byte[] dump)
    {
        if (dump == null || dump.Length == 0)
        {
            return new ValidationResult(false, "Пустой файл или данные отсутствуют");
        }

        if (!MemoryMap.IsValidDumpSize(dump.Length))
        {
            return new ValidationResult(false, 
                $"Неверный размер дампа: {dump.Length} байт. Ожидалось {MemoryMap.SIZE_24C32} или {MemoryMap.SIZE_24C64}");
        }

        _logger?.LogInformation("Размер дампа корректен: {Size} байт ({Type})", 
            dump.Length, MemoryMap.GetDumpType(dump.Length));
        
        return new ValidationResult(true, "Размер корректен");
    }

    /// <summary>
    /// Идентификация типа приборной панели по сигнатурам
    /// </summary>
    public DashboardIdentificationResult IdentifyDashboard(byte[] dump)
    {
        var result = new DashboardIdentificationResult();
        
        // Проверка базовой валидности
        var sizeValidation = ValidateSize(dump);
        if (!sizeValidation.IsValid)
        {
            result.IsValid = false;
            result.Error = sizeValidation.Message;
            return result;
        }

        result.DumpSize = dump.Length;
        result.DetectedType = dump.Length == MemoryMap.SIZE_24C64 
            ? DumpType.VDO_5JA920810B_24C64 
            : DumpType.VDO_5JA920810B_24C32;

        // Поиск номера детали
        try
        {
            string? partNumber = ExtractPartNumber(dump);
            result.PartNumber = partNumber;
            
            // Проверка на наличие ожидаемой сигнатуры VDO
            if (!string.IsNullOrEmpty(partNumber) && partNumber.Contains("5JA"))
            {
                result.IsConfirmedVDO = true;
                _logger?.LogInformation("Обнаружена приборная панель VDO. Part Number: {PartNumber}", partNumber);
            }
            else if (!string.IsNullOrEmpty(partNumber))
            {
                _logger?.LogWarning("Найден Part Number, но он не соответствует ожидаемому формату VDO: {PartNumber}", partNumber);
                result.IsConfirmedVDO = false;
            }
            else
            {
                _logger?.LogWarning("Part Number не найден по ожидаемому смещению");
                result.IsConfirmedVDO = false;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка при извлечении Part Number");
            result.IsConfirmedVDO = false;
        }

        // Извлечение VIN (самое простое - ASCII строка)
        try
        {
            string? vin = ExtractVin(dump);
            result.VIN = vin;
            
            if (!string.IsNullOrEmpty(vin) && vin.Length >= 17)
            {
                _logger?.LogInformation("VIN найден: {VIN}", vin);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка при извлечении VIN");
        }

        result.IsValid = result.IsConfirmedVDO || !string.IsNullOrEmpty(result.PartNumber);
        
        if (!result.IsValid)
        {
            result.Error = "Не удалось подтвердить тип приборной панели. Режим ограниченного доступа.";
        }

        return result;
    }

    /// <summary>
    /// Извлечение номера детали из дампа
    /// </summary>
    private string? ExtractPartNumber(byte[] dump)
    {
        if (dump.Length < MemoryMap.OFFSET_PART_NUMBER + 16)
            return null;

        // Чтение номера детали (ASCII строка, может быть завершена нулями)
        var partNumberBytes = dump
            .Skip(MemoryMap.OFFSET_PART_NUMBER)
            .Take(16)
            .TakeWhile(b => b != 0)
            .ToArray();

        if (partNumberBytes.Length == 0)
            return null;

        string partNumber = System.Text.Encoding.ASCII.GetString(partNumberBytes).Trim();
        
        // Фильтрация мусорных символов
        return string.Join("", partNumber.Where(c => char.IsLetterOrDigit(c) || c == '-'));
    }

    /// <summary>
    /// Извлечение VIN кода из дампа
    /// </summary>
    private string? ExtractVin(byte[] dump)
    {
        if (dump.Length < MemoryMap.OFFSET_VIN + MemoryMap.VIN_LENGTH)
            return null;

        var vinBytes = dump
            .Skip(MemoryMap.OFFSET_VIN)
            .Take(MemoryMap.VIN_LENGTH)
            .ToArray();

        // Проверка на валидность VIN (должен содержать только допустимые символы)
        string vin = System.Text.Encoding.ASCII.GetString(vinBytes);
        
        // VIN должен состоять из букв и цифр (кроме I, O, Q в некоторых позициях)
        if (vin.All(c => char.IsLetterOrDigit(c)) && vin.Length == MemoryMap.VIN_LENGTH)
        {
            return vin;
        }

        return null;
    }

    /// <summary>
    /// Сохранение модифицированного дампа в файл
    /// </summary>
    public async Task SaveDumpAsync(string filePath, byte[] data, CancellationToken cancellationToken = default)
    {
        await using var fileStream = new FileStream(
            filePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None);
        
        await fileStream.WriteAsync(data.AsMemory(0, data.Length), cancellationToken);
        
        _logger?.LogInformation("Дамп сохранен в файл: {FilePath}, размер: {Size} байт", filePath, data.Length);
    }
}

/// <summary>
/// Результат валидации
/// </summary>
public record ValidationResult(bool IsValid, string? Message = null);

/// <summary>
/// Результат идентификации приборной панели
/// </summary>
public class DashboardIdentificationResult
{
    public bool IsValid { get; set; }
    public bool IsConfirmedVDO { get; set; }
    public string? PartNumber { get; set; }
    public string? VIN { get; set; }
    public int DumpSize { get; set; }
    public DumpType DetectedType { get; set; }
    public string? Error { get; set; }
}
