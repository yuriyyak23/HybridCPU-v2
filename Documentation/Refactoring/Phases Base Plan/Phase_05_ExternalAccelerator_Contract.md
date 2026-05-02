# Phase 5 - ExternalAccelerator Contract

Status date: 2026-04-29.

Status: closed for the current implemented contract; executable external
accelerator ISA remains a future architecture decision.

Goal: define current L7-SDC behavior without turning model APIs into
implemented instruction execution.

## Current L7-SDC Carrier Contract

Code evidence:
`HybridCPU_ISE/Core/Pipeline/MicroOps/SystemDeviceCommandMicroOp.cs`.

- L7-SDC carriers are hard-pinned to lane7 `SlotClass.SystemSingleton`.
- `ACCEL_SUBMIT` is `MemoryOrdered` metadata/classification.
- `ACCEL_WAIT`, `ACCEL_CANCEL`, and `ACCEL_FENCE` are `FullSerial`
  metadata/classification.
- `ACCEL_QUERY_CAPS` and `ACCEL_POLL` are CSR-ordered model surfaces.
- Every carrier has `WritesRegister = false`.
- Every carrier has empty architectural write metadata.
- Direct `Execute(...)` throws fail-closed.
- No carrier writes architectural `rd`.
- No carrier dispatches a backend or publishes staged writes through
  `Execute(...)`.

## Current SDC1 Descriptor Model

Code evidence:

- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Descriptors/AcceleratorDescriptorParser.cs`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Descriptors/AcceleratorCommandDescriptor.cs`

Current SDC1 facts:

- magic: `0x31434453`;
- ABI version: `1`;
- header size: `128`;
- supported class/device/operation: `Matrix`, `ReferenceMatMul`, `MatMul`;
- supported datatypes: `Float32`, `Float64`, `Int32`;
- supported shape: `Matrix2D`, rank `2`;
- range count limit: `16`;
- source and destination ranges are required;
- scratch ranges are required only when scratch bytes are non-zero;
- alignment must be a non-zero power of two;
- owner/domain guard authority must come from the guard plane.

## Current Model-Only APIs

- `AcceleratorRegisterAbi`: model result packing only, not pipeline writeback.
- `AcceleratorFenceModel`: model fence behavior only, not executable
  `ACCEL_FENCE`.
- `AcceleratorCommandQueue`: explicit model queue, placeholder conflict
  evidence by default.
- `FakeMatMulExternalAcceleratorBackend`: limited test-only backend.
- `AcceleratorCommitCoordinator`: guarded model commit coordinator, not
  instruction retire exception path.
- `ExternalAcceleratorConflictManager`: explicit optional model component, not
  global CPU load/store truth.

## Closed Audit Fixes

| Audit ID | Result |
|---|---|
| AUDIT-001 | Register ABI is model-only; carrier writeback remains absent. |
| AUDIT-003 | Model APIs are explicitly separated from instruction execution. |
| AUDIT-004 | Conflict manager is explicit/optional, not global truth. |
| AUDIT-008 | L7 faults are guarded observations/results, not retire exceptions. |
| AUDIT-010 | Fence model is not executable `ACCEL_FENCE`. |

## Test Evidence

- Direct `ACCEL_*` execution negative tests.
- Register ABI model-only tests.
- Fence model-only versus executable `ACCEL_FENCE` negative test.
- Queue admission and placeholder conflict evidence tests.
- Conflict manager optionality tests.
- Commit result tests with no retire exception publication.
- MatMul backend test-only contract tests.
- Compiler tests that preserve no-write carrier semantics.

## Future Device Protocol Gate

Executable `ACCEL_*` requires a separate decision for:

- register operand encoding and `rd` writeback;
- submit timing and token allocation;
- queue capacity and backpressure;
- backend dispatch interface;
- staged write memory portal;
- commit/retire boundary;
- fault priority and exception publication;
- polling/interrupt completion;
- fence/order semantics;
- conflict-manager global integration;
- cache/SRF invalidation and coherency.
