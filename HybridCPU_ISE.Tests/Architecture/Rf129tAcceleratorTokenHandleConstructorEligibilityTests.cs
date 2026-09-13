namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf129tAcceleratorTokenHandleConstructorEligibilityTests
{
    [Fact]
    public void PublicConstructorStillHasControlledDefaultAndReflectionWitnesses()
    {
        string handle = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Execution", "ExternalAccelerators", "Tokens", "AcceleratorTokenHandle.cs");
        string guard = Read("HybridCPU_ISE.Tests", "Architecture", "Rf120ResourceIdIngressGuardTests.cs");
        Assert.Contains("record struct AcceleratorTokenHandle(ulong Value)", handle, StringComparison.Ordinal);
        Assert.Contains("new AcceleratorTokenHandle(0)", guard, StringComparison.Ordinal);
        Assert.Contains("typeof(AcceleratorTokenHandle).GetConstructors", guard, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionAllocationAlreadyUsesCheckedOpaqueIngress()
    {
        string store = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Execution", "ExternalAccelerators", "Tokens", "AcceleratorTokenStore.cs");
        string microOp = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "MicroOps", "Lane7Accelerator", "SystemDeviceCommandMicroOp.cs");
        Assert.Contains("AcceleratorTokenHandle.FromOpaqueValue(value)", store, StringComparison.Ordinal);
        Assert.Contains("AcceleratorTokenHandle.FromOpaqueValue(rawHandle)", microOp, StringComparison.Ordinal);
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
