# 04. Compiler Secure/Virtual Semantic Intent

## Goal

Extend compiler IR/lowering so programs can request secure and/or virtualized external execution without encoding runtime authority or CXL topology.

## IR requirements

Add or normalize provider-neutral semantic fields equivalent to:

- execution domain: host / virtualized / secure / secure+virtualized;
- external-effect class: pure/idempotent/non-idempotent and replay constraints;
- staged publication required/preferred;
- coherent access required/optional;
- secure evidence required + minimum assurance class;
- bounded VirtualIo required;
- cancellation/containment requirement;
- input/output device-read/write intent.

These are requirements, not handles. IR must not contain live child/secure leases, mapping IDs, CXL endpoint IDs, HDM decoder IDs, DPA, switch paths, Fabric Manager bindings or evidence generations.

## Lowering

Reuse existing typed-slot/resource and bundle carriers where possible. Introduce the minimum semantic descriptor necessary to preserve requirements through scheduling/lowering.

Do not add a CXL lane or `Remote Lane`. lane6 `DmaStreamCompute` and lane7 L7-SDC retain distinct existing execution semantics.

## Legality

Compiler legality proves structural/semantic admissibility only. Runtime may still reject the operation because exact domain, mapping, device, security or provider generations are unavailable/stale.

`RequiresVirtualizedDomain` and secure evidence flags may remain compact planning fields, but they must lower to a runtime request requiring an exact context before an external effect.

## Replay

Compiler/replay certificates classify whether an external effect can be retried or reused. They never authorize provider resubmission. Non-idempotent effects require explicit runtime containment/closure before replay.

## Direct coherent path

Do not make direct coherent output the default lowering. `DirectRequired` must fail if the runtime contract cannot prove CPU alias exclusion, coherent binding, visibility and symmetric release. Staged publication is the safe default.

## Tests

- no raw CXL topology survives parsing/IR/lowering;
- secure+virtual requirement survives all optimization/lowering stages;
- typed-slot admission rejects missing external-operation resources;
- unsafe external replay is rejected;
- staged fallback obeys source semantic policy;
- runtime rejection cannot be overridden by compiler certificates.
