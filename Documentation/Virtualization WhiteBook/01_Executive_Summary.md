# Executive Summary

HybridCPU ISE virtualization is a neutral runtime architecture with a VMX-compatible frontend. The runtime owns execution domains, memory domains, I/O domains, capabilities, evidence, nested composition, trap policy, completion records, and retire publication. VMX is preserved as frozen compatibility ABI and projection vocabulary.

The current implementation deliberately avoids the older model where VMX/VMCS objects behaved like the virtualization substrate. The architectural substrate is now carried by runtime descriptors and services under `CloseToRTL/Core/Runtime`. VMX-facing code under `CloseToRTL/Core/Virtualization/Compatibility` decodes frozen opcodes, checks alias/projection legality, calls `RuntimeBoundaryAdmissionService`, and then either projects neutral runtime state or denies/fails closed.

## One-Sentence Model

VMX can name a compatibility result after neutral authority has decided it; VMX cannot be the authority that decides it.

## Core Flow

```text
ISA / compatibility opcode
  -> VMX decode boundary
  -> compatibility alias / projection validation
  -> RuntimeBoundaryAdmissionService
  -> neutral runtime owner
  -> neutral completion / trap / retire publication
  -> VMX-compatible projection
```

This flow is visible in the VMREAD and VMCALL slices:

- VMREAD: decode `VMREAD`, validate the frozen alias, admit `ReadCompatibilityProjection`, look up `VmcsFieldProjectionSchema`, then project only when `CompletionRecord`, `MemoryDomainDescriptor`, or `ExecutionDomainDescriptor` exposes an explicit read-only neutral value source. This is partial generated/read-only projection, not VMCS backend execution.
- VMCALL trap projection: decode `VMCALL`, validate the alias, admit `ProjectCompatibilityTrap`, evaluate neutral trap policy, form `NeutralTrapResult`, evaluate hypercall backend admission and completion route policy, map through `VmxTrapProjectionMapper`, and deny backend publication because production still has no neutral hypercall backend owner.

## Implemented Authority Split

The authority split has three layers:

- Neutral runtime authority: `DomainRuntimeContext`, `RootAuthorityDescriptor`, `DomainBoundaryDescriptor`, `CapabilityBoundaryRequirement`, `EvidenceBoundaryRequirement`, `TrapPolicyDescriptor`, `TrapPolicyBitmap`, `NeutralTrapResult`, `CompletionRecord`, and publication fences.
- Compatibility projection vocabulary: `VmExitReason`, `VmxExitQualification`, `TrapDecision`, `VmxCompletionProjection`, `VmcsField`, `VmxOperationKind`, and generated alias schemas.
- Denied/fail-closed compatibility paths: VMCS scalar writes, VMCS mutable helpers, VMCALL backend execution, arbitrary VMX intercept retirement, and any frontend attempt to mutate authoritative runtime state.

## Current State Snapshot

- Physical legacy VMX runtime is absent.
- `VmxExecutionUnit.cs`, `VmcsManager.cs`, and `IVmcsManager.cs` are absent in the active VMX runtime path.
- `Core/VMX/Substrate` authority has been removed from the current model.
- VMCSv2 mutable helper authority has been fenced or removed.
- Generated/frontend projection inventory is treated as compatibility artifact inventory, not as runtime state authority.
- VMREAD value projection is open only for completion-owned fields, memory-owned `GuestCr3`/`EptPointer`/`Vpid`/`Cr3TargetCount`, and execution-owned `GuestPc`/`GuestSp`/`GuestFlags`.
- `GuestCr0`, `GuestCr4`, host execution aliases, `HostCr3`, compatibility-control fields, unknown fields, and all writes remain explicitly denied.
- Trap projection now has a neutral result, hypercall backend admission policy, completion route policy, and retire/completion publication fences.
- Completion and retire are structurally separable: the future-gated neutral fence can publish a completion record with `CompletionPublishedRetireDenied` while retire remains false. Neither positive route descriptor is connected to VMX frontend.

## Why The Model Matters

This architecture lets HybridCPU keep VMX compatibility contracts without letting historical VMX naming contaminate runtime legality. It also keeps future successful virtualization paths honest: a new path must first land under a neutral owner, pass runtime admission, publish neutral completion, and only then expose a VMX-compatible projection.
