# HybridCPU L7-SDC: Lane7 System Device Command Model for External Accelerators

Status: target architecture with implementation closed through Phase 14; Phase
15 full validation baseline is the next open gate.

This document defines the target architecture for moving the retained
`CustomAcceleratorMicroOp` / `MatMulAccelerator` / custom accelerator registry
scaffold into a canonical external accelerator command model. The plan is based
on the current HybridCPU-v2 code surfaces, the DmaStreamCompute contract
documents, the StreamEngine/SRF/VDSA documents, the local
`Documentation/CustomExternalAccelerator/Ideas` material, and the follow-up
architecture review notes incorporated on 2026-04-28.

The old requested `Documentation/MemoryAccelerators` path is provenance only.
The live repository inputs are `Documentation/Stream WhiteBook/DmaStreamCompute` and
`Documentation/Stream WhiteBook/StreamEngine DmaStreamCompute`.

## 1. Executive summary

Custom accelerator execution is not currently an architecture-facing compute
path. `CustomAcceleratorMicroOp.Execute` fails closed, the accelerator registry
is diagnostic metadata and negative-control surface only, and
`MatMulAccelerator.Execute` is a descriptor/resource fixture that returns the
destination address without publishing memory.

The target model is:

- Lane7 `SystemSingleton` issues system-device commands.
- The external accelerator fabric performs autonomous data-plane compute.
- Descriptor sideband defines the ABI; raw reserved instruction bits are never
  ABI.
- Owner/domain guard is authority and must precede descriptor acceptance,
  capability acceptance, command submission, device authorization, token commit,
  and exception publication.
- Device completion is not architectural commit.
- Staged writes become architectural only through token commit.
- `DmaStreamCompute` remains the canonical lane6 descriptor-backed native
  stream compute path.
- StreamEngine/SRF/VectorALU and VDSA assist remain separate helper/assist
  contours and cannot silently satisfy external accelerator commands.

Current implementation boundary:

- Phases 00-14 are closed.
- L7-SDC implementation surfaces exist under
  `HybridCPU_ISE/Core/Execution/ExternalAccelerators`.
- Lane7 command carriers exist in
  `HybridCPU_ISE/Core/Pipeline/MicroOps/SystemDeviceCommandMicroOp.cs` and
  remain hard-pinned `SystemSingleton` lane7 with direct `Execute` fail-closed.
- Staged writes are the only L7-SDC architectural publication path.
- Poll, wait, cancel, fence, and fault publication revalidate guard authority
  and cannot bypass the Phase 08 commit coordinator.
- Phase 10 `ExternalAcceleratorConflictManager` owns active token footprint
  truth for L7-SDC overlap decisions.
- Phase 11 MatMul exists as metadata-only capability provider, conservative
  typed descriptor schema/resource model, and staging-only fake backend test
  contour.
- Phase 12 compiler emission exists only through explicit accelerator intent.
  Typed `AcceleratorCommandDescriptor` sideband survives compiler IR, slot
  metadata, bundle lowering, ISE transport, decoder, and projector; runtime
  rejection after emitted `ACCEL_SUBMIT` remains rejection.
- Phase 13 telemetry/evidence exists as observation-only counters, immutable
  snapshots, and additive diagnostics export. Telemetry cannot authorize
  descriptor acceptance, capability acceptance, submit/token creation, backend
  execution, commit/publication, cancellation, fence, fault, or exception
  publication.
- Phase 14 documentation quarantine confirms public L7-SDC claims remain bound
  to the implemented lane7 `SystemSingleton`, typed sideband, guard, token,
  staging, conflict, compiler, and telemetry boundaries.

Expected final stance:

```text
CustomAccelerator is not execution.
CustomAccelerator is capability scaffold.

Lane7 SystemSingleton command is control-plane.
External accelerator fabric is data-plane.
Token commit is architectural visibility.
Owner/domain guard is authority.

DmaStreamCompute remains the canonical lane6 descriptor-backed stream compute path.
External Accelerators become a separate canonical lane7 system-device command model.
```

## 2. Name recommendation

Final model name:

```text
HybridCPU L7-SDC: Lane7 System Device Command Model for External Accelerators
```

Short names:

- `L7-SDC`
- `Lane7 System Device Command`
- `External Accelerator Carrier Contract`

Recommended specification names:

- `L7_SDC_External_Accelerator_Carrier_Contract`
- `L7_SDC_Descriptor_ABI`
- `L7_SDC_Token_And_Commit_Contract`
- `L7_SDC_Memory_Conflict_Contract`

Recommended namespace and type style:

- Runtime namespace: `YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators`
- Descriptor namespace: `YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors`
- Token namespace: `YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens`
- Backend namespace: `YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Backends`
- Micro-op namespace remains under `YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps`
- Compiler namespace: `HybridCPU_Compiler.Core.IR.ExternalAccelerators`
- Primary carrier type: `SystemDeviceCommandMicroOp`
- Operation-specific micro-ops: `AcceleratorSubmitMicroOp`,
  `AcceleratorPollMicroOp`, `AcceleratorWaitMicroOp`,
  `AcceleratorCancelMicroOp`, `AcceleratorFenceMicroOp`,
  `AcceleratorQueryCapsMicroOp`

