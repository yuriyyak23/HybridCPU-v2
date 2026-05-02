# L7-SDC Test And Rollback Plan

Status: validation and rollback plan; phases 00-14 are closed and Phase 15 full
validation is the next open gate.

## Test strategy

Tests must prove that L7-SDC is a lane7 system-device command model, not a
legacy custom accelerator activation, not a lane6 DmaStreamCompute variant, and
not a StreamEngine/SRF/VectorALU fallback.

Early phases were allowed to reject all submits while parser, guard, token,
placement, capability, and negative-control surfaces were being introduced.
Current Phase 14 state includes guarded token, backend, staging, commit,
observation/control, fault-publication, conflict-manager, MatMul
metadata/schema/fake-backend contours, explicit compiler emission, and
telemetry/evidence export. Documentation is quarantined to those implemented
boundaries and must not create runtime fallback or authority by prose.

## Must-have regression blockers

### Legacy quarantine

- `CustomAcceleratorMicroOp.Execute` remains fail-closed until the legacy type is
  removed or replaced by canonical L7-SDC carriers.
- `InstructionRegistry.RegisterAccelerator` does not grant decode authority.
- `InstructionRegistry.RegisterAccelerator` does not grant execution authority.
- `InstructionRegistry.RegisterAccelerator` does not grant commit authority.
- `MatMulAccelerator.Execute` cannot publish architectural memory.
- Legacy accelerator DMA registration/transfer seams remain unsupported as
  production L7-SDC authority. The Phase 07 staged fake backend is separate and
  cannot call legacy custom execution.

Suggested test areas:

- `Phase4Extensibility*`
- new `L7SdcLegacyQuarantineTests`

### Placement and ISA

- `ACCEL_SUBMIT` must classify as `InstructionClass.System`.
- `ACCEL_SUBMIT` must place as `SlotClass.SystemSingleton`.
- `ACCEL_SUBMIT` must call
  `SetHardPinnedPlacement(SlotClass.SystemSingleton, 7)`, not class-flexible
  placement.
- `ACCEL_SUBMIT` must be lane7-only.
- `ACCEL_SUBMIT` must not use `BranchControl` authority.
- `ACCEL_SUBMIT` must not be accepted in lane6 `DmaStreamClass`.
- Branch/System alias conflict tests remain green.
- `DmaStreamCompute` remains lane6 `DmaStreamClass`.
- No VLIW instruction size change.
- No 8-slot bundle change.
- Dense submit/poll traffic must be throttleable so lane7 branch/system
  progress is not silently starved.

Suggested test areas:

- `Phase09*`
- `CompilerV5ContractAlignment*`
- new `L7SdcPlacementTests`

### Descriptor ABI

- Descriptor sideband is required.
- Native raw carrier slot index must be 7.
- Native raw carrier placement must be hard-pinned `SystemSingleton`.
- Raw reserved bits are rejected as ABI.
- Raw `VirtualThreadId` hints are rejected as authority.
- Raw pointer fields are rejected as authority.
- Raw custom opcode registry identity is rejected as authority.
- Dirty descriptor reserved fields reject.
- Unknown magic rejects.
- Unknown ABI rejects.
- Unknown accelerator class/id rejects.
- Unknown operation rejects.
- Unsupported shape rejects.
- Unsupported datatype rejects.
- Unsupported layout rejects.
- Missing source/destination ranges reject.
- Non-normalized footprint rejects.
- Descriptor identity hash mismatch rejects.
- Normalized footprint hash mismatch rejects.
- Missing owner/domain binding rejects.

Suggested test areas:

- new `L7SdcDescriptorParserTests`
- parity tests modeled after `DmaStreamCompute*Descriptor*`

### Authority

- Owner/domain guard is required before descriptor acceptance.
- Owner/domain guard is required before capability acceptance.
- Owner/domain guard is required before command submission.
- Owner/domain guard is required before device execution authorization.
- Owner/domain guard is required before token commit.
- Owner/domain guard is required before exception publication.
- Telemetry is evidence only.
- Replay evidence is evidence only.
- Certificate identity is evidence only.
- Token identity is evidence/container only.
- Registry success is not authority.

Suggested test areas:

- new `L7SdcOwnerDomainGuardTests`
- new `L7SdcEvidenceIsNotAuthorityTests`

### Token lifecycle

- `ACCEL_SUBMIT` creates a token only after guarded descriptor and capability
  acceptance.
