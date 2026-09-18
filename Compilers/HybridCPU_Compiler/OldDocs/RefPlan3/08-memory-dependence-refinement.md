# 08 — Memory-dependence and affine access refinement

## Goal and motivation

Replace the current coarse absolute-region overlap test with a first-class, conservative memory relation used by EBB, guarded motion, loops, fusion and donor-prefetch planning.

**Status:** `Planned`.

## Confirmed starting state

- `IrMemoryRegion` contains only absolute `Address`, `Length`, `IsWrite`.
- `HybridCpuDependencyAnalyzer` prunes disjoint domain tags and non-overlapping absolute regions; indexed/2D/stride accesses become `May`, and capability-root pruning currently returns false.
- There is no `AddressExpression`, alias class, affine induction relation, alignment/atomic/volatile model or loop-distance proof.
- Profile feedback cannot safely convert `MayAlias` to `NoAlias`.

## In scope

- Normalized address expressions, access size/alignment, address space/domain/capability provenance and volatile/atomic/ordering flags.
- `MustAlias`, `NoAlias`, `MayAlias` with proof category and conservative composition.
- Affine induction relations and loop-distance evidence for supported forms.
- Adapters back to existing memory-dependence edges.

## Explicit non-goals

- No speculative alias checks, profile legality, general polyhedral analysis or runtime MMU authority.
- No code motion/transformation in the first slice.

## Canonical types/components

Proposed `IrAddressExpression`, `IrMemoryAccess`, `IrAliasClass`, `IrAliasRelation`, `IrAffineAccessRelation`, `HybridCpuMemoryDependenceAnalysis`; current `IrMemoryRegion` remains a compatibility projection.

## Target architecture and data flow

```text
instruction operands/effects + CFG/loop induction facts
 -> normalized AddressExpression + access attributes
 -> alias class and Must/No/May relation with witness
 -> affine distance where proven
 -> memory dependence edges/resources
 -> EBB/guard/loop/fusion/VDSA consumers
```

## Invariants and authority boundaries

- `MayAlias` remains a dependence or rejects a transformation; profile cannot promote it.
- Volatile, atomic, barrier and unknown ordering are hard constraints.
- Checked arithmetic and overflow produce `MayAlias`, never `NoAlias`.
- Compiler analysis does not replace runtime memory protection, ordering or commit authority.

## Dependencies

Phases 03 and 04 verified.

## Implementation backlog

1. Inventory address encodings for scalar, vector, stream, DMA and matrix operations.
2. Define expression nodes for base, constant, affine induction, stride/row stride and unknown.
3. Attach size/alignment, read/write, volatility/atomicity, domain/capability root and contour.
4. Implement proof-labelled alias lattice and symmetric/monotonic composition.
5. Add affine loop relation/distance for bounded supported forms.
6. Preserve current `IrMemoryRegion` result through an adapter and compare.
7. Route refined edges first to diagnostics, then Phase 13 loop analysis.
8. Freeze negative overflow/wrap/unknown/atomic/capability corpora.

## Migration and compatibility strategy

Shadow-compare current and refined results. New analysis may add conservative edges immediately only after parity review; edge removal requires a `NoAlias` witness and qualification. Unknown keeps current behavior.

## Tests

### Positive/property/determinism

- Same/disjoint ranges, alignment, affine equal/disjoint/distance cases and domain-disjoint witnesses.
- Alias symmetry; `MustAlias`/`NoAlias` never both; weakening facts cannot strengthen to `NoAlias`.
- Stable proof bytes and edge order.

### Negative

- Overflow/wrap, unknown base/stride, volatile/atomic, capability uncertainty, mixed contour/domain and profile-only non-conflict.

### Static

- Profile reader absent from alias legality implementation.
- No runtime MMU/commit owner mutation.

## Benchmarks and KPI

- 100% agreement with reference enumeration for bounded concrete generated accesses.
- Report Must/No/May counts, affine-distance coverage, memory edges removed/added and transform eligibility delta.
- Zero incorrect `NoAlias`; analysis compile time/memory p95 <=1.08x.

## Diagnostics and telemetry

Access/expression IDs, attributes, alias result/witness, affine coefficients/distance, overflow/unknown reason and consumers enabled/rejected.

## Bounded-search, timeout and fallback policy

No wall-clock cutoff. Max 64 expression nodes/access, 4096 access pairs/region and 256 affine relation steps/pair. Cap/unsupported yields `MayAlias` deterministically.

## Risks and forbidden shortcuts

- No profile-to-NoAlias conversion.
- No treating bank/channel difference as address non-alias.
- No dropping an unknown memory edge to make modulo scheduling succeed.

## Rollback / kill switch

Disable refined edge removal and use the current conservative `IrMemoryRegion` analyzer.

## Acceptance and merge gates

- Alias lattice/properties, concrete oracle and negative ordering tests pass.
- Every removed edge has an archived static witness.
- Current compatibility projection and rollback verified.

## Status criteria

- `implemented`: normalized accesses and proof-labelled analysis exist.
- `verified`: reference/property/negative gates pass.
- `default-enabled`: conservative analysis first; edge pruning separately qualified.
- `release-authorized`: compiler dependence evidence only.

## Residual work / next gate

Phases 10–11, 13–16 and 23 consume this layer without weakening its fail-closed policy.
