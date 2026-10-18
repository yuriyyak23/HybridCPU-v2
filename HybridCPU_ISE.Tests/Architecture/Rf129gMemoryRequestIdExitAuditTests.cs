namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf129gMemoryRequestIdExitAuditTests
{
    [Fact]
    public void RequestFamilyRetainsControllerLocalLifecycleAndTypedConsumers()
    {
        string controller = Read("HybridCPU_ISE", "CloseToHSL", "Memory", "Timing", "MemoryCycleController.cs");
        string vector = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "MicroOps", "Vector", "VectorMicroOps.Data.cs");
        string paper = Read("ResearchPaper", "section", "md base", "3_Architectural_Overview_and_Frontend_Contract.md");

        Assert.Contains("Identity of one request accepted", controller, StringComparison.Ordinal);
        Assert.Contains("TryTakeCompletion(MemoryRequestId", controller, StringComparison.Ordinal);
        Assert.Contains("TryCancel(MemoryRequestId", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("_controllerRequestId ?? default", vector, StringComparison.Ordinal);
        Assert.Contains("absent/default carrier is not a cancellation request", paper, StringComparison.Ordinal);
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
