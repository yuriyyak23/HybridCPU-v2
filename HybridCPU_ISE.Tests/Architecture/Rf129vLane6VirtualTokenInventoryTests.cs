namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf129vLane6VirtualTokenInventoryTests
{
    [Fact]
    public void Lane6VirtualTokenIsCompositeGuestProjectionWithHostEvidenceBinding()
    {
        string state = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Runtime", "Lanes", "Lane6", "Lane6StateBlock.cs");
        string runtime = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Runtime", "Lanes", "Lane6", "Lane6QueueRuntime.cs");
        string evidence = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Runtime", "Lanes", "Lane6", "HostOwnedEvidence", "Lane6HostOwnedEvidenceStore.cs");
        Assert.Contains("record struct Lane6VirtualToken(", state, StringComparison.Ordinal);
        Assert.Contains("GuestTokenId", state, StringComparison.Ordinal);
        Assert.Contains("VirtualTokenId", state, StringComparison.Ordinal);
        Assert.Contains("virtualToken = new Lane6VirtualToken(", runtime, StringComparison.Ordinal);
        Assert.Contains("Dictionary<Lane6VirtualToken, DmaStreamComputeTokenHandle>", evidence, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidAndNativeExposureSeamsRemainExplicitForDecision()
    {
        string state = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Runtime", "Lanes", "Lane6", "Lane6StateBlock.cs");
        string evidence = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Runtime", "Lanes", "Lane6", "Lane6VirtualToken.Evidence.partial.cs");
        Assert.Contains("public bool IsValid", state, StringComparison.Ordinal);
        Assert.Contains("ExposesHostTokenHandle", evidence, StringComparison.Ordinal);
    }

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root() }.Concat(parts).ToArray()));
    private static string Root(){ DirectoryInfo? c=new(AppContext.BaseDirectory); while(c is not null){if(Directory.Exists(Path.Combine(c.FullName,"HybridCPU_ISE"))&&Directory.Exists(Path.Combine(c.FullName,"ResearchPaper"))) return c.FullName;c=c.Parent;} throw new DirectoryNotFoundException(); }
}
