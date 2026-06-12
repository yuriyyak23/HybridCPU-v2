# Security Invariants

The virtualization model is fail-closed by construction. Security comes from denying authority unless neutral ownership, capability grants, evidence policy, and publication fences all agree.

## Invariant 1: Neutral Owner First

No compatibility frontend can own production state. A stateful virtualization feature must have a neutral owner before VMX-compatible projection is added.

## Invariant 2: Projection Does Not Mutate

Generated VMCS projection, VMX capability projection, completion projection, and trap projection cannot mutate authoritative runtime state.

## Invariant 3: VMX Exit Vocabulary Is Presentation

`VmExitReason`, `VmxExitQualification`, and `TrapDecision` are VMX-facing presentation vocabulary. They can describe how a neutral result appears to a compatibility consumer. They cannot decide runtime policy.

## Invariant 4: Host Evidence Does Not Leak

Host-owned evidence must remain host-owned unless a neutral evidence policy explicitly publishes a guest-visible projection. Debug trace, host caches, accelerator evidence, lane evidence, and runtime diagnostics are not automatically VMX-visible.

## Invariant 5: Completion Publication Is Fenced

Completion records that can later be projected to VMX must pass neutral publication rules. Compatibility completion helpers require a `TrapCompletionPublicationFenceResult` before creating compatibility-exit records.

## Invariant 6: Retire Publication Is Separate

Even if completion publication is allowed, retire publication can still be denied. `VmxRetireEffect.InterceptExit` must fail closed unless the publication fence says retire publication is authorized.

Route authorization is also not publication. `RuntimeOwnedCompletionPublication` may authorize only the completion route flag while retire stays false; the current fence still emits no completion record for that state.

## Invariant 7: Migration Serializes Neutral Model

Migration serializes guest-visible domain model and neutral descriptors, not host caches, VMCS backing stores, or manager state. VMCS projection fields with migration policy `RecomputedCompletion` or `ProjectionOnly` are not independent migration payload authority.

## Invariant 8: Legacy Absence Is Protected

The absence of `Legacy/VMX`, `VmxExecutionUnit`, `VmcsManager`, and active VMCS pointer state is part of the security boundary. Reintroducing these names as production implementations is not a refactor; it is an authority regression.

## Invariant 9: Denied Means Denied

Denied aliases can be useful compatibility evidence. They must not be treated as delayed success objects. A denied VMX alias remains denied until a future neutral owner, admission path, and publication path are implemented and tested.

## Invariant 10: Tests Are Not Authority

Test-only behavior, fake backends, and conformance probes do not define production requirements. Production authority must be present in runtime owners and admission services.

## Invariant 11: VMREAD Success Is Field-Local

A successful read-only VMREAD projection for one field does not open related fields. `GuestPc` does not open `GuestCr0`; `GuestCr3` does not open `HostCr3`; completion projection does not open migration payload authority.

## Invariant 12: SecureCompute Is Not VMX-Owned

VMX compatibility surfaces, `VmxCaps`, VMCS projection metadata, and VMX frontend handlers cannot activate, grant, materialize, checkpoint, migrate, or publish SecureCompute authority. SecureCompute authority must come from neutral secure runtime descriptors and policies.

## Threats Prevented

These invariants prevent:

- VMX-shaped state stores returning under new names;
- VMCS field write helpers becoming backdoor state mutation;
- trap policy being decided by VMX exit reasons;
- backend success being inferred from compatibility projection;
- host evidence becoming guest ABI;
- compiler emission bypassing runtime admission;
- migration importing/exporting host runtime caches;
- VMREAD field slices being generalized into privileged/control/host authority;
- VMX compatibility being mistaken for SecureCompute activation.
