namespace VagImmoEditor.Models;

/// <summary>
/// Опция/настройка в прошивке иммобилайзера
/// </summary>
public class ImmoOption
{
    /// <summary>
    /// Уникальный идентификатор опции
    /// </summary>
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// Отображаемое имя опции
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Описание опции
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Категория опции
    /// </summary>
    public string Category { get; set; } = "General";
    
    /// <summary>
    /// Смещение в EEPROM (байт)
    /// </summary>
    public int Offset { get; set; }
    
    /// <summary>
    /// Бит внутри байта (0-7), если опция битовая
    /// </summary>
    public int? BitPosition { get; set; }
    
    /// <summary>
    /// Маска для битовой опции
    /// </summary>
    public byte? BitMask { get; set; }
    
    /// <summary>
    /// Длина данных в байтах
    /// </summary>
    public int DataLength { get; set; } = 1;
    
    /// <summary>
    /// Текущее значение (для не битовых опций)
    /// </summary>
    public byte[]? Value { get; set; }
    
    /// <summary>
    /// Активна ли опция (для битовых опций)
    /// </summary>
    public bool IsEnabled { get; set; }
    
    /// <summary>
    /// Тип опции
    /// </summary>
    public OptionType Type { get; set; } = OptionType.Boolean;
    
    /// <summary>
    /// Доступные значения для Enum опций
    /// </summary>
    public Dictionary<int, string>? EnumValues { get; set; }
    
    /// <summary>
    /// Минимальное значение для числовых опций
    /// </summary>
    public int? MinValue { get; set; }
    
    /// <summary>
    /// Максимальное значение для числовых опций
    /// </summary>
    public int? MaxValue { get; set; }
    
    /// <summary>
    /// Можно ли редактировать опцию
    /// </summary>
    public bool IsReadOnly { get; set; }
    
    /// <summary>
    /// Зависимости от других опций
    /// </summary>
    public List<string>? Dependencies { get; set; }
}

/// <summary>
/// Тип опции
/// </summary>
public enum OptionType
{
    Boolean,      // Вкл/Выкл (бит)
    Integer,      // Целое число
    Enum,         // Выбор из списка
    String,       // Строка
    ByteArray     // Массив байтов
}

/// <summary>
/// Группа опций для организации в UI
/// </summary>
public class OptionGroup
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<ImmoOption> Options { get; set; } = new();
}
