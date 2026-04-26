using System;
using System.Collections.Generic;
using System.IO;
using HybridCPU.Compiler.Core.API.Facade;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Memory;

namespace HybridCPU_ISE.Tests.Phase12;

/// <summary>
/// REF-12: freeze the legacy ISA/VLIW compatibility plane so new production code
/// cannot silently depend on compat-only containers instead of canonical IR.
/// </summary>
[Trait("Category", CompatFreezeGateCatalog.Category)]
public sealed class Phase12VliwCompatFreezeTests
{
    [Fact]
    public void VliwInstruction_NoNewProductionMentionsOutsideCompatAllowList()
        => AssertFrozenAllowlist(CompatFreezeGateCatalog.VliwInstructionMentions);

    [Fact]
    public void InstructionsEnum_NoNewProductionMentionsOutsideCompatAllowList()
        => AssertFrozenAllowlist(CompatFreezeGateCatalog.InstructionsEnumMentions);

    [Fact]
    public void ToInstructionsEnum_RuntimeConversionsRemainConfinedToCanonicalOpcodeBoundary()
    {
        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string[] violations = CompatFreezeScanner.ScanProductionFilesForPatterns(
            repoRoot,
            new[] { "ToInstructionsEnum(" },
            new[]
            {
                Path.Combine("HybridCPU_ISE", "Core", "Common", "CPU_Core.Enums.cs"),
            },
            new[]
            {
                "HybridCPU_ISE",
            });

        Assert.Empty(violations);
    }

    [Fact]
    public void LiveStreamCompatRuntimeMentionsStayInsideIngressBoundary()
        => AssertFrozenAllowlist(CompatFreezeGateCatalog.LiveStreamCompatMentions);

    [Fact]
    public void AddVliwInstruction_ProductionCallersRemainFrozen()
        => AssertFrozenAllowlist(CompatFreezeGateCatalog.AddVliwInstructionCallers);

    [Fact]
    public void ReferenceRawFallback_RuntimeExecuteHelpersRetainOnlyFailClosedBoundary()
    {
        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string helperPath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Pipeline",
            "Core",
            "CPU_Core.PipelineExecution.ExecuteHelpers.cs");
        string source = File.ReadAllText(helperPath);

