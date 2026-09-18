namespace HybridCPU.ExternalRuntime.Contracts;

public enum ExternalEffectClass : byte { ReadOnly = 1, Idempotent = 2, NonIdempotent = 3 }
public enum ExternalVisibilityRequirement : byte { Explicit = 1, Coherent = 2, StagedOutput = 3 }
public enum ExternalCancellationMode : byte { Unsupported = 1, BestEffort = 2, ExactAcknowledgement = 3 }
public enum ExternalOperationStage : byte
{
    Prepared = 1, Admitted = 2, Submitted = 3, DeviceComplete = 4, Visible = 5,
    Published = 6, Released = 7, Failed = 8, Stale = 9
}

/// <summary>Opaque, unordered provider snapshot. Tokens have no CPU-interpretable meaning.</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(ExternalGenerationSetJsonConverter))]
public sealed class ExternalGenerationSet : IEquatable<ExternalGenerationSet>
{
    private readonly Guid[] tokens;
    public ExternalGenerationSet(HybridCpuExternalContractVersion contractVersion, IEnumerable<Guid> tokens)
    {
        ExternalOperationContract.ValidateVersion(contractVersion);
        ArgumentNullException.ThrowIfNull(tokens);
        this.tokens = tokens.Order().ToArray();
        if (this.tokens.Length == 0 || this.tokens.Any(x => x == Guid.Empty) ||
            this.tokens.Distinct().Count() != this.tokens.Length)
            throw new ArgumentException("Snapshot must contain nonempty, unique opaque tokens.", nameof(tokens));
        ContractVersion = contractVersion;
    }
    public HybridCpuExternalContractVersion ContractVersion { get; }
    internal Guid[] CopyTokens() => (Guid[])tokens.Clone();
    public bool Equals(ExternalGenerationSet? other) => other is not null &&
        ContractVersion == other.ContractVersion && tokens.SequenceEqual(other.tokens);
    public override bool Equals(object? obj) => obj is ExternalGenerationSet other && Equals(other);
    public override int GetHashCode()
    {
        var hash = new HashCode(); hash.Add(ContractVersion);
        foreach (var token in tokens) hash.Add(token);
        return hash.ToHashCode();
    }
}

public readonly record struct ExternalRequestCorrelation(Guid Value);

/// <summary>Exact semantic descriptor. Correlation must identify immutable submitted work.</summary>
public sealed record ExternalOperationRequest
{
    public ExternalOperationRequest(ExternalOperationIdentity operation, ExternalDomainLease scope,
        HybridCpuExternalContractVersion contractVersion, ExternalGenerationSet generations,
        ExternalRequestCorrelation correlation, ExternalEffectClass effectClass,
        ExternalVisibilityRequirement visibilityRequirement, ExternalCancellationMode cancellationMode)
    {
        ExternalOperationContract.ValidateVersion(contractVersion);
        ArgumentNullException.ThrowIfNull(generations);
        if (operation.Handle.Value == Guid.Empty || operation.Generation.Value == 0)
            throw new ArgumentException("Exact operation attempt required.", nameof(operation));
        if (scope.Handle.Value == Guid.Empty || scope.Epoch.Value == 0)
            throw new ArgumentException("Exact opaque lease scope required.", nameof(scope));
        if (correlation.Value == Guid.Empty) throw new ArgumentException("Correlation required.", nameof(correlation));
        if (generations.ContractVersion != contractVersion) throw new ArgumentException("Snapshot version mismatch.", nameof(generations));
        if (!Enum.IsDefined(effectClass)) throw new ArgumentOutOfRangeException(nameof(effectClass));
        if (!Enum.IsDefined(visibilityRequirement)) throw new ArgumentOutOfRangeException(nameof(visibilityRequirement));
        if (!Enum.IsDefined(cancellationMode)) throw new ArgumentOutOfRangeException(nameof(cancellationMode));
        Operation = operation; Scope = scope; ContractVersion = contractVersion; Generations = generations;
        Correlation = correlation; EffectClass = effectClass; VisibilityRequirement = visibilityRequirement;
        CancellationMode = cancellationMode;
    }
    public ExternalOperationIdentity Operation { get; }
    public ExternalDomainLease Scope { get; }
    public HybridCpuExternalContractVersion ContractVersion { get; }
    public ExternalGenerationSet Generations { get; }
    public ExternalRequestCorrelation Correlation { get; }
    public ExternalEffectClass EffectClass { get; }
    public ExternalVisibilityRequirement VisibilityRequirement { get; }
    public ExternalCancellationMode CancellationMode { get; }
    public ExternalOperationStage Stage => ExternalOperationStage.Prepared;
}

