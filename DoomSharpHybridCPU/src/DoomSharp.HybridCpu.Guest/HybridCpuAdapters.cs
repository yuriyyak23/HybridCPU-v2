using DoomSharp.Core;
using DoomSharp.Core.Graphics;

namespace DoomSharp.HybridCpu.Guest;

internal sealed class HybridCpuConsole : IConsole
{
    private readonly IHybridCpuGuestServices _services;
    public HybridCpuConsole(IHybridCpuGuestServices services) => _services = services;
    public void Write(string message) => _services.ConsoleWrite(message);
    public void WriteLine(string message)
    {
        _services.ConsoleWrite(message);
        _services.ConsoleWrite("\n");
    }
    public void SetTitle(string title) => _services.ConsoleSetTitle(title);
    public void Shutdown() { }
}

internal sealed class HybridCpuGraphics : IGraphics
{
    private readonly IHybridCpuGuestServices _services;
    public HybridCpuGraphics(IHybridCpuGuestServices services) => _services = services;
    public void Initialize() => _services.InitializeFramebuffer(Constants.ScreenWidth, Constants.ScreenHeight);
    public void UpdatePalette(byte[] palette) => _services.UpdatePalette(palette);
    public void ScreenReady(byte[] output) => _services.PresentFramebuffer(output);

    public void StartTic()
    {
        while (true)
        {
            var inputEvent = _services.PullInput();
            if (inputEvent is null)
                break;
            DoomGame.Instance.PostEvent(inputEvent);
        }
    }
}
