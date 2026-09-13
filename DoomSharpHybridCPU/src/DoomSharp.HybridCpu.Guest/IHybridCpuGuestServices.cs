using System.Diagnostics.CodeAnalysis;
using DoomSharp.Core.Input;

namespace DoomSharp.HybridCpu.Guest;

/// <summary>
/// Loader-owned typed services. The byte array returned for the WAD is an immutable,
/// process-lifetime boot blob; the guest never writes it and does not embed another copy.
/// </summary>
public interface IHybridCpuGuestServices
{
    byte[] GetBootBlob(int blobId);
    void ConsoleWrite(string message);
    void ConsoleSetTitle(string title);
    void InitializeFramebuffer(int width, int height);
    void UpdatePalette(byte[] palette);
    void PresentFramebuffer(byte[] framebuffer);
    InputEvent? PullInput();
    int GetMonotonicDoomTics();
    void WaitUntilDoomTic(int targetTic);

    [DoesNotReturn]
    void ProcessExit(int exitCode);
}
