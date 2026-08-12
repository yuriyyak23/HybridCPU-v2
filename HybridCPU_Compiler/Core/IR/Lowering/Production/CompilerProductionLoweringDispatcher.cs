using System;
using HybridCPU.Compiler.Core.IR.Artifacts;
using HybridCPU.Compiler.Core.IR.Authority;
using HybridCPU.Compiler.Core.IR.Contours;
using HybridCPU.Compiler.Core.IR.Intent;

namespace HybridCPU.Compiler.Core.IR.Lowering.Production;

public enum CompilerProductionDispatchKind
{
    RejectedBeforeProvider = 0,
    FutureGatedNoProvider,
    ProviderEvaluated
}

public enum CompilerProductionDispatchAuthority
{
    NoRuntimeAuthority = 0
}

public enum CompilerCarrierProductionMode
{
    CompatibilityOnly = 0,
    ExplicitCarrierProduction
}

/// <summary>
/// Canonical product request. The caller supplies semantic intent and an
/// already compiled program, but cannot supply or relabel a candidate package
/// or selected contour. Those are derived inside the compiler boundary.
/// </summary>
public sealed record CompilerCompiledProgramDispatchRequest(
    CompilerSemanticIntent Intent,
    HybridCpuCompiledProgram CompiledProgram,
    CompilerProductionLoweringProfile ProductionProfile,
    CompilerProductionLoweringReadiness Readiness,
    CompilerCarrierProductionMode CarrierProductionMode,
    string ProducerSurface);

/// <summary>
/// Canonical compiler-only result for routing an already constructed candidate
/// package to its exact production provider. Dispatch is evidence about
/// compiler package construction. It is never runtime admission, execution,
/// completion, publication, commit, or retire authority.
/// </summary>
public sealed record CompilerProductionDispatchResult(
    CompilerProductionDispatchKind DispatchKind,
    CompilerExecutionContourSelection Selection,
    ContourAnalysisReport Analysis,
    CompilerProductionLoweringResult? ProviderResult,
    string? ProviderType,
    string Reason,
    CompilerProductionDispatchAuthority Authority =
        CompilerProductionDispatchAuthority.NoRuntimeAuthority)
{
    public CompilerRuntimeAuthorityDependency RuntimeAuthorityStillRequired =>
        ProviderResult?.RuntimeAuthorityStillRequired ?? Selection.RuntimeDependency;
}

/// <summary>
/// Product-facing compiler caller for the explicit production-provider set.
/// The dispatcher derives the contour from semantic intent, performs analysis,
/// resolves only the exact registered provider, and never routes fallback.
/// Candidate construction remains outside this class and runtime independently
/// revalidates every emitted carrier and lifecycle transition.
/// </summary>
public sealed class CompilerProductionLoweringDispatcher
{
    public static CompilerProductionLoweringDispatcher Default { get; } = new(
        CompilerDefaultExecutionContourSelector.Instance,
        DefaultContourLoweringProviderRegistry.Instance);

    private readonly IExecutionContourSelector _selector;
    private readonly IContourLoweringProviderRegistry _registry;

    internal CompilerProductionLoweringDispatcher(
        IExecutionContourSelector selector,
        IContourLoweringProviderRegistry registry)
    {
        _selector = selector ?? throw new ArgumentNullException(nameof(selector));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public CompilerProductionDispatchResult DispatchCompiledProgram(
        CompilerCompiledProgramDispatchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Intent);
        ArgumentNullException.ThrowIfNull(request.CompiledProgram);
        ArgumentNullException.ThrowIfNull(request.ProductionProfile);
        ArgumentNullException.ThrowIfNull(request.Readiness);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ProducerSurface);

        CompilerExecutionContourSelection selection =
            _selector.SelectContour(request.Intent);
        CompilerEmissionPackage? candidate = selection.IsEmissionForbidden
            ? null
            : HybridCpuCompiledProgramEnvelopeAdapter.Instance.Project(
                request.CompiledProgram,
                new CompilerArtifactProjectionOptions(
                    request.Intent.Kind,
                    selection.Kind,
                    request.ProducerSurface,
                    "Canonical compiled-program package projection; runtime authority remains pending."));
        candidate = NormalizeCanonicalSideband(candidate);

