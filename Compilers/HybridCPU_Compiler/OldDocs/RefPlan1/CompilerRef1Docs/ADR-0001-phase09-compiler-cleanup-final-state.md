# ADR-0001: Phase 09 Compiler Cleanup Final State

Date: 2026-07-09

Status: accepted

## Context

Phase 09 cleaned public compiler/API boundaries after the Phase 02-08 authority
architecture was introduced. The goal was to remove ambiguity at the
compiler/runtime boundary without changing emitted carrier behavior or adding a
production backend lowering path.

## Decision

The current Phase 09 compiler cleanup backlog is closed.

Completed compiler-facing surfaces:

- positive MatrixTile/VectorTransfer emission is available through
  decision-bearing results:
  `CompilerPositiveEmissionResult<TPlan>`, `LowerWithDecision(...)`, and
  `Compile*WithDecision(...)`;
- raw plan-returning MatrixTile/VectorTransfer APIs remain only as obsolete
  compatibility shims;
- `HybridCpuThreadCompilerContext` public facades are classified by
  `HybridCpuThreadCompilerFacadeAudit`;
- runtime-owned DSC/L7 guard reads are wrapped as
  `CompilerRuntimeGuardObservation`;
- directive parser success is parse-only through `IsDirectiveParsed` and
  `CompilerDirectiveParseObservation`;
- structural slot facts use `StructurallyAllowedSlots` and structural
  SlotModel/placement-quality entrypoints;
- `CompilerHelperRecoveryResult<TPlan>` lives in its own lowering result file;
- final public API scan allowlists only deliberate obsolete compatibility
  shims for bare authority-like names.

## Validation

Most recent recorded gates:

- `CompilerPhase09CleanupMigrationReadinessTests`: 30/30
- MatrixTile/VectorTransfer positive-emission contracts: 16/16
- wide Phase 09 authority/negative matrix slice set: 175/175

## Consequences

Compiler-produced plans, carriers, descriptors, parser observations, helper
observations, typed-slot facts, metadata compatibility checks, and evidence
remain artifacts or observations. They do not become runtime legality,
execution-ready state, publication, commit, retire, final authority, or
production backend lowering.

Future compiler-facing public surfaces must be added in the same order:

```text
observe -> wrap -> early negative gates -> type decisions -> migrate callers -> remove legacy ambiguity
```

