namespace VagImmoEditor.Models;

/// <summary>
/// Результаты парсинга данных иммобилайзера VAG
/// </summary>
public class ImmoData
{
    /// <summary>
    /// PIN-код иммобилайзера (5 или 7 знаков)
    /// </summary>
    public string? PinCode { get; set; }
    
    /// <summary>
    /// Пробег автомобиля (одометр)
    /// </summary>
    public int Mileage { get; set; }
    
    /// <summary>
    /// Единицы измерения пробега (км/мили)
    /// </summary>
    public string MileageUnit { get; set; } = "km";
    
    /// <summary>
    /// Идентификатор компонента (Component ID)
    /// </summary>
    public string? ComponentId { get; set; }
    
    /// <summary>
    /// Тип иммобилайзера (IMMO2, IMMO3, IMMO4 и т.д.)
    /// </summary>
    public string? ImmoType { get; set; }
    
    /// <summary>
    /// VIN автомобиля (если доступен в EEPROM)
    /// </summary>
    public string? Vin { get; set; }
    
    /// <summary>
    /// Количество запрограммированных ключей
    /// </summary>
    public int KeyCount { get; set; }
    
    /// <summary>
    /// Дата последней адаптации
    /// </summary>
    public DateTime? LastAdaptationDate { get; set; }
    
    /// <summary>
    /// Статус защиты компонентов (Component Protection)
    /// </summary>
    public bool ComponentProtectionEnabled { get; set; }
    
    /// <summary>
    /// Код страны/региона
    /// </summary>
    public string? CountryCode { get; set; }
    
    /// <summary>
    /// Дополнительная информация
    /// </summary>
    public string? AdditionalInfo { get; set; }
    
    public override string ToString()
    {
        return $"PIN: {PinCode ?? "N/A"}, Mileage: {Mileage} {MileageUnit}, Keys: {KeyCount}";
    }
}
