namespace VagImmoEditor.Pro.Core.Crc
{
    /// <summary>
    /// Интерфейс для алгоритмов контрольной суммы
    /// </summary>
    public interface ICrcAlgorithm
    {
        string Name { get; }
        ushort Calculate(byte[] data, int offset, int length);
        bool Verify(byte[] data, int offset, int length, ushort expectedCrc);
    }
}