Rejected naming:

- Any name that looks like a revision of the legacy custom micro-op: it implies
  activation of the quarantine surface.
- Any name that presents the carrier as DMA: it confuses external accelerator
  control with lane6 stream compute.
- Any name that embeds a single operation such as MatMul into the carrier:
  it overfits the system-device command model to one capability provider.
- Any name that binds external accelerator authority to `BranchControl`.

## 3. Current-state assessment

### Slot and bundle topology

Current `SlotClassDefinitions` defines fixed W=8 topology:

- lanes 0-3: `AluClass`
- lanes 4-5: `LsuClass`
- lane 6: `DmaStreamClass`
- lane 7: `BranchControl`
- lane 7: `SystemSingleton`

`BranchControl` and `SystemSingleton` are lane7 aliases, but they are not the
same authority class. External accelerator commands must use `SystemSingleton`
because they are system-device command issue operations, not control-flow
operations.

### Legacy custom accelerator scaffold

`CustomAcceleratorMicroOp` is intentionally fail-closed:

- `InstructionClass = System`
- `SerializationClass = FullSerial`
- hard-pinned to `SlotClass.Unclassified`
- retained custom accelerator metadata only
- `Execute` throws through
  `InstructionRegistry.CreateUnsupportedCustomAcceleratorException`

This is a useful negative-control surface and compatibility quarantine. It must
not be activated as the new execution path because it lacks canonical opcode
placement, descriptor ABI, owner/domain admission, typed sideband, token
lifecycle, staged write commit, and memory conflict semantics.

### MatMul fixture

`MatMulAccelerator` is retained as a descriptor/resource fixture. Its `Execute`
method validates matrix shape and returns `matC_addr`; it does not perform
architectural memory compute. Comments in the file explicitly say a future
active path requires descriptor ABI, owner/domain guard, placement, DMA transfer,
and replay/retire contracts.

The correct migration is `MatMulCapabilityProvider` plus a typed MatMul
descriptor schema and resource model, not activation of the legacy `Execute`.

### Custom registry

`InstructionRegistry.RegisterAccelerator` stores diagnostic metadata and
reserved custom opcodes for negative-control tests. Registry success does not
grant decode authority, execution authority, command submission authority, or
commit authority.

### DmaStreamCompute

`DmaStreamCompute` is the canonical native lane6 stream-compute contour:

- native opcode `OpCode.DmaStreamCompute`
- `InstructionClass.Memory`
- `SerializationClass.MemoryOrdered`
- `SlotClass.DmaStreamClass`
- mandatory typed descriptor sideband
- owner/domain guard before admission
- descriptor-backed normalized footprints
- tokenized staged-write commit path
- direct `DmaStreamComputeMicroOp.Execute` intentionally disabled/fail-closed

This contour must not become an external accelerator fallback or alternate
execution engine.

### StreamEngine/SRF/VectorALU

StreamEngine, SRF, and VectorALU provide raw/helper stream-vector primitives.
They can be used inside explicitly wrapped fake backend tests, but their success
is not external accelerator authority, DmaStreamCompute descriptor authority, or
architectural command success.

### VDSA and assist

Assist micro-ops are architecturally invisible, non-retiring, replay-discardable
and cannot publish architectural memory or register state. VDSA assist may warm
SRF/data ingress, but it is not DmaStreamCompute, not external accelerator
command issue, and not a compute authority.

## 4. Target architecture

The L7-SDC target splits external accelerator work into five planes:

- Control-plane: lane7 `SystemSingleton` command issue, status, cancel, wait,
  and fence.
- Data-plane: external/autonomous accelerator fabric and device-private
  execution resources.
- Commit-plane: staged writes, token commit, rollback/fault, cache/SRF
  invalidation, and retire-visible architectural publication.
- Authority-plane: owner/domain guard, capability admission, descriptor
  validation, queue authorization, execution authorization, commit
  authorization, and exception publication authorization.
- Evidence-plane: telemetry, replay evidence, descriptor identity, certificate
  identity, token identity, and registry metadata. Evidence is never authority.

Lane7 does not perform the compute. Lane7 only issues, observes, fences, or
revokes commands. L7-SDC commands must be coarse-grain; `SUBMIT`/`POLL` storms
must be throttled by scheduler/runtime policy, and `WAIT`/`FENCE` must remain
rare serializing operations. The device fabric reads source ranges, computes
internally, and produces staged writes. Runtime/token commit publishes those
staged writes only if owner/domain, mapping epoch, footprint, ordering, and
fault checks still pass.

## 5. Boundary model

### Control-plane

Control-plane operations are native VLIW system instructions hard-pinned to
lane7 through `SetHardPinnedPlacement(SlotClass.SystemSingleton, 7)`. They
carry small architectural operands and typed sideband metadata. They may create
or inspect tokens, but they do not directly write destination memory.

### Data-plane

The data-plane is represented by an external accelerator backend and command
queue. It may maintain device-private scratch and internal execution state. It
must not publish architectural memory directly.

