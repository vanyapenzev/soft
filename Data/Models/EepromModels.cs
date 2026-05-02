using System;
using System.Collections.Generic;
using VagImmoEditor.Data.Maps;

namespace VagImmoEditor.Data.Models;

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

public record ImmoOption(
    string Name,
    string Description,
    int Offset,
    int BitIndex,
    bool Value,
    byte Mask = 0x01
);

public record EepromResult(
    bool Success,
    string Message,
    byte[]? Data = null
);

public record ChangeRecord(
    int Offset,
    byte OldValue,
    byte NewValue,
    DateTime Timestamp
);
