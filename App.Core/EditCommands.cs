namespace App.Core;

using App.Models;

/// <summary>
/// Сервис для безопасного редактирования дампа EEPROM
/// Реализует паттерн "Команда" для всех операций изменения
/// </summary>
public interface IEditCommand
{
    EditOperation OperationType { get; }
    bool CanExecute(byte[] dump, DashboardData data);
    EditResult Execute(byte[] dump, DashboardData data);
}

/// <summary>
/// Базовый класс для команд редактирования
/// </summary>
public abstract class EditCommandBase : IEditCommand
{
    public abstract EditOperation OperationType { get; }
    
    public abstract bool CanExecute(byte[] dump, DashboardData data);
    
    public abstract EditResult Execute(byte[] dump, DashboardData data);
    
    /// <summary>
    /// Создание копии дампа для безопасного редактирования
    /// </summary>
    protected byte[] CloneDump(byte[] original)
    {
        var clone = new byte[original.Length];
        Array.Copy(original, clone, original.Length);
        return clone;
    }
    
    /// <summary>
    /// Создание успешного результата
    /// </summary>
    protected EditResult Success(byte[] modifiedData, string message)
    {
        return new EditResult
        {
            Success = true,
            Message = message,
            ModifiedData = modifiedData,
            Operation = OperationType
        };
    }
    
    /// <summary>
    /// Создание результата ошибки
    /// </summary>
    protected EditResult Failure(string message)
    {
        return new EditResult
        {
            Success = false,
            Message = message,
            Operation = OperationType
        };
    }
}

/// <summary>
/// Команда изменения пробега
/// </summary>
public class ChangeMileageCommand : EditCommandBase
{
    private readonly uint _newMileage;
    private readonly ICrcCalculator _crcCalculator;

    public ChangeMileageCommand(uint newMileage, ICrcCalculator crcCalculator)
    {
        _newMileage = newMileage;
        _crcCalculator = crcCalculator;
    }

    public override EditOperation OperationType => EditOperation.MileageChange;

    public override bool CanExecute(byte[] dump, DashboardData data)
    {
        if (!data.IsValidDump)
            return false;
        
        if (_newMileage > 9999999)
            return false;
        
        // Проверка на разумное значение (не меньше текущего более чем на 10%)
        if (_newMileage < data.Mileage * 0.9m)
            return false;
        
        return true;
    }

    public override EditResult Execute(byte[] dump, DashboardData data)
    {
        if (!CanExecute(dump, data))
        {
            return Failure("Невозможно выполнить операцию изменения пробега");
        }

        var modifiedDump = CloneDump(dump);
        
        try
        {
            // Запись нового значения в основную копию
            WriteMileageToOffset(modifiedDump, MemoryMap.OFFSET_MILEAGE_PRIMARY, _newMileage);
            
            // Запись нового значения в резервные копии
            WriteMileageToOffset(modifiedDump, MemoryMap.OFFSET_MILEAGE_BACKUP_1, _newMileage);
            WriteMileageToOffset(modifiedDump, MemoryMap.OFFSET_MILEAGE_BACKUP_2, _newMileage);
            
            // Пересчет контрольных сумм
            _crcCalculator.RecalculateMileageCRC(modifiedDump);
            _crcCalculator.RecalculateGlobalCRC(modifiedDump);
            
            return Success(modifiedDump, $"Пробег изменен с {data.Mileage} на {_newMileage} км");
        }
        catch (Exception ex)
        {
            return Failure($"Ошибка при изменении пробега: {ex.Message}");
        }
    }

    /// <summary>
    /// Запись значения пробега по указанному смещению
    /// VDO использует little-endian формат с возможной инверсией
    /// </summary>
    private void WriteMileageToOffset(byte[] dump, int offset, uint mileage)
    {
        byte[] bytes = BitConverter.GetBytes(mileage);
        
        // Проверка на необходимость инверсии (если оригинал был инвертирован)
        // Для простоты записываем как есть - в реальной реализации нужно анализировать оригинальный формат
        
        for (int i = 0; i < 4; i++)
        {
            if (offset + i < dump.Length)
            {
                dump[offset + i] = bytes[i];
            }
        }
    }
}

