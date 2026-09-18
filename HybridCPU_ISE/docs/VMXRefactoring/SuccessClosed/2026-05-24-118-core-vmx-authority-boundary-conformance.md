# Closed task: Core VMX authority boundary conformance

Date: 2026-05-24

Rule / basis:
- Active `Core/VMX` code must not regain direct VMCS / VMX-IOMMU authority calls.
- Shadow VMCS may remain only in compatibility generated projection implementation.
- Static conformance should catch no-emission / no-authority regressions early.

Changed:
- Added `Core/VMX/Conformance/AuthorityBoundary/CoreVmxAuthorityBoundaryContract.cs`.
- Added `HybridCPU_ISE.Tests/VmxRefactoring/CoreVmxAuthorityBoundaryTests.cs`.
- The test scans active `Core/VMX` source for direct `.ShadowVmcs.` member access, VMX-shaped IOMMU hooks, direct VMCS field access, and VMCS-derived translation authority markers.
- The test skips conformance files themselves and allows `.ShadowVmcs.` only in `ShadowVmcsNestedProjectionService.cs`.

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
