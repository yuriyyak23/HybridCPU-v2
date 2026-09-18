# Closed task: nested projection service facade

Date: 2026-05-24

Rule / basis:
- Nested model must move toward domain composition rather than public VMCS12/VMCS02/ShadowVMCS architecture.
- VMCS / ShadowVMCS vocabulary may remain only as compatibility projection implementation.
- Substrate controllers should depend on nested domain descriptors and projection services, not direct VMCS state.

Changed:
- Added `Core/VMX/Substrate/Nested/Projection/INestedProjectionService.cs`.
- Added `Core/VMX/Compatibility/Generated/VmcsProjection/ShadowVmcsNestedProjectionService.cs`.
- Updated `Core/VMX/Substrate/Nested/Policies/NestedDomainController.cs` so the canonical path takes `NestedDomainDescriptor` plus `INestedProjectionService`.
- Preserved the existing `VmcsV2Descriptor` overload as a compatibility bridge.
- Moved direct `descriptor.ShadowVmcs` calls out of `NestedDomainController` and into the compatibility projection service.

Verified:
- Ran `dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore`.

Build result:
- Succeeded.
- 0 errors.
- 54 pre-existing warnings.
