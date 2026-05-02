# Phase 6 - Future Feature Backlog

Status date: 2026-04-29.

Status: open backlog. No item in this file is current implemented behavior.

Goal: separate future architecture from refactoring cleanup.

## Current Triage Result

Phase 3 selected Option A for lane6 `DmaStreamComputeMicroOp`: keep the
fail-closed descriptor/decode evidence carrier. Therefore executable lane6 DSC
is a future feature only. No item below is approved for implementation by this
backlog.

The first backlog gate is documented in
`Phase_06_FEATURE_000_Executable_Lane6_DSC_Gate.md`.

## Backlog Policy

Each future feature requires:

- explicit architecture approval;
- affected component list;
- compatibility review;
- dependency review against already-accepted current-contract boundaries;
- conformance tests;
- documentation migration from Future Design to Current Implemented Contract
  only after code lands.

## Future Features

| Feature | Description | Main affected components | Required before implementation |
|---|---|---|---|
| FEATURE-000 | Executable lane6 `DmaStreamComputeMicroOp` | lane6 carrier, pipeline issue/execute/memory/retire stages, DSC token/runtime/backend, compiler lowering | Separate Option B approval, pipeline stage, token allocation point, runtime invocation point, commit/retire boundary, fault priority, precise exception path, replay/squash/trap/context-switch behavior, memory ordering, physical vs virtual/IOMMU decision, sync vs async completion model, compiler/backend lowering contract, conformance suite. |
| FEATURE-001 | Stride/tile/2D/scatter-gather DSC | `DmaStreamComputeDescriptorParser`, descriptor/runtime/backend, compiler lowering | DSC ABI v2 or extension mechanism, parser/runtime design, range normalization tests. |
| FEATURE-002 | Partial completion as successful mode | `DmaStreamComputeToken`, commit result, runtime staging | Visibility, rollback, retry/replay, compiler-visible completion contract. |
| FEATURE-003 | DMA queue/scheduler | `DMAController`, `StreamEngine.BurstIO`, pipeline memory stages | Queue capacity, arbitration, completion event model, deterministic scheduler tests. |
| FEATURE-004 | Async CPU/DMA overlap | `DMAController`, `StreamEngine.BurstIO`, CPU pipeline, memory subsystem | Ordering rules, completion visibility, load/store litmus tests. |
| FEATURE-005 | Executable fence/order instructions | `SystemDeviceCommandMicroOp.Execute`, fence model, conflict manager, CPU load/store pipeline | Global memory/order model, token/DMA completion model, fence conformance tests. |
| FEATURE-006 | External accelerator command ISA | L7 carriers, register ABI, token store, queue, backend, commit coordinator, compiler | `rd` writeback, backend dispatch, queue/backpressure, commit/exception semantics. |
| FEATURE-007 | MMIO/register device protocol | processor memory APIs, device bus/registry, backend | Address map, register layout, privilege/security model, MMIO tests. |
| FEATURE-008 | Interrupt/polling completion | `DMAController`, token store, L7 carriers, interrupt controller | Completion event model, interrupt routing, poll/wait state machine tests. |
| FEATURE-009 | Global conflict/load-store hook | conflict manager, CPU load/store pipeline, token store, commit coordinator, DSC admission | Installation model, conflict response policy, absent-versus-installed tests. |
| FEATURE-010 | Cache/coherency model | memory subsystem, cache/SRF modules, BurstIO, DSC runtime, L7 commit/conflict | Coherency policy, invalidation rules, fence visibility, rollback tests. |

## Dependency Ordering

The backlog is not strictly linear, but implementation approval must respect
these dependency constraints:

- `FEATURE-000` is the gate for any claim that lane6 DSC executes through the
  CPU pipeline.
- `FEATURE-003` and `FEATURE-004` cannot become architectural async overlap
  until `FEATURE-000` or a separate non-DSC DMA execution model defines
  completion and ordering.
- `FEATURE-005`, `FEATURE-008`, and `FEATURE-009` depend on a global ordering
  and completion model before they can be executable instruction semantics.
- `FEATURE-010` must be resolved before any future docs claim coherent DSC or
  external accelerator memory visibility.
- `FEATURE-001` and `FEATURE-002` can be designed as descriptor/runtime model
  extensions, but production compiler lowering remains blocked until the
  executable surface is separately approved.

## Current Prohibitions

Do not implement without approval:

- executable `DmaStreamComputeMicroOp.Execute`;
- pipeline token allocation for lane6 DSC;
- production executable DSC compiler/backend lowering;
- executable `ACCEL_*`;
- production `ACCEL_SUBMIT`;
- architectural `rd` writeback for `ACCEL_*`;
- async DMA scheduler/overlap;
- executable fences/order semantics;
- global CPU load/store conflict hook;
- cache/coherency model;
- incompatible DSC1/SDC1 ABI changes.

## Backlog Exit Criteria

The backlog remains valid while Phase 0-5 cleanup and the Phase 3 Option A
decision do not accidentally implement any listed feature. If one feature is
approved, create a dedicated implementation phase before editing code.
