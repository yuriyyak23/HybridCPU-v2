# Parked Alternatives And Rejected Historical Ideas

Status: residual idea backlog after L7-SDC canonicalization.

## Scope

This file preserves alternative designs and historical ideas only as parked or
rejected decisions. None of these entries are current architecture claims.

It does not repeat the active L7-SDC, DmaStreamCompute, StreamEngine/SRF, or
VDSA assist contracts except to name the canonical replacement for a rejected
idea.

## Canonical References

- `Documentation/CustomExternalAccelerator/00_L7_SDC_Executive_Spec.md`
- `Documentation/CustomExternalAccelerator/02_L7_SDC_Test_And_Rollback_Plan.md`
- `Documentation/CustomExternalAccelerator/Phases/Phase_01_Legacy_Scaffold_Quarantine.md`
- `Documentation/CustomExternalAccelerator/Phases/Phase_02_Capability_Registry_Metadata_Only.md`
- `Documentation/CustomExternalAccelerator/Phases/Phase_03_Lane7_System_Opcode_Surface.md`
- `Documentation/CustomExternalAccelerator/Phases/Phase_04_Descriptor_ABI_Parser_And_Carrier_Validation.md`
- `Documentation/CustomExternalAccelerator/Phases/Phase_08_Staged_Writes_And_Commit_Contour.md`
- `Documentation/CustomExternalAccelerator/Phases/Phase_10_Memory_Conflict_Manager.md`
- `Documentation/CustomExternalAccelerator/Phases/Phase_11_MatMul_Capability_Provider.md`
- `Documentation/CustomExternalAccelerator/Phases/Phase_12_Compiler_Emission_Path.md`
- `Documentation/Stream WhiteBook/DmaStreamCompute/01_Current_Contract.md`
- `Documentation/Stream WhiteBook/StreamEngine DmaStreamCompute/01_StreamEngine_SFR_SRF_VectorALU.md`
- `Documentation/Stream WhiteBook/StreamEngine DmaStreamCompute/03_VDSA_Assist_Warming_Prefetch_SRF_DataIngress.md`

## Remaining Ideas

The entries below are parked alternatives. They are not v1 architecture and
must start fail-closed if later implemented.

### Batch, chain, scratch-map, and signal commands

- Idea: add future commands such as `ACCEL_SUBMIT_BATCH`,
  `ACCEL_SUBMIT_CHAIN`, scratch mapping commands, and device signal commands.
- Why not covered yet: canonical docs list these as future fail-closed opcode
  candidates, but no descriptor ABI, ordering, token, fault, conflict, or
  commit contract exists for them.
- Relevant phase(s): post-v1 after phase 15 closure.
- Required authority/placement/commit constraints: each command must begin as a
  native lane7 `SystemSingleton` carrier that rejects until typed sideband,
  owner/domain guard, mapping epoch, capability admission, token lifecycle, and
  staged commit rules are specified.
- Risk if implemented incorrectly: batch or chain commands can hide partial
  failure, merge authority across descriptors, or bypass per-footprint conflict
  checks.
- Suggested tests: fail-closed decode for future opcodes; per-descriptor fault
  isolation; no mixed-owner batch acceptance; no shared commit without exact
  staged write coverage.
- Decision status: Parked.

### Physical device output through protected staging memory

- Idea: allow a backend implementation to use device DMA into a protected
  non-architectural staging buffer as an optimization, while preserving token
  commit as the only architectural publication point.
- Why not covered yet: canonical docs require staged writes but do not specify
  whether staging is CPU-allocated memory, device-local memory, an IOMMU-shadow
  range, or another implementation-private buffer.
- Relevant phase(s): phase 07, phase 08, phase 10, and post-v1 backend work.
- Required authority/placement/commit constraints: staging memory must be
  inaccessible as architectural destination memory before commit; owner/domain,
  mapping epoch, descriptor identity, footprint identity, conflict validation,
  and exact coverage still gate commit.
- Risk if implemented incorrectly: an optimization can accidentally publish
  bytes before commit or make rollback impossible after partial device output.
- Suggested tests: staging range cannot alias destination range; direct
  destination mutation attempt faults; partial staging coverage faults; rollback
  after simulated physical write failure.
- Decision status: Parked.

### User-mode detachable long-running accelerator commands

- Idea: permit long-running external commands to continue across CPU context
  switches under an owner/domain-bound token rather than always draining or
  canceling.
- Why not covered yet: canonical docs define detach/suspend as possible policy,
  but v1 can safely start with privileged drain/cancel. Full user-mode detach
  needs IOMMU/domain epoch, mapping pinning, fault publication, and resource
  quota policy.
- Relevant phase(s): phase 05, phase 06, phase 09, phase 10, phase 15, and
  post-v1 OS/runtime integration.
- Required authority/placement/commit constraints: detached tokens must not
  commit after owner/domain or mapping epoch drift; token handle identity is not
  authority; privileged diagnostics must not become user-visible success.
- Risk if implemented incorrectly: stale commands can write after process exit,
  domain switch, unmap, or remap.
- Suggested tests: process/domain switch with active detachable token; unmap
  before completion; remap same virtual address to different physical page;
  fault publication only to authorized context.
