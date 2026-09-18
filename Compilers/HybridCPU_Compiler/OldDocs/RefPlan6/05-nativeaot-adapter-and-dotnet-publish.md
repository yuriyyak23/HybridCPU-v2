# Phase 5 — NativeAOT/ILCompiler adapter, restricted startup and `dotnet publish -r hybridcpu`

## Gate

Phase 4 produces deterministic executable multi-method images through the existing
HybridCPU object/link path.

## Goal

Integrate ScalarControlFlowV2 with NativeAOT/ILCompiler without creating a second
reachability or code-generation authority, and make a clean `dotnet publish -r
hybridcpu` produce the same bounded backend result without LLVM/JIT fallback.

## NativeAOT authority contract

In NativeAOT mode:

- ILCompiler/adapter presents the selected root(s), available managed bodies and required
  dependency/body-provider world;
- HybridCPU compiler owns semantic admission, direct-call graph facts, SCC rejection,
  deterministic method compilation order and all Canonical/Core/backend work;
- the adapter must not independently widen the method set after the compiler finds an
  unavailable edge;
- a direct call to a body not presented by the adapter is a deterministic contract or
  unresolved-dependency failure;
- there is one HybridCPU Canonical backend path for every admitted method.

Standalone restricted-PE reachability and NativeAOT root presentation are separate input
modes over the same Phase 2 graph semantics; they are not two competing whole-program
backends.

## Required work

- Remove single-method assumptions from the current NativeAOT seam.
- Preserve method identity across ILCompiler -> HybridCPU body presentation.
- Keep exact method bodies/metadata available for Phase 1/2 import.
- Route all admitted methods through the same CIL -> Canonical -> Core -> HCO path.
- Reject any request for unsupported runtime helpers/features before image release.
- Prove the transitive publish dependency graph does not require LLVM packages/tools/
  libraries and never falls back to host JIT/native host execution.
- Prevent prebuilt artifact leakage from making clean-publish tests pass accidentally.

## Restricted managed entry/startup contract

Because references/arrays/runtime services are out of V1, do not assume the full normal
console-program startup surface.

Freeze and test:

- exact accepted managed entrypoint signatures;
- mapping from image/startup shim to managed root ABI;
- initialization assumptions allowed before the root call;
- root return-value/exit convention;
- shutdown/termination behavior;
- deterministic rejection of signatures requiring `string[]`, managed objects or other
  out-of-profile runtime services unless separately admitted.

Reuse the existing restricted startup/image contract where sufficient; extend it only by
a targeted versioned change.

## Integration constraints

The adapter must not:

- own CFG/SSA/scheduling/allocation/linking;
- become a second call-graph implementation with different semantics;
- synthesize managed heap/GC/EH/dispatch/threading services;
- translate through LLVM;
- silently use host JIT/native fallback;
- treat ILCompiler reachability as permission to widen the ScalarControlFlowV2 semantic
  profile.

## Required tests

Positive end-to-end publish:

- supported no-argument/scalar entrypoint according to the frozen entry contract;
- static helper call and helper chain;
- shared callee;
- helper with `for`, `while` and `do/while` qualified shapes;
- branch + loop-carried accumulator;
- nested non-recursive call with real frame/spill behavior.

Negative publish:

- unsupported entrypoint signature;
- unavailable body/root boundary mismatch;
- recursion;
- allocation/reference/array;
- EH;
- virtual/interface dispatch;
- delegate/reflection/dynamic;
- async/threading;
- P/Invoke/TLS;
- graph/frame/image budget overflow;
- LLVM/JIT/host fallback request.

Determinism/parity:

- two clean publishes from clean output directories produce byte-identical `.hcexe` and
  evidence;
- changing working directory does not alter artifact identity where paths are not
  semantically relevant;
- shuffled body discovery produces the same Phase 2 graph order;
- direct restricted input and NativeAOT adapter agree on admission and Canonical method
  content for equivalent presented bodies;
- LLVM unavailable does not alter successful profile output.

## Closure evidence

CI/publish evidence must include clean checkout/restore/build/publish steps, declared
transitive dependencies, runtime-pack/RID provenance, graph/body-world evidence, image
hashes and proof of execution on a compatible ISE.

## Release disposition

Eligible only for opt-in preview after Phase 6.
