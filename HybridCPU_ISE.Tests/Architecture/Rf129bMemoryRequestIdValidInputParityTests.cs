namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf129bMemoryRequestIdValidInputParityTests
{
    [Fact]
    public void ControllerRetainsOneTypedCarrierAcrossAdmissionStorageAndPublication()
    {
        string controller = Read("HybridCPU_ISE", "CloseToHSL", "Memory", "Timing", "MemoryCycleController.cs");

        Assert.Contains("private readonly Queue<MemoryRequestId> _readQueue", controller, StringComparison.Ordinal);
        Assert.Contains("private readonly Dictionary<MemoryRequestId, ControllerRequest> _outstanding", controller, StringComparison.Ordinal);
        Assert.Contains("MemoryRequestId requestId = AllocateRequestId()", controller, StringComparison.Ordinal);
        Assert.Contains("return MemoryAdmissionResult.Accepted(requestId)", controller, StringComparison.Ordinal);
        Assert.Contains("new MemoryCompletion(requestId", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void PipelineConsumersRetainTypedCompletionAndCancellationSignatures()
    {
        string loadStore = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "MicroOps", "Memory", "MicroOp.LoadStore.cs");
        string vector = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "MicroOps", "Vector", "VectorMicroOps.Memory.cs");

        Assert.Contains("TryTakeCompletion(", loadStore, StringComparison.Ordinal);
        Assert.Contains("_controllerRequestId.Value", loadStore, StringComparison.Ordinal);
        Assert.Contains("TryCancel(_controllerRequestId.Value)", vector, StringComparison.Ordinal);
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
