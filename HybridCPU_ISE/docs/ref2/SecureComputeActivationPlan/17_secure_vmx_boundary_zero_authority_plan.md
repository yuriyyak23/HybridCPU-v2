# Secure VMX Boundary Zero Authority ADR/Plan

## Phase Metadata

- File name: `17_secure_vmx_boundary_zero_authority_plan.md`
- Phase goal: preserve VMX/VMCS/`VmxCaps` as zero-authority compatibility vocabulary for every named SecureCompute positive-looking path.
- Status: confirmed narrowly as read-only compatibility zero-authority; the named-path classifier itself is policy evidence, not runtime authority.
- Scope: VMX activation, VMCS state, compatibility read projection, compatibility write mutation, `VmxCaps`, active pointer identity, VMCS checkpoint metadata, completion/retire publication and compatibility projection after neutral result.
- No-goals: no SecureCompute VMX mode, secure VMCS, VmxCaps grant, VMX runtime manager, backend executor, VMCS state store or publication authority.

## ADR-SC-VMX-NAMED-PATH-ZERO-AUTHORITY

Phase 17 adds `SecureComputeNamedPathVmxZeroAuthorityPolicy` as a fail-closed named-path classifier. It covers the current closed path set:

- Phase 10 `GuestCr0`/`GuestCr4` read-only projection;
- Phase 13 hypercall backend-owner proof-only admission;
- Phase 14 completion/retire publication gate;
- Phase 15 migration/output-manifest classification;
- Phase 16 debug/attestation visibility classification;
- future Phase 20 restricted runtime execution path.

The policy records that each named path remains VMX zero-authority. Compatibility projection can be marked allowed only after a neutral runtime result exists, and that projection still creates no VMX authority, backend success, completion publication or retire publication.

For the Phase 13 hypercall path, this gate keeps the audit-required proof that trap projection and compatibility completion do not become SecureCompute service identity or backend-owner authority.

## Current Baseline

VMX/VMCS/`VmxCaps` deny SecureCompute activation, grant, secure-state storage, secure-state mutation, checkpoint authority, active pointer identity and publication authority. Compatibility read projection can expose only read-only values after neutral owner, value source, visibility, migration class and conformance proof exist.

## Authority Owner

Neutral runtime owners only. VMX is a compatibility frontend and projection vocabulary. VMCS metadata, active VMCS pointers, trap projection, compatibility completion and `VmxCaps` are not SecureCompute authority owners.

## Request/Result Vocabulary

`SecureComputeNamedPathVmxZeroAuthorityRequest` names the SecureCompute path and explicitly marks forbidden shortcut attempts:

- VMX activation;
- `VmxCaps` grant;
- VMCS state store;
- active pointer domain identity;
- compatibility read as secure-state authority;
- compatibility write as secure-state mutation;
- VMCS checkpoint authority;
- completion publication;
- retire publication;
- compatibility projection before a neutral result.

`SecureComputeNamedPathVmxZeroAuthorityResult` carries only classification and zero-authority bits. All authority bits are false in Phase 17.

## Exact Preconditions For Compatibility Projection

Compatibility projection after a named SecureCompute path requires:

- an already neutral runtime result from the path owner;
- explicit request for compatibility projection;
- no VMX activation, `VmxCaps`, VMCS store, active pointer, read-authority, write-mutation, checkpoint, completion or retire shortcut.

Without the neutral result, projection is denied as `DeniedProjectionWithoutNeutralResult`.

## Denied Shortcuts

Phase 17 denies:

- VMX activation as SecureCompute activation;
- `VmxCaps` as SecureCompute grant or descriptor materialization;
- VMCS or active VMCS pointer as SecureCompute state or identity;
- compatibility read projection as SecureCompute state authority;
- compatibility write projection as SecureCompute mutation;
- VMCS metadata as checkpoint/restore authority;
- compatibility projection as backend success;
- compatibility projection as completion publication;
- compatibility projection as retire publication.

