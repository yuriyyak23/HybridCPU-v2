# ADR 10: External Accelerator L7 SDC Gate

## Status

Proposed design gate.

This ADR does not approve executable L7 ISA. Current `ACCEL_*` carriers remain fail-closed/model-only.

## Context

Phase 10 covers TASK-008: keep L7-SDC model APIs separate from executable ISA and define the gates for any future executable `ACCEL_*` surface.

The repository contains L7 carrier micro-ops, descriptors, tokens, queues, fences, capability metadata, register ABI helpers, fake backend, conflict manager, and commit model surfaces. Those surfaces are not production instruction execution.

## Current Contract

- `SystemDeviceCommandMicroOp.Execute(...)` throws fail-closed.
- `SystemDeviceCommandMicroOp.WritesRegister = false`.
- `ACCEL_QUERY_CAPS`, `ACCEL_SUBMIT`, `ACCEL_POLL`, `ACCEL_WAIT`, `ACCEL_CANCEL`, and `ACCEL_FENCE` are carrier/classification surfaces, not executable backend dispatch.
- `ACCEL_QUERY_CAPS` and `ACCEL_POLL` use `CsrOrdered` metadata.
- `ACCEL_SUBMIT` uses `MemoryOrdered` metadata.
- `ACCEL_WAIT`, `ACCEL_CANCEL`, and `ACCEL_FENCE` use `FullSerial` metadata.
- `AcceleratorRegisterAbi` is model-side result packing only and does not imply architectural `rd` writeback.
- `AcceleratorTokenStore`, `AcceleratorCommandQueue`, `AcceleratorFenceModel`, fake backends, conflict manager, and commit model are explicit model/test surfaces.
- `FakeMatMulExternalAcceleratorBackend` is test-only and not production device protocol.
- L7 model APIs do not publish architectural memory or exceptions through `ACCEL_*` instruction execution.
- Compiler/backend production lowering must not expect executable `ACCEL_*`, register writeback, backend dispatch, queue execution, wait/fence/cancel behavior, or memory publication.

## Decision

Keep L7-SDC model-only under the current contract.

Future executable L7 ISA requires a separate implementation ADR before changing `SystemDeviceCommandMicroOp.Execute`, `WritesRegister`, result publication, backend dispatch, token admission, or memory commit semantics.

The recommended future path is staged:

- first possible tier: read-only `QUERY_CAPS` and `POLL`, only after result publication, privilege, ordering, and fault semantics are approved;
- later tier: `SUBMIT`, `WAIT`, `FENCE`, and `CANCEL`, only after token, queue, backend, commit/retire, conflict, cache, and compiler gates are complete.

## Accepted Direction

### Read-Only Tier Gate

Executable `QUERY_CAPS` or `POLL` requires:

- instruction operand encoding;
- `rd` or CSR result publication;
- `WritesRegister` metadata change rules;
- privilege and guard checks;
- result word ABI;
- precise rejection/fault behavior;
- ordering semantics;
- no memory side effects;
- tests proving no backend dispatch or memory commit occurs.

### Submit/Wait/Fence/Cancel Tier Gate

Executable `SUBMIT`, `WAIT`, `FENCE`, and `CANCEL` require:

- token ID namespace and active token store;
- guarded queue admission and backpressure;
- production backend dispatch interface;
- production device protocol separate from fake/test backend;
- staged memory write model;
- commit/retire publication boundary;
- precise fault publication and priority;
- wait/fence/cancel semantics;
- global conflict service integration;
- cache flush/invalidate integration;
- context-switch/trap/squash cancellation;
- compiler/backend capability contract.

### Backend Boundary

Fake/test backends may remain useful for model tests, but production execution requires a backend contract that states:

- device identity;
- capability discovery;
- memory portal authority;
- staging versus direct write restrictions;
- completion/fault reporting;
- cancellation;
- no direct architectural memory publication outside commit/retire rules.

## Rejected Alternatives

### Alternative 1: Treat Register ABI Helpers As Writeback

Rejected. Packing helpers are not pipeline writeback and current carriers have `WritesRegister = false`.

### Alternative 2: Execute Full SUBMIT First

Rejected. Full submit touches token lifecycle, backend dispatch, memory effects, ordering, cache visibility, precise faults, and compiler lowering.

### Alternative 3: Promote Fake Backend To Production Protocol

