namespace DoomSharp.Core.Data;

public sealed class ByteReader
{
    private readonly byte[] _data;
    private int _position;

    public ByteReader(byte[] data, int position = 0)
    {
        _data = data;
        _position = position;
    }

    public int Position => _position;

    public byte ReadByte()
    {
        EnsureAvailable(1);
        return _data[_position++];
    }

    public short ReadInt16()
    {
        EnsureAvailable(2);
        var value = (short)(_data[_position] | (_data[_position + 1] << 8));
        _position += 2;
        return value;
    }

    public ushort ReadUInt16()
    {
        EnsureAvailable(2);
        var value = (ushort)(_data[_position] | (_data[_position + 1] << 8));
        _position += 2;
        return value;
    }

    public int ReadInt32()
    {
        EnsureAvailable(4);
        var value = _data[_position]
            | (_data[_position + 1] << 8)
            | (_data[_position + 2] << 16)
            | (_data[_position + 3] << 24);
        _position += 4;
        return value;
    }

    public uint ReadUInt32() => unchecked((uint)ReadInt32());

    public string ReadName(int width)
    {
        EnsureAvailable(width);
        var start = _position;
        var end = start + width;
        while (_position < end && _data[_position] != 0)
            _position++;
        var length = _position - start;
        _position = end;
        var chars = new char[length];
        for (var i = 0; i < length; i++)
        {
            chars[i] = (char)_data[start + i];
        }

        return new string(chars);
    }

    public string ReadName8() => ReadName(8);

    public byte[] ReadBytes(int count)
    {
        EnsureAvailable(count);
        var result = new byte[count];
        for (var i = 0; i < count; i++)
        {
            result[i] = _data[_position + i];
        }

        _position += count;
        return result;
    }

    public void Seek(int position)
    {
        if (position < 0 || position > _data.Length)
            throw new WadFormatException("WAD reader seek is outside the image.");
        _position = position;
    }

    private void EnsureAvailable(int count)
    {
        if (count < 0 || _position < 0 || _position > _data.Length - count)
            throw new WadFormatException("WAD data is truncated.");
    }
}
