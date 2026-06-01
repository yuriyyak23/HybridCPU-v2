# Roadmap And Residual Risk

Future work must continue the neutral-owner-first rule. No future VMX feature should be implemented by restoring legacy VMX authority.

## Recently Closed Or Reclassified Work

The following items are no longer open as generic roadmap bullets:

- runtime-owned trap completion route design is closed as a neutral route policy, with the VMX frontend still using projection-only denial;
- generated read-only VMREAD value projection is partially implemented through explicit neutral owners;
- descriptor readiness policy is closed fail-closed and does not derive from VMREAD values;
- migration/evidence proof for recomputed completion-owned fields is closed;
- VMCALL backend admission policy exists, but production VMCALL remains `MissingNeutralOwner`.

## Next Heavy Steps

Recommended next heavy steps:

1. Continue VMREAD field-by-field only when a new explicit neutral owner/value source exists.
2. Design neutral privileged execution-state semantics before opening `GuestCr0` or `GuestCr4`; otherwise keep them denied.
3. Materialize a real neutral hypercall backend owner only when concrete runtime hypercall semantics, typed capability, and evidence policy exist.
4. Use `TrapCompletionRouteDescriptor.RuntimeOwnedPublication` only after a neutral backend owner authorizes backend execution.
5. Design neutral nested child-intent owner only for a real admitted nested path; do not use mutable shadow VMCS state as authority.
6. Keep SecureCompute/VMX compatibility as projection/denial unless SecureCompute runtime descriptors and policy explicitly authorize a read-only projection.

## Successful VMCALL Future Shape

A future successful VMCALL path must look like:

```text
VMCALL
  -> decode and alias projection validation
  -> RuntimeBoundaryAdmissionService
  -> neutral trap result
  -> neutral hypercall backend owner
  -> typed capability grant and evidence policy
  -> TrapCompletionRouteService with runtime-owned publication route
  -> neutral completion record
  -> TrapCompletionPublicationFence allowed
  -> retire publication allowed
  -> VMX-compatible completion projection
```

The path must not use `VmExitReason.VmCall` as the proof that a hypercall is authorized. Current production VMCALL intentionally stops at missing neutral backend owner.

## VMREAD Expansion Shape

A future VMREAD field can be opened only when the full chain exists:

- generated schema owner matches the expected neutral owner;
- `RuntimeBoundaryAdmissionService` admits `ReadCompatibilityProjection`;
- evidence/access policy allows the field's visibility class;
- the neutral owner exposes an explicit read-only value source;
- migration/evidence classification proves the value is projection, recomputed, or descriptor-owned as appropriate;
- writes remain denied unless a separate neutral write owner exists.

Current allowed slices are completion-owned fields, memory-owned `GuestCr3`/`EptPointer`/`Vpid`/`Cr3TargetCount`, and execution-owned `GuestPc`/`GuestSp`/`GuestFlags`.

Current denied slices include `GuestCr0`, `GuestCr4`, host execution aliases, `HostCr3`, compatibility-control fields, unknown fields, and all writes.

## Privileged Execution Future Shape

Opening `GuestCr0` or `GuestCr4` requires a neutral privileged execution-state owner. That owner must define actual CR0/CR4 semantics, including at least:

- bit legality and reserved-bit handling;
- paging/protection/extension interaction rules;
- guest-visible read-only snapshot semantics;
- evidence visibility class;
- migration/checkpoint classification;
- conformance proving no VMCS scalar fallback.

Until those semantics exist, `PrivilegedExecutionStateProjectionDenied` is the correct current behavior.

## Nested Future Shape

Nested expansion should land through:

- neutral child domain intent descriptors;
- neutral capability filter;
- nested memory composition owner;
- nested evidence policy;
- compatibility projection to VMX only after neutral admission.

Shadow VMCS compatibility bridge objects must remain bridge/projection vocabulary, not mutable nested runtime authority.

## SecureCompute Future Shape

VMX compatibility can expose SecureCompute-related projection only through secure runtime descriptors and explicit policies. `VmxCaps`, VMCS projection metadata, and VMX frontend paths cannot grant, activate, materialize, migrate, or checkpoint SecureCompute authority.

SecureCompute compatibility work must keep:

- secure owner/value source proof;
- secure visibility policy;
- migration classification;
- conformance proof;
- write/backend/checkpoint denial unless a neutral secure owner admits them.

## Residual Risks

Known residual risks:

- VMX vocabulary is still present in many compatibility and historical test surfaces; static checks must distinguish projection vocabulary from authority.
- Some older research text contains stale counts and pre-closure risks; use it for principles, not current inventory.
- Broad `FullyQualifiedName~Vmx` filters can include `NonVmx` tests.
- Conformance files intentionally mention forbidden manager names as strings; inventory tools must classify those as tripwire evidence, not implementation.
- A future developer could misread admitted-denied projection as backend success if documentation and tests are not kept current.
- A future developer could infer CR0/CR4 or host state from existing guest execution/memory views; this remains forbidden.

## Red Flags For Future Reviews

Stop a change immediately if it:

- adds a VMX runtime manager;
- adds a mutable VMCS field store;
- adds active VMCS pointer state;
- uses `VmExitReason` inside neutral runtime policy;
- constructs compatibility completion without a neutral fence;
- uses `TrapCompletionRouteDescriptor.RuntimeOwnedPublication` without a real backend owner;
- treats a `TrapDecision` as runtime policy;
- turns VMX no-emission tests into production backend emission;
- stores host evidence in guest-visible VMCS projection;
- opens `GuestCr0`, `GuestCr4`, host execution aliases, or control fields without neutral semantics and conformance.

## Done Criteria For Future VMX Work

A future VMX-compatible feature is done only when:

- neutral owner exists;
- admission gates are explicit;
- capability and evidence requirements are explicit;
- generated projection artifacts are updated if needed;
- denied/fail-closed states are tested;
- no-emission and static quarantine remain green;
- documentation classifies authority, projection, denied aliases, and residual risk.
