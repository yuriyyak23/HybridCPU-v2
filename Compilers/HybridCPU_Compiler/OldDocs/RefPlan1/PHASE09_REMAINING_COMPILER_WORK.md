# Phase 09 Remaining Compiler Work

Date: 2026-07-09

Status: closed for the current Phase 09 compiler cleanup backlog.

## Short Answer

There is no open compiler-code or validation-only Phase 09 cleanup task left in
this backlog.

The broad Phase 02-08 architecture construction, the Phase 09 public compiler
cleanup, and the requested runtime-local validation-only `IsValid` audit are
complete. The final state has been promoted into ADR-style documentation under
`CompilerRefDocs`.

## Latest Green Gates

Most recent recorded Phase 09 gates:

- `CompilerPhase09CleanupMigrationReadinessTests`: 30/30
- MatrixTile/VectorTransfer positive-emission contracts: 16/16
- wide Phase 09 authority/negative matrix slice set: 175/175

These gates protect the completed cleanup boundaries. They do not grant runtime
legality, execution, publication, commit, retire, or production lowering
authority to compiler artifacts.

## Closed Compiler-Code Work

Closed public/compiler cleanup surfaces:

- positive MatrixTile/VectorTransfer emission wrappers:
  `CompilerPositiveEmissionResult<TPlan>`, `LowerWithDecision(...)`, and
  `Compile*WithDecision(...)`;
- `HybridCpuThreadCompilerContext` public facade authority classification;
- compiler observations for runtime-owned DSC/L7 guard `IsAllowed` reads;
- directive parser cleanup with `IsDirectiveParsed` and
  `CompilerDirectiveParseObservation`;
- `LegalSlots` structural wording migration through
  `StructurallyAllowedSlots`, structural SlotModel entrypoints, materialized
  structural placement aliases, and placement-quality structural slot facts;
- `CompilerHelperRecoveryResult<TPlan>` moved into its own lowering result
  file;
- final public API scan/obsolete sweep for bare authority-like public names and
  plan-only public lowering facades.

The retained legacy members are compatibility shims only. They are obsolete or
quarantined behind typed replacements and must not be interpreted as production
authority.

## Closed Validation-Only Work

Closed runtime-local audit surfaces:

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

These predicates remain runtime-local descriptor, routing, transport,
occupancy, or domain-validation checks. No compiler typed predicate was added
because the compiler must not consume these runtime-owned validation results as
lowering or authority.

## Final Source Of Truth

Use the ADR-style documents in `CompilerRefDocs` for the final Phase 09
compiler cleanup state:

- `CompilerRefDocs/README.md`
- `CompilerRefDocs/ADR-0001-phase09-compiler-cleanup-final-state.md`
- `CompilerRefDocs/ADR-0002-authority-boundary-invariants.md`
- `CompilerRefDocs/ADR-0003-legacy-compatibility-shims.md`
- `CompilerRefDocs/ADR-0004-runtime-local-validation-boundaries.md`

The older phase and slice documents remain historical audit context. They are
not the active next-task source when they describe transitions that are already
closed here.

## First Open Task

No open Phase 09 compiler cleanup task remains.

For future maintenance, start only when a new compiler-facing surface appears
or an existing public API changes. Apply the same order:

```text
observe -> wrap -> early negative gates -> type decisions -> migrate callers -> remove legacy ambiguity
```
