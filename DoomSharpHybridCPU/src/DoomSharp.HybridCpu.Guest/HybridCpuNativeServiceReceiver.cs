using System;
using System.Diagnostics.CodeAnalysis;
using DoomSharp.Core.Input;

namespace DoomSharp.HybridCpu.Guest;

/// <summary>
/// Managed receiver for the exact IHybridCpuGuestServices native binding package. Allocation
/// and the static root execute on the guest CPU; no host object reference enters the image.
/// Interface calls are bound to the existing native HCSV helpers by that package. A missing
/// binding or accidental CoreCLR call must fail rather than simulate a platform service.
/// </summary>
internal sealed class HybridCpuNativeServiceReceiver : IHybridCpuGuestServices
{
    public byte[] GetBootBlob(int blobId) => throw Unsupported();
    public void ConsoleWrite(string message) => throw Unsupported();
    public void ConsoleSetTitle(string title) => throw Unsupported();
    public void InitializeFramebuffer(int width, int height) => throw Unsupported();
    public void UpdatePalette(byte[] palette) => throw Unsupported();
    public void PresentFramebuffer(byte[] framebuffer) => throw Unsupported();
    public InputEvent? PullInput() => throw Unsupported();
    public int GetMonotonicDoomTics() => throw Unsupported();
    public void WaitUntilDoomTic(int targetTic) => throw Unsupported();
    [DoesNotReturn]
    public void ProcessExit(int exitCode) => throw Unsupported();
    private static NotSupportedException Unsupported() =>
        new NotSupportedException("HybridCPU native guest-service binding is required.");
}