/// <summary>
/// Команда отключения иммобилайзера (IMMO OFF)
/// </summary>
public class ImmoOffCommand : EditCommandBase
{
    private readonly ICrcCalculator _crcCalculator;

    public ImmoOffCommand(ICrcCalculator crcCalculator)
    {
        _crcCalculator = crcCalculator;
    }

    public override EditOperation OperationType => EditOperation.ImmoOff;

    public override bool CanExecute(byte[] dump, DashboardData data)
    {
        if (!data.IsValidDump)
            return false;
        
        if (!data.IsImmoEnabled)
            return false; // Уже выключен
        
        // Проверка на известную версию крипто-маски
        // Если версия неизвестна - блокируем операцию для предотвращения повреждения
        if (!IsKnownCryptoVersion(dump))
        {
            return false;
        }
        
        return true;
    }

    public override EditResult Execute(byte[] dump, DashboardData data)
    {
        if (!CanExecute(dump, data))
        {
            return Failure("Невозможно выполнить IMMO OFF: неизвестная версия крипто-маски или другие ограничения");
        }

        var modifiedDump = CloneDump(dump);
        
        try
        {
            // Изменение статуса иммобилайзера
            modifiedDump[MemoryMap.OFFSET_IMMO_STATUS] = MemoryMap.IMMO_STATUS_DISABLED;
            
            // Патчинг байтов авторизации (конкретные байты зависят от версии ПО)
            PatchAuthorizationBytes(modifiedDump);
            
            // Пересчет контрольных сумм
            _crcCalculator.RecalculateGlobalCRC(modifiedDump);
            
            return Success(modifiedDump, "Иммобилайзер успешно отключен");
        }
        catch (Exception ex)
        {
            return Failure($"Ошибка при отключении иммобилайзера: {ex.Message}");
        }
    }

    /// <summary>
    /// Проверка на известную версию крипто-маски
    /// </summary>
    private bool IsKnownCryptoVersion(byte[] dump)
    {
        // В реальной реализации здесь должна быть проверка сигнатур известных версий
        // Для примера возвращаем true если найден Part Number
        
        if (dump.Length < MemoryMap.OFFSET_PART_NUMBER + 4)
            return false;
        
        // Проверка на наличие данных в области IMMO
        bool hasImmoData = !dump
            .Skip(MemoryMap.OFFSET_IMMO_DATA)
            .Take(64)
            .All(b => b == 0x00 || b == 0xFF);
        
        return hasImmoData;
    }

    /// <summary>
    /// Патчинг байтов авторизации
    /// Конкретная реализация зависит от версии ПО приборки
    /// </summary>
    private void PatchAuthorizationBytes(byte[] dump)
    {
        // Пример патчинга - в реальности нужны точные данные из реверс-инжиниринга
        // Обычно это изменение определенных байтов в блоке IMMO
        
        // Байт флага авторизации ECU
        int authFlagOffset = MemoryMap.OFFSET_IMMO_DATA + 0x10;
        if (authFlagOffset < dump.Length)
        {
            // Установка флага "авторизовано"
            dump[authFlagOffset] |= 0x01;
        }
    }
}

/// <summary>
/// Интерфейс для калькулятора контрольных сумм
/// </summary>
public interface ICrcCalculator
{
    void RecalculateMileageCRC(byte[] dump);
    void RecalculateGlobalCRC(byte[] dump);
    ushort CalculateBlockCRC(byte[] data, int startOffset, int length);
}

/// <summary>
/// Реализация калькулятора контрольных сумм для VDO
/// </summary>
public class VdoCrcCalculator : ICrcCalculator
{
    public VdoCrcCalculator()
    {
    }

