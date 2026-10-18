using System.Text.Json;
using HybridCPU_ISE.CloseToHSL.Memory.DMA;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7b zero-caller DMA channel representation contract.</summary>
public sealed class Rf127bDmaChannelIdCoreValidInputContractTests
{
    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)1)]
    [InlineData((byte)6)]
    [InlineData((byte)7)]
    public void ValidChannelsRoundTripExactly(byte raw)
    {
        DmaChannelId constructor = new(raw);
        DmaChannelId created = DmaChannelId.Create(raw);
        DmaChannelId fromRaw = DmaChannelId.FromRawValue(raw);
        DmaChannelId cast = (DmaChannelId)raw;

        Assert.True(DmaChannelId.IsRepresentable(raw));
        Assert.True(DmaChannelId.TryCreate(raw, out DmaChannelId tried));
        Assert.Equal(constructor, created);
        Assert.Equal(constructor, fromRaw);
        Assert.Equal(constructor, cast);
        Assert.Equal(constructor, tried);
        Assert.Equal(raw, constructor.Value);
        Assert.Equal(raw, constructor.ToRawValue());
        Assert.Equal(raw, (byte)constructor);
        Assert.Equal($"dma-channel{raw}", constructor.ToString());
        Assert.Equal(constructor,
            JsonSerializer.Deserialize<DmaChannelId>(JsonSerializer.Serialize(constructor)));
    }

    [Fact]
    public void ZeroAndDefaultArePresentChannelZero()
    {
        Assert.Equal(DmaChannelId.Zero, default);
        Assert.Equal((byte)0, DmaChannelId.Zero.Value);
    }

    [Theory]
    [InlineData((byte)8)]
    [InlineData((byte)9)]
    [InlineData(byte.MaxValue)]
    public void OutOfRangeChannelsRejectWithoutZeroAlias(byte raw)
    {
        Assert.False(DmaChannelId.IsRepresentable(raw));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DmaChannelId(raw));
        Assert.Throws<ArgumentOutOfRangeException>(() => DmaChannelId.Create(raw));
        Assert.Throws<ArgumentOutOfRangeException>(() => DmaChannelId.FromRawValue(raw));
        Assert.Throws<ArgumentOutOfRangeException>(() => (DmaChannelId)raw);
        Assert.False(DmaChannelId.TryCreate(raw, out DmaChannelId failed));
        Assert.Equal(default, failed);
        Assert.Equal(DmaChannelId.Zero, failed);
    }


    private static int Count(string text, string marker) => text.Split(marker).Length - 1;
    private static string Join(string root, string? exclude = null) => string.Join("\n",
        Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => exclude is null ||
                !path.EndsWith(exclude, StringComparison.Ordinal))
            .Where(path => !path.Replace('\\', '/').Contains("/bin/", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Replace('\\', '/').Contains("/obj/", StringComparison.OrdinalIgnoreCase))
            .Select(File.ReadAllText));
    private static string Root()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current.FullName, "ResearchPaper"))) return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException();
    }
}