        bool explicitCarrierProduction =
            request.CarrierProductionMode == CompilerCarrierProductionMode.ExplicitCarrierProduction &&
            request.ProductionProfile.Mode == CompilerProductionLoweringProfileMode.ExplicitlyEnabled;
        CompilerProductionLoweringContext context = new(
            new CompilerTargetProfile(
                $"{request.ProductionProfile.Name}.canonical-dispatch",
                AllowsCarrierEmission: explicitCarrierProduction,
                AllowsBackendEmission: explicitCarrierProduction),
            request.ProducerSurface,
            request.ProductionProfile)
        {
            CandidatePackage = candidate,
            Readiness = request.Readiness
        };

        return Dispatch(request.Intent, context);
    }

    private static CompilerEmissionPackage? NormalizeCanonicalSideband(
        CompilerEmissionPackage? package)
    {
        if (package?.Descriptor is not
            {
                Status: DescriptorAbiStatus.ValidTransportDescriptor,
                Descriptors.Count: > 0
            } || package.Sideband is not { } sideband)
        {
            return package;
        }

        CompilerSidebandEnvelope descriptorSideband = sideband with
        {
            Requirement = SidebandRequirement.RequiredForDescriptorSubmit,
            PreservationClass = SidebandPreservationClass.PreservedCompilerSideband,
            IsEmptyCompatibilitySideband = false
        };
        return package with
        {
            Sideband = descriptorSideband,
            RuntimeBridgeInput = package.RuntimeBridgeInput is { } bridge
                ? bridge with { Sideband = descriptorSideband }
                : null
        };
    }

    private CompilerProductionDispatchResult Dispatch(
        CompilerSemanticIntent intent,
        CompilerProductionLoweringContext context)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(context);

        CompilerExecutionContourSelection selection = _selector.SelectContour(intent);
        CompilerLoweringContext analysisContext = new(
            context.TargetProfile,
            context.ProducerSurface);
        ContourAnalysisReport analysis =
            _registry.ResolveAnalyzer(selection.Kind).Analyze(intent, analysisContext);

        if (!selection.IsKnownContour || selection.IsEmissionForbidden)
        {
            return new(
                CompilerProductionDispatchKind.RejectedBeforeProvider,
                selection,
                analysis,
                ProviderResult: null,
                ProviderType: null,
                $"Production dispatch stopped before provider resolution: {selection.SelectionReason}");
        }

        IContourProductionLoweringProvider? provider =
            _registry.ResolveProductionProvider(selection.Kind, context);
        if (provider is null)
        {
            return new(
                CompilerProductionDispatchKind.FutureGatedNoProvider,
                selection,
                analysis,
                ProviderResult: null,
                ProviderType: null,
                "No exact production provider is enabled by the compiler profile; cross-contour fallback is forbidden.");
        }

        if (provider.ContourKind != selection.Kind || analysis.ContourKind != selection.Kind)
        {
            return new(
                CompilerProductionDispatchKind.RejectedBeforeProvider,
                selection,
                analysis,
                ProviderResult: null,
                ProviderType: provider.GetType().Name,
                "Production dispatch rejected a selector, analyzer, or provider contour mismatch; fallback is forbidden.");
        }

        CompilerProductionLoweringResult result = provider.TryProduce(intent, analysis, context);
        if (result.GateResult.ContourKind != selection.Kind ||
            (result.Package is not null &&
             (result.Package.Identity.ContourKind != selection.Kind ||
              result.Package.Identity.IntentKind != intent.Kind)))
        {
            throw new InvalidOperationException(
                "An exact production provider returned a cross-contour or cross-intent package.");
        }

        return new(
            CompilerProductionDispatchKind.ProviderEvaluated,
            selection,
            analysis,
            result,
            provider.GetType().Name,
            "Exact compiler production provider evaluated the candidate; runtime authority remains independently required.");
    }
}
