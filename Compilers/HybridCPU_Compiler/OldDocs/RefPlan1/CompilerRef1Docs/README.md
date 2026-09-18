# Compiler Refactor ADR Index

Date: 2026-07-09

This folder is the ADR-style source of truth for the final Phase 09 compiler
cleanup state.

## ADRs

- `ADR-0001-phase09-compiler-cleanup-final-state.md`
  - final accepted Phase 09 compiler cleanup state;
  - completed public compiler/API cleanup surfaces;
  - latest validation gates.
- `ADR-0002-authority-boundary-invariants.md`
  - non-negotiable authority boundaries between carriers, execution,
    publication, authority, commit, retire, evidence, and production lowering.
- `ADR-0003-legacy-compatibility-shims.md`
  - retained obsolete compatibility members and their typed replacements.
- `ADR-0004-runtime-local-validation-boundaries.md`
  - runtime-local `IsValid` surfaces classified as non-compiler lowering
    boundaries.

## Reading Rule

Use these ADRs for the final Phase 09 state. Use the older files in
`RefPlan1` as historical plan, implementation, and audit context.

If an older document says a Phase 09 cleanup item is still open, prefer this
ADR index and `PHASE09_REMAINING_COMPILER_WORK.md`.