## Migration/Evidence Classification

VMCS metadata and compatibility projection metadata remain denied as SecureCompute migration, checkpoint or restore authority. Future named paths must continue to classify VMX-visible outputs as recomputed or compatibility-visible only, never host-owned evidence or guest checkpoint payload authority.

## Completion/Retire Implications

VMX compatibility projection cannot publish completion or retire effects. Completion/retire publication remains owned by Phase 14 policy after a future neutral backend result and explicit owner/path/reachability proof.

## Compiler Implications

Phase 17 does not add compiler secure emission. Phase 19 remains the compiler boundary decision.

## Code Anchors

- `SecureComputeNamedPathVmxZeroAuthorityPolicy.cs`
- `SecureComputeVmxAuthorityBoundaryContract.cs`
- `SecureComputeCompatibilityBoundaryMatrixPolicy.cs`
- `SecureComputeVmReadVisibilityPolicy.cs`
- `SecureComputeVmWriteDenyPolicy.cs`
- `SecureComputeVmxCapsProjectionFence.cs`
- `SecureComputeVmcsProjectionFence.cs`

## Required Tests

- named SecureCompute paths remain VMX zero-authority;
- compatibility projection requires a neutral runtime result;
- projection after neutral result remains projection-only;
- VMX activation shortcut is denied;
- `VmxCaps` grant shortcut is denied;
- VMCS state store and active pointer identity shortcuts are denied;
- compatibility read/write authority shortcuts are denied;
- VMCS checkpoint metadata shortcut is denied;
- completion and retire publication shortcuts are denied;
- existing VMX boundary matrix remains negative for backend success and mutation;
- source scans prove no VMX runtime manager, field read/write helper, backend executor, publication true flag or compiler controlled-emission shortcut.

## Required Static/Source Scans

Use owner/path/reachability-scoped scans over Phase 17 authority-boundary and compatibility projection sources:

- no `VmcsManager`, `IVmcsManager` or VMX execution unit dependency;
- no field read/write helper call;
- no `VmxCaps.Secure` grant marker;
- no `new SecureComputeDomainDescriptor` from VMX compatibility code;
- no backend execution request/result dependency;
- no `BackendExecutionAuthorized: true`;
- no `CompletionPublicationAuthorized: true`;
- no `RetirePublicationAuthorized: true`;
- no compiler controlled-emission dependency.

Repository-wide VMX terms remain legitimate in VMX compatibility code and tests; only owner/path/reachability to SecureCompute authority is a violation.

## Bounded Rollback Procedure

Rollback Phase 17 by removing `SecureComputeNamedPathVmxZeroAuthorityPolicy.cs`, removing `SecureComputeVmxPhase17NamedPositivePathZeroAuthorityTests.cs`, reverting this file and restoring Phase 17 to compatibility-guard status. Do not reset Phase 13 through Phase 16 closures. Re-run the VMX boundary slice and release gate after rollback.

## SecureCompute Activation Implications

Phase 17 is a zero-authority guard only. It does not provide backend execution, production SecureCompute activation, compiler secure emission, VMX activation, VMCS authority or publication authority.

## Exit Criteria

- zero-authority matrix covers activate, grant, store, read, write, checkpoint, active pointer, publish and retire;
- named positive-looking paths are classified;
- compatibility projection is allowed only after a neutral result and remains zero-authority;
- tests remain negative for VMX authority;
- activation files cannot weaken VMX denial.

Exit status: satisfied only for the current VMX zero-authority claim. `VmxCompatibilityAdmissionService` is a reachable read-only/projection frontend and uses generic `Ordinary` runtime admission; it does not supply SecureCompute identity, grants, execution or publication. Every future named path must reprove this boundary independently.

## Dependency

Previous: `16_secure_debug_attestation_api_plan.md`. Next: `18_secure_nested_child_intent_owner_rfc.md`.
