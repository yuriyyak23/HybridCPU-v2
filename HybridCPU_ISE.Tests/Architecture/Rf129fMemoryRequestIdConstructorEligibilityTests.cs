namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf129fMemoryRequestIdConstructorEligibilityTests
{
    [Fact]
    public void PublicConstructorHasProductionAndReflectionContractConsumers()
    {
        string controller = Read("HybridCPU_ISE", "CloseToHSL", "Memory", "Timing", "MemoryCycleController.cs");
        string guard = Read("HybridCPU_ISE.Tests", "Architecture", "Rf120ResourceIdIngressGuardTests.cs");

        Assert.Contains("MemoryRequestId requestId = new(value)", controller, StringComparison.Ordinal);
        Assert.Contains("new MemoryRequestId(0)", guard, StringComparison.Ordinal);
        Assert.Contains("typeof(MemoryRequestId).GetConstructors", guard, StringComparison.Ordinal);
    }

    [Fact]
    public void CompatibilitySignaturesStillExposeTheTypedCarrier()
    {
        string controller = Read("HybridCPU_ISE", "CloseToHSL", "Memory", "Timing", "MemoryCycleController.cs");
        Assert.Contains("TryTakeCompletion(MemoryRequestId requestId", controller, StringComparison.Ordinal);
        Assert.Contains("TryCancel(MemoryRequestId requestId)", controller, StringComparison.Ordinal);
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
