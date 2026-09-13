using HybridCPU.ExternalRuntime.Contracts;

namespace HybridCPU.ExternalRuntime;

internal enum BackendOutcome : byte { Accepted, Unsupported, Denied, Revoked, Faulted, Unknown }

internal sealed record BackendBindResult(BackendOutcome Outcome, ExternalDomainBindReceipt? Receipt, string Reason);
internal sealed record BackendTransitionResult(BackendOutcome Outcome, ExternalDomainTransitionReceipt? Receipt, string Reason);
internal sealed record BackendCloseResult(BackendOutcome Outcome, ExternalDomainCloseReceipt? Receipt, string Reason);

internal interface IExternalDomainExecutionOwner
{
    BackendBindResult Open(ExternalDomainLease lease, ExternalOperationIdentity operation,
        HybridCpuExternalContractVersion version, ulong manifestGeneration);
    BackendTransitionResult Transition(ExternalDomainLease lease, ExternalDomainTransition transition,
        ExternalOperationIdentity operation, HybridCpuExternalContractVersion version, ulong manifestGeneration);
    BackendCloseResult Close(ExternalDomainLease lease, ExternalOperationIdentity operation,
        HybridCpuExternalContractVersion version, ulong manifestGeneration);
}

internal interface IExternalDomainDiagnostics
{
    void Attach(ExternalDomainLease lease, ulong manifestGeneration);
    void Detach(ExternalDomainLease lease, ulong manifestGeneration);
}

internal sealed class AdmissionDomainLifetimeOwner : IExternalDomainExecutionOwner
{
    public BackendBindResult Open(ExternalDomainLease lease, ExternalOperationIdentity operation,
        HybridCpuExternalContractVersion version, ulong manifestGeneration) =>
        new(BackendOutcome.Accepted,
            new(lease, operation, version, manifestGeneration, ExternalDomainState.Ready), string.Empty);

    public BackendTransitionResult Transition(ExternalDomainLease lease, ExternalDomainTransition transition,
        ExternalOperationIdentity operation, HybridCpuExternalContractVersion version, ulong manifestGeneration) =>
        new(BackendOutcome.Unsupported, null,
            "The audited ISE owner has no independently receipted domain Start/Park/Resume mechanism.");

    public BackendCloseResult Close(ExternalDomainLease lease, ExternalOperationIdentity operation,
        HybridCpuExternalContractVersion version, ulong manifestGeneration) =>
        new(BackendOutcome.Accepted,
            new(lease, operation, version, manifestGeneration, ExternalDomainState.Closed, IsTerminal: true), string.Empty);
}
