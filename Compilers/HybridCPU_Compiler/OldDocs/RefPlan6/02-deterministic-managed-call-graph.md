# Phase 2 — Deterministic managed method identity, call graph and SCC rejection

## Gate

Phase 1 provides verified method-local CFG/value flow for every method body presented to
the compiler.

## Goal

Extend the restricted frontend from one method to a bounded deterministic graph of
supported static managed methods without creating a second whole-program reachability
authority in NativeAOT mode.

## Managed method identity

Introduce one stable identity used by importer, call graph, diagnostics, symbols and
provenance. Display names are not identity.

For V1 the identity must contain enough stable metadata to distinguish overloads and
modules, for example canonical assembly/module identity + metadata token + canonical
signature. It must not depend on:

- filesystem paths;
- process/hash iteration order;
- reflection enumeration order;
- incidental display formatting.

If MVID or another upstream identifier participates, its determinism assumption must be
recorded in Phase 0 evidence.

## Two reachability modes

### Standalone restricted input

The configured root and admitted input module/assembly set define the body world. The
HybridCPU compiler may deterministically follow admitted direct `call` edges inside that
world.

### NativeAOT/ILCompiler integration

ILCompiler/adapter owns presentation of roots and available managed bodies/dependencies.
HybridCPU compiler owns semantic admission, direct-call graph facts, deterministic
compilation order and SCC/recursion checks for the presented world.

If an admitted `call` references a body not presented by the adapter, fail closed with a
boundary/unresolved-dependency diagnostic. Do not silently scan arbitrary PE files or
expand NativeAOT roots independently.

## Required work

- Resolve exact direct static `call` targets.
- Admit only supported static non-generic signatures/bodies.
- Traverse with a stable worklist and deterministic tie-breakers.
- Deduplicate shared callees.
- Build ordered call edges and SCCs.
- Detect and reject self-recursion and mutually recursive SCCs in V1.
- Compile every admitted method through the same method-local CIL -> Canonical -> Core
  pipeline.
- Produce stable method ordering for objects/symbols/link input.
- Keep intrinsic/runtime helper allowlists explicit and closed.

## Direct-call policy

Allowed:

- direct static managed calls within the exact scalar signature matrix and current body
  world;
- already-qualified closed intrinsic/runtime helper calls.

Rejected:

- virtual/interface dispatch;
- `calli`;
- delegates/function pointers unless separately authorized;
- generic method instantiations;
- unresolved external managed bodies;
- reflection/dynamic dispatch;
- recursion in V1.

## Budgets

Enforce deterministic limits on reachable methods, outgoing calls per method, total call
edges, acyclic call depth, aggregate IL instructions and metadata/body resolution steps.
Budget failure emits no image.

## Tests

Positive:

- entry -> helper;
- entry -> helper1 -> helper2;
- diamond graph with shared callee;
- repeated calls to one helper;
- helpers containing branches/loops;
- primitive arguments/returns and `void` signatures that Phase 0 admits.

Negative:

- self-recursion;
- mutual recursion;
- virtual/interface call;
- generic callee;
- unavailable body in standalone and NativeAOT modes;
- unsupported signature;
- graph/depth/resolution budget overflow.

Determinism/property:

- shuffled metadata/body discovery produces identical graph order;
- stable SCC order;
- graph identity excludes paths/hash order;
- every emitted method is reachable and emitted once;
- every admitted direct-call edge resolves to exactly one method identity;
- repeated graph evidence is byte-identical where serialized.

## Authority boundary

The graph is a compiler fact, not runtime execution authority. NativeAOT mode must not
have two independent components deciding whole-program reachability with divergent
semantics.

## Closure evidence

Checked-in graph/identity tests must prove deterministic multi-method discovery, shared
callee deduplication and fail-closed V1 recursion rejection. SCC infrastructure belongs
here even though recursive execution remains deferred to Phase 7.

## Release disposition

Profile-gated. Managed calls are not release-ready until Phase 3/4 close.
