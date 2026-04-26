using System;
using System.IO;
using HybridCPU_ISE.Core;
using Xunit;

namespace HybridCPU_ISE.Tests.tests;

public sealed class Phase14TraceFormatWindowTests
{
    private const uint TraceMagic = 0x54524143;

    [Fact]
    public void ReplayEngine_LoadBinaryTrace_AcceptsCurrentAndTwoPriorVersions()
    {
        ushort oldestSupportedVersion = (ushort)(TraceSink.BinaryTraceVersion - 2);

        for (ushort version = oldestSupportedVersion; version <= TraceSink.BinaryTraceVersion; version++)
        {
            string path = CreateEmptyBinaryTrace(version);
            try
            {
                var replay = new ReplayEngine(path);

                var stats = replay.GetStatistics();
                Assert.Equal(0, stats.TotalEvents);
                Assert.Equal(0, stats.ThreadsWithEvents);
                Assert.Equal(0, stats.MaxCycle);
            }
            finally
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void ReplayEngine_LoadBinaryTrace_RejectsOlderThanLiveReplayWindow()
    {
        ushort legacyVersion = (ushort)(TraceSink.BinaryTraceVersion - 3);
        string path = CreateEmptyBinaryTrace(legacyVersion);

        try
        {
            InvalidDataException ex = Assert.Throws<InvalidDataException>(() => new ReplayEngine(path));
            Assert.Contains($"Unsupported trace version: {legacyVersion}", ex.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReplayEngine_LoadBinaryTrace_RejectsAncientLegacyVersions()
    {
        string path = CreateEmptyBinaryTrace(14);

        try
        {
            InvalidDataException ex = Assert.Throws<InvalidDataException>(() => new ReplayEngine(path));
            Assert.Contains("Unsupported trace version: 14", ex.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReplayEngine_LoadBinaryTrace_RejectsFutureVersions()
    {
        ushort futureVersion = (ushort)(TraceSink.BinaryTraceVersion + 1);
        string path = CreateEmptyBinaryTrace(futureVersion);

        try
        {
            InvalidDataException ex = Assert.Throws<InvalidDataException>(() => new ReplayEngine(path));
            Assert.Contains($"Unsupported trace version: {futureVersion}", ex.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string CreateEmptyBinaryTrace(ushort version)
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-trace-v{version}.bin");
        using var fs = new FileStream(path, FileMode.CreateNew);
        using var writer = new BinaryWriter(fs);

        writer.Write(TraceMagic);
        writer.Write(version);
        writer.Write(0);
        writer.Write(0);

        return path;
    }
}
