# ADR 12: Testing Conformance And Documentation Migration Gate

## Status

Current documentation migration policy plus future conformance gate.
Phase12 implementation adds meta-conformance guard tests; this ADR remains the
policy record and does not approve executable DSC/L7/DSC2 behavior.

This ADR defines the test and documentation evidence required before any
future-gated feature can be described as current implemented behavior.

## Context

Phase 12 covers the conformance strategy for the current fail-closed/model-only behavior and future executable DSC/L7/IOMMU/cache/compiler features.

The main risk is claim drift: helper/model/parser surfaces can be mistaken for executable architecture unless tests and documentation migration rules block premature wording.

## Current Contract

- Tests must protect lane6 fail-closed behavior.
- Tests must protect L7 fail-closed/model-only behavior.
- Tests must protect DSC1 immutability and `AllOrNone`.
- Tests must protect compiler/backend prohibitions on production executable lowering.
- Documentation must not promote Future Design to Current Implemented Contract without architecture approval, code, tests, and reviewed migration.

## Decision

Adopt a traceability-based conformance gate for every future architectural claim.

A feature may move from Future Design to Current Implemented Contract only when:

1. Architecture approval is recorded.
2. Code implementation lands.
3. Positive and negative tests pass, including compatibility tests unless an explicit compatibility policy retires them.
4. Fault/order/cache/backend/compiler conformance tests pass where applicable.
5. Documentation claim-safety checks pass.
6. Compiler/backend contract is updated when production lowering changes.
7. Migration is reviewed in the same or a later explicit documentation change.

## Required Test Taxonomy

Every feature gate must classify tests as:

- current-contract compatibility tests;
- positive implementation tests;
- negative rejection tests;
- precise fault tests;
- ordering/conflict litmus tests;
- backend/addressing/no-fallback tests;
- cache flush/invalidate tests;
- replay/squash/trap/context-switch cancellation tests;
- compiler/backend conformance tests;
- documentation claim-safety tests.

## Feature-Specific Test Gates

### Lane6 DSC

Required:

- direct `DmaStreamComputeMicroOp.Execute` fail-closed tests until executable gate changes;
- `ExecutionEnabled == false` tests until parser/execution gate changes;
- token issue/admission tests;
- completion/commit/retire tests;
- fault priority tests;
- replay/squash/trap/cancel tests;
- all-or-none rollback tests;
- ordering and cache visibility tests.

### L7 SDC

Required:

- direct `ACCEL_*` fail-closed tests until executable gate changes;
- `WritesRegister == false` tests until result publication is approved;
- model-only register ABI tests;
- read-only tier tests if `QUERY_CAPS`/`POLL` are approved;
- full submit/wait/fence/cancel tests if approved;
- fake-backend test-only boundary tests.

### Addressing And Cache

Required:

- physical/IOMMU backend resolver tests;
- no-fallback tests;
- device/owner/domain/mapping epoch tests;
- translation/permission fault tests;
- range flush/invalidate tests;
- assist/SRF/prefetch invalidation tests;
- separate VLIW fetch invalidation tests;
- coherent-DMA tests only after coherent-DMA ADR.

### Compiler/Backend

Required:

- current production lowering rejection tests;
- descriptor preservation tests;
- model/test helper separation tests;
- future capability-gated lowering tests;
- absent/partial capability rejection tests.

## Documentation Claim-Safety Rule

Documentation must be checked for forbidden current claims:

- lane6 DSC is executable;
- async DMA overlap is implemented;
- current DSC memory access is IOMMU-translated;
- L7 `ACCEL_*` is executable ISA;
- `QUERY_CAPS`/`POLL` write architectural registers;
- fake/test backend is production protocol;
- global conflict service is installed;
- cache hierarchy is coherent for DMA/accelerators;
- compiler/backend may production-lower to executable DSC/L7;
- partial completion is successful architectural mode.

These claims may appear only as Future Design or explicitly rejected alternatives until implementation and tests land.

## Compatibility Policy

Fail-closed tests must remain active while the current contract holds.

If a future implementation changes the contract, the approval record must state whether old fail-closed tests:

- remain as compatibility-mode tests;
- move to feature-disabled mode;
- are replaced by new negative tests;
- are retired with an explicit migration rationale.

## Rejected Alternatives

### Alternative 1: Migrate Documentation On Design Approval Alone

Rejected. Design approval is not implementation evidence.

### Alternative 2: Remove Fail-Closed Tests Before Replacement

Rejected. That would hide accidental execution or writeback regressions.

### Alternative 3: Treat Model Helper Tests As ISA Tests

Rejected. Model/helper tests prove helper behavior only.

## Exact Non-Goals

- Do not rewrite tests in this ADR.
- Do not retire existing tests.
- Do not approve executable features.
- Do not migrate documentation claims.
- Do not change compiler/backend behavior.

## Code And Test Evidence

- `HybridCPU_ISE.Tests\tests\Ex1Phase12ConformanceMigrationTests.cs`
  - Phase12 meta-conformance tests cover the Ex1 phase traceability matrix,
    forbidden current documentation claims, fail-closed compatibility evidence,
    future migration gates, and helper/parser/model/fake-backend separation.
- `HybridCPU_ISE.Tests\tests\Ex1Phase13DependencyOrderTests.cs`
  - Phase13 meta-conformance tests cover dependency-order blockers,
    planning-only status, Phase12 migration gating, and downstream evidence
    non-inversion.
- `HybridCPU_ISE.Tests\tests\DmaStreamCompute*.cs`
  - Existing DSC descriptor/token/runtime/helper tests protect current surfaces.
- `HybridCPU_ISE.Tests\CompilerTests\DmaStreamComputeCompilerContractTests.cs`
  - Existing compiler contract tests cover descriptor emission and rejection behavior.
- `HybridCPU_ISE.Tests\CompilerTests\L7SdcCompilerPhase12Tests.cs`
  - Existing L7 compiler tests cover carrier sideband emission.
- `HybridCPU_ISE.Tests\tests\L7Sdc*.cs`
  - Existing L7 model/conflict/SRF/cache tests protect model-only boundaries.
- `Documentation\Refactoring\Phases Ex1\*.md`
  - Current phase files separate Current Contract from Future Design.

## Exit Rule

No future feature exits its gate until its positive, negative, compatibility, conformance, compiler, and documentation claim-safety tests pass.

## Strict Prohibitions

This ADR must not be used to claim:

- this ADR alone approves implementation or executable behavior;
- documentation migration is complete;
- executable DSC/L7 is approved;
- compiler/backend lowering is approved;
- future design is current implemented behavior.
