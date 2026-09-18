# ADR-SC-HYP-BACKEND-OWNER

## Decision Status

- Phase: `13`.
- ADR identifier: `ADR-SC-HYP-BACKEND-OWNER`.
- Status: proof-only ABI/identifier contract confirmed; backend execution owner, production carrier and result path remain open.
- Date: 2026-06-17.
- Maximum implemented result: `AllowedProofOnlyNoExecution`.
- Explicitly closed: backend execution, completion publication, retire publication and compiler secure emission.

The repository proves the frozen `VMCALL` transport opcode `259` and the register form used to carry a decoded leaf and descriptor selector. Phase 13 now allocates the exact SecureCompute decoded leaf, SecureCompute service ID and backend owner ID in `SecureHypercallBackendOwnerAbiRegistry`.

The allocation closes only the owner-contract identifier gate. It does not authorize backend execution, completion publication, retire publication, compiler emission or production SecureCompute activation.

## Identifier Contract

The following are separate typed concepts:

| Concept | Type | Current production decision |
| --- | --- | --- |
| transport opcode | `SecureHypercallTransportOpcode` | `VMCALL` transport is `259`; transport recognition owns no SecureCompute authority |
| decoded leaf | `SecureHypercallDecodedLeaf` | `0x5343_4859_5042` from `SecureHypercallBackendOwnerAbiRegistry.DecodedLeaf` |
| SecureCompute service ID | `SecureComputeServiceId` | `0x5343_5356_4345` from `SecureHypercallBackendOwnerAbiRegistry.ServiceId` |
| backend owner ID | `SecureBackendOwnerId` / `SecureBackendOwnerDescriptor.OwnerId` | `0x5343_4F57_4E52` from `SecureHypercallBackendOwnerAbiRegistry.OwnerId` |

No numeric equality, decode event, trap reason or compatibility projection may infer another identifier. In particular, opcode `259`, `VmExitReason.VmCall == 18`, register selectors, VMFUNC leaves and fixture `0x10` are not SecureCompute service or owner assignments.

## Owner And Lifetime

The contract requires backend owner identity to come from a neutral runtime registry/service, but no such executable registry/service is proven. `SecureBackendOwnerDescriptor` and `SecureHypercallBackendOwnerAbiRegistry` provide proof vocabulary and identifiers only. VMX, VMCS, `VmxCaps`, decoded leaf values, compatibility projection, evidence payloads and migration metadata cannot source owner identity.

The owner contract is immutable for one owner epoch:

- owner ID must match the accepted service contract;
- source must be neutral runtime-owned;
- owner epoch must match the contract epoch and current policy epoch;
- stale, missing, compatibility-sourced or wrong owners fail closed;
- owner replacement requires a new epoch and re-materialization of contract proof.

Owner admission remains proof acceptance only. It does not grant execution or publication.

## Descriptor Materialization

`SecureHypercallBackendContractDescriptor.IsMaterialized` requires:

- exact decoded leaf;
- exact SecureCompute service ID;
- exact backend owner ID;
- materialized owner epoch;
- supported contract version;
- materialized typed grant;
- replay, cancellation and migration classifications.

`Unresolved` is the production-safe default and is denied. Descriptor presence without exact identifiers is not contract materialization.

## Request And Result Vocabulary

The request vocabulary is `SecureHypercallBackendContractRequest`. It carries separately typed transport opcode, decoded leaf, service ID, contract version, owner descriptor, current epoch, typed grant, evidence state, argument list, replay state and cancellation intent.

The Phase 13 result is `SecureHypercallBackendContractAdmissionResult`. Its only allowed state is `AllowedProofOnlyNoExecution`; every result keeps:

- `BackendExecutionAuthorized == false`;
- `CompletionPublicationAuthorized == false`;
- `RetirePublicationAuthorized == false`.

No backend executor or success stub is part of this ADR.

## Contract Versioning

The contract uses `SecureHypercallContractVersion(major, minor)`.

- major `0` is unmaterialized;
- exact version equality is required during Phase 13;
- unsupported major or minor versions fail closed;
- future compatible-minor negotiation requires a separate ADR amendment and tests;
- decoded leaf and service ID allocations cannot be changed by a version alias.

The production contract version is `1.0`. Version equality is a contract check only; it is not a service ID allocation.

## Argument Ownership

Arguments have explicit ownership:

- `GuestImmediate`: copied scalar value; never a host pointer;
- `ExplicitSharedBuffer`: descriptor-owned buffer ID plus bounded length and current typed grant;
- `OpaqueRuntimeHandle`: nonzero opaque value with current runtime provenance; not dereferenceable guest or host address;
- `RawHostPointerDenied`: always denied.

Argument ownership does not transfer backend authority. Request objects are immutable proof inputs and are not active host objects.

## Shared Buffers And Bounds

Shared-buffer admission requires:

- `SecureIoDmaPolicy.ExplicitSharedBuffersOnly`;
- nonzero validated domain owner;
- current policy and buffer lifetime epoch;
- allowed evidence class;
- current buffer grant;
- requested nonzero length within the materialized buffer length.

Buffer ID alone is never authority. Raw private pointers, active host pointers, native device pointers and out-of-bounds slices are denied.

## Required Grants And Evidence

The presented grant must exactly match the contract grant, carry runtime provenance and match the current epoch. Missing, forged, wrong-kind or stale grants are denied.

