namespace HybridCPU.ExternalRuntime.Contracts;

public static class ExternalOriginalNestedChildContract
{
    public const uint ContractVersion = 1;
    public const int MaximumDepth = 64; // Bounded software provider admission, independent of machine nesting.
}

/// <summary>Original software child factory intent; no machine virtualization claim.</summary>
public sealed record ExternalOriginalNestedChildRequest(
    ExternalChildDomainCreateReceipt ParentAdmission,
    ExternalChildDomainCreateRequest CreationRequest,
    bool AllowNestedCreation = false);

public sealed record ExternalOriginalNestedChildObservation(
    ExternalOriginalNestedChildRequest Request,
    ExternalChildResult<ExternalChildDomainCreateReceipt> Raw,
    bool NoEffect,
    bool NoFurtherAdmission,
    bool NestedCreationAllowed);

public interface IHybridCpuOriginalNestedChildRuntimeV1
{
    ExternalOriginalNestedChildObservation? CreateOriginalNestedChild(ExternalOriginalNestedChildRequest request);
    ExternalOriginalNestedChildObservation? ResolveOriginalNestedChild(ExternalOriginalNestedChildRequest originalRequest);
}
