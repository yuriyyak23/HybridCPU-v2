using DoomSharp.Core;

namespace DoomSharp.HybridCpu.Guest;

public sealed class DeterministicDoomClock : IDoomClock
{
    private readonly IHybridCpuGuestServices _services;
    private int _lastTic;

    public DeterministicDoomClock(IHybridCpuGuestServices services)
    {
        _services = services;
        _lastTic = services.GetMonotonicDoomTics();
    }

    public int GetTime()
    {
        var now = _services.GetMonotonicDoomTics();
        if (now < _lastTic)
            throw new DoomTerminationException("HybridCPU virtual clock moved backwards.", 3);
        _lastTic = now;
        return now;
    }

    public void WaitTic()
    {
        _services.WaitUntilDoomTic(GetTime() + 1);
    }

    public void WaitVBL(int count)
    {
        if (count <= 0)
            return;
        var doomTics = (count + 1) / 2;
        _services.WaitUntilDoomTic(GetTime() + doomTics);
    }
}
