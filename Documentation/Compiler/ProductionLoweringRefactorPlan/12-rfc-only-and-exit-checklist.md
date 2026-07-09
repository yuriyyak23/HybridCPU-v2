# 12 — RFC-only contours, ADRs, and final exit checklist

## Goal

Define contours that must stay outside the normal production-lowering implementation track, list required ADR/RFC work, and record the exit checklist for the full production-lowering refactor.

## RFC-only contours

### VMX projection-only

VMX remains projection/no-emission. A normal compiler production-lowering PR must not introduce:

- VMX carrier emission;
- VMCS compiler-owned state;
- `VmxCaps` as capability authority;
- VMX execution, commit, retire, or publication claims;
- guest architectural state mutation from compiler evidence.

A VMX production track requires a separate architectural RFC that defines runtime-owned VMX backend authority, VMCS ownership, legality stages, bridge handoff, evidence isolation, and ISE conformance.

### SecureCompute policy/admission-only

SecureCompute remains policy/admission/evidence-only. A normal compiler production-lowering PR must not introduce:

- secure backend carrier emission;
- policy/admission success as runtime execution authority;
- certificate/token/evidence as execution rights;
- host-owned evidence entering guest/domain architectural state.

A SecureCompute production track requires a separate architectural RFC that defines runtime-owned secure backend architecture, admission/attestation boundaries, domain isolation, publication rules, and rollback semantics.

### ParserOnly / NoEmission / FutureGated / UnknownRejected

These contours remain non-production unless a future RFC creates a new runtime-owned architecture and test matrix:

- `ParserOnly` stays parser evidence.
- `NoEmission` stays no-emission.
- `FutureGated` stays blocked until gates exist.
- `UnknownRejected` stays fail-closed.

## ADRs/RFCs required before implementation

1. **ADR: Production lowering authority model**
   - Defines production package semantics.
   - States compiler product is not runtime legality/execution/publication/commit/retire.

2. **ADR: Contour-specific provider lifecycle**
   - Defines analyzer, shell provider, production provider, registry responsibilities.
   - Freezes no-fallback routing policy.

3. **ADR: Carrier/sideband/descriptor/facts production gates**
   - Defines required artifacts per contour.
   - Defines envelope separation and telemetry/evidence requirements.

4. **ADR: Runtime Legality A/B handoff for production compiler packages**
   - Defines runtime-owned gates that remain after compiler package creation.
   - Defines bridge acceptance semantics.

5. **ADR: Golden artifact and ISE parity requirements**
   - Defines snapshot format, decode/encode parity, and lane/slot parity.

6. **ADR: Non-production contours: VMX and SecureCompute**
   - States why they are excluded from normal production-lowering PR slicing.
   - Defines separate RFC-only process.

7. **ADR: No-fallback policy for production lowering**
   - Defines proof requirements.
   - Prevents scalar/stream/DSC/L7 fallback leakage.

## Final exit checklist

Production refactor compiler lowering is ready only when all items are true:

- [ ] Production-capable contours are explicitly listed.
- [ ] Helper/parser/no-emission/future-gated contours are explicitly listed.
- [ ] Production provider code appears only under the production-provider contract.
- [ ] Every production result identifies what is produced.
- [ ] Every production result identifies the contour.
- [ ] Every production result identifies the exact production gate.
- [ ] Every production result identifies required runtime authority still pending.
- [ ] Every production result states what authority it explicitly does not have.
- [ ] Carrier remains separate from execution/publication/commit/retire.
- [ ] Descriptor success remains separate from execution authority.
- [ ] Helper success remains separate from production lowering.
- [ ] No-fallback proof is mandatory for production paths.
- [ ] ISE decode/encode parity is tested.
- [ ] Lane/slot ownership parity is tested.
- [ ] Positive golden artifacts exist per contour.
- [ ] Negative tests exist before caller migration.
- [ ] Legacy APIs remain adapters until deprecated.
- [ ] Deprecated APIs have a removal plan.
- [ ] Telemetry/evidence fields prove artifact identity and gate satisfaction.
- [ ] Rollback can disable production gates without runtime ABI break.

## Recommended next implementation PR

The next implementation PR after this documentation should be:

```text
PR-1: tests — production-lowering readiness source scanner
```

It should add CI guardrails only. It must not add production providers or migrate callers.
