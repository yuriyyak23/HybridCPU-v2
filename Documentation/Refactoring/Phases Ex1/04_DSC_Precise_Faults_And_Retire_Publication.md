# Phase 04 - DSC Precise Faults And Retire Publication

Status:
Design required. Implementation-ready only after phase 02 ADR and phase 03 token lifecycle design.

Scope:
Define TASK-003 fault metadata, priority, and precise retire publication for future executable lane6 DSC.

Current code evidence:
- `DmaStreamComputeFaultRecord.RequiresRetireExceptionPublication` exists.
- `DmaStreamComputeFaultRecord.CreateRetireException()` maps model fault records to exception objects.
- `DmaStreamComputeValidationResult` exposes descriptor, carrier, owner/domain, range, alignment, quota/backpressure, token-cap, and execution-disabled fault classes.
- `DmaStreamComputeToken.PublishFault(...)` records token faults.
- `CPU_Core.PipelineExecution.Memory.ApplyRetiredDmaStreamComputeTokenCommit` and test-support commit helpers exist as explicit publication surfaces.
- Normal pipeline does not currently allocate DSC tokens or publish DSC token faults at retire.

Architecture decision:
Future gated:
- Executable DSC faults must be precise retire publications, not arbitrary exceptions thrown from async engine code.
- Every executable token must carry issuing PC, bundle identity, slot/lane, thread/context/core/pod, domain tag, device ID, descriptor identity, and token ID.
- Descriptor faults, runtime faults, backend/IOMMU faults, ordering faults, cancellation faults, and commit faults must map to architectural exceptions only at the approved publication boundary.

Non-goals:
- Do not claim current DSC helper faults are full pipeline-precise exceptions.
- Do not let backend exceptions escape as unprioritized host exceptions.
- Do not publish memory and then report a fault unless all-or-none rollback and retire semantics say so.
- Do not define successful partial completion here.

Required design gates:
- Fault priority table across VLIW slots and token completions.
- Issuing metadata format and lifetime.
- Mapping from validation/runtime/backend/commit faults to architectural exception classes.
- Retire observation rule for pending, complete, faulted, canceled, and commit-pending tokens.
- Multi-slot priority against scalar, vector, memory, and system slot faults.
- Replay and squash handling for tokens with pending faults.

Implementation plan:
1. Extend token metadata with issuing PC, bundle ID, slot index, lane class, and age.
2. Add a fault priority table before enabling execution.
3. Normalize all async/backend errors into `DmaStreamComputeFaultRecord`.
4. Keep token fault state side-effect-free until retire/commit publication.
5. At retire, select the architecturally oldest/highest-priority fault for the bundle.
6. Publish exception through existing pipeline exception machinery, not through helper-only paths.
7. On all-or-none commit failure, roll back and publish a precise commit fault.

Fault priority draft, subject to ADR:

| Priority | Class | Notes |
|---|---|---|
| 1 | Trap/squash/replay cancellation before architectural issue | Token must not publish architectural fault if instruction is canceled before issue. |
| 2 | Carrier decode and typed-slot legality | Must be associated with issuing bundle/slot. |
| 3 | Descriptor ABI, reserved field, owner/domain guard, footprint normalization | Admission faults before async side effects. |
| 4 | Token admission quota/backpressure/token-cap | ADR must decide architectural fault versus stall/retry. |
| 5 | Runtime read/translation/permission/alignment faults | Held in token until retire publication. |
| 6 | Compute/stage coverage faults | No memory visibility. |
| 7 | Commit guard and all-or-none physical/write faults | Rollback before fault publication. |
| 8 | Cancellation after issue | Maps to replay/squash/trap semantics, not generic success. |

Affected files/classes/methods:
- `DmaStreamComputeFaultRecord`
- `DmaStreamComputeValidationResult`
- `DmaStreamComputeToken.PublishFault`
- `DmaStreamComputeToken.Commit`
- `CPU_Core.PipelineExecution.Memory.ApplyRetiredDmaStreamComputeTokenCommit`
- future token store retire observer
- CPU exception/fault priority machinery

Testing requirements:
- Descriptor fault maps to expected architectural exception.
- Runtime read/write/IOMMU fault maps through token fault record.
- Commit failure rolls back and publishes one precise fault.
- Faulted token never commits.
- Multi-slot bundle priority is deterministic.
- Younger token completion cannot preempt older architectural fault.
- Backend host exception is converted to token fault.

Documentation updates:
Document fault kinds, priority, issuing metadata, retire publication, and the difference between model fault APIs and future architectural exceptions.

Compiler/backend impact:
Compiler/backend cannot rely on executable DSC faults until this phase is implemented. Future lowering must treat DSC faults as precise retire exceptions with explicit ordering and cancellation semantics.

Compatibility risks:
Imprecise faults would break VLIW exception semantics and make compiler scheduling unsafe. Commit faults without rollback would break all-or-none.

Exit criteria:
- Fault priority table approved.
- Token metadata and retire publication rules approved.
- Test matrix covers multi-slot and async completion ordering.

Blocked by:
Phase 02 ADR gate and phase 03 token lifecycle.

Enables:
Executable lane6 DSC, safe compiler scheduling, L7 fault model reuse, and conformance documentation migration.