/// <summary>Data issued only by an authoritative provider. Construction is not authentication.</summary>
public abstract record ExternalOperationReceipt
{
    private protected ExternalOperationReceipt(ExternalOperationRequest request, ExternalOperationStage stage, ExternalRuntimeOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Enum.IsDefined(outcome)) throw new ArgumentOutOfRangeException(nameof(outcome));
        Request = request; Stage = stage; Outcome = outcome;
    }
    public ExternalOperationRequest Request { get; }
    public ExternalOperationStage Stage { get; }
    public ExternalRuntimeOutcome Outcome { get; }
}
public sealed record ExternalOperationAdmissionReceipt : ExternalOperationReceipt
{
    public ExternalOperationAdmissionReceipt(ExternalOperationRequest request, ExternalRuntimeOutcome outcome)
        : base(request, ExternalOperationStage.Admitted, outcome) { }
}
/// <summary>Separate acknowledgements for submission and visibility; neither implies publication.</summary>
public sealed record ExternalOperationProgressReceipt : ExternalOperationReceipt
{
    public ExternalOperationProgressReceipt(ExternalOperationRequest request, ExternalOperationStage stage, ExternalRuntimeOutcome outcome)
        : base(request, stage, outcome)
    {
        if (stage is not (ExternalOperationStage.Submitted or ExternalOperationStage.Visible))
            throw new ArgumentOutOfRangeException(nameof(stage));
    }
}
public sealed record ExternalOperationCompletionReceipt : ExternalOperationReceipt
{
    public ExternalOperationCompletionReceipt(ExternalOperationRequest request, ExternalRuntimeOutcome outcome)
        : base(request, ExternalOperationStage.DeviceComplete, outcome) { }
}
public sealed record ExternalOperationPublicationReceipt : ExternalOperationReceipt
{
    public ExternalOperationPublicationReceipt(ExternalOperationRequest request, ExternalRuntimeOutcome outcome)
        : base(request, ExternalOperationStage.Published, outcome) { }
}
public sealed record ExternalOperationReleaseReceipt : ExternalOperationReceipt
{
    public ExternalOperationReleaseReceipt(ExternalOperationRequest request, ExternalRuntimeOutcome outcome)
        : base(request, ExternalOperationStage.Released, outcome) { }
}

/// <summary>Pure validation, never provider authentication or permission to perform an effect.</summary>
public static class ExternalOperationContract
{
    public static HybridCpuExternalContractVersion Version { get; } = new(1, 4, 0);
    public static void ValidateVersion(HybridCpuExternalContractVersion version)
    {
        if (version != Version) throw new ArgumentOutOfRangeException(nameof(version), "Unsupported external-operation schema.");
    }
    public static ExternalOperationStage Revalidate(ExternalOperationStage current,
        ExternalOperationRequest request, ExternalGenerationSet? currentGenerations)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Enum.IsDefined(current)) return ExternalOperationStage.Failed;
        if (current is ExternalOperationStage.Failed or ExternalOperationStage.Stale or ExternalOperationStage.Released) return current;
        return request.Generations.Equals(currentGenerations) ? current : ExternalOperationStage.Stale;
    }
    public static ExternalOperationStage Accept(ExternalOperationStage current, ExternalOperationRequest request,
        ExternalGenerationSet? currentGenerations, ExternalOperationReceipt? receipt)
    {
        var checkedStage = Revalidate(current, request, currentGenerations);
        if (current == ExternalOperationStage.Released) return ExternalOperationStage.Failed;
        if (checkedStage != current || current is ExternalOperationStage.Failed or ExternalOperationStage.Stale)
            return checkedStage;
        if (receipt is null) return ExternalOperationStage.Failed;
        if (receipt.Request != request) return ExternalOperationStage.Stale;
        if (receipt.Outcome == ExternalRuntimeOutcome.Stale) return ExternalOperationStage.Stale;
        var expectedOutcome = receipt.Stage == ExternalOperationStage.Released ? ExternalRuntimeOutcome.Closed : ExternalRuntimeOutcome.Succeeded;
        if (receipt.Outcome != expectedOutcome || (byte)receipt.Stage != (byte)current + 1)
            return ExternalOperationStage.Failed;
        return receipt.Stage;
    }
}
