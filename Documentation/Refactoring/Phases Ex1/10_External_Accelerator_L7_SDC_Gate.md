# Phase 10 - External Accelerator L7 SDC Gate

Status:
Future gated / Design required. Current contract remains model-only.

Scope:
Cover TASK-008: keep L7-SDC model APIs separate from executable ISA and define gates for any future executable `ACCEL_*`.

Current code evidence:
- `SystemDeviceCommandMicroOp.Execute(...)` throws fail-closed.
- `SystemDeviceCommandMicroOp.WritesRegister = false`.
- `ACCEL_QUERY_CAPS`, `ACCEL_SUBMIT`, `ACCEL_POLL`, `ACCEL_WAIT`, `ACCEL_CANCEL`, and `ACCEL_FENCE` are carriers/classifications, not executable backend dispatch.
- `ACCEL_SUBMIT` is `MemoryOrdered`; `ACCEL_WAIT`, `ACCEL_CANCEL`, and `ACCEL_FENCE` are `FullSerial` metadata/classification.
- `AcceleratorRegisterAbi` is model-only result packing.
- `AcceleratorCommandQueue`, `AcceleratorFenceModel`, `AcceleratorTokenStore`, fake backends, conflict manager, and commit model are explicit model/test surfaces.
- `FakeMatMulExternalAcceleratorBackend` is test-only and not production device protocol.

Architecture decision:
Current contract:
- L7-SDC is model-only.
- `ACCEL_*` direct execution remains fail-closed.
- No architectural `rd` writeback exists for L7 carriers.
- Model queue/fence/register/backend APIs do not imply executable ISA.

Future gated:
- Executable L7 ISA requires a separate ADR.
- First possible tier may be read-only `QUERY_CAPS`/`POLL`, but only after `rd` or CSR result publication, ordering, privilege, and fault semantics are defined.
- Full `SUBMIT`/`WAIT`/`FENCE`/`CANCEL` is future-gated behind token store, queue/backpressure, backend dispatch, staged write commit, retire faults, ordering/conflict, cache protocol, and compiler contract.

Non-goals:
- Do not implement executable L7 in this phase.
- Do not treat register ABI packing as architectural writeback.
- Do not make fake/test backend a production protocol.
- Do not let backend results publish memory outside commit/retire rules.

Required design gates:
- L7 instruction operand encoding and `rd` or CSR publication.
- Privilege, guard, owner/domain, and device selection.
- Result word ABI for `QUERY_CAPS` and `POLL`.
- Token ID namespace and queue admission.
- Backend dispatch interface and production device protocol.
- Staged memory write model and commit/retire boundary.
- Fault priority and exception publication.
- Wait/fence/cancel semantics.
- Global conflict service and cache invalidation integration.

Implementation plan:
1. Write L7 ADR before changing `SystemDeviceCommandMicroOp`.
2. If approved, implement read-only `QUERY_CAPS`/`POLL` first with no memory side effects.
3. Define register/CSR writeback and tests.
4. Add executable submit tier only after token/queue/backend/commit/order/cache gates.
5. Keep fake backends in test-only namespace and require production backend contract for non-test execution.

Affected files/classes/methods:
- `SystemDeviceCommandMicroOp.Execute`
- `SystemDeviceCommandMicroOp.WritesRegister`
- `AcceleratorRegisterAbi`
- `AcceleratorCommandQueue`
- `AcceleratorFenceModel`
- `AcceleratorToken`
- `AcceleratorTokenStore`
- `ExternalAcceleratorBackends`
- `AcceleratorCommitModel`
- `ExternalAcceleratorConflictManager`
- compiler/backend `ACCEL_*` lowering surfaces

Testing requirements:
- Current fail-closed `ACCEL_*` tests remain.
- Register ABI model-only tests remain distinct from writeback tests.
- Future `QUERY_CAPS`/`POLL` tests prove read-only behavior and correct result publication.
- Future `SUBMIT` tests return token/status without publishing computed memory early.
- Future `WAIT`/`FENCE` tests prove ordering and completion.
- Future `CANCEL` tests prove no commit after cancel.
- Fake backend tests prove test-only boundary.

Documentation updates:
Document all current L7 APIs as model-only. Document read-only tier and full submit tier only as Future gated until ADR, code, tests, and migration are complete.

Compiler/backend impact:
Current compiler/backend must not emit production `ACCEL_*` expecting register writeback, backend dispatch, queueing, wait/fence, cancel, or memory publication. Future lowering requires feature/capability checks and conformance tests.

Compatibility risks:
Changing `WritesRegister` or `Execute` is breaking. Read-only operations can still break if result publication, privilege, or ordering is underspecified.

Exit criteria:
- L7 remains model-only until ADR.
- ADR prerequisites are documented.
- Read-only tier and full submit tier are separately gated.

Blocked by:
Phase 01 current contract. Full executable L7 is also blocked by phases 03, 04, 05, 06, 09, 11, and 12.

Enables:
Safe future L7 ADR, staged external accelerator ISA work, and compiler/backend capability planning.

