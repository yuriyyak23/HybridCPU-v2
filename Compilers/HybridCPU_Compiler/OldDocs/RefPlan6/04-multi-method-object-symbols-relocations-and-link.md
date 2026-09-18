# Phase 4 — Existing HCO symbols, direct-call relocations, static link and image layout

## Gate

Phase 3 has a tested ABI, final post-RA code sequence and executable nested-call proof for
the target semantics it relies on.

## Goal

Carry multiple compiled methods through the existing HybridCPU object/link/image path
with deterministic symbols and relocations. This phase is an incremental extension of
the existing HCO/object writer/static linker, not a new CIL-specific linker architecture.

## First required action: prove the existing gap

Before changing the object format, inspect and test the current object contracts, writer,
static linker and restricted image builder.

Prefer:

`one existing HCO object per compiled method/unit -> existing deterministic static linker`

if it already supports the required symbol/relocation semantics. Introduce a multi-method
monolithic container only if a concrete current limitation requires it.

## Required symbol contract

Freeze/version:

- stable method-symbol identity derived from the Phase 2 method identity;
- canonical symbol formatting/mangling for artifacts;
- local/global visibility rules used by the linker;
- function/section alignment;
- entrypoint symbol;
- duplicate/undefined-symbol behavior;
- deterministic method/symbol/section ordering.

Call-graph identity must not depend on lexicographic display names even if display names
are used in object dumps.

## Required relocation contract

For the exact direct-call relocation used by Phase 3, prove or define:

- relocation kind;
- target symbol basis;
- PC-relative/absolute semantics;
- code-address unit (byte/bundle/etc.);
- addend semantics;
- field width and signed/unsigned range;
- alignment constraints;
- overflow behavior;
- layout stage at which the value becomes final.

If an existing relocation kind satisfies all requirements, reuse it. Otherwise add only
the narrowly required kind.

For V1, long-call thunks/veneers are not required if Phase 0 code/image budgets guarantee
the relocation range. Out-of-range calls may fail closed with a deterministic diagnostic.

## Required work

- Emit a stable symbol for every admitted reachable method.
- Emit unresolved direct-call references/relocations before final link.
- Feed objects to the static linker in explicit stable order.
- Resolve duplicate/missing symbols deterministically.
- Preserve startup/image packaging unless the restricted entry contract requires a
  targeted versioned extension.
- Keep relocation/code-size-sensitive expansion before final layout; any inserted
  instructions must trigger the same invalidation/final-schedule rules as Phase 3.
- Extend provenance with profile ID/version, ordered method/object digest, ABI/code-model
  fingerprint and image/loader contract version.

## Ordering rules

Explicit stable ordering is required for reachable methods, object inputs, symbols,
relocations, sections/fragments and final image layout. Filesystem/hash enumeration must
never define bytes.

## Required tests

Positive:

- object A contains caller + unresolved callee relocation;
- object B defines callee;
- linker resolves after deterministic layout;
- two-method and multi-helper images;
- shared callee from multiple callers;
- helper containing loops;
- changed object-input discovery order preserves semantic resolution and deterministic
  canonical ordering.

Negative:

- missing/duplicate symbol;
- unsupported relocation;
- relocation alignment/range overflow;
- symbol/relocation/image budget overflow;
- invalid entrypoint/layout contract.

Parity/determinism:

- one-method existing path stays byte-stable unless a versioned intentional change is
  documented;
- repeated multi-method builds produce identical HCO/link maps/`.hcexe` bytes;
- relocation ordering and overflow diagnostics are stable;
- linked inter-method call executes correctly on ISE.

## Authority boundary

Object/link/image construction remains compiler authority. Loader/ISE consumes the
versioned image and retains legality/execution/publication/retire authority.

## Closure evidence

Checked-in object dumps, relocation tables, link maps, image hashes and at least one
cross-object inter-method call execution prove the complete symbol -> relocation -> link
-> loader/ISE chain.

## Release disposition

Profile remains opt-in/default-off until NativeAOT integration and Phase 6 qualification.
