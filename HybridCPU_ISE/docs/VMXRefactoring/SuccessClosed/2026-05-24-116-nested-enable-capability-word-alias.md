# Closed task: nested enablement capability word alias

Date: 2026-05-24

Rule / basis:
- Capability grants and published capability descriptors are the authority.
- `VmxCaps` vocabulary may remain as compatibility projection / ABI input, but substrate checks should move toward generic capability wording.
- Nested model should move toward domain composition and capability grants rather than VMX-owned state.

Changed:
- Updated `Core/VMX/Substrate/Nested/Policies/NestedDomainController.cs`.
- Added `NestedEnablementRequest.PublishedCapabilityWord` as a generic alias over the compatibility `PublishedVmxCaps` input.
- Routed `HasExplicitVmxCapability` through `PublishedCapabilityWord`.
- Kept public record shape, compatibility field name, and behavior unchanged.

Verified:
- Ran `dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore`.

Build result:
- Succeeded.
- 0 errors.
- 54 pre-existing warnings.
