using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU.Compiler.Core.IR;

/// <summary>
/// Result of class-level capacity analysis for a candidate instruction group.
/// Each pair (XxxCount, XxxCapacity) describes how many instructions of that class
/// are present vs. the maximum number of lanes available.
/// </summary>
public sealed record IrClassCapacityResult(
    int AluCount, int AluCapacity,
    int LsuCount, int LsuCapacity,
    int DmaStreamCount, int DmaStreamCapacity,
    int BranchControlCount, int BranchControlCapacity,
    int SystemSingletonCount, int SystemSingletonCapacity,
    bool HasAliasedLaneConflict,
    bool IsWithinCapacity)
{
    /// <summary>
    /// Returns the slot classes whose instruction count exceeds capacity.
    /// </summary>
    public IReadOnlyList<SlotClass> GetOvercommittedClasses()
    {
        var result = new List<SlotClass>();
        if (AluCount > AluCapacity) result.Add(SlotClass.AluClass);
        if (LsuCount > LsuCapacity) result.Add(SlotClass.LsuClass);
        if (DmaStreamCount > DmaStreamCapacity) result.Add(SlotClass.DmaStreamClass);
        if (BranchControlCount > BranchControlCapacity) result.Add(SlotClass.BranchControl);
        if (SystemSingletonCount > SystemSingletonCapacity) result.Add(SlotClass.SystemSingleton);
        return result;
    }
}
