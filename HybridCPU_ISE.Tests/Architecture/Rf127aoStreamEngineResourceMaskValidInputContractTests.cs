using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7ao StreamEngineId ResourceMaskBuilder valid-input overload contract.</summary>
public sealed class Rf127aoStreamEngineResourceMaskValidInputContractTests
{
    [Fact]
    public void CheckedResourceMaskOverloadMatchesEveryExistingValidRawSelector()
    {
        for (int raw = StreamEngineId.MinValue; raw <= StreamEngineId.MaxValue; raw++)
        {
            StreamEngineId checkedId = StreamEngineId.Create(raw);
            Assert.Equal(ResourceMaskBuilder.ForStreamEngine(raw),
                ResourceMaskBuilder.ForStreamEngine(checkedId));
            Assert.Equal(ResourceMaskBuilder.ForStreamEngine128(raw),
                ResourceMaskBuilder.ForStreamEngine128(checkedId));
        }
    }

    [Fact]
    public void RawSignaturesAndExistingCallerContourRemainUnchanged()
    {
        string source = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Types", "MicroOp.cs"));

        Assert.Contains("ForStreamEngine(int engineId)", source, StringComparison.Ordinal);
        Assert.Contains("ForStreamEngine128(int engineId)", source, StringComparison.Ordinal);
        Assert.Contains("ForStreamEngine(StreamEngineId engineId)", source, StringComparison.Ordinal);
        Assert.Contains("ForStreamEngine128(StreamEngineId engineId)", source, StringComparison.Ordinal);
        Assert.Contains("RequireResourceId(engineId, 4, nameof(engineId), \"stream-engine\")", source,
            StringComparison.Ordinal);
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