Evidence must be separately validated and its epoch must equal the current policy epoch. Evidence visibility is not owner authority. Host-owned evidence remains non-migratable and cannot be projected as guest authority.

## Replay And Idempotence

Default policy is `DenyReplay`.

An idempotent retry can be considered only when a later accepted contract explicitly selects `IdempotentRetryWithMatchingToken`, proves that the operation is side-effect-safe and validates a matching replay token. Otherwise any replay, duplicate sequence or mismatched token is denied.

Phase 13 introduces no replay cache and no execution side effect.

## Cancellation

Cancellation is a pre-execution denial only. A cancellation request returns `DeniedCancelledBeforeExecution`; it cannot synthesize success, completion or retire publication.

Post-dispatch cancellation semantics remain Phase 20 work because no backend dispatch exists.

## Failure Taxonomy

The fail-closed taxonomy includes:

- unresolved contract;
- unknown service ID;
- decoded leaf mismatch;
- unsupported contract version;
- missing, non-neutral or wrong owner;
- owner epoch mismatch;
- missing or stale grant;
- missing or stale evidence;
- invalid shared buffer;
- raw pointer representation;
- invalid opaque handle;
- replay/idempotence violation;
- cancellation before execution.

Failures are neutral admission results. VMX exit reasons may project a separately produced neutral denial but do not own the failure decision.

## Migration Classification

- in-flight request: `NonMigratableInFlight`;
- descriptor-only request state: `DescriptorOnlyRevalidatedAfterRestore`;
- Phase 13 result: `NoResultBeforeExecution`;
- future derived result: `RecomputedAfterRestore` unless a later owner-specific RFC explicitly classifies guest-visible state.

Checkpoint payloads continue to exclude host evidence, backend bindings, native tokens, raw measurement secrets, raw sealing keys, active host pointers, VMCS metadata and compatibility projection metadata.

## Completion And Retire Ladder

The required ladder is:

1. contract denied;
2. `AllowedProofOnlyNoExecution`;
3. future backend execution authorized;
4. future neutral backend result produced;
5. future completion owner accepts a completion record;
6. future retire owner authorizes architectural publication;
7. optional compatibility projection from the retired neutral result.

Phase 13 stops at step 2. Admission, owner acceptance, a completion fence or a route descriptor cannot skip a step.

## VMX Compatibility Projection

`VMCALL` decode and `VmExitReason.VmCall` remain compatibility vocabulary. The production VMX frontend continues to call `HypercallBackendAdmissionRequest.MissingNeutralOwner(...)`.

Any future VMX projection may occur only after neutral result and publication owners have completed their separate gates. VMX never supplies service identity, owner identity, grants, evidence, migration authority or publication authority.

## Compiler Decision

No compiler change is approved. SecureCompute compiler no-emission remains the Phase 13 decision. Typed runtime vocabulary is not an emission contract.

Controlled emission requires Phase 19 approval after an exact production ABI allocation and must not be inferred from this ADR.

## Bounded Rollback

If a Phase 13 regression is found:

1. set the production contract descriptor to `Unresolved`;
2. remove the affected service/owner registry entry without changing opcode decode;
3. retain `SecureIoHypercallAdmissionPolicy` as admitted-denied;
4. retain VMX `MissingNeutralOwner(...)` routing;
5. restore no-emission and remove any compatibility advertisement;
6. rerun Phase 13, migration, VMX boundary and release-gate negative tests;
7. correct release wording to state that Phase 13 is open.

Rollback must not rely on repository-wide destructive reset and must not disturb unrelated runtime owners.

## Code And Test Anchors

- `SecureHypercallBackendContract.cs`
- `SecureHypercallBackendOwnerAbiRegistry.cs`
- `SecureHypercallBackendContractAdmissionPolicy.cs`
- `SecureBackendOwnerDescriptor.cs`
- `SecureBackendOwnerAdmissionPolicy.cs`
- `SecureHypercallDescriptor.cs`
- `SecureIoHypercallAdmissionPolicy.cs`
- `SecureHypercallBackendOwnerPhase13Tests.cs`
- `VmxHypercallBackendOwnerDecisionReadinessTests.cs`
- `SecureMigrationPolicyTests.cs`

## Exit Criteria

Implemented and tested:

- typed identity separation;
- owner lifetime/epoch and version checks;
- request/result, argument, buffer, grant, evidence, replay, cancellation and migration vocabulary;
- fail-closed proof-only admission;
- zero backend/completion/retire authority;
- VMX zero-authority and compiler no-emission decisions;
- bounded rollback procedure.

Closed identifier decisions:

1. exact production `SecureHypercallDecodedLeaf` allocation: `0x5343_4859_5042`;
2. exact production `SecureComputeServiceId` allocation: `0x5343_5356_4345`;
3. exact production `SecureBackendOwnerId` allocation: `0x5343_4F57_4E52`;
4. registry source: `SecureHypercallBackendOwnerAbiRegistry`, revision `ADR-SC-HYP-BACKEND-OWNER-2026-06-17`.

The identifier allocation and proof-only request model are confirmed. Phase 13 is not closed for runtime authority: no SafetyVerifier certificate reaches it, no backend executes, and no opaque result exists. A future hypercall execution change must follow the neutral probe, memory/IOMMU decisions and the ordered plan in `24_audit_revalidation_and_dependency_order.md`.

## Dependency

Previous: `12_secure_io_shared_buffer_policy_plan.md`. Next: `14_secure_completion_retire_publication_plan.md`.
