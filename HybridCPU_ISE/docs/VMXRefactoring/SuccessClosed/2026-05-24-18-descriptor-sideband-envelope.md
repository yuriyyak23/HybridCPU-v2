# Closed: descriptor sideband envelope

Date: 2026-05-24

## Rule / basis

The authoritative state is generic domain/descriptor/capability runtime substrate, not VMCS, VMCSv2, VMX CSR, or VMX instruction plane. VMCS/VMCSv2 are compatibility projections. Descriptor transport must not publish unvalidated or host-owned descriptor facts as guest ABI.

## Changed

- Replaced the empty `DescriptorSidebandEnvelope` placeholder with a minimal immutable descriptor transport envelope.
- Added descriptor kind, id, epoch, hash, validation state, and visibility class.
- Added explicit compatibility projection classification.
- Added fail-closed guest exposure through `EvidencePolicyDescriptor`.

## Verified

Ran:

```text
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Build result

Build succeeded with existing warnings and `0 Error(s)`.
