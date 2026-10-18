namespace YAKSys_Hybrid_CPU.Core;

/// <summary>
/// Storage-only containment for the existing per-core virtual-thread FSM and
/// VMX compatibility gates. Existing guarded transition, VMX execution and
/// retire/control methods retain authority.
/// </summary>
internal sealed class VirtualThreadControlState
{
    internal bool IsVmxRoot;
    internal PipelineState[] PipelineStates = null!;
    internal bool VmxExecutionPlaneWired;
}
