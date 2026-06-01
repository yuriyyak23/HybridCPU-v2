# VMCS Projection And Field Access

VMCS and VMCSv2 are compatibility projections. They are not the runtime substrate, not a mutable backing store, and not the migration authority.

## Projection Schema

The generated projection schema is in:

- `CloseToRTL/Core/Virtualization/Compatibility/Generated/VmcsProjection/VmcsFieldProjectionSchema.cs`

The schema records:

- `VmcsField`
- projection owner
- evidence visibility class
- access policy
- migration policy
- generated-alias flag

The owner field is the important part. It points away from VMCS and toward the neutral owner. A schema owner is necessary, but it is not by itself a value source.

## Projection Owners

Current projection owner categories include:

- `ExecutionDomainDescriptor`
- `MemoryDomainDescriptor`
- `CompletionRecord`
- `CompatibilityControlDescriptor`

Examples:

- `GuestPc`, `GuestSp`, and `GuestFlags` project from an execution-domain read-only state view when that view is materialized;
- `GuestCr0` and `GuestCr4` are schema-owned by execution descriptors but remain denied until neutral privileged execution-state semantics exist;
- `GuestCr3`, `EptPointer`, `Vpid`, and `Cr3TargetCount` project from memory-domain descriptors;
- `HostCr3` remains denied because no neutral host-address-space owner exists;
- `HostPc`, `HostSp`, `HostFlags`, and `HostCr0` remain denied because no neutral host-execution owner exists;
- exit reason, exit qualification, guest physical address, and EPT violation qualification project from completion records;
- VMX control fields are owned by compatibility-control descriptors, but their VMREAD values remain denied until a field-by-field neutral control-bit value contract exists.

## Access Policy

The current schema exposes read-only compatibility projection for known fields. `CanWrite` is fail-closed and returns false. A field appearing in the schema is not writable unless a neutral owner explicitly admits a future write path.

All current VMCS writes remain denied. VMREAD value projection is partial and field-by-field; it is not feature-complete VMREAD backend execution.

## VMREAD

Current VMREAD value projection is:

```text
VMREAD opcode
  -> VmxCompatDecodeBoundary
  -> VmxCompatProjectionService
  -> RuntimeBoundaryAdmissionService(ReadCompatibilityProjection)
  -> VmcsFieldProjectionSchema owner lookup
  -> VmcsReadOnlyValueProjectionService
  -> neutral owner value source
  -> evidence/access policy
  -> generated read-only value projection or explicit denial
```

`VmcsV2Descriptor.TryReadScalarField` is not the current admitted value path. It remains denied compatibility ABI / historical fence vocabulary, not a backing store fallback.

## Current VMREAD Field Status

| Field group | Current status | Neutral source or denial reason |
|---|---|---|
| `ExitReason`, `ExitQualification`, `GuestPhysicalAddress`, `EptViolationQualification` | Projected read-only | Neutral `CompletionRecord` through `CompletionProjectionService` |
| `GuestCr3`, `EptPointer` | Projected read-only | `MemoryDomainDescriptor.TryCreateReadOnlyTranslationView()` |
| `Vpid` | Projected read-only only when tagging is enabled and non-zero | `MemoryDomainReadOnlyTranslationView.AddressSpaceTag` |
| `Cr3TargetCount` | Projected read-only | `MemoryDomainReadOnlyTranslationView.AddressSpaceTargetCount` |
| `GuestPc`, `GuestSp`, `GuestFlags` | Projected read-only only when materialized | `ExecutionDomainDescriptor.TryCreateReadOnlyStateView()` / `ExecutionDomainReadOnlyStateView` |
| `GuestCr0`, `GuestCr4` | Denied | `PrivilegedExecutionStateProjectionDenied`; no neutral privileged execution-state owner/value source |
| `HostPc`, `HostSp`, `HostFlags`, `HostCr0` | Denied | `HostExecutionStateOwnerMissing`; no neutral host-execution owner |
| `HostCr3` | Denied | `HostAddressSpaceOwnerMissing`; guest/domain address-space root cannot be reused as host CR3 |
| Compatibility-control fields | Denied | `CompatibilityControlValueProjectionDenied`; no admitted neutral control-bit VMREAD contract |
| Unknown fields | Denied | Not in generated VMCS field projection schema |
| Writes | Denied | No neutral write owner |

## Completion-Owned Fields

Fields such as `ExitReason`, `ExitQualification`, `GuestPhysicalAddress`, and `EptViolationQualification` are recomputed completion projections. They must be derived from completion records that passed neutral publication rules. They must not be stored as mutable VMCS state or serialized as independent checkpoint authority.

## Memory-Owned Fields

Memory-owned VMREAD values come from `MemoryDomainReadOnlyTranslationView`, not from VMCS scalar storage. `GuestCr3` is the guest/domain address-space root. `EptPointer` requires an owned second-stage root. `Vpid` requires neutral address-space tagging. `Cr3TargetCount` is the neutral address-space target count.

## Execution-Owned Fields

Execution-owned `GuestPc`, `GuestSp`, and `GuestFlags` come only from `ExecutionDomainReadOnlyStateView` when that state is materialized. The view carries materialization metadata such as `IsMaterialized`, `HasCompleteGuestPcSpFlags`, and `StateEpoch`; that metadata is not VMREAD value projection.

Control-like execution fields remain closed. `GuestCr0` and `GuestCr4` require a separate neutral privileged execution-state design before they can become read-only compatibility values.

## Removed Authority

The removed/fenced VMCSv2 mutable authority includes:

- descriptor guest-state mutators;
- host-evidence mutators;
- root/NPT/bundle/event/debug mutators;
- residual vector/dirty/security/capability backing state;
- active VMCS pointer state;
- VMCS field store behavior.

The only acceptable VMCS role is generated/read-only/denied compatibility projection.
