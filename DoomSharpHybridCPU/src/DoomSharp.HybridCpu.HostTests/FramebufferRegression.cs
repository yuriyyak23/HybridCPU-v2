using System.Security.Cryptography;
using DoomSharp.HybridCpu.Windows;
using HybridCPU.Platform.Contracts;
using HybridCPU.RuntimeKernel;

public static class FramebufferRegression
{
    public static int Run(string root)
    {
        Directory.CreateDirectory(root);
        const ulong context = 7;
        var memory = new Dictionary<ulong, byte[]>();
        byte[] palette = new byte[HybridCpuFramebufferServiceContractV1.PaletteBytes];
        palette[3] = 255; // palette index 1 = red
        byte[] pixels = [0, 1, 1, 0];
        memory[0x1000] = palette; memory[0x2000] = pixels;
        var provider = new DoomFramebufferProvider((address, length) =>
            memory.TryGetValue(address, out byte[]? value) && value.Length == length ? value.ToArray() : null);
        var init = Request(context, HybridCpuFramebufferServiceContractV1.InitializeOperation,
            0, 0, HybridCpuHostBufferAccessV1.None, [HybridCpuFramebufferServiceContractV1.PackDimensions(2, 2)]);
        Check(provider.Invoke(init).Status == HybridCpuExternalServiceStatusV1.Success, "framebuffer initialize");
        var pal = Request(context, HybridCpuFramebufferServiceContractV1.UpdatePaletteOperation,
            0x1000, palette.Length, HybridCpuHostBufferAccessV1.Read, []);
        Check(provider.Invoke(pal).Status == HybridCpuExternalServiceStatusV1.Success, "palette accepted");
        var present = Request(context, HybridCpuFramebufferServiceContractV1.PresentOperation,
            0x2000, pixels.Length, HybridCpuHostBufferAccessV1.Read, []);
        Check(provider.Invoke(present).Status == HybridCpuExternalServiceStatusV1.Success, "present accepted");
        DoomFramebufferFrame frame = provider.LatestFrame ?? throw new Exception("frame missing");
        Check(frame.Sequence == 1 && frame.Width == 2 && frame.Height == 2, "frame identity");
        Check(frame.PixelSha256 == Convert.ToHexString(SHA256.HashData(pixels)), "pixel digest");
        string bmp = Path.Combine(root, "synthetic-frame.bmp");
        DoomFramebufferExport.SaveBmp(frame, bmp);
        byte[] bytes = File.ReadAllBytes(bmp);
        Check(bytes[0] == (byte)'B' && bytes[1] == (byte)'M' && bytes.Length == 70, "BMP export");
        Check(bytes.AsSpan(54, 8).SequenceEqual(new byte[] { 0, 0, 255, 0, 0, 0, 0, 0 }) &&
            bytes.AsSpan(62, 8).SequenceEqual(new byte[] { 0, 0, 0, 0, 0, 255, 0, 0 }),
            "BMP bottom-up rows, RGB-to-BGR conversion and stride padding");
        bool duplicateRejected = false;
        try { DoomFramebufferExport.SaveBmp(frame, bmp); }
        catch (IOException) { duplicateRejected = true; }
        Check(duplicateRejected && File.ReadAllBytes(bmp).SequenceEqual(bytes), "duplicate export preserves original bytes");
        Console.WriteLine("PASS synthetic graphics provider regression");
        return 0;
    }

    private static HybridCpuExternalServiceRequestV1 Request(ulong context, ulong operation, ulong address,
        int length, HybridCpuHostBufferAccessV1 access, IReadOnlyList<ulong> args) =>
        new(context, HybridCpuHostServiceV1.Graphics, operation, HybridCpuPrivilegeModeV1.User,
            address, (ulong)length, access, args, "");

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
        Console.WriteLine("PASS " + message);
    }
}
