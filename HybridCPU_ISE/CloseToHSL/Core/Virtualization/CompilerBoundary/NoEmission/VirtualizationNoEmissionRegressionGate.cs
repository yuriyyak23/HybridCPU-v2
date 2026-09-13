namespace YAKSys_Hybrid_CPU.Core;

public enum VirtualizationEmissionDecision : byte
{
    AllowedCompatibilityProjection = 0,
    DeniedDirectSubstrateEmission = 1,
    DeniedHostEvidenceEmission = 2,
    DeniedNativeTokenEmission = 3,
    DeniedUnvalidatedDescriptorEmission = 4,
}

public readonly record struct VirtualizationEmissionRequest(
    bool IsCompatibilityFrontend,
    bool IsGeneratedProjection,
    bool DescriptorValidated,
    bool EmitsHostOwnedEvidence,
    bool EmitsNativeLaneToken);

public sealed partial class VirtualizationNoEmissionRegressionGate
{
    public VirtualizationEmissionDecision Evaluate(VirtualizationEmissionRequest request)
    {
        if (request.EmitsHostOwnedEvidence)
        {
            return VirtualizationEmissionDecision.DeniedHostEvidenceEmission;
        }

        if (request.EmitsNativeLaneToken)
        {
            return VirtualizationEmissionDecision.DeniedNativeTokenEmission;
        }

        if (!request.DescriptorValidated)
        {
            return VirtualizationEmissionDecision.DeniedUnvalidatedDescriptorEmission;
        }

        if (!request.IsCompatibilityFrontend || !request.IsGeneratedProjection)
        {
            return VirtualizationEmissionDecision.DeniedDirectSubstrateEmission;
        }

        return VirtualizationEmissionDecision.AllowedCompatibilityProjection;
    }

    public bool CanEmit(VirtualizationEmissionRequest request) =>
        Evaluate(request) == VirtualizationEmissionDecision.AllowedCompatibilityProjection;
}