Rejected. `FakeMatMulExternalAcceleratorBackend` is test-only and cannot define production external accelerator protocol.

### Alternative 4: Publish Backend Memory Directly

Rejected. Backend results must not publish memory outside an approved staged commit/retire boundary.

## Exact Non-Goals

- Do not implement executable L7 in this ADR.
- Do not change `SystemDeviceCommandMicroOp.Execute`.
- Do not change `WritesRegister`.
- Do not define `rd` writeback as current behavior.
- Do not define fake backend as production protocol.
- Do not allow backend results to publish memory directly.
- Do not authorize compiler/backend production lowering to executable L7.
- Do not claim L7 cache/IOMMU/conflict integration is complete.

## Required Prerequisites Before Code

- Separate L7 executable ISA ADR.
- Result publication contract: `rd` or CSR.
- Privilege, guard, owner/domain, and device selection contract.
- Token store and queue/backpressure contract.
- Backend dispatch and production device protocol.
- Staged memory write and commit/retire boundary.
- Precise fault priority.
- Wait/fence/cancel semantics.
- Phase 05 global conflict service.
- Phase 06 backend/addressing integration.
- Phase 09 cache flush/invalidate protocol.
- Phase 11 compiler/backend lowering contract.
- Phase 12 conformance and documentation migration tests.

## Required Tests Before Any Executable L7 Claim

- Current direct `ACCEL_*` execution remains fail-closed until gate approval.
- Current `WritesRegister == false` remains protected until result publication is approved.
- Register ABI helper tests remain model-only.
- Future `QUERY_CAPS`/`POLL` tests prove read-only behavior and correct result publication.
- Future `SUBMIT` tests allocate/admit a token without early memory publication.
- Future backend tests reject fake/test backend as production protocol.
- Future `WAIT` and `FENCE` tests prove ordering and completion semantics.
- Future `CANCEL` tests prove no commit after cancel.
- Future fault tests prove precise publication.
- Future cache/conflict tests prove memory visibility discipline.
- Compiler/backend tests reject executable L7 lowering while the gate is closed.

## Documentation Migration Rule

Documentation must call current L7 APIs model-only.

Future read-only and full-submit tiers may be documented only under Future Design until ADR, code, tests, and migration review are complete.

No document may state that `ACCEL_*` is executable ISA or writes architectural registers under the current contract.

## Code Evidence

- `HybridCPU_ISE\Core\Pipeline\MicroOps\SystemDeviceCommandMicroOp.cs`
  - Defines L7 `SystemDeviceCommandKind` values.
  - `Execute(...)` throws fail-closed.
  - `WritesRegister = false`.
  - `ACCEL_SUBMIT` is `MemoryOrdered`.
  - `ACCEL_WAIT`, `ACCEL_CANCEL`, and `ACCEL_FENCE` are `FullSerial`.
- `HybridCPU_ISE\Core\Execution\ExternalAccelerators\Tokens\AcceleratorRegisterAbi.cs`
  - Explicitly comments that result packing is model-side only and current carriers do not perform architectural `rd` writeback.
- `HybridCPU_ISE\Core\Execution\ExternalAccelerators\Tokens\AcceleratorTokenStore.cs`
  - Provides model token store and guarded lookup/cancel/commit-publication surfaces.
- `HybridCPU_ISE\Core\Execution\ExternalAccelerators\Queues\AcceleratorCommandQueue.cs`
  - Provides model queue admission surfaces.
- `HybridCPU_ISE\Core\Execution\ExternalAccelerators\Fences\AcceleratorFenceModel.cs`
  - Provides model fence/wait observation surfaces.
- `HybridCPU_ISE\Core\Execution\ExternalAccelerators\Backends\FakeMatMulExternalAcceleratorBackend.cs`
  - `IsTestOnly => true`.
- `HybridCPU_ISE\Core\Execution\ExternalAccelerators\Backends\ExternalAcceleratorBackends.cs`
  - Backend result publication flags are not architectural memory/exception publication authority.

## Strict Prohibitions

This ADR must not be used to claim:

- L7 `ACCEL_*` is executable ISA;
- `QUERY_CAPS` or `POLL` currently writes `rd`;
- `SUBMIT` currently dispatches production backend work;
- fake/test backend is production protocol;
- model queue/fence/token APIs are instruction execution;
- L7 backend results can publish memory outside commit/retire;
- compiler/backend may production-lower to executable `ACCEL_*`.
