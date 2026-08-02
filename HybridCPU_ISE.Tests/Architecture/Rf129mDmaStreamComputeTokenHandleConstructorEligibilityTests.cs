namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf129mDmaStreamComputeTokenHandleConstructorEligibilityTests
{
    [Fact]
    public void ConstructorHasStoreAndDirectFixtureCallers()
    {
        string store = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Execution", "DmaStreamCompute", "DmaStreamComputeTokenStore.cs");
        string phaseTests = Read("HybridCPU_ISE.Tests", "tests", "DmaStreamComputeTokenStorePhase03Tests.cs");
        Assert.Contains("return new DmaStreamComputeTokenHandle(", store, StringComparison.Ordinal);
        Assert.Contains("new DmaStreamComputeTokenHandle(", phaseTests, StringComparison.Ordinal);
    }

    [Fact]
    public void ReflectionGuardRetainsThePublicConstructorContract()
    {
        string guard = Read("HybridCPU_ISE.Tests", "Architecture", "Rf120ResourceIdIngressGuardTests.cs");
        Assert.Contains("DmaStreamComputeTokenHandle defaultDscHandle = new", guard, StringComparison.Ordinal);
        Assert.Contains("typeof(DmaStreamComputeTokenHandle).GetConstructors", guard, StringComparison.Ordinal);
    }

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root() }.Concat(parts).ToArray()));
    private static string Root()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")) && Directory.Exists(Path.Combine(current.FullName, "ResearchPaper"))) return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException();
    }
}
