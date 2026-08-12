using System;
using System.Collections.Generic;

namespace HybridCPU.Compiler.Core.API.Migration;

public enum CompilerCompatibilityDisposition
{
    LocalCallerMigrationRequired = 0,
    ZeroLocalCallersCompatibilityWindowRequired,
    CompatibilityWindowRequired,
    FrozenLegacyArchive
}

/// <summary>
/// Compiler-owned deprecation evidence. A row records migration/removal state;
/// it never grants runtime execution or lifecycle authority.
/// </summary>
public sealed record CompilerCompatibilitySurfaceRow(
    string SurfaceId,
    IReadOnlyList<string> SourceFiles,
    string ReplacementSurface,
    CompilerCompatibilityDisposition Disposition,
    string LocalCallerEvidence,
    string RemovalGate,
    bool CreatesRuntimeAuthority = false);

/// <summary>
/// Closed inventory of source files that declare compiler compatibility APIs.
/// Removal remains fail-closed until the row-specific caller and compatibility
/// gates are satisfied.
/// </summary>
public static class CompilerCompatibilitySurfaceCatalog
{
    public const string CatalogVersion = "CompilerCompatibilitySurfaceCatalog/v1";

    private static readonly CompilerCompatibilitySurfaceRow[] s_rows =
    [
        Row(
            "asm-facade-hierarchy",
            [
                "HybridCPU_Compiler/API/Facade/AppAsmFacade.cs",
                "HybridCPU_Compiler/API/Facade/ExpertBackendFacade.cs",
                "HybridCPU_Compiler/API/Facade/IAppAsmFacade.cs",
                "HybridCPU_Compiler/API/Facade/IExpertBackendFacade.cs",
                "HybridCPU_Compiler/API/Facade/IPlatformAsmFacade.cs",
                "HybridCPU_Compiler/API/Facade/PlatformAsmFacade.cs"
            ],
            "HybridCpuThreadCompilerContext, HybridCpuCanonicalCompiler, HybridCpuMultithreadedCompiler, or HybridCpuNonVmxScalarCompiler typed APIs",
            CompilerCompatibilityDisposition.ZeroLocalCallersCompatibilityWindowRequired,
            "All known local compiler clients are migrated from AppAsmFacade/PlatformAsmFacade; NonVmx examples use the exact typed scalar producer.",
            "Keep the local zero-caller scan green, prove the external compatibility window, then remove interfaces and implementations together."),
        Row(
            "directive-parse-success-alias",
            ["HybridCPU_Compiler/API/Frontend/Directives/HybridCpuCompilerDirectives.cs"],
            "DirectiveParseResult.IsDirectiveParsed or ToParseObservation",
            CompilerCompatibilityDisposition.CompatibilityWindowRequired,
            "No known local compiler client consumes DirectiveParseResult.Success as compiler authority.",
            "Prove zero callers and retain one published compatibility window before removing the alias."),
        Row(
            "raw-matrix-vector-thread-helpers",
            [
                "HybridCPU_Compiler/API/Threading/ThreadCompilerContext.MatrixTile.cs",
                "HybridCPU_Compiler/API/Threading/ThreadCompilerContext.VectorTransfer.cs"
            ],
            "Decision-bearing typed MatrixTile and vector-transfer emission contracts",
            CompilerCompatibilityDisposition.ZeroLocalCallersCompatibilityWindowRequired,
            "Local non-test compiler clients have zero raw MatrixTile/VectorTransfer thread-helper calls.",
            "Keep source-scan zero, publish compatibility-window decision, then remove raw plan-returning overloads."),
        Row(
            "safety-mask-compatibility-aliases",
            ["HybridCPU_Compiler/Core/IR/Admission/SafetyMaskDiagnosticChecker.cs"],
            "SafetyMaskDiagnosticChecker and SafetyMaskDiagnosticResult",
            CompilerCompatibilityDisposition.CompatibilityWindowRequired,
            "Canonical compiler paths use the diagnostic checker/result names.",
            "Prove zero external aliases and a completed compatibility window."),
        Row(
            "raw-helper-lowerer-and-try-recovery",
            [
                "HybridCPU_Compiler/Core/IR/Construction/CompilerMatrixTileEmissionLowerer.cs",
                "HybridCPU_Compiler/Core/IR/Construction/CompilerVectorTransferEmissionLowerer.cs"
            ],
            "LowerWithDecision and RecoverFromInstruction",
            CompilerCompatibilityDisposition.ZeroLocalCallersCompatibilityWindowRequired,
            "Canonical IR construction consumes typed recovery and decision-bearing lowering results.",
            "Maintain zero production callers and complete the compiler compatibility window."),
        Row(
            "legacy-structural-legality-entrypoints",
            ["HybridCPU_Compiler/Core/IR/Hazards/HybridCpuInstructionLegalityChecker.cs"],
            "AnalyzeStructuralCandidateBundle and AnalyzeClusterPreparedStructuralAdmission",
            CompilerCompatibilityDisposition.ZeroLocalCallersCompatibilityWindowRequired,
            "Scheduler and bundler callers use typed structural admission results.",
            "Maintain structural-reader scans and complete the compiler compatibility window."),
        Row(
            "legacy-slot-search-entrypoints",
            [
                "HybridCPU_Compiler/Core/IR/Hazards/HybridCpuSlotModel.Analysis.cs",
                "HybridCPU_Compiler/Core/IR/Hazards/HybridCpuSlotModel.BundleSearch.cs",
                "HybridCPU_Compiler/Core/IR/Hazards/HybridCpuSlotModel.ProgramSearch.cs"
            ],
            "Get/Analyze/Search/MaterializeStructural* APIs",
            CompilerCompatibilityDisposition.ZeroLocalCallersCompatibilityWindowRequired,
            "Canonical scheduling and bundle formation use structural aliases.",
            "Maintain all scheduler/bundler structural scans and complete the compatibility window."),
        Row(
            "legacy-backend-capability-bools",
            ["HybridCPU_Compiler/Core/IR/Model/CompilerBackendLoweringContract.cs"],
            "CompilerLoweringDecision.FromLegacyBackendLoweringDecision and typed production providers",
            CompilerCompatibilityDisposition.ZeroLocalCallersCompatibilityWindowRequired,
            "Local compiler clients do not consume IsAllowed or CanSelectForProductionLowering.",
            "Maintain zero caller scans and complete the compiler compatibility window."),
        Row(
            "legacy-structural-result-aliases",
            [
                "HybridCPU_Compiler/Core/IR/Model/IrAdjacentBundlePlacementSearchResult.cs",
                "HybridCPU_Compiler/Core/IR/Model/IrAdjacentBundleTripletPlacementSearchResult.cs",
                "HybridCPU_Compiler/Core/IR/Model/IrBasicBlockPlacementSearchResult.cs",
                "HybridCPU_Compiler/Core/IR/Model/IrBundlePlacementQuality.cs",
                "HybridCPU_Compiler/Core/IR/Model/IrBundlePlacementSearchResult.cs",
                "HybridCPU_Compiler/Core/IR/Model/IrCandidateBundleAnalysis.cs",
                "HybridCPU_Compiler/Core/IR/Model/IrGlobalBasicBlockPlacementSearchResult.cs",
                "HybridCPU_Compiler/Core/IR/Model/IrHazardDiagnostic.cs",
                "HybridCPU_Compiler/Core/IR/Model/IrMaterializedBundleSlot.cs",
                "HybridCPU_Compiler/Core/IR/Model/IrMaterializedSlotAssignment.cs",
                "HybridCPU_Compiler/Core/IR/Model/IrProgramPlacementSearchResult.cs"
            ],
            "StructurallyAdmissible/StructuralPlacement/StructurallyAllowed typed result properties",
            CompilerCompatibilityDisposition.ZeroLocalCallersCompatibilityWindowRequired,
            "Canonical compiler readers use structural result names rather than legality-authority aliases.",
            "Maintain source scans for every canonical reader and complete the compatibility window."),
        Row(
            "legacy-vmx-projection-archive",
            ["HybridCPU_Compiler/Legacy/VMX-2/Core/IR/Model/VmxCompilerAuthority.cs"],
            "Current exact virtualization intent and CompilerEmissionDecisionV1 surfaces",
            CompilerCompatibilityDisposition.FrozenLegacyArchive,
            "The VMX-2 tree is an archive and is not a current compiler production source.",
            "Remove only under an explicit archive-retention decision; never promote archive preflight evidence to authority.")
    ];

    public static IReadOnlyList<CompilerCompatibilitySurfaceRow> Rows => s_rows;

    public static CompilerCompatibilitySurfaceRow GetRequired(string surfaceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(surfaceId);
        foreach (CompilerCompatibilitySurfaceRow row in s_rows)
        {
            if (string.Equals(row.SurfaceId, surfaceId, StringComparison.Ordinal))
            {
                return row;
            }
        }

        throw new ArgumentOutOfRangeException(
            nameof(surfaceId),
            surfaceId,
            "Unknown compiler compatibility surface.");
    }

    private static CompilerCompatibilitySurfaceRow Row(
        string surfaceId,
        IReadOnlyList<string> sourceFiles,
        string replacementSurface,
        CompilerCompatibilityDisposition disposition,
        string localCallerEvidence,
        string removalGate) =>
        new(
            surfaceId,
            sourceFiles,
            replacementSurface,
            disposition,
            localCallerEvidence,
            removalGate,
            CreatesRuntimeAuthority: false);
}
