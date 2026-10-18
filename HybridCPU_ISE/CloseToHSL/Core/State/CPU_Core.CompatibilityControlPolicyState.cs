using YAKSys_Hybrid_CPU.Core;

namespace YAKSys_Hybrid_CPU;

public partial struct Processor
{
    public sealed partial class CPU_Core
    {
        internal CompatibilityControlPolicyOwner? CompatibilityControlPolicyOwner =>
            Runtime.CompatibilityControlPolicy.Owner;

        internal CompatibilityControlPolicySnapshot? CaptureCompatibilityControlPolicy() =>
            Runtime.CompatibilityControlPolicy.Owner?.CaptureCurrent();

        internal CompatibilityControlPolicySnapshot RebindCompatibilityControlPolicy(
            ulong domainIdentity) =>
            Runtime.CompatibilityControlPolicy.Owner?.RebindDomain(domainIdentity) ??
            throw new InvalidOperationException(
                "Compatibility-control policy owner is not configured for this core.");

        internal CompatibilityControlPolicySnapshot ReplaceCompatibilityControlPolicy(
            CompatibilityEventRoutingPolicy policy) =>
            Runtime.CompatibilityControlPolicy.Owner?.ReplacePolicy(policy) ??
            throw new InvalidOperationException(
                "Compatibility-control policy owner is not configured for this core.");
    }
}
