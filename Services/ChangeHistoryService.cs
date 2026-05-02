using System;
using System.Collections.Generic;
using System.Linq;
using VagImmoEditor.Models;

namespace VagImmoEditor.Services;

/// <summary>
/// Сервис для управления историей изменений и отката операций
/// </summary>
public class ChangeHistoryService
{
    private readonly EepromData _eeprom;
    private readonly Stack<ChangeRecord> _undoStack;
    private readonly Stack<ChangeRecord> _redoStack;
    private const int MaxHistorySize = 100;
    
    public event EventHandler<ChangeEventArgs>? ChangeApplied;
    public event EventHandler? HistoryChanged;
    
    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;
    public int UndoCount => _undoStack.Count;
    public int RedoCount => _redoStack.Count;
    
    public ChangeHistoryService(EepromData eeprom)
    {
        _eeprom = eeprom ?? throw new ArgumentNullException(nameof(eeprom));
        _undoStack = new Stack<ChangeRecord>();
        _redoStack = new Stack<ChangeRecord>();
    }
    
    /// <summary>
    /// Записать изменение в историю
    /// </summary>
    public void RecordChange(int offset, byte oldValue, byte newValue, string description = "")
    {
        if (oldValue == newValue)
            return;
        
        var record = new ChangeRecord(offset, oldValue, newValue, DateTime.Now);
        _undoStack.Push(record);
        _redoStack.Clear(); // Очищаем redo при новом изменении
        
        // Ограничиваем размер истории
        while (_undoStack.Count > MaxHistorySize)
        {
            var items = _undoStack.ToArray();
            _undoStack.Clear();
            for (int i = items.Length - 2; i >= 0; i--)
                _undoStack.Push(items[i]);
        }
        
        OnHistoryChanged();
    }
    
    /// <summary>
    /// Отменить последнее изменение
    /// </summary>
    public bool Undo()
    {
        if (!CanUndo)
            return false;
        
        var change = _undoStack.Pop();
        _eeprom[change.Offset] = change.OldValue;
        _redoStack.Push(change);
        
        ChangeApplied?.Invoke(this, new ChangeEventArgs(change.Offset, change.OldValue, true));
        OnHistoryChanged();
        
        return true;
    }
    
    /// <summary>
    /// Повторить отмененное изменение
    /// </summary>
    public bool Redo()
    {
        if (!CanRedo)
            return false;
        
        var change = _redoStack.Pop();
        _eeprom[change.Offset] = change.NewValue;
        _undoStack.Push(change);
        
        ChangeApplied?.Invoke(this, new ChangeEventArgs(change.Offset, change.NewValue, false));
        OnHistoryChanged();
        
        return true;
    }
    
    /// <summary>
    /// Получить список последних изменений
    /// </summary>
    public List<ChangeRecord> GetRecentChanges(int count = 10)
    {
        return _undoStack.Take(count).Reverse().ToList();
    }
    
    /// <summary>
    /// Очистить всю историю
    /// </summary>
    public void ClearHistory()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        OnHistoryChanged();
    }
    
    /// <summary>
    /// Экспорт истории в JSON
    /// </summary>
    public string ExportToJson()
    {
        var json = new System.Text.StringBuilder();
        json.AppendLine("{");
        json.AppendLine("  \"history\": [");
        
        var changes = _undoStack.Reverse().ToList();
        for (int i = 0; i < changes.Count; i++)
        {
            var change = changes[i];
            json.Append($"    {{ \"offset\": {change.Offset}, \"oldValue\": {change.OldValue}, \"newValue\": {change.NewValue}, \"timestamp\": \"{change.Timestamp:O}\" }}");
            if (i < changes.Count - 1)
                json.AppendLine(",");
            else
                json.AppendLine();
        }
        
        json.AppendLine("  ]");
        json.AppendLine("}");
        
        return json.ToString();
    }
    
    protected virtual void OnHistoryChanged()
    {
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// Аргументы события изменения
/// </summary>
public class ChangeEventArgs : EventArgs
{
    public int Offset { get; }
    public byte Value { get; }
    public bool IsUndo { get; }
    
    public ChangeEventArgs(int offset, byte value, bool isUndo)
    {
        Offset = offset;
        Value = value;
        IsUndo = isUndo;
    }
}