- Decision status: Parked.

## Rejected Historical Ideas

### Legacy custom micro-op as production compute carrier

- Idea: revive `CustomAcceleratorMicroOp.Execute` as the active path for
  external accelerator compute.
- Why rejected: the legacy surface has no canonical opcode placement,
  descriptor ABI, owner/domain admission, token lifecycle, staged commit plane,
  or memory conflict model.
- Canonical replacement: new L7-SDC native lane7 system-device carriers, with
  `SystemDeviceCommandMicroOp` and operation-specific command micro-ops.
- Tests that must prevent regression: legacy quarantine tests; no production
  call into `ICustomAccelerator.Execute`; hard-pinned lane7 placement tests;
  descriptor sideband required tests.

### MatMul fixture as memory-compute implementation

- Idea: treat `MatMulAccelerator.Execute` as proof of active matrix compute.
- Why rejected: the current fixture validates operands and returns the
  destination address; it does not read matrices, perform MAC work, generate
  staged bytes, or commit memory.
- Canonical replacement: `MatMulCapabilityProvider`, MatMul descriptor schema,
  fake/backend staged result generation, and token commit.
- Tests that must prevent regression: MatMul fixture cannot publish memory;
  unsupported shape/datatype rejects; fake MatMul stages results only; no
  production backend calls legacy execute.

### Registry success as decode, execution, or commit permission

- Idea: let accelerator registration or lookup create executable authority.
- Why rejected: registry state is metadata and evidence only. It cannot replace
  native opcode decode, descriptor validation, owner/domain guard, queue
  admission, token validation, or commit checks.
- Canonical replacement: metadata-only `AcceleratorCapabilityRegistry` plus
  independent L7-SDC command admission.
- Tests that must prevent regression: capability registration cannot decode an
  opcode; registration cannot accept missing sideband; registration cannot
  submit or commit; unknown accelerator id rejects.

### Branch/control or lane6 placement for external accelerator commands

- Idea: place external accelerator command authority on the branch/control class
  or the lane6 DMA/stream class.
- Why rejected: external accelerator command issue is a system-device
  control-plane operation. Lane6 remains the canonical DmaStreamCompute
  descriptor path, and branch/control is not a device-command authority.
- Canonical replacement: hard-pinned lane7 `SystemSingleton` L7-SDC carriers.
- Tests that must prevent regression: `ACCEL_SUBMIT` is lane7-only; no
  branch/control classification; no lane6 acceptance; DmaStreamCompute remains
  lane6-only.

### Raw carrier fields as descriptor or owner shortcut

- Idea: encode accelerator descriptor data or owner identity in raw reserved
  instruction fields or raw virtual-thread hint fields.
- Why rejected: raw carrier fields are not a stable descriptor ABI and cannot
  replace guard-plane owner/domain authority.
- Canonical replacement: clean native raw carrier plus typed sideband
  `AcceleratorCommandDescriptor`, descriptor identity, normalized footprint,
  and owner/domain guard evidence.
- Tests that must prevent regression: dirty reserved fields reject; nonzero raw
  virtual-thread hint rejects; missing sideband rejects; sideband/reference
  mismatch rejects.

### Silent runtime fallback after rejected accelerator command

- Idea: after an L7-SDC runtime rejection, execute the workload through
  DmaStreamCompute, StreamEngine, VectorALU, GenericMicroOp, scalar, or ALU
  lowering and report success.
- Why rejected: runtime rejection is an architectural result. Alternative
  lowering is legal only before an accelerator command is emitted.
- Canonical replacement: compiler-only pre-emission selection between L7-SDC,
  DmaStreamCompute, or ordinary CPU lowering.
- Tests that must prevent regression: no runtime fallback tests; compiler
  adoption mode rejects; DmaStreamCompute rejection cannot route to L7-SDC;
  StreamEngine/VectorALU helper success cannot prove accelerator success.

### Backend completion or direct destination mutation as architectural visibility

- Idea: treat device completion or backend destination mutation as sufficient
  architectural publication.
- Why rejected: architectural visibility belongs to the commit plane. Backend
  output must be staged, exact, rollback-safe, owner/domain revalidated, and
  conflict-validated before publication.
- Canonical replacement: `AcceleratorToken` state reaches commit-pending, then
  staged writes publish only through the commit coordinator.
- Tests that must prevent regression: staged writes invisible before commit;
  direct destination mutation attempt faults; partial staged coverage faults or
  rolls back; owner/domain drift blocks commit.

### Evidence identity as authority

- Idea: let telemetry, replay evidence, certificate identity, token identity, or
  diagnostic registry state authorize descriptor acceptance, command submit,
  exception publication, or commit.
- Why rejected: those surfaces explain or identify events; they do not grant
  authority.
- Canonical replacement: owner/domain guard and mapping epoch validation before
  descriptor acceptance, capability acceptance, submit, device execution
  authorization, token observation, exception publication, and commit.
- Tests that must prevent regression: evidence-is-not-authority tests;
  telemetry counter cannot authorize commit; token handle alone cannot commit;
  replay/certificate identity cannot bypass guard revalidation.