### Commit-plane

The commit-plane owns architectural visibility. Device results enter staging
buffers first. A token transitions to `CommitPending` only after device
completion and exact staged-write coverage are available. Commit writes staged
data to architectural memory, invalidates overlapping SRF/cache state, records
telemetry, and transitions the token to `Committed` or `Faulted`.

### Authority-plane

Owner/domain guard is the root authority. The guard must precede:

- descriptor acceptance
- capability acceptance
- replay/certificate reuse
- command submission
- device execution authorization
- token commit
- exception publication

Raw `VirtualThreadId` hints, replay evidence, certificate identity, token
identity, telemetry, and registry success are not authority.

### Evidence-plane

Evidence records what happened and helps debugging, validation, replay, and
regression tests. Evidence cannot convert a rejected command into a successful
command and cannot bypass owner/domain checks.

## 6. ISA and micro-op design

### Canonical native system-device opcodes

All v1 opcodes are `InstructionClass.System`, use
`SetHardPinnedPlacement(SlotClass.SystemSingleton, 7)`, and are lane7-only.
None use `BranchControl` authority. Class-flexible placement is insufficient
for L7-SDC because the authority-sensitive carrier must expose an exact
placement invariant to decoder, scheduler, and test code.

| Opcode | Serialization | Writes register | Memory-visible | Creates token | Can fault | Required sideband |
| --- | --- | --- | --- | --- | --- | --- |
| `ACCEL_QUERY_CAPS` | `CsrOrdered` or `FullSerial` for v1 | yes, packed query status/cap summary | no | no | yes | capability query descriptor or typed query reference |
| `ACCEL_SUBMIT` | `MemoryOrdered` plus system-device ordering | yes, token handle or zero reject status | no direct memory write | yes on accepted submit | yes | `AcceleratorCommandDescriptor` |
| `ACCEL_POLL` | `CsrOrdered` | yes, packed token state/status/fault | no | no | yes | token reference sideband or register token operand |
| `ACCEL_WAIT` | `FullSerial` or device-wait serializing | yes, final packed status | no direct memory write | no | yes | token reference and wait policy |
| `ACCEL_CANCEL` | `FullSerial` | yes, final packed cancel status | no direct memory write | no | yes | token reference and cancel policy |
| `ACCEL_FENCE` | `FullSerial` | optional status | no direct memory write | no | yes | fence scope descriptor |

Future opcodes remain out of v1 execution:

- `ACCEL_SUBMIT_BATCH`
- `ACCEL_SUBMIT_CHAIN`
- `ACCEL_SIGNAL`
- `ACCEL_MAP_SCRATCH`
- `ACCEL_UNMAP_SCRATCH`

Future opcodes must initially decode to a fail-closed trap or unsupported
system-device command until their descriptor ABI, authority, token, and memory
ordering contracts are specified.

### Native raw carrier validation

L7-SDC has a native raw VLIW carrier, but the raw carrier is only a carrier.
The descriptor ABI and authority are sideband/guard data, never raw reserved
fields.

`ACCEL_*` raw carrier validation:

- opcode must be a canonical native `ACCEL_*` opcode, not a custom registry
  opcode
- slot index must be 7
- carrier placement must become
  `SetHardPinnedPlacement(SlotClass.SystemSingleton, 7)`
- instruction class must be `System`
- reserved raw bits must be zero
- raw `VirtualThreadId` hint must be zero in v1; nonzero raw VT rejects rather
  than becoming owner/domain authority
- no raw pointer field is accepted as authority
- no raw custom opcode registry identity is accepted as command authority
- `ACCEL_SUBMIT` requires `AcceleratorCommandDescriptor` typed sideband
- `ACCEL_QUERY_CAPS`, `ACCEL_WAIT`, `ACCEL_CANCEL`, and `ACCEL_FENCE` require
  their operation-specific typed sideband when the register operands are not
  sufficient to represent scope/policy
- a carrier with clean raw bits but missing required sideband rejects
- a carrier with sideband but wrong lane/class rejects

This mirrors the existing DmaStreamCompute discipline: native carrier first,
typed sideband for ABI, owner/domain guard for authority, and fail-closed
rejection for any raw compatibility shortcut.

### Register result ABI

The v1 GPR result ABI is explicit so tests can distinguish handle creation,
status observation, and fault/reject cases:

- `ACCEL_SUBMIT`: `rd` receives a nonzero opaque `AcceleratorTokenHandle` on
  accepted submit. If the command returns a non-trapping rejection status, `rd`
  is zero and telemetry/status evidence records the reject reason. If the
  implementation raises a precise fault, `rd` is not architecturally written.
- `ACCEL_POLL`: `rd` receives a packed `AcceleratorTokenStatusWord`.
- `ACCEL_WAIT`: `rd` receives the final packed `AcceleratorTokenStatusWord`,
  including timeout/fault/commit-pending information.
- `ACCEL_CANCEL`: `rd` receives the final packed cancel/status word.
- `ACCEL_QUERY_CAPS`: `rd` receives a packed query status or bounded capability
  summary; large capability data remains sideband/metadata, not raw GPR ABI.
