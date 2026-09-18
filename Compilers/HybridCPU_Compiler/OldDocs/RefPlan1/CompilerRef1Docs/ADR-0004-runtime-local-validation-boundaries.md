# ADR-0004: Runtime-Local Validation Boundaries

Date: 2026-07-09

Status: accepted

## Context

The final Phase 09 audit reviewed runtime-owned `IsValid` predicates that could
look like compiler legality or lowering success if consumed across the wrong
boundary.

## Decision

The audited runtime-local `IsValid` surfaces remain runtime-local. They are not
compiler lowering boundaries and no compiler typed predicate is added for them.

Classified runtime-local groups:

- memory/IOMMU/domain validation:
  `DmaWindowDescriptor.IsValid`, `IommuDomainBinding.IsValid`,
  `DomainValidationResult.IsValid`;
- event/completion routing validation:
  `LaneCompletionDescriptor.IsValid`, `EventInjectionDescriptor.IsValid`,
  `MemoryTrapRange.IsValid`;
- assist transport validation:
  `AssistInterCoreTransport.IsValid`;
- pipeline slot descriptor validation:
  `DecodedBundleDescriptor` slot `IsValid` and runtime FSP slot descriptor
  `IsValid` consumers.

These predicates validate runtime descriptors, routing state, transport
eligibility, occupancy, pipeline-local state, or domain binding. They do not
grant compiler runtime legality, execution, publication, commit, retire, or
production lowering authority.

## Consequences

Compiler source must remain guarded against consuming these runtime-owned types
or their `.IsValid` predicates as authority.

If a future feature makes one of these results compiler-facing, add a typed
compiler observation first, then negative tests, then migrate callers. Do not
reuse the runtime-local `IsValid` name as a compiler lowering predicate.

