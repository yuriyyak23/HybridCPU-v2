# Phase 13 - Lane7 Cache, TLB, And IOMMU

## Goal

Promote Lane7 maintenance rows only when non-VMX cache/TLB/IOMMU coherency,
privilege, retire, replay, and virtualization boundaries are explicit.

## Phase 13 Closure

Phase 13 is closed only as a negative production decision gate. The package
strengthens maintenance, replay, and virtualization-boundary marker partials for
the listed rows, but it does not open executable closure.

No numeric opcode allocation, decoder/encoder ABI, `InstructionIR` projection,
registry/materializer row, typed Lane7 maintenance MicroOp, scheduler binding,
execution/capture path, retire side-effect publication, replay/rollback engine,
golden artifact, compiler helper, generic fence fallback, Lane6/Lane7
accelerator fallback, external backend fallback, or VMX-specific path is
published by this phase.

## Production Path Overlay

Use the Lane7 full production path in `README.md`. Maintenance rows require
scope/range ABI, privilege/admission legality, side-effect publication, replay
ordering, memory/DMA-domain evidence, and golden conformance. VMX-compatible
projection is required only for guest-visible translation/cache effects,
VMCS-visible state, VM-exit, VPID/EPT/NPT-like policy, migration/checkpoint, or
IOMMU/DMA authority.

## Instructions / Contours

- Translation fence: `SFENCE.VMA`.
- Cache maintenance: `ICACHE_INVAL`, `DCACHE_CLEAN`, `DCACHE_INVAL`, `DCACHE_FLUSH`.
- IOMMU maintenance: `IOTLB_INV`, `IOMMU_FENCE`.

## Existing Partial Files

- `Lane07SystemControl\TranslationFences\SfenceVmaInstruction.cs`
- `Lane07SystemControl\CacheMaintenance\*.cs`
- `Lane07SystemControl\Iommu\IotlbInvInstruction.cs`
- `Lane07SystemControl\Iommu\IommuFenceInstruction.cs`
- Aggregate metadata compatibility: `Lane07SystemControl\NonVmxLane07DeferredTemplates.cs`
- Phase 13 marker partials:
  - `*.MaintenanceContract.cs`
  - `*.ReplayContract.cs`
  - `*.VirtualizationBoundary.cs`

## New Partial Files Allowed

- `*.MaintenanceContract.cs` for privilege/coherency/no-execution metadata.
- `*.ReplayContract.cs` for invalidation/fence rollback notes.
- `*.VirtualizationBoundary.cs` for future policy markers only.

## Local CloseToRTL Logic

Production/local partials may document maintenance scope, address/range sideband requirements, privilege policy placeholders, no host-evidence leak, and fail-closed execution authority. Maintenance execution opens only when the Lane7 production path closes coherency, privilege, retire, replay, and virtualization policy.

## Production Evidence Gates

MMU/TLB/cache/coherency model, IOMMU domain ownership, address/range ABI, decoder/encoder ABI, `InstructionIR`, Lane7 materializer, typed MicroOps, runtime maintenance engine, retire-owned side-effect publication, rollback/replay invalidation model, virtualization policy, conformance, and golden/no-emission tests.

## Metadata Constants

Preserve `Lane7TranslationFenceDeferred`, `Lane7CacheMaintenanceDeferred`, `Lane7IommuMaintenanceDeferred`, `RequiresRetireOwnedPublication`, `NoGuestVisibleHostEvidence`, `IsExecutable=false`, and `CompilerHelperAllowed=false`. Leaf files that are anchor-only must not be treated as implemented because aggregate metadata exists.

## Evidence Chain Stories

- Decoder/encoder ABI: production gate; range/scope encoding must be explicit.
- InstructionIR/projection: production gate; maintenance scope must be typed.
- Typed MicroOp/materializer: production gate for maintenance MicroOps.
- Execute/capture semantics: absent until coherency model closes.
- Retire/writeback/side effects: retire-owned maintenance side effects only; no speculative publication.
- Replay/rollback/conformance: invalidation rollback, ordering, domain ownership, range corner cases, and no-emission tests.

## Boundaries

- Vector VLM: not applicable.
- Lane6: IOMMU rows may interact with DMA tokens only through a production authority gate.
- Lane7/VMX: privileged guest-visible effects and IOMMU/cache state require virtualization policy before execution.
- No-emission: mandatory.

## Risks

- Conflating non-VMX `SFENCE.VMA` with VMX/EPT/NPT semantics.
- Publishing host-owned cache/IOMMU evidence as guest state.
- Maintenance side effects occurring before retire.

## Closure Criteria

- Phase 13 is closed when marker partials and focused tests prove the rows
  remain fail-closed and non-executable. This is not production execution
  closure.
- A production package may promote each row only after coherency, domain ownership, retire, replay, golden, and virtualization-boundary policy close.
- Local partials without that package strengthen deferral/contract metadata only.

## Prohibited Actions

- Execute cache/TLB/IOMMU maintenance only through the Lane7 production path.
- Add VMX-compatible projection, VM-exit, or VPID/EPT/NPT semantics only by explicit virtualization-boundary decision.