- Legal states are:

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

- Illegal transitions reject.
- Token alone cannot commit.
- Token handle is an opaque lookup key, not authority.
- `ACCEL_SUBMIT rd` returns nonzero token handle on accepted submit.
- `ACCEL_SUBMIT rd` returns zero for non-trapping rejection or performs no
  architectural write on precise fault.
- `ACCEL_POLL rd` returns packed token state/status/fault code.
- `ACCEL_WAIT rd` returns final packed status.
- Device completion is not architectural commit.
- Owner/domain drift prevents commit.
- Mapping epoch drift prevents commit.
- Faulted/canceled/timed-out tokens cannot become committed.
- Token detach/suspend requires owner/domain-bound process context.
- Detach/suspend requires pinned or epoch-validated mappings and IOMMU/domain
  epoch binding.

Suggested test areas:

- new `L7SdcTokenLifecycleTests`
- new `L7SdcContextSwitchTests`

### Backend and commit

- Null backend cannot publish memory.
- Fake backend writes only staged buffers.
- Direct architectural write attempt is detected and rejected/faulted.
- Staged writes are invisible before commit.
- Commit requires exact staged-write coverage.
- Partial staged write rolls back or faults.
- Commit invalidates overlapping SRF windows.
- Commit updates or invalidates cache model.
- Commit records bytes committed.
- Owner/domain drift after device completion prevents commit.
- If owner/domain is invalid at completion, user-visible commit is forbidden,
  token becomes `Faulted`/`Abandoned`, and only privileged diagnostics may
  record the device fault.

Suggested test areas:

- new `L7SdcBackendTests`
- new `L7SdcCommitTests`
- `StreamRegisterFile*`

### Memory conflicts

v1 policy is serialize or reject. Tests must cover:

- CPU store overlaps accelerator read/write.
- CPU load overlaps accelerator write.
- DmaStreamCompute overlaps accelerator write.
- Accelerator write overlaps SRF warmed window.
- Assist/SRF warm overlaps accelerator write.
- Two accelerator tokens write same region.
- Fence/serializing boundary while accelerator token active.
- VM/domain transition while accelerator token active.
- Submit-time footprint reservation.
- Execution-time conflict monitoring.
- Commit-time final validation.

`ExternalAcceleratorConflictManager` must own the active token footprint table,
CPU/DmaStreamCompute/SRF/assist overlap checks, and SRF/cache invalidation on
commit.

Suggested test areas:

- new `L7SdcConflictTests`
- `DmaStreamCompute*`
- `AssistRuntime*`
- `StreamRegisterFile*`

### No silent fallback

- Runtime rejection cannot fallback to DmaStreamCompute.
- Runtime rejection cannot fallback to StreamEngine.
- Runtime rejection cannot fallback to VectorALU.
- Runtime rejection cannot fallback to GenericMicroOp.
- Runtime rejection cannot fallback to ALU/scalar/vector lowering.
- DmaStreamCompute rejection cannot fallback to external accelerator.
- Compiler may choose a non-accelerator lowering only before emitting an
  accelerator command.

Suggested test areas:

- new `L7SdcNoFallbackTests`
- compiler strategy tests
- existing DmaStreamCompute fail-closed tests

### MatMul migration

- `MatMulCapabilityProvider` advertises capability metadata only.
- MatMul descriptor validates `A_base`, `B_base`, `C_base`, `M`, `N`, `K`,
  `lda`, `ldb`, `ldc`, tile sizes, datatypes, layout flags, and `AllOrNone`
  partial policy.
- Unsupported MatMul shapes reject.
- Unsupported datatype combinations reject.
- Legacy `MatMulAccelerator.Execute` remains non-architectural.
- No production L7-SDC path may call `ICustomAccelerator.Execute()`.
- Fake backend MatMul result is staged and invisible before token commit.

Suggested test areas:

- `Phase4Extensibility*`
- new `L7SdcMatMulCapabilityTests`

### Compiler path

- High-level accelerator intent lowers to native lane7 `ACCEL_SUBMIT` only when
  compile-time capability strategy allows it.
- Descriptor sideband travels through compiler transport metadata.
- Unknown adoption/compatibility modes reject.
- Regular stream compute still lowers to lane6 `DmaStreamCompute`.
- Runtime rejection of emitted `ACCEL_SUBMIT` does not scalarize or streamize.

