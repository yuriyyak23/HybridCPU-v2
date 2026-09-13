namespace YAKSys_Hybrid_CPU.Core;

public enum VirtualizationNoEmissionContractDecision : byte
{
    Allowed = 0,
    NoEmissionGateDenied = 1,
    MissingGoldenArtifact = 2,
    GeneratedProjectionRequired = 3,
    HostEvidenceEmissionDenied = 4,
    NativeTokenEmissionDenied = 5,
}

public readonly record struct VirtualizationNoEmissionContractRequest(
    VirtualizationEmissionRequest Emission,
    bool GoldenArtifactCaptured);

public readonly record struct VirtualizationNoEmissionContractResult(
    VirtualizationNoEmissionContractDecision Decision,
    string Reason)
{
    public bool IsAllowed => Decision == VirtualizationNoEmissionContractDecision.Allowed;

    public static VirtualizationNoEmissionContractResult Allowed { get; } =
        new(VirtualizationNoEmissionContractDecision.Allowed, "No-emission conformance contract allowed.");

    public static VirtualizationNoEmissionContractResult Denied(
        VirtualizationNoEmissionContractDecision decision,
        string reason) =>
        new(decision, reason);
}

public sealed partial class VirtualizationNoEmissionContract
{
    private readonly VirtualizationNoEmissionRegressionGate _gate;

    public VirtualizationNoEmissionContract()
        : this(new VirtualizationNoEmissionRegressionGate())
    {
    }

    public VirtualizationNoEmissionContract(VirtualizationNoEmissionRegressionGate gate)
    {
        _gate = gate;
    }

    public VirtualizationNoEmissionContractResult Validate(
        VirtualizationNoEmissionContractRequest request)
    {
        if (!request.GoldenArtifactCaptured)
        {
            return VirtualizationNoEmissionContractResult.Denied(
                VirtualizationNoEmissionContractDecision.MissingGoldenArtifact,
                "No-emission conformance requires a golden artifact capture.");
        }

        if (request.Emission.EmitsHostOwnedEvidence)
        {
            return VirtualizationNoEmissionContractResult.Denied(
                VirtualizationNoEmissionContractDecision.HostEvidenceEmissionDenied,
                "No-emission conformance rejects host-owned evidence emission.");
        }

        if (request.Emission.EmitsNativeLaneToken)
        {
            return VirtualizationNoEmissionContractResult.Denied(
                VirtualizationNoEmissionContractDecision.NativeTokenEmissionDenied,
                "No-emission conformance rejects native lane token emission.");
        }

        if (!request.Emission.IsGeneratedProjection)
        {
            return VirtualizationNoEmissionContractResult.Denied(
                VirtualizationNoEmissionContractDecision.GeneratedProjectionRequired,
                "No-emission conformance requires generated compatibility projection.");
        }

        if (!_gate.CanEmit(request.Emission))
        {
            return VirtualizationNoEmissionContractResult.Denied(
                VirtualizationNoEmissionContractDecision.NoEmissionGateDenied,
                "No-emission regression gate denied this emission request.");
        }

        return VirtualizationNoEmissionContractResult.Allowed;
    }

    public bool IsSatisfied(VirtualizationNoEmissionContractRequest request) =>
        Validate(request).IsAllowed;
}