    /// <summary>
    /// Пересчет CRC пробега
    /// </summary>
    public void RecalculateMileageCRC(byte[] dump)
    {
        if (dump.Length < MemoryMap.OFFSET_MILEAGE_CRC + 2)
            return;

        // Расчет XOR CRC для блока пробега
        byte crc = 0;
        for (int i = MemoryMap.OFFSET_MILEAGE_PRIMARY; i < MemoryMap.OFFSET_MILEAGE_PRIMARY + 4; i++)
        {
            crc ^= dump[i];
        }

        // Запись новой CRC
        dump[MemoryMap.OFFSET_MILEAGE_CRC] = crc;
        dump[MemoryMap.OFFSET_MILEAGE_CRC + 1] = (byte)(~crc); // Инвертированная копия
        
        _logger?.LogDebug("CRC пробега пересчитана: {CRC:X2}", crc);
    }

    /// <summary>
    /// Пересчет общей CRC всего дампа
    /// </summary>
    public void RecalculateGlobalCRC(byte[] dump)
    {
        if (dump.Length < MemoryMap.OFFSET_GLOBAL_CRC + 2)
            return;

        // Расчет CRC16 для всего дампа (кроме области самой CRC)
        ushort crc = CalculateCRC16(dump, 0, MemoryMap.OFFSET_GLOBAL_CRC);
        
        // Запись в little-endian формате
        dump[MemoryMap.OFFSET_GLOBAL_CRC] = (byte)(crc & 0xFF);
        dump[MemoryMap.OFFSET_GLOBAL_CRC + 1] = (byte)((crc >> 8) & 0xFF);
        
        _logger?.LogDebug("Глобальная CRC пересчитана: {CRC:X4}", crc);
    }

    /// <summary>
    /// Расчет CRC16 для блока данных
    /// Используется полином 0x8005 (стандартный CRC-16-IBM)
    /// </summary>
    public ushort CalculateBlockCRC(byte[] data, int startOffset, int length)
    {
        return CalculateCRC16(data, startOffset, length);
    }

    /// <summary>
    /// Стандартный алгоритм CRC-16
    /// </summary>
    private ushort CalculateCRC16(byte[] data, int start, int length)
    {
        ushort crc = 0xFFFF;
        int end = Math.Min(start + length, data.Length);
        
        for (int i = start; i < end; i++)
        {
            crc ^= (ushort)data[i];
            
            for (int j = 0; j < 8; j++)
            {
                if ((crc & 0x0001) != 0)
                {
                    crc >>= 1;
                    crc ^= 0xA001; // Полином 0x8005 в отраженном виде
                }
                else
                {
                    crc >>= 1;
                }
            }
        }
        
        return crc;
    }
}

/// <summary>
/// Фасад для управления командами редактирования
/// </summary>
public class EditCommandService
{
    private readonly ICrcCalculator _crcCalculator;

    public EditCommandService(ICrcCalculator crcCalculator)
    {
        _crcCalculator = crcCalculator;
    }

    /// <summary>
    /// Создание команды изменения пробега
    /// </summary>
    public IEditCommand CreateMileageCommand(uint newMileage)
    {
        return new ChangeMileageCommand(newMileage, _crcCalculator);
    }

    /// <summary>
    /// Создание команды отключения иммобилайзера
    /// </summary>
    public IEditCommand CreateImmoOffCommand()
    {
        return new ImmoOffCommand(_crcCalculator);
    }

    /// <summary>
    /// Выполнение команды с проверкой возможности
    /// </summary>
    public EditResult ExecuteCommand(IEditCommand command, byte[] dump, DashboardData data)
    {
        if (!command.CanExecute(dump, data))
        {
            _logger?.LogWarning("Команда не может быть выполнена: {Operation}", command.OperationType);
            return new EditResult
            {
                Success = false,
                Message = $"Операция {command.OperationType} недоступна для текущего дампа",
                Operation = command.OperationType
            };
        }

        _logger?.LogInformation("Выполнение команды: {Operation}", command.OperationType);
        return command.Execute(dump, data);
    }
}
