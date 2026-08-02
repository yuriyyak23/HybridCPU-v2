namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7au Lane-6 status StreamEngineId valid-input cutover.</summary>
public sealed class Rf127auLane6StatusStreamEngineValidInputCutoverTests
{
    [Fact]
    public void StatusMaskUsesCheckedStreamZeroAndKeepsDmaDomainRoles()
    {
        string status = Read("DmaStreamComputeStatusMicroOp.cs");

        Assert.Contains("StreamEngineId streamEngine = StreamEngineId.Zero;", status,
            StringComparison.Ordinal);
        Assert.Contains("ResourceMaskBuilder.ForStreamEngine(streamEngine)", status,
            StringComparison.Ordinal);
        Assert.DoesNotContain("ForStreamEngine(0)", status, StringComparison.Ordinal);
        Assert.Contains("ResourceMaskBuilder.ForDMAChannel(0)", status, StringComparison.Ordinal);
        Assert.Contains("ResourceMaskBuilder.ForMemoryDomain(resourceDomainBucket)", status,
            StringComparison.Ordinal);
    }


    private static string Read(string file) => File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE", "CloseToHSL",
        "Core", "Pipeline", "MicroOps", "Lane6DmaStream", file));

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
