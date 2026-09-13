namespace DoomSharp.Core.Input;

public sealed class InputEventRingBuffer
{
    private readonly InputEvent[] _items;
    private int _read;
    private int _write;
    private int _count;

    public InputEventRingBuffer(int capacity)
    {
        _items = new InputEvent[capacity];
    }

    public void Enqueue(InputEvent value)
    {
        if (_count == _items.Length)
            return;
        _items[_write] = value;
        _write = (_write + 1) % _items.Length;
        _count++;
    }

    public bool TryDequeue(out InputEvent value)
    {
        var item = DequeueOrNull();
        if (item is null)
        {
            value = null!;
            return false;
        }

        value = item;
        return true;
    }

    public InputEvent? DequeueOrNull()
    {
        if (_count == 0)
            return null;

        var value = _items[_read];
        _read = (_read + 1) % _items.Length;
        _count--;
        return value;
    }
}
