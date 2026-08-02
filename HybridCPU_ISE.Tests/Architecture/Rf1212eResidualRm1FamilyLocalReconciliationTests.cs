using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf1212eResidualRm1FamilyLocalReconciliationTests
{
    [Fact]
    public void EveryResidualBuilderRejectsInvalidBeforeItsShift()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ResourceMaskBuilder.ForMemoryBank(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ResourceMaskBuilder.ForMemoryBank(16));
        Assert.Throws<ArgumentOutOfRangeException>(() => ResourceMaskBuilder.ForMemoryDomain(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ResourceMaskBuilder.ForMemoryDomain(16));
        Assert.Throws<ArgumentOutOfRangeException>(() => ResourceMaskBuilder.ForDMAChannel(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ResourceMaskBuilder.ForDMAChannel(4));
        Assert.Throws<ArgumentOutOfRangeException>(() => ResourceMaskBuilder.ForStreamEngine(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ResourceMaskBuilder.ForStreamEngine(4));
        Assert.Throws<ArgumentOutOfRangeException>(() => ResourceMaskBuilder.ForAccelerator(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ResourceMaskBuilder.ForAccelerator(4));
        Assert.Throws<ArgumentOutOfRangeException>(() => ResourceMaskBuilder.ForExtendedGRLBChannel(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ResourceMaskBuilder.ForExtendedGRLBChannel(32));
        Assert.Throws<ArgumentOutOfRangeException>(() => ResourceMaskBuilder.ForExtendedMemoryDomain(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ResourceMaskBuilder.ForExtendedMemoryDomain(16));
    }

    [Fact]
    public void SourceRetainsOneRangeGateBeforeEveryResidualShift()
    {
        string source = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "MicroOps", "Types", "MicroOp.cs");
        foreach ((string method, int bound) in new[]
        {
            ("ForMemoryBank", 16), ("ForMemoryDomain", 16), ("ForDMAChannel", 4),
            ("ForStreamEngine", 4), ("ForAccelerator", 4), ("ForExtendedGRLBChannel", 32),
            ("ForExtendedMemoryDomain", 16)
        })
        {
            int start = source.IndexOf($"{method}(int ", StringComparison.Ordinal);
            Assert.True(start >= 0, $"Missing {method} declaration.");
            int end = source.IndexOf("\n        }", start, StringComparison.Ordinal);
            string body = source[start..end];
            Assert.Contains($"RequireResourceId(", body, StringComparison.Ordinal);
            Assert.Contains($", {bound},", body, StringComparison.Ordinal);
            Assert.Contains("1UL <<", body, StringComparison.Ordinal);
            Assert.True(body.IndexOf("RequireResourceId(", StringComparison.Ordinal) < body.IndexOf("1UL <<", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void EvidenceRecordsEveryFamilyAndCurrentDisposition()
    {
        string evidence = Read("Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF12",
            "rf12.12e-residual-rm1-family-local-reconciliation.md");
        foreach (string builder in new[]
        {
            "ForMemoryBank", "ForMemoryDomain", "ForDMAChannel", "ForStreamEngine", "ForAccelerator",
            "ForExtendedGRLBChannel", "ForExtendedMemoryDomain"
        })
            Assert.Contains(builder, evidence, StringComparison.Ordinal);

        Assert.Contains("No residual builder aliases an invalid selector to bit zero", evidence, StringComparison.Ordinal);
        Assert.Contains("RF-12.12f", evidence, StringComparison.Ordinal);
    }

    private static string Read(params string[] parts)
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")))
                return File.ReadAllText(Path.Combine(new[] { current.FullName }.Concat(parts).ToArray()));
            current = current.Parent;
        }

        throw new DirectoryNotFoundException();
    }
}
