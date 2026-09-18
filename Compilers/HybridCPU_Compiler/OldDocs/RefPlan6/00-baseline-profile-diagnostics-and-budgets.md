# Phase 0 — Baseline, semantic profile, target contract, diagnostics and budgets

## Gate

No feature implementation starts until the implementation baseline and profile contracts
are immutable and checked in.

Record:

- Compiler branch/HEAD/tree;
- ISE branch/HEAD/tree;
- hashes of all RefPlan6 files;
- .NET SDK/Roslyn version used for qualifying C# -> CIL shapes;
- profile ID/version;
- native ABI, object/image and compiler <-> ISE contract versions.

## Goal

Define `HybridCPU.DotNetAot.ScalarControlFlowV2` as a bounded, deterministic,
fail-closed profile that reuses the existing Canonical/Core backend and freezes the
semantic assumptions required by CFG/SSA, calls, frames and linking before those phases
start.

## Required semantic contract

Check in a normative scalar matrix. It must define, rather than infer from enum names:

- admitted CIL/storage types;
- evaluation-stack representation for each admitted type;
- exact integer widths and signed/unsigned interpretation;
- `bool` representation and branch truth semantics;
- narrow integer load/store/call normalization;
- widening, narrowing, sign-extension and zero-extension rules;
- signed and unsigned comparison semantics;
- argument and return extension/truncation rules;
- `native int`/pointer-sized scalar policy if it is required internally;
- target address width;
- exact admitted entrypoint signatures.

A type is not advertised as supported until admission, lowering and execution evidence
exists for its full boundary semantics.

## Required target/ABI/code-model freeze

Before Phase 1/3/4 implementation relies on these facts, identify the existing source of
truth and freeze/version the required assumptions:

- authoritative HybridCPU native ABI contract (extend/version the existing contract,
  do not create a parallel profile ABI);
- reserved, argument, return, stack-pointer and return-address/link registers;
- caller-saved/callee-saved partition;
- stack and frame alignment assumptions;
- function alignment;
- code-address unit (byte, bundle or other architectural unit);
- direct-call relocation basis, width/range/addend semantics or a documented
  `UNVERIFIED` item that Phase 4 must close before emission;
- deterministic overflow behavior for out-of-range calls;
- loader/image contract version and entry/startup/exit boundary;
- stack/frame execution-environment assumptions and maximum admitted frame/call-depth
  budget.

Unknown values may be recorded as `UNVERIFIED`, but any unknown that affects semantic
correctness is a blocker for the consuming phase.

## Budgets

Define deterministic limits for at least:

- PE/module bytes and method-body bytes;
- IL instructions per method and across the reachable program;
- basic blocks and CFG edges;
- locals, arguments and evaluation-stack depth;
- merge/phi values and inserted copy temporaries;
- loop count and nesting depth;
- reachable methods, outgoing calls, total call edges and acyclic call depth;
- metadata/body resolution steps;
- spills and frame bytes;
- symbols and relocations;
- final code/image bytes.

Budget exhaustion is a stable admission/link failure and emits no `.hcexe`.

## Diagnostics

Use distinct deterministic families for:

- unsupported opcode/CIL shape;
- unsupported scalar/reference type;
- malformed branch target or unverifiable CFG/stack merge;
- irreducible CFG;
- profile/loop/SSA/call/frame/image budget exhaustion;
- unresolved/disallowed call target;
- recursive SCC;
- unsupported ABI signature;
- ABI/frame/stack contract mismatch;
- missing/duplicate symbol;
- unsupported/out-of-range relocation;
- NativeAOT body/root boundary violation;
- compiler <-> loader/ISE contract mismatch;
- stale-analysis consumption/internal invariant failure;
- nondeterministic evidence violation.

## Dependency and authority tests

- CIL frontend may consume Canonical contracts, not scheduler internals.
- NativeAOT adapter may present roots/bodies and consume profile/importer contracts but
  may not own a second backend or silently widen reachability.
- LLVM assemblies/tools/libraries must not be required by the ScalarControlFlowV2
  build/publish path.
- compiler packages must not depend on runtime publication/commit/retire authority.
- compiler lane/resource facts must not become runtime legality authority.

## Closure evidence

- immutable baseline manifest;
- exact scalar semantic matrix;
- target/ABI/code-model assumption table with no unresolved blocker for Phase 1;
- deterministic budget table;
- support matrix separating `Guaranteed`, `Conditional`, `Missing` and `Rejected`;
- diagnostic golden tests;
- dependency graph proving no LLVM dependency for this profile;
- repeated builds produce byte-identical profile/config evidence.

## Release disposition

Default-off. No new language capability is enabled in this phase.
