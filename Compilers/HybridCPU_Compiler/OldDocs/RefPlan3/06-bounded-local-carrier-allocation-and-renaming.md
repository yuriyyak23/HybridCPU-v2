# 06 — Optional bounded local carrier allocation and anti-dependence breaking

## Goal and motivation

Define the missing compiler-local value-to-architectural-carrier path before removing WAR/WAW edges. If a complete legal allocator cannot be established, this phase remains disabled and Phase 05 stays analysis-only.

**Status:** `Planned`.

## Confirmed starting state

- `IrOperand` is a normalized `(Kind, Value, Name)` record; no separate SSA/virtual-register namespace exists.
- Lowering packs architectural carriers directly from operands.
- Therefore a rename cannot be specified as a string/name substitution or a late unspecified “mapping back”.

## In scope

- Restricted `IrVirtualValue` for compiler-created temporaries only.
- Live intervals, legal carrier candidate sets, interference graph, bounded allocation and explicit encoding rewrite.
- Removal of WAR/WAW only after allocation and full def-use closure proof.

## Explicit non-goals

- No general SSA conversion, global register allocator or renaming of ABI/special/predicate/control/VT-visible values.
- No compiler-owned physical PRF state or runtime rename behavior.
- No EBB-wide renaming until the BB-only implementation is verified.

## Canonical types/components

Proposed `IrVirtualValue`, `IrCarrierCandidateSet`, `IrInterferenceGraph`, `LocalCarrierAllocationV1`, `IrEncodingRewrite`; Phase 05 liveness and existing lowerer/encoder validate the result.

## Target architecture and data flow

```text
eligible compiler-created temporary
 -> IrVirtualValue + complete def-use closure
 -> live interval + legal architectural carrier candidates
 -> interference graph
 -> bounded deterministic allocation
 -> operand/encoding rewrite
 -> rebuild dependencies/resources/schedule
 -> lowering + reference equivalence
```

## Invariants and authority boundaries

- Allocation is compiler encoding selection, not live runtime PRF allocation.
- No architectural/ABI identity, VT ownership, predicate/control register or cross-contour value may be renamed.
- All analyses and placements are rebuilt after rewrite; stale evidence is invalid.
- Profile affects profitability only.

## Dependencies

Phase 05 verified.

## Implementation backlog

1. Inventory which current producers create genuinely compiler-private temporaries.
2. Define virtual-value identity and complete def-use closure proof.
3. Generate carrier candidates from ISA/ABI reserved/available rules; unknown policy rejects.
4. Build interference plus hard exclusions for ABI, predicate, control, special and VT-visible carriers.
5. Allocate by stable value/candidate order with deterministic backtracking limits.
6. Rewrite normalized operands and final encoding together.
7. Rebuild DAG/resource/pressure/schedule and compare lexicographic cost.
8. Remove only WAR/WAW edges proven eliminated; RAW remains.
9. Add an analysis-only outcome when no legal compiler-private namespace exists.

## Migration and compatibility strategy

Default disabled. Original IR/schedule are retained until the candidate fully validates. Absence of eligible virtual values is a supported `NoEligibleValues` outcome, not a reason to invent carriers.

## Tests

### Positive/property/determinism

- Small interference graphs, successful bounded allocation, def-use closure and encoding round-trip.
- Candidate execution/effect trace equals original and rebuilt DAG has only justified edge changes.
- Stable allocation across runs.

### Negative

- Architectural/ABI/special/predicate/control/cross-VT values, incomplete closure, carrier exhaustion, interference conflict, encode failure and stale analysis.

### Static

- No string-name-only rename path.
- No runtime PRF/rename/commit/retire owner access.

## Benchmarks and KPI

- Zero semantic/encoding divergence and zero unjustified dependence removal.
- Report candidates, allocated/fallback, WAR/WAW removed, pressure delta, schedule cycles and bundles.
- Adopt only with cycles non-regressing and pressure within Phase 05 cap; compile time/memory p95 <=1.05x.

## Diagnostics and telemetry

Virtual value, closure proof, carrier candidates, interference degree, allocation states/prunes, rewrite fingerprint, removed edges and fallback reason.

## Bounded-search, timeout and fallback policy

No production wall-clock cutoff. Maximum 64 virtual values, 16 carriers/value, 4096 allocation states and 2 alternatives/value. Limit or failure preserves original IR byte-for-byte. CI watchdog may abort a test but cannot select code.

## Risks and forbidden shortcuts

- No assumed free register or carrier scavenging without an ABI rule.
- No removing WAR/WAW before successful encoding-aware allocation.
- No expanding this into runtime rename ownership.

## Rollback / kill switch

Disable allocator/rewriter and retain Phase 05 pressure analysis.

## Acceptance and merge gates

- Carrier inventory/ABI review and exhaustive tiny-graph allocation tests.
- Rebuilt-analysis and reference-execution equivalence.
- Deterministic fallback and no eligible-value behavior verified.

## Status criteria

- `implemented`: restricted value/interference/allocation/encoding pipeline exists.
- `verified`: ABI, equivalence and bounded-search gates pass.
- `default-enabled`: only for an allowlisted compiler-private value class.
- `release-authorized`: local encoding rewrite only; no runtime rename claim.

## Residual work / next gate

Phase 07 may consume verified allocation, but EBB scheduling must remain correct with this feature disabled.
