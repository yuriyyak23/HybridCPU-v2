using System;
using YAKSys_Hybrid_CPU.Core.Nested;
using YAKSys_Hybrid_CPU.Core.Vmcs.V2;

namespace YAKSys_Hybrid_CPU.Core;

public sealed class ShadowVmcsNestedProjectionService : INestedProjectionService
{
    public const string CompatibilityBridgePath =
        "Core/VMX/Compatibility/Generated/VmcsProjection/ShadowVmcsNestedProjectionService.cs";

    public static bool IsRetirementFenced => true;

    public ShadowVmcsNestedProjectionService(VmcsV2Descriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
    }

    public bool TryEnable(
        NestedDomainDescriptor domain,
        NestedEnablementRequest request,
        out NestedValidationResult validation)
    {
        ArgumentNullException.ThrowIfNull(domain);
        validation = NestedValidationResult.Fail(
            NestedValidationCode.CompatibilityProjectionFailed,
            "Shadow VMCS block was removed without replacement; compatibility nested admission cannot bypass the neutral nested projection/checkpoint service.");
        return false;
    }

    public void Disable(NestedDomainDescriptor domain)
    {
        ArgumentNullException.ThrowIfNull(domain);
    }
}
