# 24 — NativeAOT object/link/runtime integration

## Goal

Produce linkable NativeAOT artifacts and a minimal runnable managed-native image before adding full GC/EH breadth.

## Required work

- Complete Phase 08B object writer/relocations/sections for NativeAOT-generated method/data nodes.
- Define linker command/response contract, symbol visibility/binding, relocation addends, COMDAT/duplicate policy, static data and entrypoint/startup sequence.
- Emit NativeAOT metadata sections required by the currently qualified subset and version them against the Phase 22 code-manager/managed-ABI contracts.
- Integrate runtime bootstrap, static initialization subset and helper resolution.
- Keep OS/platform assumptions explicit: executable/object format, page/alignment rules, startup ABI, dynamic-library support or explicit non-support.
- Define target runtime libraries/helper import libraries and prove every imported helper is HybridCPU code or a separately qualified platform service; no host-ISA object may enter the image.
- Define linker ownership: which relocations are resolved by compiler/object writer, which by linker, and which forms are unsupported. Unknown relocation kinds fail before link rather than being emitted approximately.
- Define reproducible link provenance: linker binary/version, response/options, input object digests, runtime-library digests and target ABI/runtime-pack version.
- Add a minimal platform abstraction for file/executable loading/syscall/host services needed by the restricted image; do not conflate compiler codegen with OS integration.

## Layered qualification

Phase 24 qualification is incremental:

```text
24A object format + relocation matrix
24B static multi-object linking
24C startup/entrypoint + restricted runtime helpers
24D minimal runnable restricted managed image
```

Each gate has an independent feature descriptor. Passing 24A/24B does not imply a runnable NativeAOT runtime.

## Boundaries

Object/link success does not imply GC/EH correctness. Images requiring an unimplemented managed capability must be rejected before link/publish qualification.

The compiler owns code/object generation; the linker owns symbol resolution under the versioned linker contract; the runtime owns startup state and managed execution services. Runtime SafetyVerifier/LegalityDecision and execution/publication/commit/retire authority remain unchanged by successful linking.

## Determinism / fallback

Section/symbol/relocation ordering is deterministic. Build timestamps/build IDs are either reproducibly derived or explicitly excluded from byte-identical artifact claims and recorded separately. Unsupported relocations/sections/platform services produce stable failures; there is no fallback to host object format or host linker semantics.

## Tests / acceptance

Relocation matrix, multi-object/multi-method linking, duplicate/undefined symbols, deterministic section ordering, startup smoke tests and corrupted-object negative controls.

Add cross-object call/data/TLS-placeholder cases, large-distance relocation overflow cases, alignment/page-edge cases, helper import purity and host-object contamination tests. Compare link maps and final image digests across repeated builds.

**Acceptance:** restricted managed programs from Phase 21/23 produce deterministic linkable/runnable HybridCPU images using one explicit object/link/startup contract; every relocation/helper/platform dependency is qualified and no unimplemented GC/EH/interop capability is smuggled in through the linker/runtime bootstrap.
