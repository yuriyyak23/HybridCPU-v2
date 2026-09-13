namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-08.3h makes the remaining scalar-load source gap executable: the
/// authorization does not permit a dynamic or synthetic memory contract.
/// </summary>
public sealed class Rf083hScalarLoadContractSourceBlockerAuditTests
{
    [Fact]
    public void CanonicalLoadDoesNotCarryTheImmutableFootprintAndBankRequiredByMemoryCapability()
    {
        string root = FindRepositoryRoot();
        string canonical = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Decoder", "CanonicalDecodedContracts.cs");
        string contracts = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Decoder", "Rf06ExecutionContracts.cs");

        string canonicalRecord = Slice(canonical, "public sealed record CanonicalDecodedInstruction(", "public sealed class CanonicalBundle");
        Assert.Contains("long Immediate", canonicalRecord, StringComparison.Ordinal);
        Assert.DoesNotContain("FrozenMemoryRange", canonicalRecord, StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryBankId", canonicalRecord, StringComparison.Ordinal);
        Assert.Contains("A memory capability must declare its frozen footprint.", contracts, StringComparison.Ordinal);
        Assert.Contains("A memory capability must declare its typed bank identity.", contracts, StringComparison.Ordinal);
    }

    [Fact]
    public void ExistingLoadBankAndFootprintDerivationReadsLiveCarrierAndRuntimeGeometry()
    {
        string root = FindRepositoryRoot();
        string loadStore = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "MicroOps", "Memory", "MicroOp.LoadStore.cs");
        string specializedProjection = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Decoder", "Rf06SpecializedCapabilityProjection.cs");

        Assert.Contains("ResolveSchedulerVisibleBankId(MemoryAddress)", loadStore, StringComparison.Ordinal);
        Assert.Contains("ResolveSchedulerVisibleBankId(footprint[0].Address)", specializedProjection, StringComparison.Ordinal);
        Assert.Contains("MemoryCapability.Create(", specializedProjection, StringComparison.Ordinal);
    }

    [Fact]
    public void AuthorizationForbidsUsingThatDynamicStateAsASyntheticLoadIngress()
    {
        string root = FindRepositoryRoot();
        string paper = Read(root, "ResearchPaper", "section", "md base", "5_Two_Stage_Admission_and_Bundle_Compositional_SMT_Packing.md");
        string fsp = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Scheduling", "Fsp", "CPU_Core.PipelineExecution.Fsp.cs");
        string specializedProjection = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Decoder", "Rf06SpecializedCapabilityProjection.cs");

        Assert.Contains("resolved dynamic-address state into the admission contract", paper, StringComparison.Ordinal);
        Assert.Contains("candidate is not Core.ScalarALUMicroOp", fsp, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectLoad", specializedProjection, StringComparison.Ordinal);
    }

    private static string Slice(string text, string start, string end)
    {
        int startIndex = text.IndexOf(start, StringComparison.Ordinal);
        int endIndex = text.IndexOf(end, startIndex, StringComparison.Ordinal);
        Assert.True(startIndex >= 0 && endIndex > startIndex, "Canonical record boundary was not found.");
        return text[startIndex..endIndex];
    }

    private static string Read(string root, params string[] segments) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(segments).ToArray()));

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? current = new(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE.Tests")))
            {
                return current.FullName;
            }
        }

        throw new DirectoryNotFoundException("Repository root was not found from the test output directory.");
    }
}
