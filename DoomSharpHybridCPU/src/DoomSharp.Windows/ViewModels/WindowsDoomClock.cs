using System.Diagnostics;
using System.Threading;
using DoomSharp.Core;

namespace DoomSharp.Windows.ViewModels;

public sealed class WindowsDoomClock : IDoomClock
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    public int GetTime()
    {
        return (int)(_stopwatch.ElapsedMilliseconds * Constants.TicRate / 1000);
    }

    public void WaitTic()
    {
        Thread.Sleep(1000 / Constants.TicRate);
    }

    public void WaitVBL(int count)
    {
        Thread.Sleep(count * 1000 / 70);
    }
}
