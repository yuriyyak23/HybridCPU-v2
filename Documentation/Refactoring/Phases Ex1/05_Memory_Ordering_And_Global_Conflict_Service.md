# Phase 05 - Memory Ordering And Global Conflict Service

Status:
Design required. Implementation-ready only after executable DSC/L7 gates.

Scope:
Cover TASK-004 and TASK-009: ordering, fences, waits, polls, resource masks, and the future mandatory global conflict service for executable overlap.

Current code evidence:
- `DmaStreamComputeMicroOp` uses `SerializationClass = MemoryOrdered`, normalized footprints, and resource-mask evidence.
- `SystemDeviceCommandMicroOp` marks `ACCEL_SUBMIT` as `MemoryOrdered` and `ACCEL_WAIT`, `ACCEL_CANCEL`, `ACCEL_FENCE` as `FullSerial` metadata/classification.
- `ExternalAcceleratorConflictManager` exposes optional model methods including `TryReserveOnSubmit`, `NotifyCpuLoad`, `NotifyCpuStore`, `NotifyDmaStreamComputeAdmission`, `ValidateBeforeCommit`, and `ReleaseTokenFootprint`.
- `MemoryUnit` and `AtomicMemoryUnit` do not currently install that conflict manager as mandatory global CPU load/store truth.
- Phase 4 states that L7 conflict notifications do not imply a global CPU load/store hook.

Architecture decision:
Future gated:
- Current `SerializationClass`, `MemoryOrdered`, `FullSerial`, resource masks, and normalized footprints are evidence for future scheduling and conflict checks, not proof of executable ordering.
- Optional model conflict manager can inform design, but executable overlap requires a mandatory installable `GlobalMemoryConflictService`.
- CPU loads, CPU stores, atomics, DSC tokens, DMAController transfers, StreamEngine/SRF prefetch, assist-resident ranges, and L7 accelerators must be visible to one ordering/conflict authority when executable overlap is enabled.

Non-goals:
- Do not claim current global ordering or coherent conflict protection.
- Do not rely on model conflict APIs for production CPU load/store safety.
- Do not implement implicit coherency through ordering metadata.
- Do not define executable `ACCEL_FENCE` or DSC fence semantics without ADR.

Required design gates:
- Conflict service installation model: absent, present-passive, present-enforcing.
- Footprint representation: read/write, physical/translated, domain, owner, device, mapping epoch.
- Conflict policy: stall, replay, serialize, reject, fault, or wait.
- CPU load/store/atomic hook points.
- DSC issue/admission reservation points.
- Commit validation and release points.
- Fence/wait/poll instruction semantics and interaction with active tokens.
- Deadlock, fairness, and backpressure rules.

Implementation plan:
1. Define normalized footprint records shared by DSC, L7, DMA, CPU memory, atomics, SRF/assist, and cache protocol.
2. Introduce `GlobalMemoryConflictService` behind a feature gate.
3. Add active token footprint reservations at issue/admission.
4. Add CPU load/store/atomic hooks in a compatibility-preserving way when service is enabled.
5. Add commit-time validation before memory publication.
6. Release footprints on commit, fault, cancel, squash, trap, and context switch according to token state.
7. Define wait/poll/fence semantics only after completion and retire semantics are approved.

Affected files/classes/methods:
- `DmaStreamComputeMicroOp`
- `DmaStreamComputeToken`
- future `DmaStreamComputeTokenStore`
- `ExternalAcceleratorConflictManager`
- future `GlobalMemoryConflictService`
- `MemoryUnit`
- `AtomicMemoryUnit`
- `DMAController`
- `StreamEngine.BurstIO`
- `SystemDeviceCommandMicroOp`
- cache/assist/SRF invalidation surfaces

Testing requirements:
- Absent service preserves current scalar and model-only behavior.
- Active DSC write footprint conflicts with CPU load/store according to selected policy.
- CPU atomic reservation is invalidated or protected against overlapping DMA/accelerator write.
- Non-overlapping footprints proceed.
- Domain and mapping-epoch mismatches are rejected or serialized.
- Fence waits for prior active tokens as specified.
- Poll observes completion without committing memory early.
- Litmus tests cover load-after-DMA-write, store-before-DMA-read, atomics versus DMA, two active tokens, and L7/DSC overlap.

Documentation updates:
Document current metadata as evidence only. Document future global conflict service as mandatory for executable DSC/L7 overlap and any fence/wait/poll claims.

Compiler/backend impact:
Compiler/backend must not assume async overlap or ordering today. Future lowering may schedule overlap only after conflict/fence semantics are implemented and exposed through capabilities.

Compatibility risks:
An always-on hook can perturb existing scalar tests. The service must have an absent/current-contract mode and an enabled executable mode. Ambiguous conflict policies can deadlock or create imprecise replay.

Exit criteria:
- Conflict service API and policies approved.
- Hook points identified.
- Litmus test suite defined.
- Service is a prerequisite for executable overlap, not retroactively current behavior.

Blocked by:
Phase 02 for executable DSC, phase 03 token lifecycle, phase 04 precise retire publication, and phase 10 for executable L7.

Enables:
Safe async overlap, wait/poll/fence semantics, future executable L7 submit/wait/fence, compiler scheduling, and cache protocol integration.