- `ACCEL_FENCE`: `rd` is optional; when written it is a packed fence status.

`AcceleratorTokenHandle`:

- value zero is invalid/no-token
- nonzero values are opaque lookup keys with implementation-private
  index/generation structure
- handle arithmetic is undefined
- handle identity is not authority
- every poll, wait, cancel, detach, suspend, commit, and exception publication
  revalidates owner/domain and mapping epoch

`AcceleratorTokenStatusWord` v1 layout:

```text
bits  7:0   token_state
bits 15:8   fault_code
bits 23:16  flags
bits 31:24  reserved_zero
bits 63:32  implementation status sequence or generation
```

The reserved byte must be zero on write and ignored only for forward-compatible
read interpretation; it is never accepted as authority.

### Micro-op families

Base type:

```text
SystemDeviceCommandMicroOp
```

Common properties:

- placement: `SetHardPinnedPlacement(SlotClass.SystemSingleton, 7)`
- instruction class: `System`
- serialization: command-specific, v1 conservative
- direct architectural memory write: never
- descriptor sideband: required for submit and complex operations
- owner/domain guard: required before command admission
- registry/capability metadata: evidence only

| Micro-op | Placement | Register effects | Memory effects | Retire behavior | Fault behavior | Token behavior | Fail-closed conditions |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `AcceleratorQueryCapsMicroOp` | hard-pinned lane7 SystemSingleton | writes packed status/cap summary | none | retires after guarded query | unknown class/domain fault | no token | unknown query ABI, domain reject, registry treated as authority |
| `AcceleratorSubmitMicroOp` | hard-pinned lane7 SystemSingleton | writes nonzero token handle or zero reject status | no direct memory write | retires after token creation/queue admission | descriptor/capability/owner/domain/queue fault | creates `AcceleratorToken` | missing sideband, unknown id/ABI, dirty reserved fields, unsupported op/shape/datatype, conflict, raw VT authority |
| `AcceleratorPollMicroOp` | hard-pinned lane7 SystemSingleton | writes packed token state/status/fault | none | retires after guarded token read | token/domain/owner/mapping fault | observes token only | token identity used as authority, owner drift ignored |
| `AcceleratorWaitMicroOp` | hard-pinned lane7 SystemSingleton | writes final packed status | none directly | retires after completion/timeout/fault | wait policy/token/domain/mapping fault | may drive state toward `CommitPending`; does not itself bypass commit checks | wait publishes direct device writes, timeout hidden as success |
| `AcceleratorCancelMicroOp` | hard-pinned lane7 SystemSingleton | writes final packed cancel/status | none directly | retires after cancel request resolution | cancel policy/token/domain fault | transitions Queued/Running tokens to Canceled or Faulted according to policy | cancel after owner drift treated as commit permission |
| `AcceleratorFenceMicroOp` | hard-pinned lane7 SystemSingleton | optional packed status | no direct write; may force commit/drain ordering | retires after scoped active tokens are drained, committed, faulted, canceled, or rejected | active conflict/domain/fence fault | serializes scoped tokens | fence ignores active conflicting token |

`ACCEL_SUBMIT` is not a compute lane operation. It is a command issue operation.
The command may be accepted, queued, or rejected. The backend may later execute
the data-plane work, but architectural visibility remains token-commit gated.

## 7. Descriptor, capability, and token design

### AcceleratorCommandDescriptor

The descriptor is typed sideband metadata. It must not be encoded through raw
reserved bits in `VLIW_Instruction`.

Required fields:

```text
magic
abi_version
descriptor_size
accelerator_class
accelerator_id
operation_kind
datatype
shape
source_ranges
destination_ranges
scratch_requirements
alignment_requirements
partial_completion_policy
owner_binding
domain_tag
capability_version
descriptor_identity_hash
normalized_footprint_hash
reserved fields must be zero
```

Suggested structural subtypes:

- `AcceleratorDescriptorHeader`
- `AcceleratorClassId`
- `AcceleratorDeviceId`
- `AcceleratorOperationKind`
- `AcceleratorDatatype`
- `AcceleratorShape`
- `AcceleratorMemoryRange`
- `AcceleratorScratchRequirement`
- `AcceleratorAlignmentRequirement`
- `AcceleratorPartialCompletionPolicy`
- `AcceleratorOwnerBinding`
- `AcceleratorDomainTag`
- `AcceleratorDescriptorIdentity`
- `AcceleratorNormalizedFootprint`

Rejected descriptor patterns:

- missing sideband descriptor
- raw reserved instruction bits treated as ABI
- raw `VirtualThreadId` hint treated as authority
- unknown magic or ABI
- descriptor size smaller than header or larger than policy limit
- dirty reserved fields
- unknown accelerator class/id
- unknown operation/datatype/shape/layout
- unsupported shape for capability
- unsupported datatype combination
- source/destination ranges missing, empty, unaligned, non-normalized, or
  outside owner/domain coverage
- destination range overlaps unsupported alias class
- scratch requirement exceeds capability or domain policy
- `partial_completion_policy` not supported by v1 all-or-none commit
- descriptor identity hash mismatch
- normalized footprint hash mismatch
- owner/domain binding absent, stale, or not guard-accepted before parse