        Assert.DoesNotContain("ExecuteSingleLaneReferenceRawFallback(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IsReferenceRawFallbackAllowed(", source, StringComparison.Ordinal);
        Assert.Contains("RejectSingleLaneReferenceRawFallbackEntry(", source, StringComparison.Ordinal);
        Assert.Null(typeof(Processor.CPU_Core).GetMethod(
            "ExecuteSingleLaneReferenceRawFallback",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic));
    }

    [Fact]
    public void ReferenceRawFallback_PipelineControlDoesNotExposeLegacyBudgetCounter()
    {
        Assert.Null(typeof(Processor.CPU_Core.PipelineControl).GetField(
            "ReferenceRawFallbackCount",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic));
    }

    [Fact]
    public void MemoryUnitExecute_PublicRuntimeSurfaceIsRemoved()
    {
        Assert.Null(typeof(MemoryUnit).GetMethod(
            "Execute",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public));
    }

    [Fact]
    public void MicroOp_LegacySlotMetadataSurfaceIsRemovedFromRuntimeAuthorityBoundary()
    {
        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string microOpPath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Pipeline",
            "MicroOps",
            "MicroOp.cs");
        string source = File.ReadAllText(microOpPath);

        Assert.DoesNotContain("public SlotMetadata? SlotMeta", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IsMetadataStealable(", source, StringComparison.Ordinal);
        Assert.Null(typeof(MicroOp).GetProperty(
            "SlotMeta",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public));
        Assert.Null(typeof(MicroOp).GetMethod(
            "IsMetadataStealable",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public));
    }

    [Fact]
    public void ExecutionDispatcherMemorySurface_DoesNotCallMemoryUnitExecuteDirectly()
    {
        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string helperPath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Execution",
            "ExecutionDispatcherV4.MemoryAndControl.cs");
        string source = File.ReadAllText(helperPath);

        Assert.DoesNotContain("_memoryUnit.Execute(", source, StringComparison.Ordinal);
        Assert.Contains("_memoryUnit.ResolveArchitecturalAccess(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void CpuCoreSystem_DoesNotReferenceDirectBridgeAppendSurface()
    {
        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string helperPath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "System",
            "CPU_Core.System.cs");
        string source = File.ReadAllText(helperPath);

        Assert.DoesNotContain("Compiler.Add_VLIW_Instruction(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessorCompilerBridge", source, StringComparison.Ordinal);
    }

    [Fact]
    public void LegacyPowerState_RuntimeAndFacadeSurfaceAreRemoved()
    {
        Assert.Null(typeof(Processor.CPU_Core).GetMethod(
            "PowerState",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic));
        Assert.Null(typeof(IPlatformAsmFacade).GetMethod("PowerState"));
        Assert.Null(typeof(PlatformAsmFacade).GetMethod("PowerState"));
    }

    [Fact]
    public void LegacyPowerStatePatterns_DoNotReappearInProductionSources()
    {
        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string[] violations = CompatFreezeScanner.ScanProductionFilesForPatterns(
            repoRoot,
            new[]
            {
                ".PowerState(",
                " PowerState(",
                "GetCurrentPowerState(",
                "GetCurrentPerformanceLevel("
            },
            Array.Empty<string>());

        Assert.Empty(violations);
    }

    [Fact]
    public void CoreDetailsGui_ReadsPowerStateOnlyFromObservationSnapshot()
    {
        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string formPath = Path.Combine(
            repoRoot,
            "forms",
            "Form_Main.CoreDetails",
            "Form_Main.CoreDetails.cs");
        string source = File.ReadAllText(formPath);

        Assert.Contains("ObservationService.GetCoreState", source, StringComparison.Ordinal);
        Assert.Contains("coreState.CurrentPowerState", source, StringComparison.Ordinal);
        Assert.Contains("coreState.CurrentPerformanceLevel", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GetCurrentPowerState(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GetCurrentPerformanceLevel(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void CompatIngressAdapter_ProductionCallersRemainFrozen()
    {
        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string[] unexpectedCallSites = CompatFreezeScanner.FindUnexpectedCallSites(
            repoRoot,
            "VliwCompatIngress.",
            Array.Empty<string>());

        Assert.Empty(unexpectedCallSites);
    }

    [Fact]
    public void CompatIngressAdapter_RuntimeAssemblyAndSourceFileAreRemoved()
    {
        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string runtimeAdapterPath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Arch",
            "Compat",
            "VliwCompatIngress.cs");

        Assert.False(File.Exists(runtimeAdapterPath));
        Assert.Null(typeof(VLIW_Instruction).Assembly.GetType(
            "HybridCPU_ISE.Arch.Compat.VliwCompatIngress",
            throwOnError: false));
    }

    [Fact]
    public void RetainedCompatDecoderFactory_ProductionCallersRemainFrozen()
    {
        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string[] unexpectedCallSites = CompatFreezeScanner.FindUnexpectedCallSites(
            repoRoot,
            "CreateRetainedCompatDecoder(",
            Array.Empty<string>());

        Assert.Empty(unexpectedCallSites);
    }

    [Fact]
    public void RetainedCompatDecoderFactory_RuntimeFactoryExposesCanonicalDecoderOnly()
    {
        Assert.NotNull(typeof(DecoderFeatureFlags).GetMethod(
            "CreateDecoder",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static));
        Assert.Null(typeof(DecoderFeatureFlags).GetMethod(
            "CreateRetainedCompatDecoder",
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Static));
    }

    [Fact]
    public void RetainedCompatDecoder_RuntimeTypeIsAbsent()
    {
        Type? compatDecoderType = typeof(VLIW_Instruction).Assembly.GetType(
            "YAKSys_Hybrid_CPU.Core.Decoder.VliwCompatDecoderV4",
            throwOnError: false);

        Assert.Null(compatDecoderType);
    }

    [Fact]
    public void DescriptorPublicationContour_RuntimeAndBridgeSurfacesAreRemoved()
    {
        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string runtimeDescriptorPath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Decoder",
            "DecodedBundleDescriptor.cs");
        string testSupportPath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Pipeline",
            "Core",
            "CPU_Core.TestSupport.cs");

        string runtimeDescriptorSource = File.ReadAllText(runtimeDescriptorPath);
        string testSupportSource = File.ReadAllText(testSupportPath);

        Assert.DoesNotContain(
            "BuildTransportFactsFromDescriptorPublicationContour",
            runtimeDescriptorSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "DecodedBundleStateOrigin.DescriptorPublication",
            runtimeDescriptorSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "TestRepublishDecodedBundleTransportFactsFromDescriptors",
            testSupportSource,
            StringComparison.Ordinal);
        Assert.Null(typeof(DecodedBundleStateOrigin).GetField("DescriptorPublication"));
        Assert.Null(typeof(CpuInterfaceBridge.DecodedBundleStateOrigin).GetField("DescriptorPublication"));
    }

    [Fact]
    public void LegacyCompatRepairMethodNames_DoNotReappearInProductionSources()
    {
        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        Assert.Empty(CompatFreezeScanner.FindUnexpectedCallSites(
            repoRoot,
            "TryReadBytesCompat(",
            Array.Empty<string>()));
        Assert.Empty(CompatFreezeScanner.FindUnexpectedCallSites(
            repoRoot,
            "SetInstructionCompat(",
            Array.Empty<string>()));
        Assert.Empty(CompatFreezeScanner.FindUnexpectedCallSites(
            repoRoot,
            "SanitizeRetiredPolicyGapBitForCompatIngress(",
            Array.Empty<string>()));
        Assert.Empty(CompatFreezeScanner.FindUnexpectedCallSites(
            repoRoot,
            "SanitizeWord3ForCompatIngress(",
            Array.Empty<string>()));
    }

    [Fact]
    public void VliwInstruction_DoesNotReintroduceRetiredClassificationMembers()
    {
        Assert.Null(typeof(VLIW_Instruction).GetProperty("IsControlFlow"));
        Assert.Null(typeof(VLIW_Instruction).GetProperty("IsMathOrVector"));
        Assert.Null(typeof(VLIW_Instruction).GetProperty("IsScalar"));
    }

    [Fact]
    public void ScalarHeuristicMentions_DoNotReappearInDecoderOrCompilerSources()
    {
        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        Assert.Empty(CompatFreezeScanner.FindUnexpectedCallSites(
            repoRoot,
            "instruction.IsScalar",
            Array.Empty<string>()));
    }

    [Fact]
    public void HybridCpuEnvironment_OuterClassIsRemoved()
    {
        Assert.Null(typeof(VLIW_Instruction).Assembly.GetType(
            "HybridCPU_ISE.Arch.Hybrid_CPU_Environment",
            throwOnError: false));
    }

    [Fact]
    public void HybridCpuEnvironment_NoProductionMentionsRemain()
    {
        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string[] unexpectedCallSites = CompatFreezeScanner.FindUnexpectedCallSites(
            repoRoot,
            "Hybrid_CPU_Environment",
            Array.Empty<string>());

        Assert.Empty(unexpectedCallSites);
    }

    [Fact]
    public void CompatFreezeAllowances_DeclareOwnerMilestoneAndReachableFiles()
    {
        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        var violations = new List<string>();

        foreach (CompatFreezeGateCatalog.SymbolAllowance allowance in CompatFreezeGateCatalog.Allowances)
        {
            if (string.IsNullOrWhiteSpace(allowance.Owner))
            {
                violations.Add($"{allowance.Name}: owner is missing.");
            }

            if (string.IsNullOrWhiteSpace(allowance.RemovalMilestone))
            {
                violations.Add($"{allowance.Name}: removal milestone is missing.");
            }

            foreach (string relativePath in allowance.AllowedRelativePaths)
            {
                string absolutePath = Path.Combine(repoRoot, relativePath);
                if (!File.Exists(absolutePath))
                {
                    violations.Add($"{allowance.Name}: tracked file is missing: {relativePath}");
                }
            }
        }

        if (violations.Count != 0)
        {
            Console.WriteLine("Compat freeze allowance catalog violations:");
            foreach (string violation in violations)
            {
                Console.WriteLine(violation);
            }
        }

        Assert.True(
            violations.Count == 0,
            "Compat freeze allowance catalog contains stale ownership entries.");
    }

    [Fact]
    public void CompatFreezePolicyDocument_TracksAllAllowedSymbolFamilies()
    {
        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string policyPath = Path.Combine(repoRoot, CompatFreezeGateCatalog.PolicyDocumentRelativePath);

        if (!File.Exists(policyPath))
        {
            return;
        }

        string policyText = File.ReadAllText(policyPath);
        foreach (CompatFreezeGateCatalog.SymbolAllowance allowance in CompatFreezeGateCatalog.Allowances)
        {
            Assert.Contains(allowance.Name, policyText, StringComparison.Ordinal);
            Assert.Contains(allowance.Owner, policyText, StringComparison.Ordinal);
            Assert.Contains(allowance.RemovalMilestone, policyText, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void CompatFreezeGate_HasDedicatedRunnerScript()
    {
        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, CompatFreezeGateCatalog.RunnerScriptRelativePath);

        Assert.True(File.Exists(scriptPath), $"Compat freeze runner script is missing: {scriptPath}");

        string scriptText = File.ReadAllText(scriptPath);
        Assert.Contains("Phase11ObservationBoundaryClosureTests", scriptText, StringComparison.Ordinal);
        Assert.Contains("Phase12VliwCompatFreezeTests", scriptText, StringComparison.Ordinal);
    }

    private static void AssertFrozenAllowlist(CompatFreezeGateCatalog.SymbolAllowance allowance)
    {
        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string[] unexpectedCallSites = CompatFreezeScanner.FindUnexpectedCallSites(repoRoot, allowance);
        Assert.Empty(unexpectedCallSites);
    }
}
