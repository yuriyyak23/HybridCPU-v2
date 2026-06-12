namespace YAKSys_Hybrid_CPU.Core;

public enum TrapCompletionRouteDecision : byte
{
    Allowed = 0,
    DeniedRuntimeAdmission = 1,
    DeniedNoNeutralTrap = 2,
    MissingRouteDescriptor = 3,
    DeniedRouteAuthority = 4,
    DeniedDomainValidation = 5,
    DeniedBackendExecution = 6,
    DeniedCompletionPublication = 7,
    DeniedRetirePublication = 8,
}

public sealed partial class TrapCompletionRouteDescriptor
{
    public TrapCompletionRouteDescriptor()
        : this(
            CompletionRouteAuthority.Runtime,
            allowsCompletionPublication: false,
            allowsRetirePublication: false,
            requiresValidatedDomain: true)
    {
    }

    public TrapCompletionRouteDescriptor(
        CompletionRouteAuthority authority,
        bool allowsCompletionPublication,
        bool allowsRetirePublication,
        bool requiresValidatedDomain)
    {
        Authority = authority;
        AllowsCompletionPublication = allowsCompletionPublication;
        AllowsRetirePublication = allowsRetirePublication;
        RequiresValidatedDomain = requiresValidatedDomain;
    }

    public static TrapCompletionRouteDescriptor ProjectionOnlyDenied { get; } =
        new(
            CompletionRouteAuthority.Runtime,
            allowsCompletionPublication: false,
            allowsRetirePublication: false,
            requiresValidatedDomain: false);

    public static TrapCompletionRouteDescriptor RuntimeOwnedCompletionPublication { get; } =
        new(
            CompletionRouteAuthority.Runtime,
            allowsCompletionPublication: true,
            allowsRetirePublication: false,
            requiresValidatedDomain: true);

    public static TrapCompletionRouteDescriptor RuntimeOwnedPublication { get; } =
        new(
            CompletionRouteAuthority.Runtime,
            allowsCompletionPublication: true,
            allowsRetirePublication: true,
            requiresValidatedDomain: true);

    public CompletionRouteAuthority Authority { get; }

    public bool AllowsCompletionPublication { get; }

    public bool AllowsRetirePublication { get; }

    public bool RequiresValidatedDomain { get; }

    public bool IsRuntimeAuthoritative =>
        Authority == CompletionRouteAuthority.Runtime;
}

public readonly record struct TrapCompletionRouteRequest(
    NeutralTrapResult TrapResult,
    RuntimeBoundaryAdmissionResult RuntimeAdmission,
    TrapCompletionRouteDescriptor? RouteDescriptor,
    bool DomainValidated,
    bool BackendExecutionAuthorized,
    ulong Qualification = 0,
    ulong FaultAddress = 0,
    ulong FaultAux = 0)
{
    public static TrapCompletionRouteRequest ProjectionOnlyDenied(
        NeutralTrapResult trapResult,
        RuntimeBoundaryAdmissionResult runtimeAdmission,
        bool backendExecutionAuthorized = false) =>
        new(
            trapResult,
            runtimeAdmission,
            TrapCompletionRouteDescriptor.ProjectionOnlyDenied,
            DomainValidated: true,
            backendExecutionAuthorized);
}

