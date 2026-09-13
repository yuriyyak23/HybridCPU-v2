using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7am additive StreamEngineId valid-input contract.</summary>
public sealed class Rf127amStreamEngineIdCoreValidInputContractTests
{
    [Fact]
    public void EveryRepresentableEngineRoundTripsWithoutChangingRawSelectors()
    {
        for (int raw = StreamEngineId.MinValue; raw <= StreamEngineId.MaxValue; raw++)
        {
            Assert.True(StreamEngineId.IsRepresentable(raw));
            Assert.True(StreamEngineId.TryCreate(raw, out StreamEngineId tried));

            StreamEngineId created = StreamEngineId.Create(raw);
            StreamEngineId fromRaw = StreamEngineId.FromRawValue((byte)raw);

            Assert.Equal(raw, (int)tried);
            Assert.Equal(raw, (int)created);
            Assert.Equal((byte)raw, created.ToRawValue());
            Assert.Equal(created, fromRaw);
            Assert.Equal(created, (StreamEngineId)raw);
        }
    }


    [Fact]
    public void ExistingRawResourceMaskValidInputsKeepExactBitPlacement()
    {
        for (int raw = StreamEngineId.MinValue; raw <= StreamEngineId.MaxValue; raw++)
        {
            ulong expected = 1UL << (55 + raw);
            Assert.Equal(expected, (ulong)ResourceMaskBuilder.ForStreamEngine(raw));
            Assert.Equal(expected, ResourceMaskBuilder.ForStreamEngine128(raw).Low);
        }
    }

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
