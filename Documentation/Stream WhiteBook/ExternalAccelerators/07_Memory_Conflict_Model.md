# Memory Conflict Model

## Conflict manager role

`ExternalAcceleratorConflictManager` is an explicit model-local footprint tracker.
It applies only inside the model instance that is passed to token store, fence,
queue, or commit helpers. It does not publish memory and does not replace commit
authority. It reserves token-bound normalized footprints at submit, records
active overlap notifications, validates footprint state before commit, and
releases reservations after guarded token resolution.

There is no global CPU load/store pipeline hook that automatically consults
`ExternalAcceleratorConflictManager`. If no manager is explicitly passed, token
store, fence, and commit helpers do not infer global load/store ordering from a
hidden process-wide manager.

Under Ex1 Phase05, this is absent/passive/current-non-authority behavior.
Executable overlap requires a future mandatory `GlobalMemoryConflictService`
with CPU load/store/atomic, DSC, DMA, StreamEngine/SRF/assist, L7, fence/wait,
poll, cache, and cancellation hook points. The current conflict manager is not
that installed authority.

Code anchors:

- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Conflicts/ExternalAcceleratorConflictManager.cs`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Commit/AcceleratorCommitModel.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcConflictManagerTests.cs`

## Overlap classes

The v1 conflict classes include:

- CPU load/store overlap with accelerator writes.
- CPU store overlap with accelerator reads.
- DmaStreamCompute overlap with accelerator writes.
- Accelerator write overlap with SRF warmed windows.
- Assist ingress overlap with accelerator writes.
- Accelerator write/write overlap.
- Fence or serializing boundary while a token is active.
- VM/domain/mapping transition while a token is active.
- Missing or stale commit footprint reservation.

Code anchors:

- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Conflicts/ExternalAcceleratorConflictManager.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcDmaStreamComputeConflictTests.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcSrfAssistConflictTests.cs`

## Three validation points

Submit-time reservation rejects incomplete footprint truth and active write/write
overlap. Execution-time notification serializes or rejects CPU, DmaStreamCompute, SRF,
assist, and mapping conflicts against active reservations. Commit-time validation checks
that the active footprint still matches the token-bound descriptor and that recorded
conflict evidence does not invalidate the commit.

`AcceleratorCommandQueue.ConflictAccepted` defaults to placeholder evidence for
the explicit queue model. It must not be read as global ordering acceptance or as
proof that CPU load/store conflicts were checked by the pipeline.

Passive conflict observations, active footprint records, and explicit
non-coherent invalidation fan-out are downstream evidence. They must not satisfy
upstream executable DSC/L7, async overlap, coherent DMA/cache, or compiler
lowering gates.

Code anchors:

- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Conflicts/ExternalAcceleratorConflictManager.cs`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Tokens/AcceleratorTokenStore.cs`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Fences/AcceleratorFenceModel.cs`

WhiteBook / Ex1 anchors:

- `Documentation/Stream WhiteBook/ExternalAccelerators/06_Backend_Staging_Commit_And_Rollback.md`
- `Documentation/Refactoring/Phases Ex1/05_Memory_Ordering_And_Global_Conflict_Service.md`
- `Documentation/Refactoring/Phases Ex1/13_Dependency_Graph_And_Execution_Order.md`
