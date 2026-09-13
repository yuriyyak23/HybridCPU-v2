using System;
using DoomSharp.Core;

namespace DoomSharp.HybridCpu.Guest;

public static class EntryPoint
{
    public const int DoomWadBootBlob = 1;

    public static int Run()
    {
        var services = HybridCpuGuestRuntime.RequirePlatform();
        try
        {
            var console = new HybridCpuConsole(services);
            DoomGame.SetConsole(console);
            DoomGame.SetOutputRenderer(new HybridCpuGraphics(services));
            DoomGame.SetClock(new DeterministicDoomClock(services));

            var wad = services.GetBootBlob(DoomWadBootBlob);
            if (wad is null || wad.Length == 0)
                throw new DoomTerminationException("The immutable DOOM WAD boot blob is missing.", 4);

            DoomGame.Instance.Run(GameMode.Retail, wad);
            services.ProcessExit(0);
        }
        catch (DoomTerminationException termination)
        {
            services.ProcessExit(termination.ExitCode);
        }
        catch (Exception error)
        {
            services.ConsoleWrite("Unhandled managed exception: " + error.Message + "\n");
            services.ProcessExit(255);
        }

        throw new DoomTerminationException("HybridCPU process_exit unexpectedly returned.", 2);
    }
}
