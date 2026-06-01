using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Registers;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class VmxCapsProjectionBoundaryTests
{
    [Fact]
    public void VmxCapsProjection_ReturnsProjectedCapabilityDescriptorSet_NotRawCsr()
    {
        var csr = new CsrFile();
        csr.HardwareWrite(CsrAddresses.VmxCaps, CapabilityDescriptorSetSchema.KnownVmxV2CompatibilityMask);

        var projectedDescriptorSet = new CapabilityDescriptorSet(
            globalHardwareCaps: VmxV2InstructionCaps.VmFunc | VmxV2InstructionCaps.VmCall,
            runtimeEnabledCaps: VmxV2InstructionCaps.VmFunc,
            domainGrantedCaps: VmxV2InstructionCaps.VmFunc | VmxV2InstructionCaps.RootDescriptorOperand);

        ulong projected = new VmxCapsProjection().Read(projectedDescriptorSet);

        Assert.Equal(VmxV2InstructionCaps.VmFunc, projected);
        Assert.NotEqual(csr.DirectRead(CsrAddresses.VmxCaps), projected);
    }

    [Fact]
    public void VmxCapsProjection_WritePolicy_IsRejectOrCompatibilityNoEffectOnly()
    {
        var strictProjection = new VmxCapsProjection();
        Assert.Equal(VmxCapsWriteResult.Rejected, strictProjection.EvaluateWrite(ulong.MaxValue));

        var unfencedCompatibilityProjection = new VmxCapsProjection(
            new CapabilityPublicationPolicy(
                allowCompatibilityAliasPublication: true,
                writeDisposition: CapabilityWriteDisposition.CompatibilityNoEffect));

        Assert.Equal(VmxCapsWriteResult.Rejected, unfencedCompatibilityProjection.EvaluateWrite(ulong.MaxValue));

        var fencedCompatibilityProjection = new VmxCapsProjection(
            new CapabilityPublicationPolicy(
                allowCompatibilityAliasPublication: true,
                writeDisposition: CapabilityWriteDisposition.CompatibilityNoEffect),
            CapabilityDescriptorSetSchema.VmxCompatibility,
            compatibilityWriteFenceEnabled: true);

        Assert.Equal(
            VmxCapsWriteResult.CompatibilityNoEffect,
            fencedCompatibilityProjection.EvaluateWrite(ulong.MaxValue));
    }

    [Fact]
    public void RemovedVmxExecutionUnit_CannotReadVmxCapsCsrDirectly()
    {
        string sourcePath = Path.Combine(
            FindRepositoryRoot(),
            "HybridCPU_ISE",
            LegacyVmxExecutionUnitRemovalContract.LegacyOriginPath.Replace('/', Path.DirectorySeparatorChar));

        Assert.True(LegacyVmxExecutionUnitRemovalContract.RemovedWithoutReplacement);
        Assert.False(File.Exists(sourcePath));
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "HybridCPU_ISE.sln")) ||
                Directory.Exists(Path.Combine(directory.FullName, "HybridCPU_ISE")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate HybridCPU ISE repository root.");
    }
}
