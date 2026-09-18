# Closed task: legacy CSR-backed VmxCaps source quarantined

Date: 2026-05-24

Rule / basis:
- `VmxCaps` must be a compatibility alias/projection over `CapabilityDescriptorSet`, not an active source of truth in `Core/VMX`.
- Legacy CSR fallback paths belong in `Legacy/VMX` until rewritten or removed.
- Capability descriptor authority must stay visible in the active model.

Changed:
- Removed `LegacyCsrBackedVmxCapabilityDescriptorSource` from `Core/VMX/Compatibility/Generated/CsrProjection/VmxCapabilityDescriptorSource.cs`.
- Kept `IVmxCapabilityDescriptorSource` and `StaticVmxCapabilityDescriptorSource` in `Core/VMX`.
- Added `Legacy/VMX/Compatibility/Generated/CsrProjection/LegacyCsrBackedVmxCapabilityDescriptorSource.cs` for legacy compatibility callers.
- Kept namespace and behavior unchanged for existing legacy callers.

Verified:
- Ran `dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore`.

Build result:
- Succeeded.
- 0 errors.
- 54 pre-existing warnings.
