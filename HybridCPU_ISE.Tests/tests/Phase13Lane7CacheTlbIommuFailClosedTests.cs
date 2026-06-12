using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HybridCPU.Compiler.Core.IR;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;
using CloseToRtlDcacheClean = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane07SystemControl.CacheMaintenance.DcacheCleanInstruction;
using CloseToRtlDcacheFlush = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane07SystemControl.CacheMaintenance.DcacheFlushInstruction;
using CloseToRtlDcacheInval = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane07SystemControl.CacheMaintenance.DcacheInvalInstruction;
using CloseToRtlIcacheInval = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane07SystemControl.CacheMaintenance.IcacheInvalInstruction;
using CloseToRtlIommuFence = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane07SystemControl.Iommu.IommuFenceInstruction;
using CloseToRtlIotlbInv = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane07SystemControl.Iommu.IotlbInvInstruction;
using CloseToRtlSfenceVma = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane07SystemControl.TranslationFences.SfenceVmaInstruction;

namespace HybridCPU_ISE.Tests.Phase13;

public sealed class Phase13Lane7CacheTlbIommuFailClosedTests
{
    private static IReadOnlyList<string> MaintenanceMnemonics =>
        CompilerFailClosedEmissionInventory.Lane7CacheTlbIommuMnemonics;

    private static IReadOnlyList<string> CompilerForbiddenTokens =>
        CompilerFailClosedEmissionInventory.Lane7CacheTlbIommuCompilerSourceFragments;

    private static readonly string[] VmxForbiddenTokens =
    [
        "SFENCE.VMA",
        "ICACHE_INVAL",
        "DCACHE_CLEAN",
        "DCACHE_INVAL",
        "DCACHE_FLUSH",
        "IOTLB_INV",
        "IOMMU_FENCE",
        "SfenceVmaInstruction",
        "IcacheInvalInstruction",
        "DcacheCleanInstruction",
        "DcacheInvalInstruction",
        "DcacheFlushInstruction",
        "IotlbInvInstruction",
        "IommuFenceInstruction"
    ];

    [Fact]
    public void Phase13Rows_RemainReservedWithoutProductionPublication()
    {
        foreach (string mnemonic in MaintenanceMnemonics)
        {
            AssertReservedNoAllocationRow(mnemonic);
        }
    }

    [Fact]
    public void Phase13Docs_RecordNegativeGateNotExecutableClosure()
    {
        string phase13 = ReadProjectFile(
            "HybridCPU_ISE/CloseToRTL/Core/ISA/Instructions/NonVmx/Docs/ImplPlan/PHASE_13_LANE7_CACHE_TLB_IOMMU.md");
        string tracking = ReadProjectFile(
            "HybridCPU_ISE/CloseToRTL/Core/ISA/Instructions/NonVmx/Docs/NON_VMX_CLOSE_TO_RTL_IMPLEMENTATION_PLAN.md");

        Assert.Contains("Phase 13 is closed only as a negative production decision gate", phase13);
        Assert.Contains("does not open executable closure", phase13);
        Assert.Contains("Phase 13 negative decision gate closed", tracking);
        Assert.Contains("does not allocate opcodes", tracking);
    }

    [Fact]
    public void TranslationFenceLeafMarkers_RecordPhase13NegativeDecisionGate()
    {
        Type templateType = typeof(CloseToRtlSfenceVma);

        Assert.Equal("SFENCE.VMA", GetConstant<string>(templateType, "Mnemonic"));
        Assert.Equal("Lane7TranslationFenceDeferred", GetConstant<string>(templateType, "EvidenceBoundary"));
        Assert.Equal("Phase13NegativeDecisionGate", GetConstant<string>(templateType, "ProductionDecision"));
        Assert.Equal("Lane7TranslationFenceProductionPathOnly", GetConstant<string>(templateType, "MaintenanceAuthorityBoundary"));
        AssertCommonMaintenanceFailClosedMarkers(templateType);
        Assert.True(GetConstant<bool>(templateType, "RequiresTranslationFenceAbi"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresAddressSpaceSelectorAbi"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresTranslationStateOwnershipModel"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresPageTableWalkOwnershipModel"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresCrossCoreShootdownPolicy"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "VmxTranslationEvidenceIsInsufficient"), templateType.FullName);
    }