Suggested test areas:

- `CompilerV5ContractAlignment*`
- new `L7SdcCompilerEmissionTests`

## Telemetry counters

Telemetry is evidence only. Required counters:

- capability query attempts
- capability query successes
- capability query rejects
- descriptor parse attempts
- descriptor accepts
- descriptor rejects
- submit attempts
- submit accepted
- submit rejected
- tokens created
- tokens validated
- tokens queued
- tokens running
- tokens device-completed
- tokens commit-pending
- tokens committed
- tokens faulted
- tokens canceled
- tokens timed out
- tokens abandoned
- device busy rejects
- queue full rejects
- domain rejects
- owner drift rejects
- mapping epoch drift rejects
- footprint conflict rejects
- direct-write violation rejects
- commit rollback count
- bytes read
- bytes staged
- bytes committed
- operation count
- latency cycles
- SRF invalidations caused by accelerator commit
- cache invalidations caused by accelerator commit
- DmaStreamCompute conflict rejects
- lane7 submit/poll throttle rejects

Counter tests must assert that counter evidence cannot authorize descriptor
acceptance, command submission, token commit, or exception publication.

## Documentation claim safety

Documentation tests must reject affirmative claims that would make evidence,
legacy scaffolds, helper surfaces, or lane aliases look authoritative. Final
specifications must keep these boundaries explicit:

- L7-SDC is lane7 `SystemSingleton`, hard-pinned to lane7.
- L7-SDC commands use typed sideband descriptors and clean raw carriers.
- Owner/domain/mapping guard decisions are authority.
- Token handles, telemetry snapshots, replay/certificate ids, and registry
  metadata are evidence only.
- External device completion is not commit.
- Direct backend writes are not architectural commit.
- Staged-write token commit is the only L7-SDC architectural publication path.
- `DmaStreamCompute` remains lane6 CPU-native stream compute and is not a
  runtime fallback for rejected L7-SDC commands.
- No production L7-SDC path may call `ICustomAccelerator.Execute()`.
- Detach/suspend claims must mention pinned or epoch-validated mappings and
  IOMMU/domain epoch binding.

## Suggested validation sequence

Phases 00-14 have already satisfied their focused gates. Phase 15 starts from
the full validation baseline without changing the authority model.

1. Run legacy quarantine tests.
2. Run placement/topology/compiler slot tests.
3. Run descriptor parser negative tests.
4. Run owner/domain guard tests.
5. Run token lifecycle tests with null backend.
6. Run fake backend staging tests.
7. Run commit and rollback tests.
8. Run memory conflict tests.
9. Run no-silent-fallback tests.
10. Run MatMul capability tests.
11. Run compiler emission tests.
12. Run telemetry/evidence tests.
13. Run documentation claim-safety tests.
14. Run existing baseline filters:

```text
DmaStreamCompute*
Phase09*
Phase12*
AssistRuntime*
StreamRegisterFile*
CompilerV5ContractAlignment*
Phase4Extensibility*
```

15. Run full repository validation.

## Rollback rules

- Do not revert unrelated files.
- Do not delete fail-closed seams prematurely.
- Do not weaken owner/domain guards.
- Do not reinterpret telemetry as authority.
- Do not reinterpret replay evidence as authority.
- Do not reinterpret certificates as authority.
- Do not reinterpret token identity as authority.
- Do not reinterpret registry success as authority.
- If execution risk appears, disable submit/backend execution while preserving
  parse, guard, token, telemetry, and fail-closed surfaces.
- If commit risk appears, stop at `CommitPending` and fault/reject commit.
- If conflict coverage is incomplete, reject overlapping submits
  conservatively.
- If compiler emission is unsafe, disable accelerator lowering while keeping
  runtime rejection tests.
- If MatMul migration is unsafe, unregister the provider while keeping legacy
  fixture quarantine.

## Minimum release gate

The feature can be considered architecture-ready only when:

- all required placement and authority tests pass
- all required descriptor rejection tests pass
- no direct write path can publish memory
- token commit is the only architectural visibility point
- all v1 conflict classes serialize or reject
- no runtime fallback test can produce successful alternative execution
- DmaStreamCompute, StreamEngine/SRF, assist, and compiler baseline tests remain
  green
- `TestAssemblerConsoleApps` matrix diagnostics have been compared against
  `Documentation/AsmAppTestResults.md` with no unexplained regression
