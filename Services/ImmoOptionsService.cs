using VagImmoEditor.Models;

namespace VagImmoEditor.Services;

/// <summary>
/// Сервис для управления опциями иммобилайзера
/// </summary>
public class ImmoOptionsService
{
    private readonly EepromData _eeprom;
    private readonly List<ImmoOption> _options;
    
    public ImmoOptionsService(EepromData eeprom)
    {
        _eeprom = eeprom ?? throw new ArgumentNullException(nameof(eeprom));
        _options = InitializeOptions();
    }
    
    /// <summary>
    /// Инициализация списка доступных опций
    /// </summary>
    private List<ImmoOption> InitializeOptions()
    {
        var options = new List<ImmoOption>();
        
        // Опции для IMMO3
        options.Add(new ImmoOption
        {
            Id = "component_protection",
            Name = "Component Protection",
            Description = "Защита компонентов (вкл/выкл)",
            Category = "Security",
            Offset = 0x1F5,
            BitPosition = 0,
            BitMask = 0x01,
            Type = OptionType.Boolean,
            IsEnabled = false
        });
        
        options.Add(new ImmoOption
        {
            Id = "key_learning_mode",
            Name = "Режим обучения ключей",
            Description = "Разрешить обучение новых ключей",
            Category = "Keys",
            Offset = 0x1F5,
            BitPosition = 1,
            BitMask = 0x02,
            Type = OptionType.Boolean,
            IsEnabled = false
        });
        
        options.Add(new ImmoOption
        {
            Id = "immobilizer_status",
            Name = "Статус иммобилайзера",
            Description = "Активен ли иммобилайзер",
            Category = "Security",
            Offset = 0x1F5,
            BitPosition = 2,
            BitMask = 0x04,
            Type = OptionType.Boolean,
            IsEnabled = true
        });
        
        options.Add(new ImmoOption
        {
            Id = "key_count",
            Name = "Количество ключей",
            Description = "Максимальное количество программируемых ключей",
            Category = "Keys",
            Offset = 0x1F8,
            DataLength = 1,
            Type = OptionType.Integer,
            MinValue = 1,
            MaxValue = 8,
            IsReadOnly = false
        });
        
        options.Add(new ImmoOption
        {
            Id = "country_code",
            Name = "Код страны",
            Description = "Региональные настройки",
            Category = "Configuration",
            Offset = 0x1A0,
            DataLength = 2,
            Type = OptionType.Enum,
            EnumValues = new Dictionary<int, string>
            {
                { 0x0000, "Europe" },
                { 0x0001, "USA" },
                { 0x0002, "Japan" },
                { 0x0003, "China" },
                { 0x0004, "Russia" }
            },
            IsReadOnly = false
        });
        
        options.Add(new ImmoOption
        {
            Id = "mileage_unit",
            Name = "Единицы пробега",
            Description = "Км или мили",
            Category = "Configuration",
            Offset = 0x1D0,
            BitPosition = 7,
            BitMask = 0x80,
            Type = OptionType.Boolean,
            IsEnabled = false // false = km, true = miles
        });
        
        options.Add(new ImmoOption
        {
            Id = "vin_write_protect",
            Name = "Защита VIN от записи",
            Description = "Блокировка изменения VIN",
            Category = "Security",
            Offset = 0x1F6,
            BitPosition = 3,
            BitMask = 0x08,
            Type = OptionType.Boolean,
            IsEnabled = true
        });
        
        options.Add(new ImmoOption
        {
            Id = "secret_challenge",
            Name = "Secret Challenge Algorithm",
            Description = "Алгоритм проверки ключа",
            Category = "Security",
            Offset = 0x1F7,
            DataLength = 1,
            Type = OptionType.Enum,
            EnumValues = new Dictionary<int, string>
            {
                { 0x00, "Algorithm A" },
                { 0x01, "Algorithm B" },
                { 0x02, "Algorithm C" },
                { 0x03, "Algorithm D" }
            },
            IsReadOnly = true
        });
        
        // Группировка по категориям для удобства
        return options;
    }
    
    /// <summary>
    /// Получить все доступные опции
    /// </summary>
    public IReadOnlyList<ImmoOption> GetAllOptions()
    {
        // Обновляем значения из EEPROM
        foreach (var option in _options)
        {
            LoadOptionValue(option);
        }
        
        return _options.AsReadOnly();
    }
    
    /// <summary>
    /// Получить опции по категории
    /// </summary>
    public IEnumerable<ImmoOption> GetOptionsByCategory(string category)
    {
        return _options.Where(o => o.Category == category);
    }
    
    /// <summary>
    /// Загрузить значение опции из EEPROM
    /// </summary>
    private void LoadOptionValue(ImmoOption option)
    {
        if (option.Type == OptionType.Boolean && option.BitPosition.HasValue)
        {
            byte value = _eeprom[option.Offset];
            option.IsEnabled = (value & option.BitMask) != 0;
        }
        else if (option.DataLength > 0)
        {
            option.Value = _eeprom.GetRange(option.Offset, option.DataLength);
            
            if (option.Type == OptionType.Integer && option.Value?.Length == 1)
            {
                // Для integer опций обновляем IsEnabled как временное хранение значения
                // В реальном приложении нужно отдельное свойство
            }
        }
    }
    
    /// <summary>
    /// Применить изменение опции к EEPROM
    /// </summary>
    public bool ApplyOptionChange(ImmoOption option)
    {
        if (option.IsReadOnly)
            return false;
        
        try
        {
            if (option.Type == OptionType.Boolean && option.BitPosition.HasValue && option.BitMask.HasValue)
            {
                byte currentValue = _eeprom[option.Offset];
                
                if (option.IsEnabled)
                    currentValue |= option.BitMask.Value;
                else
                    currentValue &= (byte)~option.BitMask.Value;
                
                _eeprom[option.Offset] = currentValue;
                return true;
            }
            else if (option.Value != null && option.Value.Length > 0)
            {
                _eeprom.SetRange(option.Offset, option.Value);
                return true;
            }
        }
        catch (Exception)
        {
            return false;
        }
        
        return false;
    }
    
    /// <summary>
    /// Сбросить опцию к значению по умолчанию
    /// </summary>
    public bool ResetOptionToDefault(ImmoOption option)
    {
        if (option.IsReadOnly)
            return false;
        
        // Значения по умолчанию в зависимости от типа
        if (option.Type == OptionType.Boolean)
        {
            option.IsEnabled = false;
            return ApplyOptionChange(option);
        }
        
        return false;
    }
    
    /// <summary>
    /// Получить группы опций
    /// </summary>
    public List<OptionGroup> GetOptionGroups()
    {
        var groups = new List<OptionGroup>();
        
        var categories = _options.Select(o => o.Category).Distinct().OrderBy(c => c);
        
        foreach (var category in categories)
        {
            var group = new OptionGroup
            {
                Name = category,
                Description = GetCategoryDescription(category),
                Options = _options.Where(o => o.Category == category).ToList()
            };
            
            groups.Add(group);
        }
        
        return groups;
    }
    
    private string GetCategoryDescription(string category)
    {
        return category switch
        {
            "Security" => "Настройки безопасности и защиты",
            "Keys" => "Управление ключами и адаптация",
            "Configuration" => "Конфигурационные параметры",
            _ => "Другие настройки"
        };
    }
}
