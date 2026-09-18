# 22 — HybridCPU managed ABI

## Goal

Define the compiler↔managed-runtime contract required before ILCompiler integration.

## Required contract areas

- managed/unmanaged calling conventions and reference/value return rules;
- object/reference representation, pointer/reference width, alignment, null representation and interior/byref rules owned jointly with runtime;
- GC-visible register/stack locations and versioned stack-map/safepoint record format;
- safepoint categories, call-site/poll-site semantics and what machine state must be reconstructible;
- thread/runtime context registers or TLS access convention;
- write/read barrier and allocation helper ABI;
- generic/context/runtime lookup parameters where supported;
- managed↔unmanaged transition thunk ABI;
- exception/unwind personality and preserved state contract, even if implementation remains gated;
- versioned runtime-helper symbol table including side effects, GC transition behavior, throwing behavior and calling convention;
- metadata/code-manager contract needed to map code ranges, methods, unwind information and GC records;
- pinning/handle conventions required by future P/Invoke or donor-prefetch of managed addresses.

## Contract layering

Separate the managed ABI into independently versioned subcontracts:

```text
ManagedCallAbi
ManagedReferenceAbi
GcInfoAbi
SafepointAbi
RuntimeHelperAbi
ThreadContextAbi
TransitionThunkAbi
EhUnwindAbi
CodeManagerMetadataAbi
```

A phase may qualify only a subset. “Managed ABI v1 supported” must not imply that GC/EH/interop subcontracts are implemented unless the feature descriptor explicitly says so.

## Ownership / safety

The compiler describes where references and safepoints are and emits metadata required by the runtime. The runtime owns actual object lifetime, GC phase/state, thread suspension, stack walking, write/read barrier semantics, exception dispatch and transition state.

Compiler GC/EH metadata is descriptive evidence/metadata, not permission to reclaim, move, publish, commit or retire execution. FSP/VDSA/compiler profile facts cannot alter reference liveness or safepoint requirements.

Unknown runtime-owned representation details remain `Unsupported`; no host/CoreCLR ABI default fills gaps. All managed ABI facts are tied to Phase 08 target DataLayout and target/runtime-pack revision.

## Separation

This phase defines ABI; it does not implement GC, EH or full NativeAOT. Existing native contour-specific ABI contracts are reused where compatible, not overwritten silently. Phase 25 implements/qualifies GC/EH/interop consumers against these contracts.

## Tests / acceptance

Golden call-frame/reference-map encodings, version skew, transition boundary and helper signature tests. Static checks ensure managed metadata never becomes runtime execution authority.

Add independent compatibility tests for each ABI sub-schema, register/stack reference-map coverage tests, byref/interior-reference negative cases, helper-side-effect verification and target DataLayout mismatch rejection. A runtime consumer must reject unknown major versions before using metadata.

**Acceptance:** ILCompiler/codegen and runtime teams can independently implement against one versioned managed-ABI family with explicit feature/subcontract support; no unimplemented GC/EH/interop capability is implied by the existence of the base calling convention.
