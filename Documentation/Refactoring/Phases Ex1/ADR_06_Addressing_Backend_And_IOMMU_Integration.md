# ADR 06: Addressing Backend And IOMMU Integration Gate

## Status

Proposed design gate.

This ADR is implementation-oriented, but it does not approve CPU/ISE code changes by itself. It does not make lane6 DSC, lane7 L7, DMAController, StreamEngine, or accelerator memory portals executable or IOMMU-integrated.

## Context

Phase 06 covers TASK-005: explicit physical versus IOMMU-translated backend selection for any future executable DSC and L7 memory access.

The current repository has IOMMU and burst-backend infrastructure, but current lane6 DSC runtime/helper execution uses physical main memory. Treating `IOMMUBurstBackend` as proof of executable DSC/L7 IOMMU integration would make descriptor ABI, owner/domain guard, device ID binding, translation authority, and fault mapping claims untrue.

## Current Contract

- Lane6 `DmaStreamComputeMicroOp` remains fail-closed and is not executable DSC ISA.
- `DmaStreamComputeDescriptorParser.ExecutionEnabled == false`.
- DSC1 remains immutable: ABI v1, `InlineContiguous`, `AllOrNone`, reserved fields rejected.
- DSC1 does not define an approved executable `AddressSpace` field.
- DSC1 reserved fields must not be reused for physical/IOMMU address-space semantics.
- `DmaStreamComputeRuntime.ExecuteToCommitPending` remains an explicit helper/model path, not ISA execution.
- Current DSC helper reads source ranges through `DmaStreamAcceleratorBackend`, which uses physical main memory.
- Current DSC token commit uses `Processor.MainMemoryArea` physical read/write helpers for all-or-none commit and rollback.
- `IBurstBackend` and `IOMMUBurstBackend` exist as infrastructure, but current DSC runtime/helper does not use them.
- `IOMMUBurstBackend.Read` and `Write` delegate to `Memory.IOMMU.ReadBurst` and `WriteBurst`.
- `IOMMUBurstBackend.RegisterAcceleratorDevice` and `InitiateAcceleratorDMA` remain fail-closed and are not production accelerator protocol.
- `DMAController.UseIOMMU` and `StreamEngine.BurstIO` are separate paths and do not prove lane6 DSC or L7 integration.
- L7 `ACCEL_*` carriers remain fail-closed/model-only for production ISA.
- Compiler/backend production lowering must not assume executable DSC/L7 or IOMMU-translated DSC addresses.

## Decision Under Review

The decision under review is whether future executable DSC/L7 memory access may select between physical and IOMMU-translated addressing.

### Recommended Decision

Adopt an explicit address-space and backend-selection architecture as a mandatory prerequisite for any executable DSC/L7 memory path.

The recommended position is:

- Preserve the current DSC helper physical path until a separate executable feature gate changes it.
- Introduce a future `AddressSpace` contract with at least `Physical` and `IOMMUTranslated`.
- Place executable address-space selection only in DSC2 or a capability-gated extension block, not in DSC1 reserved fields.
- Require an explicit backend resolver for future executable paths.
- Bind owner, domain, device ID, address-space mode, and mapping epoch at issue/admission before any translated memory access.
- Reject unsupported or ambiguous address-space/backend combinations. Do not silently fall back from IOMMU-translated to physical memory.
- Map translation, permission, domain, device, alignment, bounds, and revocation failures into precise token fault records before any executable claim.

## Accepted Direction

### AddressSpace Model

Future executable descriptors must declare an explicit address-space mode:

- `Physical`: addresses are physical main-memory addresses under the current-style physical helper semantics.
- `IOMMUTranslated`: addresses are IO virtual addresses translated through an approved IOMMU backend and approved device/domain binding.
- Future modes are reserved for later ADRs and must reject until explicitly approved.

For the first executable design, the recommended MVP is a descriptor-wide address-space selector. Mixed per-range address spaces are deferred until a later ADR proves footprint normalization, conflict service, fault priority, and compiler lowering rules for mixed modes.

### Descriptor ABI Boundary

Address-space selection must be encoded only in:

- DSC2; or
- a capability-gated extension block whose version, size, flags, and compatibility rules are explicit.

It must not be encoded by reusing DSC1 reserved bits, DSC1 range table fields, lane6 carrier transport hints, or inferred thread/context state.

### Backend Resolver Contract

