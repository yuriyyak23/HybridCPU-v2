# ADR 13: Dependency Graph And Execution Order Gate

## Status

Planning dependency ADR.

This ADR is documentation only. It does not approve implementation and does not override any phase-specific gate.

## Context

Phase 13 records the safe execution order for HybridCPU-v2 DSC/L7/cache/compiler refactoring. It prevents downstream surfaces from being used as proof that upstream architecture prerequisites exist.

## Decision

Future gates must execute in dependency order.

Parser/model surfaces may be built earlier only when explicitly labeled parser-only or model-only and when they do not change current ISA behavior.

Compiler/backend production lowering is last-mile work and must not force architecture behavior that is not implemented.

Phase 12 remains the conformance/documentation migration gate for any future claim that moves from Future Design into Current Implemented Contract.

## Dependency Order

1. Preserve current contract and fail-closed tests.
2. Approve executable lane6 DSC Option B only if the project chooses to proceed.
3. Define token store and issue/admission allocation.
4. Define precise fault metadata and retire publication.
5. Define backend/addressing resolver and no-fallback behavior.
6. Define ordering/conflict service and litmus tests.
7. Define non-coherent cache flush/invalidate protocol.
8. Define DSC2/capability parser work as parser-only unless executable gates are complete.
9. Enable executable lane6 DSC MVP only behind a feature gate.
10. Consider L7 read-only tier ADR.
11. Consider full L7 submit/wait/fence/cancel only after shared token/order/cache foundations.
12. Enable compiler/backend production lowering only after conformance and documentation migration.

## Blocking Conditions

### Executable Lane6 DSC

Blocked by:

- Phase 02 executable DSC ADR;
- Phase 03 token lifecycle;
- Phase 04 precise faults;
- Phase 05 ordering/conflict service;
- Phase 06 backend/addressing;
- Phase 08 all-or-none/progress contract;
- Phase 09 non-coherent cache protocol;
- Phase 11 compiler contract;
- Phase 12 tests and migration.

Phase 07 DSC2 is required for new descriptor features but parser-only DSC2 does not make lane6 executable.

### Async DMA Overlap

Blocked by:

- token scheduler and completion model;
- global conflict service;
- CPU load/store/atomic hooks;
- fence/wait/poll semantics;
- replay/squash/trap/context-switch cancellation;
- cache flush/invalidate protocol.

### Executable L7 ISA

Blocked by:

- Phase 10 L7 ADR;
- result publication contract;
- token/queue/backpressure model;
- production backend dispatch;
- staged commit/retire model;
- precise faults;
- global conflict service;
- backend/addressing authority;
- cache invalidation/flush;
- compiler/backend contract;
- conformance tests.

### Cache Visibility And Coherency

Blocked by:

- range-based data-cache invalidate;
- data flush/writeback decision;
- assist/SRF/prefetch invalidation;
- separate VLIW fetch invalidation;
- memory/coherency observer hooks;
- future coherent-DMA ADR before any coherent DMA claim.

### Compiler/Backend Production Lowering

Blocked by:

- executable DSC/L7 implementation;
- capability discovery;
- ordering/fence/wait/poll semantics;
- backend/addressing contract;
- cache protocol;
- conformance tests;
- documentation migration.

## Rejected Alternatives

### Alternative 1: Use Downstream Compiler Work To Drive ISA Semantics

Rejected. Compiler lowering cannot define missing execution, fault, ordering, cache, or backend semantics.

### Alternative 2: Treat Parser/Model Code As Implementation Approval

Rejected. Parser/model code can exist as evidence only when explicitly labeled non-executable.

### Alternative 3: Skip Cache Or Conflict Gates For MVP

Rejected for executable overlap claims. A fully serialized MVP may be approved separately, but it must not claim async overlap or coherent visibility.

## Exact Non-Goals

- Do not implement CPU/ISE code.
- Do not approve executable lane6 DSC.
- Do not approve executable L7.
- Do not approve DSC2 execution.
- Do not approve coherent DMA.
- Do not approve compiler/backend production lowering.
- Do not replace phase-specific ADRs.

## Downstream Evidence Non-Inversion

Downstream surfaces may be useful only as explicitly labeled parser-only,
model-only, test-only, or sideband-only evidence. They must not satisfy upstream
executable gates.

The following evidence is not upstream execution evidence:

- parser-only DSC2 descriptors, capability grants, and normalized footprints;
- model token stores, retire observations, progress diagnostics, and helper/runtime tokens;
- L7 fake backend, capability registry, queue, fence, token, register ABI, and commit model APIs;
- IOMMU backend infrastructure, addressing resolver decisions, and no-fallback resolver tests;
- conflict/cache observers, passive conflict observations, and explicit non-coherent invalidation fan-out;
- compiler sideband emission, descriptor preservation, and carrier projection.

None of these surfaces can close executable lane6 DSC, executable L7, DSC2
execution, coherent DMA/cache, async DMA overlap, or production compiler/backend
lowering gates.

## Documentation Migration Rule

The graph may be updated only when:

- a phase-specific ADR is added or changed;
- implementation and tests land;
- a future gate is explicitly closed or reopened.

Downstream docs must cite upstream gates when describing future executable behavior.

## Code And Documentation Evidence

- Phase 01 locks current fail-closed/model-only contract.
- Phase 02 keeps executable lane6 DSC future-gated.
- Phase 03 through Phase 09 define the required DSC execution foundations.
- Phase 10 gates L7 executable ISA.
- Phase 11 blocks compiler/backend production lowering.
- Phase 12 defines conformance and migration.
- `HybridCPU_ISE.Tests\tests\Ex1Phase13DependencyOrderTests.cs` verifies dependency order, planning-only status, Phase12 migration gating, and downstream evidence non-inversion.
- `Documentation\Stream WhiteBook\ExtentionsAnalytic_Audit.md` lists corrected final recommendations and dependency graph for TASK-001 through TASK-010.

## Strict Prohibitions

This ADR must not be used to claim:

- the dependency graph is implementation approval;
- downstream model/parser/compiler surfaces satisfy upstream gates;
- executable lane6 DSC is approved;
- DSC2 execution is approved;
- async overlap is implemented;
- L7 `ACCEL_*` is executable;
- IOMMU backend infrastructure proves current executable DSC/L7 memory translation;
- conflict/cache observers prove global conflict authority or coherent DMA/cache;
- cache hierarchy is coherent;
- compiler/backend production lowering is available.
