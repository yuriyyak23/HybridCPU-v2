using DoomSharp.HybridCpu.Guest;

try
{
    var services = HybridCpuGuestRuntime.RequirePlatform();
    if (services is null || !ReferenceEquals(services, HybridCpuGuestRuntime.RequirePlatform()))
        throw new Exception("Guest bootstrap did not retain one service receiver.");
    Console.WriteLine("PASS guest cctor provides a stable non-null interface receiver");
    foreach (Action call in new Action[] { () => services.GetBootBlob(1), () => services.ConsoleWrite("test"),
        () => services.GetMonotonicDoomTics(), () => services.ProcessExit(0) })
    {
        try { call(); throw new Exception("Native service method silently executed on CoreCLR."); }
        catch (NotSupportedException) { Console.WriteLine("PASS native receiver has no CoreCLR service fallback"); }
    }
    Console.WriteLine("PASS 5 checks; source/CoreCLR component evidence only.");
    return 0;
}
catch (Exception error)
{
    Console.Error.WriteLine("FAIL " + error.GetType().Name + ": " + error.Message);
    return 1;
}
