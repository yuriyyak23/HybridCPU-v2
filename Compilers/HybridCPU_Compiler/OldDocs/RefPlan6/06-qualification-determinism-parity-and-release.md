# Phase 6 — Execution qualification, determinism, parity and release gates

## Gate

Phases 0–5 are implemented behind the ScalarControlFlowV2 profile flag and expose all
contract/version evidence needed for independent qualification.

## Goal

Close the profile with source -> CIL -> compiler -> object/link -> loader -> ISE execution
evidence. Successful compilation or `dotnet publish` alone is not feature closure.

## Language support matrix

For every advertised construct record one of:

- `Guaranteed` — explicit admission + lowering + ISE execution tests pass;
- `Conditionally possible` — infrastructure exists but not a V1 language guarantee;
- `Missing` — not implemented/qualified;
- `Explicitly rejected` — stable fail-closed diagnostic.

At minimum classify:

- arithmetic for each admitted scalar type;
- signed/unsigned comparisons;
- nested `if/else`;
- forward/backward branches;
- `for`, `while`, `do/while`;
- nested loops;
- `break`;
- `continue`;
- multiple loop exits;
- scalar locals and loop-carried locals;
- direct static calls, call chains and shared callees;
- helpers containing branches/loops;
- primitive arguments/returns and `void`;
- recursion;
- `switch`.

Do not promote `break`, `continue`, `switch`, a numeric type or any other construct merely
because it could theoretically lower to supported primitives.

## Required positive execution coverage

- arithmetic/comparisons over the exact Phase 0 scalar matrix;
- nested branch diamonds;
- forward/backward branch execution;
- zero/one/many iteration `for`, `while`, `do/while`;
- induction and accumulator phis;
- nested reducible loops;
- every loop-exit construct advertised as `Guaranteed`;
- direct helper, call chain and shared callee;
- helper containing its own CFG/loop;
- maximum admitted register arguments;
- primitive and `void` returns;
- live caller values across calls;
- callee-save preservation;
- nested non-leaf frames;
- spill/reload and stack-address execution;
- cross-object linked call and return;
- restricted startup -> entry -> exit behavior.

## Required negative coverage

Fail closed for every out-of-profile managed runtime feature plus:

- malformed branch targets/CFG;
- mismatched stack merge;
- irreducible CFG;
- unsupported CIL branch/comparison shape;
- unsupported scalar/ABI signature;
- recursion and mutual-recursion SCCs;
- unavailable NativeAOT body;
- relocation range/alignment overflow;
- frame/call/image budget overflow;
- compiler <-> loader/ISE version mismatch;
- stale analysis fact consumption;
- LLVM/JIT/host fallback.

## Determinism qualification

For representative valid and invalid programs:

- repeated clean builds/publishes are byte-identical for serialized Canonical evidence,
  HCO objects, link maps and `.hcexe`;
- method/call/SCC/block/phi/loop/symbol/relocation/link order is stable;
- shuffled input discovery and hash/dictionary insertion do not alter canonical output;
- different working directories do not leak into identity where not contractually
  required;
- diagnostics are stable in code/order/method identity/IL offset;
- no wall-clock or best-effort search changes semantic output.

## Mutation/invalidation qualification

Property tests must intentionally compute facts, mutate IR/CFG, then prove stale facts
cannot be consumed. Cover at least:

- critical-edge split;
- phi insertion/destruction and parallel-copy cycle temp;
- call pseudo/clobber lowering;
- spill/reload insertion;
- physical copy resolution;
- final frame layout;
- prologue/epilogue and save/restore;
- stack-address materialization;
- branch/call or relocation-sensitive instruction expansion.

Every mutation must invalidate its dependent CFG/dominance/loop, defs/uses, dependency,
liveness, pressure, resource, distance/MII, schedule and bundle/placement facts as
applicable. Prefer generation/version-stamped analyses with fail-fast consumers.

## Compiler <-> ISE/loader parity

Qualification must prove, from the frozen baseline/contracts:

- architectural register and reserved-register assumptions match;
- ABI argument/return/clobber/save masks match executable behavior;
- return-address and call-target basis match;
- stack load/store width/alignment/fault behavior matches frame lowering;
- code-address/relocation unit matches loader/ISE interpretation;
- compiler typed-slot/resource metadata remains structural evidence only;
- ISE remains authoritative for runtime legality, lane materialization, faults,
  publication and retire;
- image/ABI/contract fingerprint/version mismatch is rejected rather than silently
  executed.

Only after these tests pass may the plan state that no ISE source changes are required.
If a mismatch is found, changes must be targeted to the proven contract gap; a new call
ISA or compiler-owned runtime lane authority is not a default remedy.

## Object/link/publish evidence

Require:

- unresolved/duplicate symbol negative tests;
- relocation addend/range/alignment/overflow tests;
- deterministic function alignment/layout;
- actual linked inter-method execution;
- clean checkout/restore/build/publish;
- declared RID/runtime-pack provenance;
- transitive dependency proof with no LLVM codegen/native fallback;
- no prebuilt output leakage;
- compatible ISE execution of the produced `.hcexe`.

## Parity/non-regression

- Existing Native ASM/API paths stay qualified.
- Existing one-method restricted CIL behavior remains supported unless an explicit
  versioned change is approved.
- Direct restricted input and NativeAOT adapter agree on equivalent body-world semantics.
- Modulo-scheduling optimization failure cannot change language admission where ordinary
  scheduling is the qualified fallback.

## Evidence package

Release evidence includes:

- immutable baseline manifest;
- exact profile/scalar/ABI/code-model support matrices;
- positive/negative execution corpus;
- CFG/SSA/call/frame/object/link/loader evidence;
- deterministic artifact hashes;
- diagnostic golden files;
- stale-fact property results;
- dependency/provenance checks;
- compiler <-> ISE contract fingerprint/version evidence;
- explicit deferred-capability list.

## Release disposition

After this gate closes, ScalarControlFlowV2 V1 may be enabled only as an explicit opt-in
profile. Default enablement is a separate release decision. Recursion remains disabled.
