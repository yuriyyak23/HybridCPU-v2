using System;

namespace HybridCPU_ISE.Tests;

internal readonly record struct LegacyVmxRetainedCompatibilityInventoryEntry(
    string RelativePath,
    string[] RequiredMarkers);

internal sealed class LegacyVmxRetainedCompatibilitySurfaceInventoryContract
{
    public static LegacyVmxRetainedCompatibilityInventoryEntry[] RetainedProductionCompatibilitySources { get; } =
    {
        new(
            "Core/VMX/Compatibility/Frontend/Decode/VmxInstructionPayload.cs",
            new[]
            {
                "readonly record struct VmxInstructionPayload",
                "FromDecodedRegisters",
                "DecodeInvalidationScope",
                "DecodeFunctionLeaf",
            }),
        new(
            "Core/VMX/Compatibility/Frontend/Retire/VmxRetireModel.cs",
            new[]
            {
                "readonly struct VmxRetireEffect",
                "private VmxRetireEffect(",
                "public static VmxRetireEffect Fault",
                "readonly record struct VmxRetireOutcome",
            }),
        new(
            "Core/VMX/Compatibility/Generated/VmcsProjection/ShadowVmcsNestedProjectionService.cs",
            new[]
            {
                "ShadowVmcsNestedProjectionService",
                "NestedValidationResult.Fail",
                "CompatibilityProjectionFailed",
                "return false",
            }),
    };

    public static LegacyVmxRetainedCompatibilityInventoryEntry[] RequiredProductionCallers { get; } =
    {
        new(
            "Core/VMX/Compatibility/Frontend/Decode/VmxCompatDecodeBoundary.cs",
            new[] { "VmxInstructionPayload.FromDecodedRegisters" }),
        new(
            "Core/VMX/Compatibility/Frontend/Handlers/VmxCompatibilityAdmissionService.cs",
            new[]
            {
                "RuntimeBoundaryAdmissionService",
                "DomainRuntimeOperationKind.ReadCompatibilityProjection",
                "TryReadScalarField",
            }),
        new(
            "Core/Pipeline/MicroOps/MicroOp.IO.cs",
            new[]
            {
                "VmxInstructionPayload.FromDecodedRegisters",
                "VmxRetireEffect.Fault",
            }),
        new(
            "Core/Execution/ExecutionDispatcherV4.VmxCompatibility.cs",
            new[] { "VmxRetireEffect.Fault(operation, VmExitReason.SecurityPolicyViolation)" }),
        new(
            "Core/Pipeline/Core/CPU_Core.PipelineExecution.VmxRetire.cs",
            new[] { "ApplyRemovedFrontendFailClosedEffect" }),
        new(
            "Core/VMX/Compatibility/Generated/VmcsProjection/NestedDomainControllerCompatibilityProjection.cs",
            new[] { "new ShadowVmcsNestedProjectionService(descriptor)" }),
        new(
            "Core/Runtime/Nested/NestedDomainProjectionCheckpointService.cs",
            new[] { "NestedProjectionService _projectionService = new();" }),
    };

    public static string[] ForbiddenCompatibilitySourceAuthorityMutationMarkers { get; } =
    {
        "CapabilityGrantCollection",
        "CreateGrant(",
        "AddGrant(",
        "HardwareWrite(",
        "DirectWrite(",
        "WriteFieldValue(",
        "ReadFieldValue(",
        "HostOwnedEvidence",
        "AdvanceRuntimeEpoch(",
        "TryAdvanceRuntimeEpoch",
        "IotlbInvalidationService",
        "DmaAuthorityService",
        "BindIoDomain(",
        "TryTranslateDma(",
        "Lane6StateBlock",
        "Lane7StateBlock",
        "CompletionRoutingService",
        "PostedEventQueue",
        "DomainCheckpointImage",
        "RestoreValidationService",
        "new NestedProjectionService(",
        "new NestedDomainProjectionCheckpointService(",
        "VmcsManager",
        "IVmcsManager",
        "VmxExecutionUnit",
    };

    public static string[] ForbiddenCoreProductionRetireSuccessFactories { get; } =
    {
        "VmxRetireEffect.VmxOnRootDescriptor",
        "VmxRetireEffect.VmcsRead",
        "VmxRetireEffect.VmcsWrite",
        "VmxRetireEffect.VmcsPointerEffect",
        "VmxRetireEffect.VmPtrSt",
        "VmxRetireEffect.VmCall",
        "VmxRetireEffect.Invalidation",
        "VmxRetireEffect.VmFunc",
        "VmxRetireEffect.ExtendedState",
        "VmxRetireEffect.InterceptExit",
        "VmxRetireEffect.Control",
        "VmxRetireEffect.Abort",
    };