### MatMul descriptor shape

MatMul is a capability-specific descriptor payload behind the common command
header:

```text
A_base
B_base
C_base
M
N
K
lda
ldb
ldc
tile_m
tile_n
tile_k
input_datatype
accumulator_datatype
output_datatype
layout_flags
partial_policy = AllOrNone
```

Validation requirements:

- `M`, `N`, `K` are positive and within capability limits.
- `lda`, `ldb`, `ldc` satisfy layout and datatype alignment.
- source footprints for A and B and destination footprint for C normalize
  exactly.
- destination C range is covered by owner/domain guard.
- partial policy is `AllOrNone` in v1.
- unsupported transpose/layout/datatype combinations reject before submission.

### Capability registry

Replace the legacy execution-shaped registry with metadata-only capability
types:

```text
AcceleratorCapabilityRegistry
AcceleratorCapabilityDescriptor
AcceleratorOperationCapability
AcceleratorShapeCapability
AcceleratorResourceModel
IAcceleratorCapabilityProvider
```

The registry may answer:

- which accelerator classes exist
- which operations, datatypes, shapes, alignments, and partial policies are
  advertised
- rough latency/resource/queue model
- descriptor ABI versions supported

The registry must not grant:

- decode authority
- lane placement authority
- descriptor acceptance authority
- execution authority
- owner/domain authority
- commit authority
- exception publication authority

`MatMulAccelerator` migrates to:

- `MatMulCapabilityProvider`
- `MatMulDescriptor` schema
- `MatMulResourceModel`
- negative-control fixture tests proving the old `Execute` path cannot publish
  architectural memory

Migration invariant:

- `MatMulAccelerator.Execute` may remain only in tests, fixtures, or an
  explicitly quarantined legacy compatibility surface.
- No production L7-SDC path may call `ICustomAccelerator.Execute()`.
- No external accelerator backend may use legacy custom `Execute` success as
  proof of descriptor acceptance, device execution, staged write creation, or
  architectural commit.

### AcceleratorToken

State machine:

```text
Created
Validated
Queued
Running
DeviceComplete
CommitPending
Committed
Faulted
Canceled
TimedOut
Abandoned
```

`Abandoned` is a privileged/runtime terminal disposition for tokens whose
owner/domain or mapping epoch became invalid before completion publication. A
user context must observe no successful commit from an abandoned token; if a
user-visible token state must be reported, it is reported as guarded `Faulted`
only to an authorized context.

Token creator:

- `AcceleratorSubmitMicroOp` creates a token only after owner/domain guard,
  descriptor validation, capability compatibility, conflict policy, and queue
  admission pass.

Token contents:

- token id
- opaque token handle generation/index data
- owner binding and domain tag
- memory mapping epoch and IOMMU/domain epoch binding when detachable or
  suspendable
- accelerator class/id and capability version
- descriptor identity hash
- normalized footprint hash
- source/destination/scratch footprints
- queue id/device id
- state and fault code
- staged write references
- context-switch detach/suspend metadata when supported
- telemetry correlation id

What token proves:

- a command passed a specific admission sequence at a specific point in time
- a descriptor identity and footprint were bound to the command
- staged writes, if present, belong to the token container

What token does not prove:

- current owner/domain authority
- current mapping epoch authority
- current commit permission
- capability authority after domain drift
- replay/certificate acceptance
- memory conflict freedom after admission
- architectural write completion

Commit:

- requires `CommitPending`
- rechecks owner/domain guard
- rechecks pinned or epoch-validated memory mappings
- verifies descriptor identity and normalized footprint identity
- verifies exact staged-write coverage for destination ranges
- verifies no v1 conflict policy violation
- writes staged buffers atomically under all-or-none policy
- invalidates overlapping SRF/cache state
- records commit telemetry
- transitions to `Committed` or `Faulted`

Fault:

- records a precise token fault code
- publishes exceptions only after owner/domain authority allows publication
- rolls back staged writes or marks them discarded
- never hides partial write failure as success

If owner/domain is invalid at completion:

- user-visible commit is forbidden
- token transitions to `Faulted` or `Abandoned` according to runtime policy
- privileged runtime/OS-visible diagnostics may record device fault evidence
- architectural publication to the old user context is forbidden
- staged writes are discarded or retained only in privileged diagnostics, never
  committed to the invalidated context

Cancel:

- Queued tokens may cancel without device execution.
- Running tokens may request cooperative cancel; v1 may drain or fault if the
  backend cannot cancel cleanly.
- DeviceComplete/CommitPending tokens may not silently discard architectural
  requirements; cancel becomes commit, fault, or policy-defined rollback.

Context switch survival:

- v1 supports runtime-chosen drain or cancel.
- Long-running detachable tokens require owner/domain-bound process context.
- Detachable tokens require pinned or epoch-validated source/destination
  mappings.
- Detachable tokens require IOMMU/domain epoch binding.
- Unmap, remap, domain switch, or mapping epoch drift invalidates detachability
  and prevents commit.
- No token may commit after owner/domain invalidation.

Difference from `DmaStreamComputeToken`:

- `DmaStreamComputeToken` belongs to lane6 CPU-native descriptor-backed
  stream compute and current helper/runtime contour.
- `AcceleratorToken` belongs to lane7 system-device command issue plus an
  external/autonomous device fabric.
- Both tokens are evidence/containers, not authority.
- Both require guard-plane authority for commit.
- External accelerator tokens require queue/device lifecycle, detach/suspend
  policy, and device-private state isolation that lane6 DmaStreamCompute does
  not imply.

### Device backend model

Recommended interfaces:

```text
IExternalAcceleratorBackend
IAcceleratorCommandQueue
IAcceleratorDevice
IAcceleratorMemoryPortal
IAcceleratorStagingBuffer
```

Backend rules:

- device reads source ranges through guarded memory portal
- device computes internally
- device writes only to staging buffers
- direct architectural writes are forbidden
- staged writes publish only through token commit
- backend completion is `DeviceComplete`, not `Committed`
- fake backends may use StreamEngine/SRF/VectorALU as explicit test helpers,
  but helper success cannot replace command authority

Null/fake backend:

- `NullExternalAcceleratorBackend`: accepts no execution; all submits reject or
  create faulted tokens according to test mode.
- `FakeExternalAcceleratorBackend`: deterministic staged-write producer for
  tests; cannot write architectural memory directly.
- `ViolationBackend`: test-only backend that attempts direct write and must be
  detected/rejected/faulted.

## 8. Memory, ordering, and context-switch model

### Strict v1 conflict policy

v1 must serialize or reject the following conflict classes:

- CPU store overlaps accelerator read/write.
- CPU load overlaps accelerator write.
- DmaStreamCompute overlaps accelerator write.
- Accelerator write overlaps SRF warmed window.
- Assist/SRF warm overlaps accelerator write.
- Two accelerator tokens write the same region.
- Fence or serializing boundary while accelerator token is active.
- VM/domain transition while accelerator token is active.

No relaxed overlap semantics are introduced in v1.

### Conflict manager ownership

`ExternalAcceleratorConflictManager` owns the v1 conflict truth for L7-SDC:

- active accelerator token footprint table
- submit-time source/destination/scratch footprint reservation
- execution-time conflict monitoring against CPU stores, CPU loads,
  DmaStreamCompute tokens, SRF warmed windows, assist ingress windows, and other
  accelerator tokens
- commit-time final validation before staged writes publish
- SRF/cache invalidation on committed writes
- conflict telemetry as evidence only

The manager distinguishes:

- admission conflict: descriptor footprint cannot reserve because an existing
  active footprint or warmed window conflicts
- execution conflict: a later CPU/DmaStreamCompute/SRF/assist event conflicts
  with an active token and must serialize, cancel, or fault according to v1
  policy
- commit conflict: final validation fails because owner/domain/mapping epoch,
  footprint reservation, or memory visibility constraints drifted before commit

### Memory visibility

Direct DMA destination writes are not architectural commit. Device completion is
not architectural commit. Staged writes become visible only through token commit.
Partial staged writes must roll back or fault. SRF and cache invalidation must
happen on committed memory writes. Owner/domain drift prevents commit.

### Context switch

For DmaStreamCompute long stream:

```text
drain / cancel / checkpoint token
```

For external accelerator:

```text
drain / cancel / detach / suspend
```

v1 policy:

- privileged runtime chooses drain or cancel by default
- detachable long-running tokens require owner/domain-bound process context
- detachable tokens require pinned or epoch-validated source/destination memory
  mappings
- detachable tokens require IOMMU/domain epoch binding
- unmap, remap, or domain switch invalidates the token mapping epoch
- suspended or detached tokens cannot commit until owner/domain guard is
  revalidated and mapping epoch is still current
- VM/domain transition with active non-detachable token serializes, drains,
  cancels, or faults
- no commit after owner/domain or mapping epoch invalidation

## 9. Interaction with DmaStreamCompute, StreamEngine/SRF, and VDSA

### DmaStreamCompute coexistence

`DmaStreamCompute` remains:

- lane6 stream compute
- CPU-native memory-memory operation
- descriptor-backed
- tokenized
- commit-visible through `DmaStreamComputeToken`
- not custom accelerator
- not external accelerator fallback

External Accelerator becomes:

- lane7 system-device command
- external/autonomous device fabric
- coarse-grain command/token lifecycle
- staged-write token commit

Forbidden:

- external accelerator runtime fallback to DmaStreamCompute
- DmaStreamCompute fallback to external accelerator
- custom registry as DmaStreamCompute success path
- GenericMicroOp fallback after external accelerator rejection
- scalar/ALU/vector fallback after runtime rejection

Allowed compiler strategy:

```text
if capability exists and workload is large/coarse:
  emit ACCEL_SUBMIT
else if regular stream compute is appropriate:
  emit DmaStreamCompute
else:
  emit normal CPU lowering
```

This choice happens before emission. Runtime rejection must not silently
scalarize, vectorize, streamize, or route to DmaStreamCompute.

### StreamEngine/SRF/VectorALU

Allowed:

