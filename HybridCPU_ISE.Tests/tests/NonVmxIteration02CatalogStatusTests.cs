using System;
using System.Linq;
using Xunit;
using YAKSys_Hybrid_CPU.Arch;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.InstructionsRefactor;

public sealed class NonVmxIteration02CatalogStatusTests
{
    private static readonly string[] BitfieldMnemonics =
    [
        "BSET",
        "BCLR",
        "BINV",
        "BEXT",
        "BSETI",
        "BCLRI",
        "BINVI",
        "BEXTI"
    ];

    private static readonly string[] IommuMaintenanceMnemonics =
    [
        "IOTLB_INV",
        "IOMMU_FENCE"
    ];

    private static readonly string[] AddressGenerationMnemonics =
    [
        "SH1ADD",
        "SH2ADD",
        "SH3ADD",
        "ADD.UW",
        "SH1ADD.UW",
        "SH2ADD.UW",
        "SH3ADD.UW",
        "SLLI.UW"
    ];

    [Fact]
    public void BitfieldRows_AreExplicitRuntimeClosed()
    {
        foreach (string mnemonic in BitfieldMnemonics)
        {
            Assert.True(
                InstructionSupportStatusCatalog.TryGetExplicitStatus(
                    mnemonic,
                    out InstructionSupportStatus status),
                $"{mnemonic} must be an explicit status row, not an implicit fallback.");

            Assert.Equal(IsaInstructionStatus.OptionalEnabled, status.Status);
            Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
            Assert.Equal("ScalarBitfield", status.ExtensionName);
            Assert.True(status.IsExecutableClaim, mnemonic);
            AssertHasOpcodeOrRuntimeEvidence(status);
            AssertHasEnumAndRegistryMnemonic(mnemonic);
        }
    }

    [Fact]
    public void AddressGenerationRows_AreExplicitRuntimeClosed()
    {
        foreach (string mnemonic in AddressGenerationMnemonics)
        {
            Assert.True(
                InstructionSupportStatusCatalog.TryGetExplicitStatus(
                    mnemonic,
                    out InstructionSupportStatus status),
                $"{mnemonic} must be an explicit status row, not an implicit fallback.");

            Assert.Equal(IsaInstructionStatus.OptionalEnabled, status.Status);
            Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
            Assert.Equal("ScalarAddressGeneration", status.ExtensionName);
            Assert.True(status.IsExecutableClaim, mnemonic);
            AssertHasOpcodeOrRuntimeEvidence(status);
            AssertHasEnumAndRegistryMnemonic(mnemonic);
        }
    }

    [Fact]
    public void Iteration02A_IommuMaintenanceRows_AreExplicitReservedWithoutOpcodeEvidence()
    {
        foreach (string mnemonic in IommuMaintenanceMnemonics)
        {
            Assert.True(
                InstructionSupportStatusCatalog.TryGetExplicitStatus(
                    mnemonic,
                    out InstructionSupportStatus status),
                $"{mnemonic} must be an explicit status row, not an implicit fallback.");

            Assert.Equal(IsaInstructionStatus.Reserved, status.Status);
            Assert.Equal(RuntimeInstructionEvidence.None, status.RuntimeEvidence);
            Assert.Equal("CacheTlbCoherency", status.ExtensionName);
            Assert.Contains(mnemonic, IsaV4Surface.ReservedOpcodes);
            Assert.False(status.IsExecutableClaim, mnemonic);
            AssertNoOpcodeOrRuntimeEvidence(status);
            AssertNoEnumOrRegistryMnemonic(mnemonic);
        }
    }

    private static void AssertNoOpcodeOrRuntimeEvidence(InstructionSupportStatus status)
    {
        Assert.False(status.HasNumericOpcode, status.Mnemonic);
        Assert.False(status.HasRuntimeOpcodeMetadata, status.Mnemonic);
        Assert.False(status.HasCanonicalDecoderAcceptance, status.Mnemonic);
        Assert.False(status.HasRegistryFactory, status.Mnemonic);
        Assert.False(status.HasExecutionSemantics, status.Mnemonic);
    }

    private static void AssertHasOpcodeOrRuntimeEvidence(InstructionSupportStatus status)
    {
        Assert.True(status.HasNumericOpcode, status.Mnemonic);
        Assert.True(status.HasRuntimeOpcodeMetadata, status.Mnemonic);
        Assert.True(status.HasCanonicalDecoderAcceptance, status.Mnemonic);
        Assert.True(status.HasRegistryFactory, status.Mnemonic);
        Assert.True(status.HasExecutionSemantics, status.Mnemonic);
    }

    private static void AssertNoEnumOrRegistryMnemonic(string mnemonic)
    {
        Assert.False(
            Enum.TryParse<InstructionsEnum>(mnemonic, ignoreCase: false, out _),
            $"{mnemonic} must not allocate an InstructionsEnum value in Iteration 02A.");
        Assert.DoesNotContain(
            OpcodeRegistry.Opcodes,
            info => string.Equals(info.Mnemonic, mnemonic, StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertHasEnumAndRegistryMnemonic(string mnemonic)
    {
        string enumCandidate = mnemonic.Replace(".", "_", StringComparison.Ordinal);
        Assert.True(
            Enum.TryParse<InstructionsEnum>(enumCandidate, ignoreCase: false, out _),
            $"{mnemonic} must allocate an InstructionsEnum value in the closed runtime surface.");
        Assert.Contains(
            OpcodeRegistry.Opcodes,
            info => string.Equals(info.Mnemonic, mnemonic, StringComparison.OrdinalIgnoreCase));
    }
}