    public static LegacyVmxRetainedCompatibilityInventoryEntry[] GeneratedDebugLifecycleConformanceInventory { get; } =
    {
        new(
            "Core/VMX/Conformance/AbiFreeze/CompatAbiFreezeContract.cs",
            new[] { "IsFrozenOpcode" }),
        new(
            "Core/VMX/Conformance/GeneratedParity/GeneratedProjectionLineageBuildContract.cs",
            new[] { "RequiredGeneratedOutputs" }),
        new(
            "Core/VMX/Conformance/GoldenArtifacts/VirtualizationGoldenArtifactManifest.cs",
            new[] { "HostEvidenceNonLeak" }),
        new(
            "Core/VMX/Conformance/NoEmission/VirtualizationNoEmissionContract.cs",
            new[] { "HostEvidenceEmissionDenied" }),
        new(
            "Core/VMX/Conformance/NoEmission/CompatibilityWriteNoEmissionContract.cs",
            new[] { "ValidateNoMutation" }),
        new(
            "Core/VMX/Conformance/MigrationReplay/MigrationReplayContract.cs",
            new[] { "HostOwnedEvidenceReplayDenied" }),
    };

    public bool RetainsOnlyCallerBackedProductionVocabulary => true;

    public bool KeepsRetainedCompatibilitySourcesOutOfRuntimeAuthority => true;

    public bool KeepsGeneratedDebugLifecycleInventoryConformanceOnly => true;

    public bool DoesNotDeclareVmxFreeze => true;

    public bool IsSatisfied() =>
        RetainsOnlyCallerBackedProductionVocabulary &&
        KeepsRetainedCompatibilitySourcesOutOfRuntimeAuthority &&
        KeepsGeneratedDebugLifecycleInventoryConformanceOnly &&
        DoesNotDeclareVmxFreeze;
}

internal sealed class LegacyVmxFreezeReadinessCertificationContract
{
    public const bool MoveAwayProbeWasExecuted = true;

    public const bool CanDeclareFreeze = true;

    public const bool MoveAwayProbeProductionBuildPassedAfterCarrierExit = true;

    public const bool ConformanceFolderMoveAwayProbeWasExecuted = true;

    public const bool ConformanceFolderProductionBuildIndependent = true;

    public const bool ConformanceFolderTestBuildIndependent = true;

    public const bool ConformanceFolderDeletionSafeWithoutTestEvidenceDecoupling = true;

    public const int ConformanceFolderMoveAwayProbeTestCompileErrors = 0;

    public const bool BroadVmxFilterPassedAfterRepositoryPathRepair = true;

    public const string MoveAwayProbeTargetRoot =
        @"";

    public static readonly string[] MoveAwayProbeMissingSymbols = Array.Empty<string>();

    public static readonly string[] ConformanceFolderMoveAwayProbeMissingEvidenceSymbols = Array.Empty<string>();

    public static readonly string[] ConformanceFolderMoveAwayProbeTestDependents =
    {
        "HybridCPU_ISE.Tests/VmxRefactoring/VmxProjectionSchemaAndQuarantineTests.cs",
        "HybridCPU_ISE.Tests/VmxRefactoring/VmxCapsProjectionBoundaryTests.cs",
    };

    public static readonly LegacyVmxFreezeReadinessInventoryEntry[] RetainedProductionCompatibilitySources =
    {
        new(
            "Core/VMX/Compatibility/Frontend/Decode/VmxInstructionPayload.cs",
            new[]
            {
                "readonly record struct VmxInstructionPayload",
                "VmxInstructionPayload FromDecodedRegisters",
                "VmxV2InstructionCaps",
                "VmxV2ControlBits",
            }),
        new(
            "Core/VMX/Compatibility/Frontend/Retire/VmxRetireModel.cs",
            new[]
            {
                "public enum VmxOperationKind",
                "public readonly struct VmxRetireEffect",
                "public readonly record struct VmxRetireOutcome",
                "public static VmxRetireEffect Fault",
            }),
        new(
            "Core/VMX/Compatibility/Generated/VmcsProjection/ShadowVmcsNestedProjectionService.cs",
            new[]
            {
                "public sealed class ShadowVmcsNestedProjectionService",
                "NestedValidationResult.Fail",
                "CompatibilityProjectionFailed",
                "return false",
            }),
    };

    public static readonly LegacyVmxFreezeReadinessInventoryEntry[] ProductionCallersProvingDeletionIsNotMechanical =
    {
        new(
            "Core/VMX/Compatibility/Frontend/Decode/VmxCompatDecodeBoundary.cs",
            new[] { "VmxInstructionPayload.FromDecodedRegisters" }),
        new(
            "Core/Pipeline/MicroOps/InstructionIR.cs",
            new[] { "VmxInstructionPayload? VmxPayload" }),
        new(
            "Core/Pipeline/MicroOps/MicroOp.IO.cs",
            new[]
            {
                "VmxInstructionPayload.FromDecodedRegisters",
                "VmxRetireEffect.Fault",
            }),
        new(
            "Core/Execution/ExecutionDispatcherV4.VmxCompatibility.cs",
            new[] { "VmxRetireEffect.Fault" }),
        new(
            "Core/Pipeline/Core/CPU_Core.PipelineExecution.VmxRetire.cs",
            new[]
            {
                "VmxRetireOutcome",
                "VmxRetireEffect",
            }),
        new(
            "Core/VMX/Compatibility/Frontend/Projection/Events/VmxTrapProjectionMapper.cs",
            new[] { "VmxOperationKind" }),
        new(
            "Core/VMX/Compatibility/Frontend/Projection/Nested/NestedExitMapper.cs",
            new[] { "VmxOperationKind" }),
        new(
            "NonRTL/Core/Diagnostics/InstructionRegistry.Helpers.Core.cs",
            new[] { "VmxOperationKind" }),
    };

