namespace DoomSharp.Core;

/// <summary>
/// Platform timing services required by the game loop.
/// </summary>
public interface IDoomClock
{
    int GetTime();
    void WaitTic();
    void WaitVBL(int count);
}

public sealed class NullDoomClock : IDoomClock
{
    private int _time;

    public int GetTime() => _time;

    public void WaitTic()
    {
        _time++;
    }

    public void WaitVBL(int count)
    {
        _time += count / 2;
    }
}