    [Theory]
    [InlineData(typeof(CloseToRtlIcacheInval), "ICACHE_INVAL", "RequiresInstructionFetchCoherencyModel")]
    [InlineData(typeof(CloseToRtlDcacheClean), "DCACHE_CLEAN", "RequiresDataCacheCoherencyModel")]
    [InlineData(typeof(CloseToRtlDcacheInval), "DCACHE_INVAL", "RequiresDataCacheCoherencyModel")]
    [InlineData(typeof(CloseToRtlDcacheFlush), "DCACHE_FLUSH", "RequiresDataCacheCoherencyModel")]
    public void CacheMaintenanceLeafMarkers_RecordPhase13NegativeDecisionGate(
        Type templateType,
        string mnemonic,
        string requiredCacheMarker)
    {
        Assert.Equal(mnemonic, GetConstant<string>(templateType, "Mnemonic"));
        Assert.Equal("Lane7CacheMaintenanceDeferred", GetConstant<string>(templateType, "EvidenceBoundary"));
        Assert.Equal("Phase13NegativeDecisionGate", GetConstant<string>(templateType, "ProductionDecision"));
        Assert.Equal("Lane7CacheMaintenanceProductionPathOnly", GetConstant<string>(templateType, "MaintenanceAuthorityBoundary"));
        AssertCommonMaintenanceFailClosedMarkers(templateType);
        Assert.True(GetConstant<bool>(templateType, "RequiresCacheMaintenanceAbi"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresCacheHierarchyAuthorityModel"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresAddressRangeScopeAbi"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoFenceIFallback"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "VmxCacheEvidenceIsInsufficient"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, requiredCacheMarker), templateType.FullName);
    }

    [Theory]
    [InlineData(typeof(CloseToRtlIotlbInv), "IOTLB_INV", "RequiresIotlbInvalidationModel")]
    [InlineData(typeof(CloseToRtlIommuFence), "IOMMU_FENCE", "RequiresIommuFenceCompletionModel")]
    public void IommuMaintenanceLeafMarkers_RecordPhase13NegativeDecisionGate(
        Type templateType,
        string mnemonic,
        string requiredIommuMarker)
    {
        Assert.Equal(mnemonic, GetConstant<string>(templateType, "Mnemonic"));
        Assert.Equal("Lane7IommuMaintenanceDeferred", GetConstant<string>(templateType, "EvidenceBoundary"));
        Assert.Equal("Phase13NegativeDecisionGate", GetConstant<string>(templateType, "ProductionDecision"));
        Assert.Equal("Lane7IommuMaintenanceProductionPathOnly", GetConstant<string>(templateType, "MaintenanceAuthorityBoundary"));
        AssertCommonMaintenanceFailClosedMarkers(templateType);
        Assert.True(GetConstant<bool>(templateType, "RequiresIommuMaintenanceAbi"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresDeviceDomainAuthority"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresDmaVisibilityModel"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresLane6TokenAuthorityGate"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresExternalDeviceQuiescencePolicy"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "ExistingLane6DmaEvidenceIsInsufficient"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "VmxIommuEvidenceIsInsufficient"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, requiredIommuMarker), templateType.FullName);
    }

    [Fact]
    public void FenceAtomicLane6AndLane7Evidence_DoesNotAuthorizeMaintenanceRows()
    {
        foreach (string existingMnemonic in new[]
                 { "FENCE", "FENCE_I", "AMOADD_W", "LR_W", "SC_W", "DmaStreamCompute", "DSC_STATUS", "ACCEL_SUBMIT" })
        {
            if (!InstructionSupportStatusCatalog.TryGetExplicitStatus(
                    existingMnemonic,
                    out InstructionSupportStatus status))
            {
                continue;
            }

            Assert.True(status.IsExecutableClaim || status.Status is not IsaInstructionStatus.OptionalEnabled);
        }

        foreach (string mnemonic in MaintenanceMnemonics)
        {
            AssertReservedNoAllocationRow(mnemonic);
        }
    }

    [Fact]
    public void VectorLegalityMatrix_DoesNotTreatPhase13RowsAsExecutableVectorContours()
    {
        foreach (string mnemonic in MaintenanceMnemonics)
        {
            Assert.DoesNotContain(
                VectorLegalityMatrix.Rows,
                row =>
                    row.FamilyName.Contains(mnemonic, StringComparison.Ordinal) ||
                    row.RuntimeEvidenceNote.Contains(mnemonic, StringComparison.Ordinal));
        }

        Assert.DoesNotContain(VectorLegalityMatrix.Rows, row =>
            row.FamilyName is
                "Lane7TranslationFenceDeferred" or
                "Lane7CacheMaintenanceDeferred" or
                "Lane7IommuMaintenanceDeferred" or
                "Lane7CacheTlbIommuMaintenance");
    }

    [Fact]
    public void CompilerFacade_DoesNotExposePhase13HelpersOrHiddenLowering()
    {
        List<string> failures = [];
        foreach (string path in CompilerSourceScanner.EnumerateCompilerSourceFiles())
        {
            string source = File.ReadAllText(path);
            foreach (string token in CompilerForbiddenTokens)
            {
                if (source.Contains(token, StringComparison.Ordinal))
                {
                    failures.Add($"{Relative(path)} contains forbidden compiler token `{token}`.");
                }
            }
        }

        Assert.True(
            failures.Count == 0,
            "Phase 13 compiler helpers and hidden lowering must remain closed:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void CompilerDeferredAbi_RecordsSfenceVmaPolicyWithoutEmissionAuthority()
    {
        CompilerLane7DeferredAbiContract contract =
            CompilerLane7DeferredAbiContract.TranslationFence;

        CompilerLane7DeferredAbiAssertions.AssertNoEmissionAuthority(
            contract,
            "SFENCE.VMA",
            CompilerLane7DeferredAbiClass.TranslationFence,
            "CacheTlbCoherency",
            "Lane7TranslationFenceDeferred");
        Assert.True(contract.RequiresPrivilegeAndAdmissionPolicy);
        Assert.True(contract.RequiresAddressSpaceSelectorAbi);
        Assert.True(contract.RequiresTlbShootdownPolicy);
        Assert.True(contract.RequiresCrossCoreShootdownPolicy);
        Assert.True(contract.RequiresRetireOwnedSideEffectPublication);
        Assert.True(contract.NoGenericFenceFallback);
        Assert.True(contract.NoVmxEptVpidNptSemanticAlias);
        CompilerLane7DeferredAbiAssertions.AssertHiddenLoweringBlocked(contract);

        CompilerLane7DeferredAbiAssertions.AssertEmissionAuthorityThrows(contract);

        string compilerSource = CompilerSourceScanner.ReadAllCompilerSource();
        CompilerLane7DeferredAbiAssertions.AssertCompilerSourceCarriesMetadataButNoEmission(
            compilerSource,
            ("SFENCE.VMA", "SFENCE_VMA", "SfenceVma"));
    }

    [Fact]
    public void CompilerDeferredAbi_RecordsCacheMaintenancePolicyWithoutEmissionAuthority()
    {
        CompilerLane7DeferredAbiContract[] rows =
        [
            .. CompilerLane7DeferredAbiContract.CacheMaintenanceRows
        ];

        Assert.Equal(
            ["DCACHE_CLEAN", "DCACHE_FLUSH", "DCACHE_INVAL", "ICACHE_INVAL"],
            rows.Select(static row => row.Mnemonic).Order(StringComparer.Ordinal).ToArray());

        foreach (CompilerLane7DeferredAbiContract contract in rows)
        {
            CompilerLane7DeferredAbiAssertions.AssertNoEmissionAuthority(
                contract,
                contract.Mnemonic,
                CompilerLane7DeferredAbiClass.CacheMaintenance,
                "CacheTlbCoherency",
                "Lane7CacheMaintenanceDeferred");
            Assert.True(contract.RequiresPrivilegeAndAdmissionPolicy);
            Assert.True(contract.RequiresCacheMaintenanceAbi);
            Assert.True(contract.RequiresCacheHierarchyAuthorityModel);
            Assert.True(contract.RequiresAddressRangeScopeAbi);
            Assert.True(contract.RequiresReplayStableInvalidationModel);
            Assert.True(contract.RequiresRetireOwnedSideEffectPublication);
            Assert.True(contract.NoGenericFenceFallback);
            Assert.True(contract.NoFenceIFallback);
            Assert.True(contract.VmxCacheEvidenceIsInsufficient);
            CompilerLane7DeferredAbiAssertions.AssertHiddenLoweringBlocked(contract);
            CompilerLane7DeferredAbiAssertions.AssertPolicyDecisions(
                contract,
                "CacheMaintenanceAbi",
                "CacheHierarchyAuthorityModel",
                "AddressRangeScopeAbi",
                "PrivilegeAndAdmissionPolicy",
                "ReplayStableInvalidationModel",
                "RetireOwnedSideEffectPublication",
                "NoFenceIFallback");

            if (contract.Mnemonic == "ICACHE_INVAL")
            {
                Assert.True(contract.RequiresInstructionFetchCoherencyModel);
                Assert.False(contract.RequiresDataCacheCoherencyModel);
                Assert.False(contract.RequiresDirtyLineOwnershipModel);
                Assert.False(contract.RequiresMemoryOrderingIntegration);
                CompilerLane7DeferredAbiAssertions.AssertPolicyDecisions(contract, "InstructionFetchCoherencyModel");
            }
            else
            {
                Assert.False(contract.RequiresInstructionFetchCoherencyModel);
                Assert.True(contract.RequiresDataCacheCoherencyModel);
                Assert.True(contract.RequiresDirtyLineOwnershipModel);
                Assert.True(contract.RequiresMemoryOrderingIntegration);
                CompilerLane7DeferredAbiAssertions.AssertPolicyDecisions(
                    contract,
                    "DataCacheCoherencyModel",
                    "DirtyLineOwnershipModel",
                    "MemoryOrderingIntegration");
            }

            CompilerLane7DeferredAbiAssertions.AssertEmissionAuthorityThrows(contract);
        }

        string compilerSource = CompilerSourceScanner.ReadAllCompilerSource();
        CompilerLane7DeferredAbiAssertions.AssertCompilerSourceCarriesMetadataButNoEmission(
            compilerSource,
            ("ICACHE_INVAL", "IcacheInval"),
            ("DCACHE_CLEAN", "DcacheClean"),
            ("DCACHE_INVAL", "DcacheInval"),
            ("DCACHE_FLUSH", "DcacheFlush"));
    }

    [Fact]
    public void CompilerDeferredAbi_RecordsIommuMaintenancePolicyWithoutEmissionAuthority()
    {
        CompilerLane7DeferredAbiContract[] rows =
        [
            .. CompilerLane7DeferredAbiContract.IommuMaintenanceRows
        ];

        Assert.Equal(
            ["IOMMU_FENCE", "IOTLB_INV"],
            rows.Select(static row => row.Mnemonic).Order(StringComparer.Ordinal).ToArray());

        foreach (CompilerLane7DeferredAbiContract contract in rows)
        {
            CompilerLane7DeferredAbiAssertions.AssertNoEmissionAuthority(
                contract,
                contract.Mnemonic,
                CompilerLane7DeferredAbiClass.IommuMaintenance,
                "CacheTlbCoherency",
                "Lane7IommuMaintenanceDeferred");
            Assert.True(contract.RequiresPrivilegeAndAdmissionPolicy);
            Assert.True(contract.RequiresIommuMaintenanceAbi);
            Assert.True(contract.RequiresDeviceDomainAuthority);
            Assert.True(contract.RequiresDmaVisibilityModel);
            Assert.True(contract.RequiresLane6TokenAuthorityGate);
            Assert.True(contract.RequiresExternalDeviceQuiescencePolicy);
            Assert.True(contract.RequiresReplayStableInvalidationModel);
            Assert.True(contract.RequiresRetireOwnedSideEffectPublication);
            Assert.True(contract.NoGenericFenceFallback);
            Assert.True(contract.NoVmxEptVpidNptSemanticAlias);
            Assert.True(contract.VmxIommuEvidenceIsInsufficient);
            Assert.True(contract.ExistingLane6DmaEvidenceIsInsufficient);
            Assert.True(contract.ExistingLane7ControlPlaneEvidenceIsInsufficient);
            Assert.True(contract.NoLane6DmaFallback);
            Assert.True(contract.NoLane7AcceleratorFallback);
            Assert.True(contract.NoExternalBackendFallback);
            CompilerLane7DeferredAbiAssertions.AssertHiddenLoweringBlocked(contract);
            CompilerLane7DeferredAbiAssertions.AssertPolicyDecisions(
                contract,
                "IommuMaintenanceAbi",
                "DeviceDomainAuthority",
                "DmaVisibilityModel",
                "Lane6TokenAuthorityGate",
                "ExternalDeviceQuiescencePolicy",
                "PrivilegeAndAdmissionPolicy",
                "ReplayStableInvalidationModel",
                "RetireOwnedSideEffectPublication",
                "NoVmxEptVpidNptSemanticAlias",
                "NoLane6DmaFallback",
                "NoLane7AcceleratorFallback",
                "NoExternalBackendFallback");

            if (contract.Mnemonic == "IOTLB_INV")
            {
                Assert.True(contract.RequiresIotlbInvalidationModel);
                Assert.False(contract.RequiresIommuFenceCompletionModel);
                CompilerLane7DeferredAbiAssertions.AssertPolicyDecisions(contract, "IotlbInvalidationModel");
            }
            else
            {
                Assert.False(contract.RequiresIotlbInvalidationModel);
                Assert.True(contract.RequiresIommuFenceCompletionModel);
                CompilerLane7DeferredAbiAssertions.AssertPolicyDecisions(contract, "IommuFenceCompletionModel");
            }

            CompilerLane7DeferredAbiAssertions.AssertEmissionAuthorityThrows(contract);
        }

        string compilerSource = CompilerSourceScanner.ReadAllCompilerSource();
        CompilerLane7DeferredAbiAssertions.AssertCompilerSourceCarriesMetadataButNoEmission(
            compilerSource,
            ("IOTLB_INV", "IotlbInv"),
            ("IOMMU_FENCE", "IommuFence"));
    }

    [Fact]
    public void VmxEptVpidNptOrHostEvidence_IsNotUsedAsHiddenPhase13Integration()
    {
        List<string> failures = [];
        foreach (string path in EnumerateVmxProductionSources())
        {
            string source = File.ReadAllText(path);
            foreach (string token in VmxForbiddenTokens)
            {
                if (source.Contains(token, StringComparison.Ordinal))
                {
                    failures.Add($"{Relative(path)} contains forbidden VMX integration token `{token}`.");
                }
            }
        }

        Assert.True(
            failures.Count == 0,
            "Phase 13 Non-VMX cache/TLB/IOMMU rows must not be integrated through VMX EPT/VPID/NPT or host evidence:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, failures));
    }

    private static void AssertReservedNoAllocationRow(string mnemonic)
    {
        Assert.True(InstructionSupportStatusCatalog.TryGetExplicitStatus(
            mnemonic,
            out InstructionSupportStatus status));
        Assert.Equal(IsaInstructionStatus.Reserved, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.None, status.RuntimeEvidence);
        Assert.Equal("CacheTlbCoherency", status.ExtensionName);
        Assert.False(status.HasNumericOpcode);
        Assert.False(status.HasRuntimeOpcodeMetadata);
        Assert.False(status.HasCanonicalDecoderAcceptance);
        Assert.False(status.HasRegistryFactory);
        Assert.False(status.HasExecutionSemantics);
        Assert.False(status.IsExecutableClaim);
        Assert.Contains(mnemonic, IsaV4Surface.ReservedOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.OptionalEnabledOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.OptionalDisabledOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.ParserOnlyOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.DescriptorOnlyOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.PipelineClassMap.Keys);
        Assert.False(HasEnum(mnemonic));
        Assert.False(HasIsaOpcodeValue(mnemonic));
        Assert.False(HasRegistryMnemonic(mnemonic));
    }

    private static void AssertCommonMaintenanceFailClosedMarkers(Type templateType)
    {
        Assert.Equal("GenericRuntimeOnly", GetConstant<string>(templateType, "VmxBoundary"));
        Assert.True(GetConstant<bool>(templateType, "RequiresRetireOwnedPublication"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresRetireOwnedSideEffectPublication"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresPrivilegeAndAdmissionPolicy"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresDecoderEncoderAbiPublication"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresInstructionIrProjectionPublication"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresLane7MaterializerPublication"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresTypedMaintenanceMicroOpPublication"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresReplayStableInvalidationModel"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresRollbackConformance"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresInvalidationOrderingModel"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresConformanceAndGoldenArtifacts"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoSpeculativeMaintenancePublication"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoRetireSideEffectBeforeAuthority"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoGenericFenceFallback"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoGuestVisibleHostEvidence"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoHostEvidenceLeak"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoHostOwnedEvidencePublication"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoHiddenScalarLowering"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoMultiOpEmission"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoLane6DmaFallback"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoLane7AcceleratorFallback"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoExternalBackendFallback"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoVmxSpecificPath"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "ExistingFenceEvidenceIsInsufficient"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "ExistingFenceIEvidenceIsInsufficient"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "ExistingAtomicOrderingEvidenceIsInsufficient"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "ExistingLane7ControlPlaneEvidenceIsInsufficient"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresFutureVirtualizationBoundaryPolicy"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "RequiresImmediateVmxProjection"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "IsExecutable"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "CompilerHelperAllowed"), templateType.FullName);
        AssertNoStaticOpcodeOrExecuteSurface(templateType);
    }

    private static void AssertNoStaticOpcodeOrExecuteSurface(Type templateType)
    {
        Assert.Null(templateType.GetProperty("Opcode", BindingFlags.Public | BindingFlags.Static));
        Assert.Null(templateType.GetField("Opcode", BindingFlags.Public | BindingFlags.Static));
        Assert.Null(templateType.GetMethod("Execute", BindingFlags.Public | BindingFlags.Static));
    }

    private static bool HasEnum(string mnemonic)
    {
        string enumCandidate = mnemonic.Replace(".", "_", StringComparison.Ordinal);
        return Enum.GetNames<InstructionsEnum>().Contains(enumCandidate);
    }

    private static bool HasIsaOpcodeValue(string mnemonic)
    {
        string enumCandidate = mnemonic.Replace(".", "_", StringComparison.Ordinal);
        return typeof(Processor.CPU_Core.IsaOpcodeValues).GetField(
            enumCandidate,
            BindingFlags.Public | BindingFlags.Static) is not null;
    }

    private static bool HasRegistryMnemonic(string mnemonic) =>
        OpcodeRegistry.Opcodes.Any(info =>
            string.Equals(info.Mnemonic, mnemonic, StringComparison.OrdinalIgnoreCase));

    private static T GetConstant<T>(Type type, string name)
    {
        FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException($"{type.FullName}.{name} was not found.");
        Assert.True(field.IsLiteral, $"{type.FullName}.{name} must remain a const template marker.");
        return Assert.IsType<T>(field.GetRawConstantValue());
    }

    private static IEnumerable<string> EnumerateVmxProductionSources()
    {
        string root = Path.Combine(FindRepositoryRoot(), "HybridCPU_ISE");
        if (!Directory.Exists(root))
        {
            return Enumerable.Empty<string>();
        }

        return Directory
            .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "NonVmx" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .Where(path =>
                path.Contains("Vmx", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("Virtualization", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal);
    }

    private static string ReadProjectFile(string relativePath) =>
        File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string Relative(string path) =>
        Path.GetRelativePath(FindRepositoryRoot(), path).Replace('\\', '/');

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
}
