namespace HybridCPU.ExternalRuntime.Contracts;

/// <summary>Explicit refusal before this admission allocates backing. The original
/// request is permanently latched; readiness recovery cannot admit it later.</summary>
public sealed record ExternalOriginalParentDeviceNoEffect(ExternalOriginalParentDeviceRequest Request,
    bool NoEffect, bool NoFurtherAdmission, HybridCpuExternalContractVersion ContractVersion, ulong ManifestGeneration);
public interface IHybridCpuOriginalParentDeviceNoEffectRuntimeV1
{
    ExternalOriginalParentDeviceNoEffect? ResolveOriginalParentDeviceNoEffect(ExternalOriginalParentDeviceRequest request);
}
