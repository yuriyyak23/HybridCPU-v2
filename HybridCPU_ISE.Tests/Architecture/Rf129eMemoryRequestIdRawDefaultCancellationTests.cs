namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf129eMemoryRequestIdRawDefaultCancellationTests
{
    [Fact]
    public void VectorTransferCancelsOnlyAStoredAcceptedRequest()
    {
        string vector = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "MicroOps", "Vector", "VectorMicroOps.Data.cs"));

        Assert.DoesNotContain("_controllerRequestId ?? default", vector, StringComparison.Ordinal);
        Assert.Contains("_requestController is not null && _controllerRequestId.HasValue", vector, StringComparison.Ordinal);
        Assert.Contains("_requestController.TryCancel(_controllerRequestId.Value)", vector, StringComparison.Ordinal);
    }

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
