using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.CompatibilityConformance;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase15;

public sealed class Phase15CompatibilityConformanceSweepTests
{
    private static readonly DeferredRow[] DeferredRows =
    [
        new("SEQZ", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("SNEZ", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("CSEL", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("POPCNT", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("CRC32", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("CRC64", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("ADC", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("SBC", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("ADDC", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("SUBC", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("RDTIME", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("RDINSTRET", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("PAUSE", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("VMERGE", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("VSELECT", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("VFIRST", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("VANY", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("VALL", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("VMSIF", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("VMSOF", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("VWADD", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("VWADDU", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("VWSUB", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("VWSUBU", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("VWMUL", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("VWMULU", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("VWMACC", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("VNSRL", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("VNSRA", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("VSEXT", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("VCVT.I", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("VCVT.U", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("VCVT.F", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("VLDSEG2", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("VLDSEG4", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("VLDSEG8", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("VSTSEG2", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("VSTSEG4", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("VSTSEG8", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("VZIP", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("VUNZIP", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("VINTERLEAVE", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("VDEINTERLEAVE", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("VSUB.SAT", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("VMUL.SAT", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("VSLL.SAT", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("VSRL.SAT", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("VSRA.SAT", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("VAVG", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("VAVG.R", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("VCLIP", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("VSCAN.MIN", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("VSCAN.MAX", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("VDOT.BLOCKSCALE", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("VDOT.ACCUM", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("VDOT.WIDE.I16", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("VDOT.WIDE.I32", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("MTILE_LOAD", IsaInstructionStatus.OptionalDisabled, RuntimeInstructionEvidence.DeclaredOnly, false),
        new("MTILE_STORE", IsaInstructionStatus.OptionalDisabled, RuntimeInstructionEvidence.DeclaredOnly, false),
        new("MTILE_MACC", IsaInstructionStatus.OptionalDisabled, RuntimeInstructionEvidence.DeclaredOnly, false),
        new("MTRANSPOSE", IsaInstructionStatus.OptionalDisabled, RuntimeInstructionEvidence.DeclaredOnly, false),
        new("DmaStreamCompute.SUB", IsaInstructionStatus.DescriptorOnly, RuntimeInstructionEvidence.DeclaredOnly, true),
        new("DmaStreamCompute.MIN", IsaInstructionStatus.DescriptorOnly, RuntimeInstructionEvidence.DeclaredOnly, true),
        new("DmaStreamCompute.MAX", IsaInstructionStatus.DescriptorOnly, RuntimeInstructionEvidence.DeclaredOnly, true),
        new("DmaStreamCompute.ABSDIFF", IsaInstructionStatus.DescriptorOnly, RuntimeInstructionEvidence.DeclaredOnly, true),
        new("DmaStreamCompute.CLAMP", IsaInstructionStatus.DescriptorOnly, RuntimeInstructionEvidence.DeclaredOnly, true),
        new("DmaStreamCompute.CONVERT", IsaInstructionStatus.DescriptorOnly, RuntimeInstructionEvidence.DeclaredOnly, true),
        new("DmaStreamCompute.COMPARE", IsaInstructionStatus.DescriptorOnly, RuntimeInstructionEvidence.DeclaredOnly, true),
        new("DmaStreamCompute.SELECT", IsaInstructionStatus.DescriptorOnly, RuntimeInstructionEvidence.DeclaredOnly, true),
        new("DmaStreamCompute.REDUCE_SUM", IsaInstructionStatus.DescriptorOnly, RuntimeInstructionEvidence.DeclaredOnly, true),
        new("DmaStreamCompute.REDUCE_MIN", IsaInstructionStatus.DescriptorOnly, RuntimeInstructionEvidence.DeclaredOnly, true),
        new("DmaStreamCompute.REDUCE_MAX", IsaInstructionStatus.DescriptorOnly, RuntimeInstructionEvidence.DeclaredOnly, true),
        new("DmaStreamCompute.REDUCE_AND", IsaInstructionStatus.DescriptorOnly, RuntimeInstructionEvidence.DeclaredOnly, true),
        new("DmaStreamCompute.REDUCE_OR", IsaInstructionStatus.DescriptorOnly, RuntimeInstructionEvidence.DeclaredOnly, true),
        new("DmaStreamCompute.REDUCE_XOR", IsaInstructionStatus.DescriptorOnly, RuntimeInstructionEvidence.DeclaredOnly, true),
        new("DSC_SHAPE_STRIDED", IsaInstructionStatus.DescriptorOnly, RuntimeInstructionEvidence.DeclaredOnly, true),
        new("DSC_SHAPE_TILED", IsaInstructionStatus.DescriptorOnly, RuntimeInstructionEvidence.DeclaredOnly, true),
        new("DSC_SHAPE_SCATTER_GATHER", IsaInstructionStatus.DescriptorOnly, RuntimeInstructionEvidence.DeclaredOnly, true),
        new("DSC_SHAPE_2D", IsaInstructionStatus.DescriptorOnly, RuntimeInstructionEvidence.DeclaredOnly, true),
        new("DSC_SHAPE_MULTI_RANGE", IsaInstructionStatus.DescriptorOnly, RuntimeInstructionEvidence.DeclaredOnly, true),
        new("DSC_POLL", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("DSC_WAIT", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("DSC_CANCEL", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("DSC_FENCE", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("DSC_COMMIT", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("DSC_QUERY_BACKEND", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("DSC_QUERY_SHAPE", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("DSC2", IsaInstructionStatus.ParserOnly, RuntimeInstructionEvidence.DeclaredOnly, true),
        new("SFENCE.VMA", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("ICACHE_INVAL", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("DCACHE_CLEAN", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("DCACHE_INVAL", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("DCACHE_FLUSH", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("IOTLB_INV", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("IOMMU_FENCE", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("ACCEL_QUERY_ABI", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("ACCEL_QUERY_TOPOLOGY", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("ACCEL_OPEN", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("ACCEL_CLOSE", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("ACCEL_BIND_QUEUE", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true),
        new("ACCEL_UNBIND_QUEUE", IsaInstructionStatus.Reserved, RuntimeInstructionEvidence.None, true)
    ];

    private static readonly IReadOnlyDictionary<string, InstructionsEnum> AuthorityOpcodeOverrides =
        new Dictionary<string, InstructionsEnum>(StringComparer.Ordinal)
        {
            ["VADD.SAT"] = InstructionsEnum.VADD
        };

    [Fact]
    public void SweepContract_RecordsAuditOnlyClosureWithoutProductionOpening()
    {
        Assert.Equal("Phase15", Phase15CompatibilityConformanceSweepContract.Phase);
        Assert.Equal("CompatibilityConformanceAuditOnly", Phase15CompatibilityConformanceSweepContract.ClosureDecision);
        Assert.Equal("GenericRuntimeOnly", Phase15CompatibilityConformanceSweepContract.VmxBoundary);
        Assert.False(Phase15CompatibilityConformanceSweepContract.AddsInstructionSemantics);
        Assert.False(Phase15CompatibilityConformanceSweepContract.AllocatesOpcode);
        Assert.False(Phase15CompatibilityConformanceSweepContract.OpensDecoderEncoderAbi);
        Assert.False(Phase15CompatibilityConformanceSweepContract.OpensInstructionIrProjection);
        Assert.False(Phase15CompatibilityConformanceSweepContract.OpensRegistryMaterializer);
        Assert.False(Phase15CompatibilityConformanceSweepContract.PublishesTypedMicroOp);
        Assert.False(Phase15CompatibilityConformanceSweepContract.OpensExecutionPath);
        Assert.False(Phase15CompatibilityConformanceSweepContract.OpensRetireSideEffects);
        Assert.False(Phase15CompatibilityConformanceSweepContract.OpensCompilerHelper);
        Assert.False(Phase15CompatibilityConformanceSweepContract.OpensVmxSpecificPath);

        Assert.Contains("CAT", Phase15CompatibilityConformanceSweepContract.ExecutableEvidenceChain);
        Assert.Contains("RET", Phase15CompatibilityConformanceSweepContract.ExecutableEvidenceChain);
        Assert.Contains("RPL", Phase15CompatibilityConformanceSweepContract.ExecutableEvidenceChain);
        Assert.Contains("GLD", Phase15CompatibilityConformanceSweepContract.ExecutableEvidenceChain);
        Assert.Contains("NOE", Phase15CompatibilityConformanceSweepContract.ExecutableEvidenceChain);
        Assert.Contains("NoCompilerHelper", Phase15CompatibilityConformanceSweepContract.DeferredEvidenceGuards);
        Assert.Contains("NoVmxSpecificPath", Phase15CompatibilityConformanceSweepContract.DeferredEvidenceGuards);
        Assert.Contains("HostEvidenceNonLeak", Phase15CompatibilityConformanceSweepContract.SweepBoundaries);
    }

    [Fact]
    public void Phase15Docs_RecordSweepOnlyClosure()
    {
        string phase15 = ReadProjectFile(
            "HybridCPU_ISE/CloseToRTL/Core/ISA/Instructions/NonVmx/Docs/ImplPlan/PHASE_15_COMPATIBILITY_CONFORMANCE_SWEEP.md");
        string readme = ReadProjectFile(
            "HybridCPU_ISE/CloseToRTL/Core/ISA/Instructions/NonVmx/Docs/ImplPlan/README.md");
        string tracking = ReadProjectFile(
            "HybridCPU_ISE/CloseToRTL/Core/ISA/Instructions/NonVmx/Docs/NON_VMX_CLOSE_TO_RTL_IMPLEMENTATION_PLAN.md");

        Assert.Contains("Phase 15 is closed as a compatibility and conformance sweep", phase15);
        Assert.Contains("not as a new executable production slice", phase15);
        Assert.Contains("does not allocate opcodes", phase15);
        Assert.Contains("Compatibility/conformance sweep closed as audit package", readme);
        Assert.Contains("Phase 15 compatibility/conformance sweep closed as an audit package", tracking);
        Assert.Contains("does not allocate", tracking);
        Assert.Contains("VMX-specific paths", tracking);
    }

    [Fact]
    public void ExecutableCatalogRows_HaveCompleteEvidenceAndPublishedRuntimeAuthority()
    {
        InstructionSupportStatus[] executableRows = InstructionSupportStatusCatalog
            .ExplicitStatuses
            .Where(static status => status.IsExecutableClaim)
            .OrderBy(static status => status.Mnemonic, StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(executableRows);

        foreach (InstructionSupportStatus status in executableRows)
        {
            Assert.True(
                status.Status is IsaInstructionStatus.Mandatory or IsaInstructionStatus.OptionalEnabled,
                $"{status.Mnemonic} executable claim must be mandatory or optional-enabled.");
            Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
            Assert.True(status.HasNumericOpcode, status.Mnemonic);
            Assert.True(status.HasRuntimeOpcodeMetadata, status.Mnemonic);
            Assert.True(status.HasCanonicalDecoderAcceptance, status.Mnemonic);
            Assert.True(status.HasRegistryFactory, status.Mnemonic);
            Assert.True(status.HasExecutionSemantics, status.Mnemonic);
            Assert.DoesNotContain(status.Mnemonic, IsaV4Surface.ReservedOpcodes);
            Assert.DoesNotContain(status.Mnemonic, IsaV4Surface.OptionalDisabledOpcodes);
            Assert.DoesNotContain(status.Mnemonic, IsaV4Surface.ParserOnlyOpcodes);
            Assert.DoesNotContain(status.Mnemonic, IsaV4Surface.DescriptorOnlyOpcodes);
            Assert.DoesNotContain(status.Mnemonic, IsaV4Surface.CarrierOnlyOpcodes);
            AssertPublishedOpcodeAuthority(status.Mnemonic);
        }
    }

    [Fact]
    public void NonExecutableCatalogRows_DoNotClaimExecutionDuringSweep()
    {
        InstructionSupportStatus[] nonExecutableRows = InstructionSupportStatusCatalog
            .ExplicitStatuses
            .Where(static status => !status.IsExecutableClaim)
            .OrderBy(static status => status.Mnemonic, StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(nonExecutableRows);

        foreach (InstructionSupportStatus status in nonExecutableRows)
        {
            Assert.False(status.IsExecutableClaim, status.Mnemonic);
            Assert.False(
                status.HasExecutionSemantics,
                $"{status.Mnemonic} must not publish execution semantics without executable evidence.");

            if (status.Status is IsaInstructionStatus.Reserved
                or IsaInstructionStatus.ParserOnly
                or IsaInstructionStatus.DescriptorOnly
                or IsaInstructionStatus.Prohibited)
            {
                Assert.False(status.HasRuntimeOpcodeMetadata, status.Mnemonic);
                Assert.False(status.HasCanonicalDecoderAcceptance, status.Mnemonic);
            }
        }
    }

    [Fact]
    public void DeferredRowsFromPhases05Through14_RemainFailClosed()
    {
        foreach (DeferredRow row in DeferredRows)
        {
            InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(row.Mnemonic);

            Assert.Equal(row.ExpectedStatus, status.Status);
            Assert.Equal(row.ExpectedEvidence, status.RuntimeEvidence);
            Assert.False(status.IsExecutableClaim, row.Mnemonic);
            Assert.False(status.HasExecutionSemantics, row.Mnemonic);
            Assert.DoesNotContain(row.Mnemonic, IsaV4Surface.OptionalEnabledOpcodes);
            Assert.DoesNotContain(row.Mnemonic, IsaV4Surface.PipelineClassMap.Keys);

            if (row.RequiresNoAllocation)
            {
                Assert.False(status.HasNumericOpcode, row.Mnemonic);
                Assert.False(status.HasRuntimeOpcodeMetadata, row.Mnemonic);
                Assert.False(status.HasCanonicalDecoderAcceptance, row.Mnemonic);
                Assert.False(status.HasRegistryFactory, row.Mnemonic);
                Assert.False(TryResolveInstructionEnum(row.Mnemonic, out _), row.Mnemonic);
                Assert.False(HasIsaOpcodeValue(row.Mnemonic), row.Mnemonic);
                Assert.False(HasRegistryMnemonic(row.Mnemonic), row.Mnemonic);
            }
            else
            {
                Assert.False(status.HasRuntimeOpcodeMetadata, row.Mnemonic);
                Assert.False(status.HasCanonicalDecoderAcceptance, row.Mnemonic);
                Assert.False(status.HasRegistryFactory, row.Mnemonic);
                Assert.False(IsRegisteredRuntimeOpcode(row.Mnemonic), row.Mnemonic);
            }
        }
    }

    [Fact]
    public void VectorLegalityMatrix_DoesNotOpenDeferredVectorContours()
    {
        foreach (DeferredRow row in DeferredRows.Where(static row =>
                     row.Mnemonic.StartsWith("V", StringComparison.Ordinal) ||
                     row.Mnemonic.StartsWith("M", StringComparison.Ordinal)))
        {
            if (TryResolveInstructionEnum(row.Mnemonic, out InstructionsEnum opcode))
            {
                Assert.False(
                    VectorLegalityMatrix.TryGetRow(opcode, out _),
                    $"{row.Mnemonic} must not gain a VLM executable row during Phase 15.");
            }
        }

        foreach (string mnemonic in new[] { "VADD", "VLOAD", "VGATHER", "VPOPC", "VSETVL" })
        {
            Assert.True(TryResolveInstructionEnum(mnemonic, out InstructionsEnum opcode), mnemonic);
            Assert.True(VectorLegalityMatrix.TryGetRow(opcode, out _), mnemonic);
        }
    }

    [Fact]
    public void BaselineExecutableRows_RemainCompatibleAfterSweep()
    {
        foreach (string mnemonic in new[] { "CTZ", "SEXT.B", "SEXT.H", "ZEXT.H", "ROL", "ROR" })
        {
            InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(mnemonic);

            Assert.Equal(IsaInstructionStatus.OptionalEnabled, status.Status);
            Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
            Assert.True(status.IsExecutableClaim, mnemonic);
            AssertPublishedOpcodeAuthority(mnemonic);
        }
    }

    private static void AssertPublishedOpcodeAuthority(string mnemonic)
    {
        Assert.True(TryResolveInstructionEnum(mnemonic, out InstructionsEnum opcode), mnemonic);
        Assert.True(HasIsaOpcodeValue(mnemonic), mnemonic);
        Assert.NotNull(OpcodeRegistry.GetInfo((uint)opcode));
        Assert.True(InstructionRegistry.IsRegistered((uint)opcode), mnemonic);
    }

    private static bool IsRegisteredRuntimeOpcode(string mnemonic)
    {
        if (!TryResolveInstructionEnum(mnemonic, out InstructionsEnum opcode))
        {
            return false;
        }

        return OpcodeRegistry.GetInfo((uint)opcode) is not null ||
               InstructionRegistry.GetDescriptor((uint)opcode) is not null ||
               InstructionRegistry.IsRegistered((uint)opcode);
    }

    private static bool TryResolveInstructionEnum(string mnemonic, out InstructionsEnum opcode)
    {
        if (AuthorityOpcodeOverrides.TryGetValue(mnemonic, out opcode))
        {
            return true;
        }

        string enumCandidate = mnemonic.Replace(".", "_", StringComparison.Ordinal);
        return Enum.TryParse(enumCandidate, ignoreCase: false, out opcode);
    }

    private static bool HasIsaOpcodeValue(string mnemonic)
    {
        if (!TryResolveInstructionEnum(mnemonic, out InstructionsEnum opcode))
        {
            return false;
        }

        string enumCandidate = opcode.ToString();
        return typeof(Processor.CPU_Core.IsaOpcodeValues).GetField(
            enumCandidate,
            BindingFlags.Public | BindingFlags.Static) is not null;
    }

    private static bool HasRegistryMnemonic(string mnemonic) =>
        OpcodeRegistry.Opcodes.Any(info =>
            string.Equals(info.Mnemonic, mnemonic, StringComparison.OrdinalIgnoreCase));

    private static string ReadProjectFile(string relativePath) =>
        File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "HybridCPU v2.slnx")) ||
                Directory.Exists(Path.Combine(directory.FullName, "HybridCPU_ISE")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate HybridCPU ISE repository root.");
    }

    private readonly record struct DeferredRow(
        string Mnemonic,
        IsaInstructionStatus ExpectedStatus,
        RuntimeInstructionEvidence ExpectedEvidence,
        bool RequiresNoAllocation);
}
