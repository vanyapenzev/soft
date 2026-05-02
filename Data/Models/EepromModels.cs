using System;
using System.Collections.Generic;
using VagImmoEditor.Data.Maps;

namespace VagImmoEditor.Data.Models;

/// <summary>
/// Модель данных иммобилайзера с распарсенными значениями
/// </summary>
public record ImmoData(
    ImmoType Type,
    string PinCode,
    uint Mileage,
    string Vin,
    ushort? StoredCrc,
    ushort? CalculatedCrc,
    bool IsCrcValid,
    List<ImmoOption> Options
);

/// <summary>
/// Опция иммобилайзера (настройка)
/// </summary>
public record ImmoOption(
    string Name,
    string Description,
    int Offset,
    int BitIndex,
    bool Value,
    byte Mask = 0x01
);

/// <summary>
/// Результат операции с EEPROM
/// </summary>
public record EepromResult(
    bool Success,
    string Message,
    byte[]? Data = null
);

/// <summary>
/// Запись истории изменений для Undo/Redo
/// </summary>
public record ChangeRecord(
    int Offset,
    byte OldValue,
    byte NewValue,
    DateTime Timestamp
);
