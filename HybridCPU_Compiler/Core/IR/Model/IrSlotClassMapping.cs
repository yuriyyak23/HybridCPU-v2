using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU.Compiler.Core.IR;

/// <summary>
/// Maps compiler-side <see cref="IrResourceClass"/> to ISE-side <see cref="SlotClass"/>.
/// Single source of truth for the cross-boundary vocabulary.
/// </summary>
/// <remarks>
/// <para>
/// Compiler-internal lane assignment (<see cref="IrIssueSlotMask"/>) may diverge
/// from ISE physical lane masks. This mapper resolves the divergence at emission time.
/// </para>
/// <para>
/// Key divergences resolved here:
/// <list type="bullet">
/// <item><c>VectorAlu</c> → <c>AluClass</c> (compiler uses lanes 4–5 internally, ISE uses 0–3)</item>
/// <item><c>LoadStore</c> → <c>LsuClass</c> (compiler uses lane 6 internally, ISE uses 4–5)</item>
/// </list>
/// </para>
/// </remarks>
public static class IrSlotClassMapping
{
    /// <summary>
    /// Maps compiler resource class to ISE slot class.
    /// </summary>
    public static SlotClass ToSlotClass(IrResourceClass resourceClass) => resourceClass switch
    {
        IrResourceClass.ScalarAlu   => SlotClass.AluClass,
        IrResourceClass.VectorAlu   => SlotClass.AluClass,
        IrResourceClass.LoadStore   => SlotClass.LsuClass,
        IrResourceClass.ControlFlow => SlotClass.BranchControl,
        IrResourceClass.System      => SlotClass.SystemSingleton,
        IrResourceClass.DmaStream   => SlotClass.DmaStreamClass,
        IrResourceClass.Unknown     => SlotClass.Unclassified,
        _ => SlotClass.Unclassified
    };

    /// <summary>
    /// Derives compiler-side binding kind from resource class and serialization.
    /// </summary>
    public static IrSlotBindingKind DerivePinningKind(
        IrResourceClass resourceClass,
        IrSerializationKind serialization) => resourceClass switch
    {
        IrResourceClass.ControlFlow => IrSlotBindingKind.HardPinned,
        IrResourceClass.System      => IrSlotBindingKind.HardPinned,
        IrResourceClass.DmaStream   => IrSlotBindingKind.SingletonClass,
        _ => (serialization & IrSerializationKind.ExclusiveCycle) != 0
            ? IrSlotBindingKind.HardPinned
            : IrSlotBindingKind.ClassFlexible
    };

    /// <summary>
    /// Maps compiler binding kind to ISE runtime pinning kind.
    /// </summary>
    /// <remarks>
    /// <see cref="IrSlotBindingKind.SingletonClass"/> maps to <see cref="SlotPinningKind.ClassFlexible"/>
    /// because the ISE runtime uses a 2-valued pinning model. The singleton constraint for
    /// <c>DmaStreamClass</c> (capacity=1, lane 6 only) is enforced by <see cref="SlotClassLaneMap"/>
    /// topology, not by pinning metadata. The runtime scheduler picks the lowest free lane in
    /// the class mask, which is always the sole available lane.
    /// </remarks>
    public static SlotPinningKind ToRuntimePinningKind(IrSlotBindingKind compilerKind) => compilerKind switch
    {
        IrSlotBindingKind.HardPinned     => SlotPinningKind.HardPinned,
        IrSlotBindingKind.ClassFlexible  => SlotPinningKind.ClassFlexible,
        IrSlotBindingKind.SingletonClass => SlotPinningKind.ClassFlexible,
        _ => SlotPinningKind.ClassFlexible
    };
}