- explicit fake backend tests may use StreamEngine/SRF/VectorALU internally to
  generate staged-write bytes
- backend wrappers must still prove descriptor, owner/domain, token, and commit
  authority independently

Forbidden:

- StreamEngine success as external accelerator success
- VectorALU success as external accelerator success
- SRF warm success as external accelerator authority
- StreamEngine fallback after external accelerator rejection

On accelerator commit:

- invalidate overlapping SRF windows
- update or invalidate cache model
- record telemetry for invalidation and committed bytes

### VDSA assist

VDSA remains:

- assist-only
- non-retiring
- replay-discardable
- SRF/data-ingress oriented
- not compute
- not external accelerator command
- not DmaStreamCompute

External accelerator v1 must not consume assist-owned SRF entries as
device-private state. Any future use requires a separate explicit SRF ownership
and device-private state contract.

## 10. Migration phases

The phase plan is defined in
`Documentation/CustomExternalAccelerator/01_L7_SDC_Migration_Phases.md`.

Each phase is fail-closed and independently testable. The plan starts with
quarantine/naming closure, introduces metadata-only capability contracts,
adds lane7 system opcodes as rejected/unimplemented carriers, then layers
descriptor validation, owner/domain guard, token lifecycle, fake backend, staged
write commit, fault/cancel/fence/wait semantics, memory conflict management,
MatMul capability provider migration, compiler emission, telemetry,
documentation claim safety, and final validation.

## 11. Test plan

The detailed test and rollback plan is defined in
`Documentation/CustomExternalAccelerator/02_L7_SDC_Test_And_Rollback_Plan.md`.

Minimum regression blockers:

- custom registry does not grant execution
- `CustomAcceleratorMicroOp.Execute` remains fail-closed until removed/replaced
  by canonical lane7 command carriers
- MatMul fixture cannot publish memory
- `ACCEL_SUBMIT` must be hard-pinned lane7 `SystemSingleton`
- `ACCEL_SUBMIT` cannot use `BranchControl` authority
- descriptor sideband required
- raw reserved bits rejected
- raw VT hint rejected
- raw pointer fields and custom registry identity rejected as authority
- token register ABI tested: accepted submit returns nonzero opaque handle;
  non-trapping rejection returns zero; precise fault writes no `rd`
- owner/domain guard required before descriptor acceptance
- mapping epoch and IOMMU/domain epoch revalidated for detachable/suspendable
  tokens
- token alone cannot commit
- device direct write is not commit
- partial device write rolls back or faults
- SRF overlapping windows invalidated on commit
- DmaStreamCompute overlap serializes or rejects
- `ExternalAcceleratorConflictManager` owns submit reservation, execution
  monitoring, and commit validation
- unknown accelerator id rejects
- unknown ABI rejects
- unsupported shape rejects
- runtime fallback to DmaStreamCompute rejected
- runtime fallback to StreamEngine/VectorALU rejected
- registry opcode rejected outside canonical lane7 command
- context switch drain/cancel/detach policy tested
- lane7 submit/poll storm throttling tested
- full validation baseline remains green

## 12. Risk register

| Risk | Failure mode | Mitigation |
| --- | --- | --- |
| Legacy custom activation | `CustomAcceleratorMicroOp.Execute` becomes success path without descriptor/token/commit contract | keep legacy micro-op fail-closed; introduce new canonical L7-SDC carriers |
| Lane authority confusion | external command classified as `BranchControl` or lane6 DMA | tests assert `SystemSingleton` lane7 and reject branch/DmaStream placement |
| Registry authority leak | capability registration grants decode/execute/commit authority | metadata-only registry contract and negative tests |
| Direct write leak | backend writes architectural memory before token commit | staging-only memory portal, violation backend tests |
| Token authority leak | token id alone commits after owner/domain drift | guard recheck at poll/wait/cancel/commit/exception publication |
| Silent fallback | rejected submit becomes DmaStreamCompute/StreamEngine/VectorALU/scalar success | runtime rejection tests and compiler-only pre-emission strategy |
| Descriptor ABI drift | raw reserved bits or raw VT hint become ABI/authority | sideband-only parser tests; dirty reserved field rejects |
| Partial write success | partial staged writes are visible without all-or-none commit | rollback/fault tests; exact coverage requirement |
| SRF/cache stale state | accelerator commit leaves warmed windows valid | commit-time invalidation tests and telemetry counters |
| Context switch leak | detached token commits under stale process/domain | detachable tokens require owner/domain-bound context and revalidation |
| Mapping epoch leak | device completes after unmap/remap/domain switch and commits to a stale mapping | detachable tokens require pinned or epoch-validated mappings, IOMMU/domain epoch binding, and commit-time epoch revalidation |
| Lane7 starvation | frequent submit/poll/wait/fence traffic blocks branch/system progress | commands must be coarse-grain; scheduler/runtime throttle submit/poll storms; WAIT/FENCE remain rare |
| Legacy MatMul backend shortcut | backend calls legacy MatMul execute and treats fixture success as architectural execution | production L7-SDC paths may not call `ICustomAccelerator.Execute()`; MatMul is capability provider plus descriptor schema |

