using VagImmoEditor.Pro.Core.Crc;
using VagImmoEditor.Pro.Core.Codecs;
using VagImmoEditor.Pro.Data.Maps;
using VagImmoEditor.Pro.Data.Models;

namespace VagImmoEditor.Pro.Services
{
    /// <summary>
    /// Сервис работы с EEPROM 24LC64 (8KB)
    /// </summary>
    public class EepromDataService
    {
        private byte[] _data;
        private readonly List<ChangeRecord> _changeHistory = new();
        private int _undoPosition = -1;
        private bool _isModified;

        public const int MinValidSize = 256;
        public const int MaxValidSize = 8192; // 24LC64 = 8KB

        public byte[] Data => _data.ToArray(); // Возвращаем копию для безопасности
        public bool IsModified => _isModified;
        public bool CanUndo => _undoPosition >= 0;
        public bool CanRedo => _undoPosition < _changeHistory.Count - 1;
        public IReadOnlyList<ChangeRecord> History => _changeHistory.AsReadOnly();

        public EepromDataService()
        {
            _data = new byte[MaxValidSize];
        }

        public EepromResult LoadFromFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return EepromResult.Fail($"File not found: {filePath}");

                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length < MinValidSize || fileInfo.Length > MaxValidSize)
                    return EepromResult.Fail($"Invalid file size: {fileInfo.Length} bytes. Must be between {MinValidSize} and {MaxValidSize} bytes.");

                var fileData = File.ReadAllBytes(filePath);
                return LoadFromBytes(fileData);
            }
            catch (Exception ex)
            {
                return EepromResult.Fail($"Error loading file: {ex.Message}", ex);
            }
        }

        public EepromResult LoadFromBytes(byte[] data)
        {
            try
            {
                if (data.Length < MinValidSize || data.Length > MaxValidSize)
                    return EepromResult.Fail($"Invalid data size: {data.Length} bytes");

                _data = new byte[MaxValidSize];
                Array.Copy(data, _data, data.Length);
                
                // Заполняем остаток нулями если файл меньше 8KB
                for (int i = data.Length; i < MaxValidSize; i++)
                {
                    _data[i] = 0x00;
                }

                _changeHistory.Clear();
                _undoPosition = -1;
                _isModified = false;

                return EepromResult.Ok($"Loaded {data.Length} bytes");
            }
            catch (Exception ex)
            {
                return EepromResult.Fail($"Error loading data: {ex.Message}", ex);
            }
        }

        public EepromResult SaveToFile(string filePath)
        {
            try
            {
                File.WriteAllBytes(filePath, _data);
                _isModified = false;
                return EepromResult.Ok($"Saved to {filePath}");
            }
            catch (Exception ex)
            {
                return EepromResult.Fail($"Error saving file: {ex.Message}", ex);
            }
        }

        public byte ReadByte(int offset)
        {
            if (offset < 0 || offset >= _data.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));
            return _data[offset];
        }

        public void WriteByte(int offset, byte value, string description = "")
        {
            if (offset < 0 || offset >= _data.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));

            var oldValue = _data[offset];
            if (oldValue != value)
            {
                RecordChange(offset, oldValue, value, description);
                _data[offset] = value;
                _isModified = true;
            }
        }

        public void SetRange(int offset, byte[] data, string description = "")
        {
            if (offset < 0 || offset + data.Length > _data.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));

            for (int i = 0; i < data.Length; i++)
            {
                var oldValue = _data[offset + i];
                if (oldValue != data[i])
                {
                    RecordChange(offset + i, oldValue, data[i], $"{description} [{i}]");
                    _data[offset + i] = data[i];
                }
            }
            
            if (description.Length > 0)
                _isModified = true;
        }

        public byte[] GetRange(int offset, int length)
        {
            if (offset < 0 || offset + length > _data.Length)
                throw new ArgumentOutOfRangeException();

            var result = new byte[length];
            Array.Copy(_data, offset, result, 0, length);
            return result;
        }

        private void RecordChange(int offset, byte oldValue, byte newValue, string description)
        {
            // Удаляем будущую историю если мы не в конце
            if (_undoPosition < _changeHistory.Count - 1)
            {
                _changeHistory.RemoveRange(_undoPosition + 1, _changeHistory.Count - _undoPosition - 1);
            }

            _changeHistory.Add(new ChangeRecord
            {
                Offset = offset,
                OldValue = oldValue,
                NewValue = newValue,
                Description = description
            });

            _undoPosition = _changeHistory.Count - 1;
        }

        public EepromResult Undo()
        {
            if (!CanUndo)
                return EepromResult.Fail("No changes to undo");

            var change = _changeHistory[_undoPosition];
            _data[change.Offset] = change.OldValue;
            _undoPosition--;
            _isModified = true;

            return EepromResult.Ok($"Undone: {change.Description}");
        }

        public EepromResult Redo()
        {
            if (!CanRedo)
                return EepromResult.Fail("No changes to redo");

            _undoPosition++;
            var change = _changeHistory[_undoPosition];
            _data[change.Offset] = change.NewValue;
            _isModified = true;

            return EepromResult.Ok($"Redone: {change.Description}");
        }

        public ushort CalculateCrc(string algorithmName, int startOffset, int length)
        {
            return CrcManager.CalculateCrc(algorithmName, _data, startOffset, length);
        }

        public bool VerifyCrc(string algorithmName, int startOffset, int length, int crcOffset)
        {
            var expectedCrc = BitConverter.ToUInt16(_data, crcOffset);
            var calculatedCrc = CalculateCrc(algorithmName, startOffset, length);
            return expectedCrc == calculatedCrc;
        }

        public void UpdateCrc(string algorithmName, int startOffset, int length, int crcOffset)
        {
            var crc = CalculateCrc(algorithmName, startOffset, length);
            var crcBytes = BitConverter.GetBytes(crc);
            SetRange(crcOffset, crcBytes, "Update CRC");
        }

        public void ClearHistory()
        {
            _changeHistory.Clear();
            _undoPosition = -1;
        }
    }
}