Future executable paths must use an explicit resolver with inputs equivalent to:

- descriptor ABI version and capability set;
- `AddressSpace`;
- read/write operation kind;
- normalized ranges;
- owner virtual thread, context, core, and pod;
- owner domain tag and active domain certificate;
- device ID and device role;
- mapping epoch or translation generation;
- token id and issue/admission sequence;
- requested access permissions.

The resolver must return an explicit result:

- selected physical backend;
- selected IOMMU backend;
- rejected unsupported mode;
- rejected capability mismatch;
- rejected owner/domain/device mismatch;
- translation or permission fault;
- mapping epoch or revocation fault;
- alignment, bounds, or backend device fault.

Boolean backend failure is not enough for executable architecture. The future resolver or backend wrapper must classify failures into precise fault kinds.

### Physical Backend Contract

A future physical backend wrapper may model the current physical semantics, but it must be explicit.

The physical path must:

- access physical main memory only;
- preserve all-or-none commit behavior until a later partial-success ADR changes it;
- preserve owner/domain guard checks;
- reject physical ranges outside main memory;
- not claim IOMMU isolation;
- not claim cache coherence;
- not become a silent fallback for failed IOMMU translation.

### IOMMU Backend Contract

The IOMMU-translated path must:

- call an approved IOMMU-backed backend with the bound device ID;
- apply read/write permission checks;
- apply owner/domain authority rules;
- record a mapping epoch or translation generation at issue/admission;
- validate or respond to mapping revocation before memory access and before commit;
- translate backend failures into `TranslationFault`, `PermissionFault`, `DomainViolation`, `DmaDeviceFault`, `AlignmentFault`, or `MemoryFault` as appropriate;
- avoid committing staged writes if translation, permission, revocation, or device validation fails;
- never retry through physical memory after IOMMU failure.

### Device ID Binding

Device ID binding must be architectural before IOMMU descriptors are accepted.

Future rules must specify:

- canonical lane6 DSC device identity;
- external L7 accelerator device identity;
- whether device IDs are per core, per pod, per accelerator instance, or per virtual context;
- how device IDs relate to owner virtual thread, context, and domain tags;
- who is authorized to bind a descriptor to a device ID;
- how stale or replayed device binding evidence is rejected;
- how device mismatch maps to a precise fault.

Device ID must not be inferred implicitly from thread ID, context ID, or lane number unless an approved binding rule states that exact mapping.

### Mapping Epoch And Revocation

Future executable tokens must capture mapping state at issue/admission.

If a mapping is changed, unmapped, permission-revoked, or domain-reassigned while a token is active, the architecture must choose one explicit response:

- serialize until the change is no longer racing;
- cancel the token;
- fault the token precisely;
- replay the token after revalidation.

Silent continuation with stale translation state is forbidden.

### Relationship To Ordering And Cache Phases

Address-space selection does not replace Phase 05 ordering/conflict service and does not replace Phase 09 cache protocol.

Executable IOMMU-backed memory access also requires:

- global conflict-service visibility for active translated and physical footprints;
- explicit fence/wait/poll interaction with translated tokens;
- explicit non-coherent flush/invalidate protocol before any cache visibility claim;
- a separate coherent-DMA ADR before any true coherence claim.

## Rejected Alternatives

### Alternative 1: Treat Existing IOMMUBurstBackend As Current DSC/L7 Integration

Rejected.

`IOMMUBurstBackend` proves infrastructure exists. It does not prove descriptor ABI, device binding, token admission, fault mapping, retire publication, ordering, or compiler lowering for executable DSC/L7.

### Alternative 2: Silent Fallback From IOMMU To Physical

Rejected.

Silent fallback violates isolation and makes translation/permission failures architecturally invisible.

### Alternative 3: Infer Device ID From Thread Or Context

Rejected unless a future ADR defines a precise binding rule.

Implicit inference would make owner/domain guards and IOMMU authority ambiguous.

### Alternative 4: Reuse DSC1 Reserved Fields

Rejected.

DSC1 is immutable and reserved fields are currently rejected. Address-space semantics belong in DSC2 or capability-gated extension blocks.

### Alternative 5: Route Current Runtime Helper Through IOMMU

Rejected for this phase.

Changing `DmaStreamComputeRuntime.ExecuteToCommitPending` to select IOMMU would not by itself define pipeline issue/admission, precise faults, ordering, mapping epoch, cancellation, or retire semantics.

