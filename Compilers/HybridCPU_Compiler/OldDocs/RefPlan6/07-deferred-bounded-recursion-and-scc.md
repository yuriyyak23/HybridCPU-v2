# Phase 7 — Deferred bounded recursion capability

## Status

Implemented and qualified as an explicit default-off extension of ScalarControlFlowV2 V1.
This is not a V2 profile and does not change the default V1 admission surface.

Phase 2 already owns deterministic SCC discovery and V1 recursion rejection. This phase
must reuse that graph infrastructure rather than introducing a second SCC/call-graph
implementation.

## Entry gate

Do not implement or enable recursion until V1 evidence has proven:

- direct-call ISA/ABI correctness on ISE;
- caller/callee-save and live-across-call correctness;
- deterministic non-leaf frame layout and stack load/store behavior;
- object relocation/link correctness;
- bounded call-graph diagnostics;
- deterministic `.hcexe` generation and loader/ISE parity.

## Why recursion is separate

Acyclic direct calls require correct calls and frames. Recursion additionally requires a
bounded dynamic stack/depth policy. SCC size alone does not bound dynamic recursion
depth, and the compiler must not fabricate runtime execution authority to obtain one.

A separately versioned V1 recursion contract must therefore authorize exactly one
well-defined model, for example:

1. reject recursion unless a static proof bounds depth; or
2. use an explicit runtime-owned stack/depth guard and failure contract; or
3. use another formally specified bounded execution contract.

The chosen model must identify the layer that owns overflow/depth enforcement.

## Selected V1 authority contract

`hybridcpu.scalar-control-flow-v2.bounded-recursion/v1` selects static proof only:

- at most one recursive SCC with at most eight static non-generic `Int32(Int32)` methods;
- exactly one recursive edge per SCC method;
- the recursive argument is exactly `arg0 - 1`;
- an `arg0 == 0` terminating path dominates that edge and the admitted recursive body has no backedge;
- every ingress from outside the SCC supplies a non-negative compile-time `Int32` constant;
- maximum dynamic call depth is computed from the SCC condensation graph and the constant countdown bound;
- final stack demand is conservatively bounded after register allocation from authoritative frame sizes;
- depth or stack overflow is rejected deterministically at compile/link time.

The compiler owns admission and evidence only. There is no runtime guard, runtime fallback,
or compiler-metadata execution authority. ISE/runtime/loader/ISA contracts are unchanged.

## Implemented work

- reuse Phase 2 SCC identities/classification;
- define direct vs mutual recursion admission policy;
- define static/dynamic call-depth semantics;
- define worst-case frame/stack accounting;
- define stack overflow/depth-failure behavior;
- prove that no runtime guard ABI is required for the admitted static subset;
- prove saved registers, return values and frame chains across recursive calls;
- define recursion-specific budgets and deterministic diagnostics;
- version the capability independently from ScalarControlFlowV2 V1.

## Qualified tests

- direct recursive scalar function;
- two-method mutual recursion;
- recursion with live caller values;
- recursion with locals/spills;
- recursion with nested nonrecursive helper calls;
- depth/stack failure at the exact specified boundary;
- deterministic object/image output;
- loader/ISE stack/frame conformance.

## Non-goals inherited from V1

Recursion does not imply GC, references, arrays, EH, virtual dispatch, delegates,
reflection, async, threading, P/Invoke or TLS.

## Release disposition

The capability is an explicit, paired depth/stack opt-in within the V1 profile. It remains
default-off. Missing options and all unproved/dynamic recursion remain fail-closed. Default
enablement is not authorized by this phase.
