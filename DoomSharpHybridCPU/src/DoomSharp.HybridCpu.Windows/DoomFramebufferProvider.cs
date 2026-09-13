using System.Security.Cryptography;
using System.IO;
using HybridCPU.Platform.Contracts;
using HybridCPU.RuntimeKernel;

namespace DoomSharp.HybridCpu.Windows;

public sealed record DoomFramebufferFrame(
    ulong ContextId, long Sequence, int Width, int Height, byte[] Pixels,
    byte[] PaletteRgb24, string PixelSha256, string PaletteSha256, DateTimeOffset CapturedAtUtc);

public static class DoomFramebufferExport
{
    public static void SaveBmp(DoomFramebufferFrame frame, string path)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        int rawStride = checked(frame.Width * 3);
        int stride = checked((rawStride + 3) & ~3);
        int bytes = checked(stride * frame.Height);
        byte[] pixels = new byte[bytes];
        for (int y = 0; y < frame.Height; y++)
            for (int x = 0; x < frame.Width; x++)
            {
                int palette = frame.Pixels[y * frame.Width + x] * 3;
                int target = (frame.Height - 1 - y) * stride + x * 3;
                pixels[target] = frame.PaletteRgb24[palette + 2];
                pixels[target + 1] = frame.PaletteRgb24[palette + 1];
                pixels[target + 2] = frame.PaletteRgb24[palette];
            }
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new BinaryWriter(stream);
        writer.Write((byte)'B'); writer.Write((byte)'M'); writer.Write(checked(54 + bytes));
        writer.Write(0); writer.Write(54); writer.Write(40); writer.Write(frame.Width);
        writer.Write(frame.Height); writer.Write((short)1); writer.Write((short)24);
        writer.Write(0); writer.Write(bytes); writer.Write(2835); writer.Write(2835);
        writer.Write(0); writer.Write(0); writer.Write(pixels);
    }
}

/// <summary>Host-owned indexed Doom framebuffer sink. It never exposes guest memory.</summary>
public sealed class DoomFramebufferProvider : IHybridCpuHostServiceProviderV1
{
    private readonly Func<ulong, int, byte[]?> read;
    private readonly object gate = new();
    private readonly int maximumPixels;
    private int width, height;
    private byte[]? palette;
    private long sequence;
    private DoomFramebufferFrame? latest;
    private bool initialized;

    public DoomFramebufferProvider(Func<ulong, int, byte[]?> read, int maximumPixels = 16_384 * 16_384)
    {
        this.read = read ?? throw new ArgumentNullException(nameof(read));
        if (maximumPixels <= 0) throw new ArgumentOutOfRangeException(nameof(maximumPixels));
        this.maximumPixels = maximumPixels;
    }

    public DoomFramebufferFrame? LatestFrame { get { lock (gate) return latest; } }
    public event Action<DoomFramebufferFrame>? FramePresented;

    public HybridCpuHostProviderResultV1 Invoke(HybridCpuExternalServiceRequestV1 request)
    {
        if (request.Service != HybridCpuHostServiceV1.Graphics)
            return new(HybridCpuExternalServiceStatusV1.MissingSymbol, 0, 2, "Graphics operation is not implemented.");
        if (request.PrivilegeMode != HybridCpuPrivilegeModeV1.User || request.Arguments.Count != 0 &&
            request.Operation != HybridCpuFramebufferServiceContractV1.InitializeOperation)
            return new(HybridCpuExternalServiceStatusV1.InvalidRequest, 0, 0, "Invalid graphics envelope.");
        if (request.Operation == HybridCpuFramebufferServiceContractV1.InitializeOperation)
        {
            if (request.Arguments.Count != 1 || request.BufferLength != 0 ||
                request.BufferAddress != 0 || request.BufferAccess != HybridCpuHostBufferAccessV1.None ||
                !HybridCpuFramebufferServiceContractV1.TryUnpackDimensions(request.Arguments[0], out int w, out int h) ||
                (long)w * h > maximumPixels)
                return new(HybridCpuExternalServiceStatusV1.InvalidRequest, 0, 0, "Invalid framebuffer dimensions.");
            lock (gate) { width = w; height = h; palette = null; latest = null; sequence = 0; initialized = true; }
            return new(HybridCpuExternalServiceStatusV1.Success, 0, 0, string.Empty);
        }
        lock (gate)
        {
            if (!initialized) return new(HybridCpuExternalServiceStatusV1.InvalidRequest, 0, 0, "Framebuffer is not initialized.");
            if (request.Arguments.Count != 0 || request.BufferAddress == 0 ||
                request.BufferAccess != HybridCpuHostBufferAccessV1.Read)
                return new(HybridCpuExternalServiceStatusV1.InvalidRequest, 0, 0, "Invalid framebuffer payload.");
            if (request.Operation == HybridCpuFramebufferServiceContractV1.UpdatePaletteOperation)
            {
                if (request.BufferLength != HybridCpuFramebufferServiceContractV1.PaletteBytes)
                    return new(HybridCpuExternalServiceStatusV1.InvalidRequest, 0, 0, "Invalid palette length.");
                byte[]? bytes = read(request.BufferAddress, checked((int)request.BufferLength));
                if (bytes is null || bytes.Length != HybridCpuFramebufferServiceContractV1.PaletteBytes)
                    return new(HybridCpuExternalServiceStatusV1.ProviderFailure, 0, 5, "Palette snapshot failed.");
                palette = bytes.ToArray();
                return new(HybridCpuExternalServiceStatusV1.Success, 0, 0, string.Empty);
            }
            if (request.Operation != HybridCpuFramebufferServiceContractV1.PresentOperation)
                return new(HybridCpuExternalServiceStatusV1.MissingSymbol, 0, 2, "Unknown graphics operation.");
            int pixels = checked(width * height);
            if (request.BufferLength != (ulong)pixels)
                return new(HybridCpuExternalServiceStatusV1.InvalidRequest, 0, 0, "Framebuffer payload size mismatch.");
            if (palette is null) return new(HybridCpuExternalServiceStatusV1.InvalidRequest, 0, 0, "Palette is not initialized.");
            byte[]? frame = read(request.BufferAddress, pixels);
            if (frame is null || frame.Length != pixels)
                return new(HybridCpuExternalServiceStatusV1.ProviderFailure, 0, 5, "Framebuffer snapshot failed.");
            var published = new DoomFramebufferFrame(request.ContextId, ++sequence, width, height, frame.ToArray(),
                palette.ToArray(), Convert.ToHexString(SHA256.HashData(frame)), Convert.ToHexString(SHA256.HashData(palette)),
                DateTimeOffset.UtcNow);
            latest = published;
            FramePresented?.Invoke(published);
            return new(HybridCpuExternalServiceStatusV1.Success, 0, 0, string.Empty);
        }
    }
}

public sealed class DoomCompositeHostProvider : IHybridCpuHostServiceProviderV1
{
    private readonly IHybridCpuHostServiceProviderV1 console;
    private readonly DoomFramebufferProvider graphics;
    public DoomCompositeHostProvider(IHybridCpuHostServiceProviderV1 console, DoomFramebufferProvider graphics)
        => (this.console, this.graphics) = (console, graphics);
    public HybridCpuHostProviderResultV1 Invoke(HybridCpuExternalServiceRequestV1 request) =>
        request.Service == HybridCpuHostServiceV1.Graphics ? graphics.Invoke(request) : console.Invoke(request);
}
