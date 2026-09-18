# 25 — GC, safepoints, EH/unwind, metadata, helpers and interop

## Goal

Close the managed-runtime correctness gaps explicitly rather than hiding them inside a generic “NativeAOT support” phase.

## Independent workstreams

1. **GC maps / safepoints** — reference maps for final architectural register assignments and stack slots; safepoint placement tied to final schedule/allocation/frame; code-manager records sufficient for stack walking.
2. **Allocation and barriers** — allocation/write-barrier/read-barrier/runtime helper lowering with explicit side effects, GC transition and throwing behavior.
3. **EH / unwind** — EH regions, landing/unwind metadata, personality/funclet policy, preserved machine state and stack unwinding.
4. **Managed metadata / runtime lookup** — method/code-range records, generic/context lookup data and selected NativeAOT metadata requirements.
5. **Managed/unmanaged interop** — transition thunks, P/Invoke and reverse P/Invoke, pinning/handles, marshaling subset and native ABI boundary.
6. **TLS / thread runtime state** — thread/runtime context access, cooperative/preemptive transition requirements and safepoint interaction.
7. **Debug/source mapping** — managed method/source/inline origin mapping and unwind/debug correlation where the tooling format is defined.

Each workstream has its own Phase 05 capability bit/schema version, negative controls, default-off gate, runtime dependency and release evidence. No umbrella “managed runtime supported” bit may enable unfinished workstreams.

## GC/safepoint requirements

- GC maps are generated **after** final Phase 20 allocation/frame decisions and are invalidated by any later code motion, spill, frame or call-site change.
- Every live managed reference/byref at a safepoint is represented in a location format understood by the pinned runtime/code manager; non-reference values must not be falsely reported as movable references.
- Calls/helpers state whether they are safepoints, may trigger GC, may block/transition modes or require a fully interruptible region.
- Loop polls/backedges, call sites and other safepoint forms are driven by the runtime/managed ABI contract, not compiler profitability.
- Compiler evidence does not control GC phase, suspension, object movement or root validity; runtime remains authoritative.

## EH/unwind requirements

EH is not “metadata only”. Qualification requires instruction/frame state restoration semantics, personality/dispatch behavior, unwind tables, preserved registers, cleanup/finally behavior and interaction with GC/reference maps. Unsupported EH constructs fail before emission.

A valid object file containing unwind-looking sections is not evidence of correct EH. Cross-frame throw/catch/finally tests and corrupted/unexpected unwind metadata must fail safely.

## Interop requirements

P/Invoke/reverse P/Invoke are enabled only after:

- native/managed calling conventions and transition-thunk ABI agree;
- thread/GC mode transitions are defined;
- pinning/handle lifetime is defined for managed references;
- marshaling subset is explicit and deterministic;
- native library naming/loading/relocation platform rules are qualified.

Unsupported marshaling or transition cases are deterministic `Unsupported`, not host-runtime fallback.

## FSP / VDSA managed-safety gate

Managed code is excluded from FSP/VDSA beyond an explicitly qualified managed-safety level. In particular, operations whose movement/reclamation/prefetch can affect safepoint visibility, reference lifetime, exception ordering, write barriers, pinning or GC transition state are ineligible until compiler and runtime jointly prove the required contract.

No compiler FSP/VDSA evidence may shorten GC liveness, suppress a safepoint, convert a managed address into a fresh runtime address, or bypass runtime replay/freshness/SafetyVerifier checks.

## Tests / acceptance

Moving-GC/root-liveness tests, safepoints around calls/loops, exception across frames, unwind corruption, pinning/interior refs where supported, P/Invoke ABI, reverse transitions, TLS and helper-version mismatch.

Add independent negative suites per workstream: missing root, stale GC map after spill/reschedule, wrong reference kind, helper with wrong GC behavior, EH across managed/native boundaries, unsupported marshaling, TLS/context mismatch and runtime ABI/schema version skew. Differential runtime tests must exercise real stack walking/GC/EH/transition behavior, not only inspect emitted metadata bytes.

## Rollout

Enable workstreams individually in the order demanded by the restricted application corpus. A feature descriptor states the exact qualified set. Disabling any workstream rejects inputs that require it rather than emitting a partially managed image.

**Acceptance:** every enabled managed feature has end-to-end compiler+runtime differential tests, final-allocation-correct metadata and explicit authority boundaries; there is no hidden fallback to unsupported host behavior and no managed FSP/VDSA exposure beyond the qualified safety level.