### Alternative 6: Treat StreamEngine Or DMAController IOMMU Paths As DSC Backend Proof

Rejected.

StreamEngine and DMAController are architecturally separate from lane6 DSC and lane7 L7. Their helper paths do not establish DSC/L7 executable addressing semantics.

## Exact Non-Goals

- Do not implement CPU/ISE code in this ADR.
- Do not make lane6 DSC executable.
- Do not make lane7 L7 executable.
- Do not modify `DmaStreamComputeMicroOp.Execute`.
- Do not modify `DmaStreamComputeRuntime` to use `IBurstBackend`.
- Do not change the current physical helper path.
- Do not alter DSC1 ABI or reuse reserved fields.
- Do not claim current DSC addresses are IOMMU-translated.
- Do not claim async DMA overlap, cache coherence, or compiler production lowering.
- Do not define fake/test accelerator backends as production device protocol.

## Required Prerequisites Before Code

- Phase 02 executable DSC gate or Phase 10 executable L7 gate approval for the affected ISA surface.
- Phase 03 token store and issue/admission allocation boundary.
- Phase 04 precise fault publication and fault priority.
- Phase 05 global conflict service for executable overlap.
- Phase 07 DSC2 or capability-gated extension ABI for address-space fields.
- Phase 09 non-coherent cache flush/invalidate protocol for cache-visible memory paths.
- Phase 11 compiler/backend lowering contract.
- Phase 12 conformance and documentation migration tests.
- Explicit `AddressSpace` model.
- Physical backend wrapper contract.
- IOMMU backend contract with typed fault classification.
- Backend resolver contract.
- Owner/domain/device ID binding authority.
- Mapping epoch and revocation behavior.
- Capability discovery and unsupported-mode rejection.
- No-fallback policy tests.

## Compatibility Impact

Current behavior remains compatible because this ADR does not change code and does not change current DSC helper physical semantics.

Future executable physical descriptors can be made compatible with the current physical helper model only if they preserve owner/domain checks, all-or-none commit, precise faults, and explicit backend selection.

Future IOMMU-translated descriptors are not compatible with DSC1 and require a new ABI or extension gate. Compiler/backend must reject or avoid such descriptors until capabilities and conformance tests exist.

## Implementation Phases Enabled Only After Approval

Approval of this ADR would allow follow-on design work only:

- define `AddressSpace` in DSC2 or a capability-gated extension;
- define backend capability discovery;
- define a physical backend wrapper;
- define an IOMMU backend wrapper that can classify faults;
- define a backend resolver and no-fallback rejection path;
- bind owner/domain/device ID/mapping epoch at token issue/admission;
- route IOMMU failures into precise token fault publication;
- add revocation behavior for active tokens;
- connect ordering/conflict and cache-protocol gates before executable overlap;
- add conformance tests before any Current Implemented Contract migration.

## Required Tests Before Any Executable Claim

- Current DSC helper path remains physical in current mode.
- Physical descriptor selects only the physical backend.
- IOMMU-translated descriptor selects only the IOMMU backend.
- Unsupported address-space mode rejects before memory access.
- Unsupported capability rejects before memory access.
- IOMMU failure never retries through physical memory.
- Device ID mismatch rejects or faults precisely.
- Owner/domain mismatch rejects or faults precisely.
- Missing or stale mapping epoch rejects, serializes, cancels, replays, or faults according to ADR policy.
- Translation failure maps to a precise token fault.
- Permission failure maps to a precise token fault.
- Alignment and bounds failures map to precise token faults.
- Mapping revocation during an active token is handled by the approved policy.
- L7 memory portal uses the same no-fallback rule once executable L7 is approved.
- StreamEngine/DMAController IOMMU tests remain separate and are not counted as DSC/L7 executable proof.
- Compiler/backend cannot lower to IOMMU-translated DSC/L7 without capability discovery and conformance tests.

## Documentation Migration Rule

Documentation must use precise wording:

- Say: `IBurstBackend` and `IOMMUBurstBackend` exist and delegate burst read/write to `Memory.IOMMU`.
- Say: current DSC runtime/helper memory remains physical.
- Say: executable DSC/L7 IOMMU integration is future gated.
- Do not say: IOMMU is absent.
- Do not say: DSC memory access is currently IOMMU-translated.
- Do not say: existing IOMMU burst backend proves executable DSC/L7 integration.
- Do not move future addressing/backend claims into Current Implemented Contract until implementation and tests are complete.

