
namespace HybridCPU.Compiler.Core.IR;

/// <summary>
/// Maps compiler-side <see cref="IrResourceClass"/> to ISE-side <see cref="IrSlotClass"/>.
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
    public static IrSlotClass ToSlotClass(IrResourceClass resourceClass) => resourceClass switch
    {
        IrResourceClass.ScalarAlu => IrSlotClass.AluClass,
        IrResourceClass.VectorAlu => IrSlotClass.AluClass,
        IrResourceClass.LoadStore => IrSlotClass.LsuClass,
        IrResourceClass.ControlFlow => IrSlotClass.BranchControl,
        IrResourceClass.System => IrSlotClass.SystemSingleton,
        IrResourceClass.DmaStream => IrSlotClass.DmaStreamClass,
        IrResourceClass.Unknown => IrSlotClass.Unclassified,
        _ => IrSlotClass.Unclassified
    };

    /// <summary>
    /// Derives compiler-side binding kind from resource class and serialization.
    /// </summary>
    public static IrSlotBindingKind DerivePinningKind(
        IrResourceClass resourceClass,
        IrSerializationKind serialization) => resourceClass switch
        {
            IrResourceClass.ControlFlow => IrSlotBindingKind.HardPinned,
            IrResourceClass.System => IrSlotBindingKind.HardPinned,
            IrResourceClass.DmaStream => IrSlotBindingKind.SingletonClass,
            _ => (serialization & IrSerializationKind.ExclusiveCycle) != 0
                ? IrSlotBindingKind.HardPinned
                : IrSlotBindingKind.ClassFlexible
        };

}
