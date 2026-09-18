# Closed: host evidence non-leak contract

Date: 2026-05-24

## Rule / basis

Host-owned runtime evidence is never guest ABI. Scheduler evidence, backend binding evidence, and native token evidence are runtime-owned facts and must not be published through VMCS, VMX CSR, sideband transport, or generated compatibility projection.

## Changed

- Replaced the empty `HostEvidenceNonLeakContract` placeholder with a minimal conformance helper.
- Added host-owned evidence class detection for runtime, scheduler, backend binding, and native token evidence.
- Added validation for guest-visible projection decisions.
- Added sideband envelope checks for evidence, completion, and descriptor transport.

## Verified

Ran:

```text
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Build result

Build succeeded with existing warnings and `0 Error(s)`.