public readonly record struct TrapCompletionRouteResult(
    TrapCompletionRouteDecision Decision,
    bool CompletionPublicationAuthorized,
    bool RetirePublicationAuthorized,
    uint NeutralReasonCode,
    string Reason)
{
    public bool CompletionPublicationAuthorizedOnly =>
        CompletionPublicationAuthorized &&
        !RetirePublicationAuthorized;

    public bool IsFullyRetirable =>
        Decision == TrapCompletionRouteDecision.Allowed &&
        CompletionPublicationAuthorized &&
        RetirePublicationAuthorized;

    public bool IsAllowed =>
        IsFullyRetirable;

    public bool DeniesBackendExecution =>
        Decision == TrapCompletionRouteDecision.DeniedBackendExecution;

    public static TrapCompletionRouteResult Denied(
        TrapCompletionRouteDecision decision,
        NeutralTrapResult trapResult,
        string reason) =>
        new(
            decision,
            CompletionPublicationAuthorized: false,
            RetirePublicationAuthorized: false,
            NeutralReasonCode: (uint)trapResult.Kind,
            reason);

    public static TrapCompletionRouteResult CompletionAuthorizedButRetireDenied(
        NeutralTrapResult trapResult,
        string reason) =>
        new(
            TrapCompletionRouteDecision.DeniedRetirePublication,
            CompletionPublicationAuthorized: true,
            RetirePublicationAuthorized: false,
            NeutralReasonCode: (uint)trapResult.Kind,
            reason);
}

public sealed class TrapCompletionRouteService
{
    public static TrapCompletionRouteService Default { get; } = new();

    public TrapCompletionRouteResult Authorize(
        TrapCompletionRouteRequest request)
    {
        if (!request.RuntimeAdmission.IsAllowed)
        {
            return TrapCompletionRouteResult.Denied(
                TrapCompletionRouteDecision.DeniedRuntimeAdmission,
                request.TrapResult,
                "Trap completion route requires runtime boundary admission.");
        }

        if (!request.TrapResult.ShouldTrap)
        {
            return TrapCompletionRouteResult.Denied(
                TrapCompletionRouteDecision.DeniedNoNeutralTrap,
                request.TrapResult,
                "Trap completion route requires a neutral trap result.");
        }

        if (request.RouteDescriptor is null)
        {
            return TrapCompletionRouteResult.Denied(
                TrapCompletionRouteDecision.MissingRouteDescriptor,
                request.TrapResult,
                "Trap completion route requires a runtime-owned route descriptor.");
        }

        TrapCompletionRouteDescriptor route = request.RouteDescriptor;
        if (!route.IsRuntimeAuthoritative)
        {
            return TrapCompletionRouteResult.Denied(
                TrapCompletionRouteDecision.DeniedRouteAuthority,
                request.TrapResult,
                "Compatibility projection cannot own trap completion routing.");
        }

        if (route.RequiresValidatedDomain && !request.DomainValidated)
        {
            return TrapCompletionRouteResult.Denied(
                TrapCompletionRouteDecision.DeniedDomainValidation,
                request.TrapResult,
                "Trap completion route requires a validated runtime domain.");
        }

        if (!request.BackendExecutionAuthorized)
        {
            return TrapCompletionRouteResult.Denied(
                TrapCompletionRouteDecision.DeniedBackendExecution,
                request.TrapResult,
                "Trap completion route denies publication until backend execution is authorized by runtime policy.");
        }

        if (!route.AllowsCompletionPublication)
        {
            return TrapCompletionRouteResult.Denied(
                TrapCompletionRouteDecision.DeniedCompletionPublication,
                request.TrapResult,
                "Trap completion route descriptor denies completion publication.");
        }

        if (!route.AllowsRetirePublication)
        {
            return TrapCompletionRouteResult.CompletionAuthorizedButRetireDenied(
                request.TrapResult,
                "Trap completion route descriptor authorizes completion publication but denies retire publication.");
        }

        return new TrapCompletionRouteResult(
            TrapCompletionRouteDecision.Allowed,
            CompletionPublicationAuthorized: true,
            RetirePublicationAuthorized: true,
            NeutralReasonCode: (uint)request.TrapResult.Kind,
            Reason: string.Empty);
    }

    public TrapCompletionPublicationFenceResult EvaluateFence(
        TrapCompletionRouteRequest request,
        TrapCompletionRouteResult route) =>
        TrapCompletionPublicationFence.Default.Evaluate(
            request.TrapResult,
            request.RuntimeAdmission.IsAllowed,
            route.CompletionPublicationAuthorized,
            route.RetirePublicationAuthorized,
            route.NeutralReasonCode,
            request.Qualification,
            request.FaultAddress,
            request.FaultAux);
}
