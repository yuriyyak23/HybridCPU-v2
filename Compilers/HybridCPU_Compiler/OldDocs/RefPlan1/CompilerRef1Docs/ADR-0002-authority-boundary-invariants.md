# ADR-0002: Authority Boundary Invariants

Date: 2026-07-09

Status: accepted

## Context

HybridCPU compiler output crosses several boundaries before any runtime action
can be trusted. Phase 09 makes those boundaries explicit in public names,
typed results, tests, and documentation.

## Decision

The following invariant is normative:

```text
carrier != execution
execution != publication
publication != authority
authority != commit
commit != retire
retire != evidence
evidence != production lowering
```

Compiler-side rules:

- compiler is not runtime authority;
- compiler never owns final runtime `LegalityDecision`;
- runtime Legality A/B remains runtime-owned;
- typed-slot facts are structural evidence only;
- sideband, descriptor, token, certificate, and evidence values are not
  execution rights;
- descriptor parser success is not production lowering;
- helper success is not production lowering;
- carrier emission is not execution, publication, commit, or retire;
- VMX is projection/no-emission only in the compiler layer;
- VMCS is not compiler-owned state;
- `VmxCaps` is not capability authority;
- SecureCompute is policy/admission/evidence-only in the compiler layer;
- DSC and L7-SDC remain distinct contours with no hidden fallback;
- L7 descriptorless submit must fail closed;
- host-owned evidence must not enter guest/domain architectural state.

## Consequences

Any future positive result at a compiler boundary must expose its authority
class, evidence class, execution claim, publication class, runtime dependencies,
and fallback posture through typed data. A raw boolean or carrier artifact alone
is not sufficient authority.

