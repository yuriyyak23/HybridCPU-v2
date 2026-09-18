# ADR-0003: Legacy Compatibility Shims

Date: 2026-07-09

Status: accepted

## Context

Phase 09 migrated ambiguous public names without breaking existing callers.
Some legacy members remain callable, but their meaning is deliberately
quarantined.

## Decision

The retained legacy surfaces are compatibility shims only. They must be paired
with typed replacements and must not be used as authority.

Representative legacy-to-typed mappings:

| Legacy surface | Typed replacement / classification |
|---|---|
| MatrixTile/VectorTransfer plan-returning `Lower(...)` | `LowerWithDecision(...)` returning `CompilerPositiveEmissionResult<TPlan>` |
| MatrixTile/VectorTransfer plan-returning `Compile*` facades | `Compile*WithDecision(...)` |
| `DirectiveParseResult.Success` | `IsDirectiveParsed` and `CompilerDirectiveParseObservation` |
| `CompilerVmxPreflightResult.Success` | `ProjectionPreflightPassed` |
| `LegalSlots` | `StructurallyAllowedSlots` |
| `GetLegalSlots(...)` | `GetStructurallyAllowedSlots(...)` |
| `HasLegalAssignment(...)` | `HasStructuralPlacement(...)` |
| `AnalyzeAssignment(...)` | `AnalyzeStructuralAssignment(...)` |
| `MaterializeAssignment(...)` | `MaterializeStructuralAssignment(...)` |
| placement-quality `Create(... legalSlots ...)` | `CreateForStructuralSlotFacts(...)` |
| materialized `InstructionLegalSlots` | `InstructionStructurallyAllowedSlots` |
| materialized `IsLegalPlacement` | `IsStructuralPlacement` |
| runtime-owned guard `.IsAllowed` reads in compiler facade | `CompilerRuntimeGuardObservation` |

## Consequences

Obsolete compatibility members may preserve source compatibility, but they do
not strengthen authority. New compiler code should call typed replacements.

Readiness tests must continue to guard against new raw bool lowering
boundaries, unclassified public facades, and bare `Success`/`Valid`/
`Accepted`/`IsLegal`/`CanExecute` authority leaks.