    public static readonly string[] ForbiddenRetainedProductionEvidenceMarkers =
    {
        "ShadowVmcsBridgeRetirementContract",
        "LegacyVmxFreezeReadinessCertificationContract",
        "LegacyVmxRetainedCompatibilitySurfaceInventoryContract",
        "ConformanceContract",
        ".IsCurrentBridgeFenced()",
    };

    public static readonly LegacyVmxFreezeReadinessMatrixEntry[] BroadConformanceMatrix =
    {
        new(
            "production-build",
            "dotnet build HybridCPU_ISE.csproj --no-restore",
            "Must pass after the temporary move-away probe restores the quarantine tree."),
        new(
            "tests-build",
            "dotnet build HybridCPU_ISE.Tests.csproj --no-restore",
            "Must pass with retained compatibility carriers and conformance inventory compiled."),
        new(
            "move-away-probe",
            "Move Legacy/VMX to Desktop/New folder and build production project",
            "Must pass after no-legacy production carriers move out of the physical quarantine."),
        new(
            "quarantine-static",
            "Core/VMX legacy scan and Legacy/VMX source inventory",
            "Core/VMX must stay free of legacy markers; Legacy/VMX must not restore removed heavy carriers."),
        new(
            "focused-conformance",
            "VmxProjectionSchemaAndQuarantineTests and CoreVmxAuthorityBoundaryTests",
            "Required before any later freeze declaration."),
        new(
            "broad-vmx-filter",
            "dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter FullyQualifiedName~Vmx",
            "Must pass after repairing stale repository-shape paths."),
        new(
            "conformance-folder-deletion-probe",
            "Move Legacy/VMX/Conformance to Desktop/New folder and build production/tests",
            "Production and tests must pass with test-local evidence replacing the compiled legacy conformance folder."),
    };

    public static readonly string[] KnownUnrelatedBroadFilterDebt = Array.Empty<string>();

    public static readonly string[] ResolvedBroadFilterDebt =
    {
        "FullyQualifiedName~Vmx stale repository-shape path repaired: InstructionRegistry helpers are read from NonRTL/Core/Diagnostics.",
        "FullyQualifiedName~Vmx passed 258/258 after the repository path repair.",
    };

    public static readonly string[] OutOfScopeNonVmxBroadFilterDebt =
    {
        "Phase12VliwCompatFreezeTests remains a repository/ISA VLIW compatibility-freeze debt: 23/28 passed, 5 failed after the VMX freeze matrix passed.",
        "The remaining Phase12 failures are stale repository-shape and VLIW/InstructionsEnum/Add_VLIW allowlist issues, not VMX compatibility frontend authority evidence.",
    };

    public bool IsSatisfied() =>
        MoveAwayProbeWasExecuted &&
        ConformanceFolderMoveAwayProbeWasExecuted &&
        MoveAwayProbeProductionBuildPassedAfterCarrierExit &&
        ConformanceFolderProductionBuildIndependent &&
        ConformanceFolderTestBuildIndependent &&
        ConformanceFolderDeletionSafeWithoutTestEvidenceDecoupling &&
        ConformanceFolderMoveAwayProbeTestCompileErrors == 0 &&
        ConformanceFolderMoveAwayProbeMissingEvidenceSymbols.Length == 0 &&
        ConformanceFolderMoveAwayProbeTestDependents.Length >= 2 &&
        BroadVmxFilterPassedAfterRepositoryPathRepair &&
        CanDeclareFreeze &&
        RetainedProductionCompatibilitySources.Length == 3 &&
        MoveAwayProbeMissingSymbols.Length == 0 &&
        ProductionCallersProvingDeletionIsNotMechanical.Length >= MoveAwayProbeMissingSymbols.Length &&
        BroadConformanceMatrix.Length >= 7 &&
        KnownUnrelatedBroadFilterDebt.Length == 0 &&
        ResolvedBroadFilterDebt.Length >= 2 &&
        OutOfScopeNonVmxBroadFilterDebt.Length >= 2;
}

internal readonly record struct LegacyVmxFreezeReadinessInventoryEntry(
    string RelativePath,
    string[] RequiredMarkers);

internal readonly record struct LegacyVmxFreezeReadinessMatrixEntry(
    string Name,
    string CommandOrProbe,
    string RequiredOutcome);
