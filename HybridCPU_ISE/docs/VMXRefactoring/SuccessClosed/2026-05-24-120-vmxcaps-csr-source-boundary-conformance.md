# Closed task: VmxCaps CSR source boundary conformance

Date: 2026-05-24

Rule / basis:
- `VmxCaps` is a frozen CSR compatibility alias and projection surface, not an authority source.
- Capability authority must flow from `CapabilityDescriptorSet`.
- Static conformance must prevent reintroducing CSR-backed capability source paths into active `Core/VMX`.

Changed:
- Extended `CoreVmxAuthorityBoundaryContract` with `DirectVmxCapsCsrSource`.
- `CsrAddresses.VmxCaps` is now allowed in active `Core/VMX` only for the compatibility projection CSR address and frozen CSR alias table.
- Existing `CoreVmxAuthorityBoundaryTests` now covers this marker through the same source scan.

Verified:
- Ran `dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore`.
- Ran `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-restore --filter FullyQualifiedName~CoreVmxAuthorityBoundaryTests`.

Build result:
- Main project build succeeded.
- 0 errors.
- 54 pre-existing warnings.

Test result:
- Passed: 1.
- Failed: 0.
