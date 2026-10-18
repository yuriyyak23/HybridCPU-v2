namespace YAKSys_Hybrid_CPU.Core;

internal sealed class CompatibilityControlPolicyState
{
    internal CompatibilityControlPolicyOwner? Owner;
    internal NeutralInterruptRoutingPolicyConsumer InterruptRoutingConsumer = new();
}
