# 09 — Predicate capability and guarded-effect ABI audit

## Goal and motivation

Determine whether the current ISA/compiler/lowering/runtime contract can represent control predicates for guarded scheduling. Until proven, Phase 10 is limited to speculation-free control-sensitive motion and Phase 11 hyperblocks remain an ISA-extension gate.

**Status:** `Planned`.

## Confirmed starting state

- `IrInstruction` contains `PredicateMask`; builder records it and `HybridCpuBundleLowerer` preserves it.
- Runtime documents `PredicateMask` as a predicate-register index and vector execution uses it for lane masking.
- `IrInstructionAnnotation` has no first-class control guard identity, guard polarity or guarded trap/store semantics.
- Several predicate-mask ISA contracts explicitly record unresolved destination/publication semantics. Vector lane masking is not proof of general scalar/control predication.

## In scope

- Opcode-by-opcode predicate capability matrix.
- Encoding, predicate def/use, polarity, inactive-lane/inactive-instruction behavior, traps, stores, atomics and side effects.
- Preservation through IR, dependence graph, bundle/lowering, annotations, decoder, replay and cache identity.
- Architecture decision: supported subset, speculation-free-only fallback, or separate ISA-extension project.

## Explicit non-goals

- No if-conversion, guard synthesis, speculative motion or new ISA encoding.
- No assumption that `PredicateMask != 0` is a general control guard.

## Canonical types/components

Proposed `PredicateCapabilityMatrixV1`, `IrGuardIdentity` only if supported, `GuardedEffectContractV1`; existing `PredicateMask`, predicate-register state, opcode classifiers, lowerer, annotations and decoder are audited.

## Target architecture and data flow

```text
opcode/encoding + IR/lowering + runtime semantics
 -> capability row: guardable? representation? inactive effects/traps?
 -> golden encode/decode/execute evidence
 -> decision
    supported subset -> Phase 10 guarded subset
    no general predication -> speculation-free motion only
    extension needed -> Phase 11 deferred to separate ISA project
```

## Invariants and authority boundaries

- No capability is inferred from field presence alone.
- Guard-false stores/traps/atomics/system effects require explicit architectural semantics.
- Decoder/runtime behavior remains authority; compiler matrix is a versioned contract projection.
- Unknown or unresolved row is not guardable.

## Dependencies

Phases 07 and 08 verified.

## Implementation backlog

1. Inventory every opcode/resource/control/effect class and current predicate encoding.
2. Trace predicate register defs/uses and mask index through builder/lowerer/decoder/execution.
3. Classify inactive behavior for result, memory, exception, system and serialization effects.
4. Verify relocation, annotation, replay and cache identity include required guard facts.
5. Create positive/negative golden carriers and effect traces.
6. Record a reviewed decision for general, subset-only or absent control predication.
7. Define Phase 10 allowed subset and Phase 11 execution gate from that decision.

## Migration and compatibility strategy

Audit-only first; codegen unchanged. Existing vector `PredicateMask` ABI is not renamed or broadened. Any new guard type/encoding requires a separate compatibility/version decision.

## Tests

### Positive/property/determinism

- Existing predicate-mask encode/decode round-trip and vector inactive-lane behavior.
- Capability matrix generated identically from the same opcode registry/version.

### Negative

- Scalar/control/system/store/trap opcodes without proven semantics, unresolved predicate publication and stale capability version.

### Static

- No guarded transform can consume a row other than `ProvenSupported`.
- Field presence cannot set capability to supported.

## Benchmarks and KPI

- 100% opcode coverage with `Supported`, `Unsupported` or `Unresolved` and evidence link/reason.
- Zero codegen/runtime delta.
- All supported rows have encode/decode/effect goldens; unresolved count is a visible blocker, not zero.

## Diagnostics and telemetry

Opcode, predicate representation, def/use, inactive result/memory/trap/system behavior, evidence source/version and decision status.

## Bounded-search, timeout and fallback policy

No search or wall-clock cutoff. Generation is bounded by the finite opcode registry. Missing/ambiguous evidence produces `Unresolved` and disables guarded codegen.

## Risks and forbidden shortcuts

- Do not conflate vector lane masks with whole-instruction control guards.
- Do not invent guarded store/trap semantics in compiler metadata.
- Do not place ISA-extension work inside compiler refactoring.

## Rollback / kill switch

Audit artifact can be ignored; compiler remains EBB/speculation-free.

## Acceptance and merge gates

- ISA/compiler/runtime owner review and complete opcode matrix.
- Golden decode/execute/effect evidence for every supported row.
- Explicit decision controlling Phases 10–11.

## Status criteria

- `implemented`: capability audit generator/matrix exists.
- `verified`: matrix coverage and owner review complete.
- `default-enabled`: audit/guard only; no transform.
- `release-authorized`: capability finding, not ISA extension or guarded codegen.

## Residual work / next gate

Phase 10 implements only the proven subset or speculation-free motion. Phase 11 is deferred if general predication is absent.
