# Rollback Boundaries

## Scope

This note externalizes the current rollback envelope implemented by `ReplayToken`.
It is intentionally narrower than a universal replay or recovery theorem.

`ReplayToken` captures enough state to support bounded replay-side rollback for the surfaces it knows how
to materialize exactly. It must fail closed, and it refuses to pretend that partially materialized or
unbound memory snapshots are safe to restore.

Scheduler-side replay reuse is a separate surface documented in
`HybridCPU_ISE/docs/replay-envelope.md`. `ReplayToken` does not define replay
phase validity, legality-witness reuse, or assist freshness.

## What `ReplayToken` captures

The token carries four rollback-relevant surfaces.

- `CaptureRegisterState(...)` snapshots selected architectural integer registers before execution.
- `CaptureMemoryState(...)` snapshots exact byte ranges from the explicitly bound main-memory surface.
- `OwnerThreadId` identifies which virtual-thread context should receive the architectural restore.
- `HasSideEffects` gates whether rollback support is required for the current micro-operation.

The register restore path is architectural, not backend-global: `Rollback(...)`
republishes register values through `RetireCoordinator` instead of reinstalling
one global pre-rollback backend image or claiming to reset the entire
rename/commit/free-list substrate.

## What it refuses to capture

`ReplayToken` deliberately refuses to capture replay-side memory state when the memory contour is not fully
bound and exactly materializable.

- If the token is unbound, `CaptureMemoryState(...)` fails closed instead of reading from a mutable ambient
  fallback.
- If the requested byte range extends past the bound memory surface, capture fails closed instead of storing
  a partial snapshot.
- If the bound memory surface does not materialize the full read, capture fails closed instead of storing a
  zero-filled or truncated image.

The same rules apply on restore.

- `Rollback(...)` requires an explicit binding whenever `PreExecutionMemoryState` is non-empty.
- Restore fails closed if a recorded byte range is outside the bound surface.
- Restore fails closed if the bound memory surface does not materialize the full writeback.

## `CanSafelyRollback()` boundary

`CanSafelyRollback()` is a bounded readiness check, not a blanket guarantee.

- It returns `true` for side-effect-free operations.
- It returns `true` when register state was captured and no replay-side memory restore is needed.
- It returns `true` when memory snapshots exist and every recorded byte range is fully covered by the bound
  main-memory surface.
- It returns `false` when rollback would require replay-side memory restore but the token is unbound.
- It returns `false` when rollback would require a memory restore outside the currently bound exact range.

This keeps the helper aligned with the fail-closed behavior of `CaptureMemoryState(...)` and `Rollback(...)`.

## Non-captured and non-claimed surfaces

`ReplayToken` does not capture every hidden backend or runtime contour.

- It does not capture a prior rename-map image.
- It does not rewind commit-map or free-list state.
- It does not capture prior lane choices, stable donor masks, or legality-witness identity.
- It does not compare DMA metadata freshness or assist provenance continuity.
- It does not claim to snapshot arbitrary speculative pipeline state.
- It does not claim to recreate partially materialized memory.
- It does not claim to reset the whole rename/commit/free-list backend
  substrate.
- It does not claim universal hidden-state rollback.
- It does not claim universal rollback.
- It does not claim universal recovery for every side effect in the emulator.

The safe repository-facing claim is narrower: `ReplayToken` supports rollback only for the architectural
register and exact main-memory contours it has explicitly captured and can materialize in full.

## Evidence anchors

- `Phase09ReplayTokenDefaultBindingSeamTests` proves that unbound replay-side memory restore fails closed.
- `Phase09ReplayTokenMainMemoryBindingSeamTests` and
  `Phase09ReplayTokenMainMemoryParameterBindingSeamTests` prove that rollback uses the explicitly bound
  main-memory surface instead of a mutable global fallback.
- `Phase09ReplayTokenRollbackBoundaryTests` proves that `CanSafelyRollback()` now respects unbound and
  out-of-range memory contours instead of advertising them as safe.

This document does not claim universal recovery. It externalizes the bounded rollback contract that the
current repository actually implements and tests.