## Code Evidence

- `HybridCPU_ISE\Core\Execution\BurstIO\IBurstBackend.cs`
  - Defines `Read(ulong deviceID, ulong address, Span<byte> buffer)`.
  - Defines `Write(ulong deviceID, ulong address, ReadOnlySpan<byte> buffer)`.
  - Exposes accelerator registration and DMA transfer surface methods.
- `HybridCPU_ISE\Core\Execution\BurstIO\IOMMUBurstBackend.cs`
  - `Read` delegates to `YAKSys_Hybrid_CPU.Memory.IOMMU.ReadBurst`.
  - `Write` delegates to `YAKSys_Hybrid_CPU.Memory.IOMMU.WriteBurst`.
  - `RegisterAcceleratorDevice` and `InitiateAcceleratorDMA` fail closed through `AcceleratorRuntimeFailClosed`.
- `HybridCPU_ISE\Memory\MMU\IOMMU.cs`
  - Provides `Map`, `Unmap`, `TranslateAndValidateAccess`, `DMARead`, `DMAWrite`, `ReadBurst`, and `WriteBurst`.
  - Implements permission checks and translation warm-state invalidation surfaces.
  - This proves IOMMU infrastructure, not DSC/L7 executable integration.
- `HybridCPU_ISE\Core\Execution\DmaStreamCompute\DmaStreamAcceleratorBackend.cs`
  - Current DSC runtime reads through `_mainMemory.TryReadPhysicalRange`.
  - It does not use `IBurstBackend` or `IOMMUBurstBackend`.
- `HybridCPU_ISE\Core\Execution\DmaStreamCompute\DmaStreamComputeRuntime.cs`
  - `ExecuteToCommitPending` creates `DmaStreamAcceleratorBackend(Processor.MainMemory, ...)` in the helper overload.
  - Runtime reads operands through `backend.TryReadRange`.
  - Runtime stages destination writes into the token; it is not pipeline ISA execution.
- `HybridCPU_ISE\Core\Execution\DmaStreamCompute\DmaStreamComputeToken.cs`
  - `Commit(Processor.MainMemoryArea mainMemory, ...)` commits through physical main memory.
  - `TryCommitAllOrNone` snapshots with `TryReadPhysicalRange` and writes/rolls back with `TryWritePhysicalRange`.
  - Fault kinds include `TranslationFault`, `PermissionFault`, `DomainViolation`, `DmaDeviceFault`, `AlignmentFault`, and `MemoryFault`, but current helper path does not classify IOMMU backend failures for executable DSC.
- `HybridCPU_ISE\Core\Execution\DmaStreamCompute\DmaStreamComputeDescriptorParser.cs`
  - `ExecutionEnabled => false`.
  - DSC1 parser rejects reserved fields and accepts only current ABI constraints.
  - Owner/domain/device evidence exists, but no approved executable `AddressSpace` ABI exists.
- `HybridCPU_ISE\Memory\DMA\DMAController.cs`
  - `TransferDescriptor.UseIOMMU` exists.
  - `PerformBurst` uses `IOMMU.ReadBurst(0UL, ...)` and `IOMMU.WriteBurst(0UL, ...)` when enabled.
  - This is separate DMAController behavior, not lane6 DSC or L7 backend selection.
- `HybridCPU_ISE\Core\Execution\StreamEngine\StreamEngine.BurstIO.cs`
  - Uses `IBurstBackend` and default `IOMMUBurstBackend` for StreamEngine burst helpers.
  - Uses `CPU_DEVICE_ID = 0`.
  - Drives large DMA helper paths synchronously.
  - This remains separate from DSC descriptor execution.

## Strict Prohibitions

This ADR must not be used to claim:

- lane6 DSC is executable;
- lane7 L7 is executable production ISA;
- current DSC memory access is IOMMU-translated;
- `IOMMUBurstBackend` proves executable DSC/L7 integration;
- `DMAController.UseIOMMU` proves lane6 DSC IOMMU support;
- `StreamEngine.BurstIO` proves lane6 DSC or L7 executable backend integration;
- compiler/backend may production-lower to IOMMU-translated DSC/L7;
- failed IOMMU translation may fall back to physical memory;
- DSC1 reserved fields may carry address-space semantics;
- cache/prefetch surfaces provide coherent DMA/cache behavior.