## 13. Definition of done

The L7-SDC migration is done when:

- native lane7 system-device opcodes exist and are the only accepted external
  accelerator frontend
- all external accelerator carriers classify as `InstructionClass.System` and
  hard-pin with `SetHardPinnedPlacement(SlotClass.SystemSingleton, 7)`
- no external accelerator carrier relies on `BranchControl`, lane6
  `DmaStreamClass`, raw reserved bits, or raw VT hints for authority
- `AcceleratorCommandDescriptor` sideband parser rejects all unknown, dirty,
  unsupported, or unguarded descriptors
- `AcceleratorCapabilityRegistry` is metadata-only and covered by negative
  authority tests
- `AcceleratorToken` lifecycle requires owner/domain guard at admission,
  execution authorization, commit, and exception publication
- external backend writes only to staging buffers
- token commit is the only architectural publication point
- detachable/suspendable tokens bind pinned or epoch-validated mappings and
  IOMMU/domain epoch data
- memory conflicts with CPU, DmaStreamCompute, SRF/cache, assist, and other
  accelerator tokens serialize or reject in v1
- MatMul exists as a capability provider and descriptor schema, not active
  legacy execution
- compiler can emit typed sideband plus lane7 `ACCEL_SUBMIT` only under explicit
  capability strategy
- runtime rejection never silently falls back
- telemetry exists and is documented as evidence only
- legacy custom accelerator tests, DmaStreamCompute tests, StreamEngine/SRF
  tests, assist tests, compiler alignment tests, and L7-SDC tests are green

## 14. Final recommended implementation order

1. Close naming and document the L7-SDC boundary.
2. Freeze the legacy custom scaffold as fail-closed negative control.
3. Introduce metadata-only capability descriptors and registry.
4. Add lane7 `SystemSingleton` opcode names and decode classification as
   hard-pinned fail-closed unsupported carriers.
5. Add raw carrier cleanliness validation and typed
   `AcceleratorCommandDescriptor` sideband parser with strict rejects.
6. Integrate owner/domain guard before descriptor/capability/submit.
7. Add `AcceleratorToken` lifecycle, token handle/status ABI, and mapping epoch
   binding without backend execution.
8. Add null/fake backend and queue model with staging-only writes.
9. Add commit-plane staged-write publication and rollback/fault handling.
10. Add poll/wait/cancel/fence semantics.
11. Add `ExternalAcceleratorConflictManager` for CPU, DmaStreamCompute,
    SRF/cache, assist, and accelerator-token overlaps.
12. Migrate MatMul to capability provider and schema.
13. Add compiler IR/lowering for explicit accelerator intent.
14. Add telemetry/evidence counters.
15. Close documentation quarantine and claim-safety tests.
16. Run full validation and keep rollback switches that disable execution while
    preserving parse/guard/token/fail-closed surfaces.

## Explicit architectural answers

1. Lane7 `SystemSingleton` is used because external accelerator command issue is
   a system-device operation with authority, descriptor, queue, token, and
   commit semantics. It must be hard-pinned with
   `SetHardPinnedPlacement(SlotClass.SystemSingleton, 7)`. `BranchControl` is a
   lane7 control-flow authority class and must not authorize device commands.
2. This is control-plane, not compute lane, because lane7 issues commands and
   observes/cancels/fences tokens. Compute happens in the external device fabric;
   architectural writes happen only at token commit.
3. `CustomAcceleratorMicroOp.Execute` cannot be revived directly because it has
   no truthful lane placement, descriptor ABI, owner/domain guard, staged write
   commit contour, memory conflict model, or token lifecycle.
4. `MatMulAccelerator` must become a capability provider because the current
   fixture validates shape and models latency/resource footprint but does not
   publish memory through a guarded token commit path.
5. External Accelerator differs from `DmaStreamCompute` because
   `DmaStreamCompute` is CPU-native lane6 descriptor-backed stream compute,
   while External Accelerator is lane7 system command issue plus autonomous
   device fabric and separate token/device lifecycle.
6. Silent fallback is avoided by rejecting runtime submissions after guard,
   descriptor, capability, queue, conflict, or backend failure, and by allowing
   scalar/DmaStreamCompute/CPU alternatives only as compiler choices before an
   accelerator command is emitted.
7. Context switch is safe when active tokens are drained, canceled, detached, or
   suspended by privileged runtime policy, and any later commit requires
   owner/domain, pinned mapping, and IOMMU/domain epoch revalidation.
8. The boundary between device completion and architectural commit is the staged
   write buffer. `DeviceComplete` means results exist in staging; `Committed`
   means token commit published them to architectural memory.
9. External accelerator conflicts with DmaStreamCompute and SRF through
   overlapping read/write/write footprints and warmed SRF windows.
   `ExternalAcceleratorConflictManager` owns submit-time reservation,
   execution-time monitoring, and commit-time validation; v1 serializes or
   rejects overlaps and invalidates SRF/cache state on committed writes.
10. Minimal regression tests are the authority, placement, sideband,
    no-direct-write, token-not-authority, no-silent-fallback, conflict,
    context-switch, and full-baseline tests listed in section 11.
