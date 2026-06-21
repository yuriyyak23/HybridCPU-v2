// Iteration 14A materialized Lane7 counter/hint metadata into per-instruction leaf partial files.

namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane07SystemControl.TranslationFences
{
    public sealed partial class SfenceVmaInstruction { public const string Mnemonic = "SFENCE.VMA"; public const string EvidenceBoundary = "Lane7TranslationFenceDeferred"; public const bool RequiresRetireOwnedPublication = true; public const bool IsExecutable = false; public const bool CompilerHelperAllowed = false; }
}

namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane07SystemControl.CacheMaintenance
{
    public sealed partial class IcacheInvalInstruction { public const string Mnemonic = "ICACHE_INVAL"; public const string EvidenceBoundary = "Lane7CacheMaintenanceDeferred"; public const bool RequiresRetireOwnedPublication = true; public const bool IsExecutable = false; public const bool CompilerHelperAllowed = false; }
    public sealed partial class DcacheCleanInstruction { public const string Mnemonic = "DCACHE_CLEAN"; public const string EvidenceBoundary = "Lane7CacheMaintenanceDeferred"; public const bool RequiresRetireOwnedPublication = true; public const bool IsExecutable = false; public const bool CompilerHelperAllowed = false; }
    public sealed partial class DcacheInvalInstruction { public const string Mnemonic = "DCACHE_INVAL"; public const string EvidenceBoundary = "Lane7CacheMaintenanceDeferred"; public const bool RequiresRetireOwnedPublication = true; public const bool IsExecutable = false; public const bool CompilerHelperAllowed = false; }
    public sealed partial class DcacheFlushInstruction { public const string Mnemonic = "DCACHE_FLUSH"; public const string EvidenceBoundary = "Lane7CacheMaintenanceDeferred"; public const bool RequiresRetireOwnedPublication = true; public const bool IsExecutable = false; public const bool CompilerHelperAllowed = false; }
}

namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane07SystemControl.Iommu
{
    public sealed partial class IotlbInvInstruction { public const string Mnemonic = "IOTLB_INV"; public const string EvidenceBoundary = "Lane7IommuMaintenanceDeferred"; public const bool RequiresRetireOwnedPublication = true; public const bool IsExecutable = false; public const bool CompilerHelperAllowed = false; }
    public sealed partial class IommuFenceInstruction { public const string Mnemonic = "IOMMU_FENCE"; public const string EvidenceBoundary = "Lane7IommuMaintenanceDeferred"; public const bool RequiresRetireOwnedPublication = true; public const bool IsExecutable = false; public const bool CompilerHelperAllowed = false; }
}

namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane07SystemControl.AcceleratorControl.Topology
{
    public sealed partial class AccelQueryAbiInstruction { public const string Mnemonic = "ACCEL_QUERY_ABI"; public const string EvidenceBoundary = "Lane7AcceleratorControlDeferred"; public const bool NoHostEvidenceLeak = true; public const bool IsExecutable = false; public const bool CompilerHelperAllowed = false; }
    public sealed partial class AccelQueryTopologyInstruction { public const string Mnemonic = "ACCEL_QUERY_TOPOLOGY"; public const string EvidenceBoundary = "Lane7AcceleratorControlDeferred"; public const bool NoHostEvidenceLeak = true; public const bool IsExecutable = false; public const bool CompilerHelperAllowed = false; }
}

namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane07SystemControl.AcceleratorControl.Lifecycle
{
    public sealed partial class AccelOpenInstruction { public const string Mnemonic = "ACCEL_OPEN"; public const string EvidenceBoundary = "Lane7AcceleratorControlDeferred"; public const bool RequiresRetireOwnedPublication = true; public const bool IsExecutable = false; public const bool CompilerHelperAllowed = false; }
    public sealed partial class AccelCloseInstruction { public const string Mnemonic = "ACCEL_CLOSE"; public const string EvidenceBoundary = "Lane7AcceleratorControlDeferred"; public const bool RequiresRetireOwnedPublication = true; public const bool IsExecutable = false; public const bool CompilerHelperAllowed = false; }
}

namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane07SystemControl.AcceleratorControl.QueueBinding
{
    public sealed partial class AccelBindQueueInstruction { public const string Mnemonic = "ACCEL_BIND_QUEUE"; public const string EvidenceBoundary = "Lane7AcceleratorControlDeferred"; public const bool RequiresRetireOwnedPublication = true; public const bool IsExecutable = false; public const bool CompilerHelperAllowed = false; }
    public sealed partial class AccelUnbindQueueInstruction { public const string Mnemonic = "ACCEL_UNBIND_QUEUE"; public const string EvidenceBoundary = "Lane7AcceleratorControlDeferred"; public const bool RequiresRetireOwnedPublication = true; public const bool IsExecutable = false; public const bool CompilerHelperAllowed = false; }
}
