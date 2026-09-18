# Owner Specific RFC ADR Process

## Phase Metadata

- File name: `03_owner_specific_rfc_adr_process.md`
- Phase goal: define the only process that can open a future positive SecureCompute path.
- Status: process gate; not activation approval.
- Scope: RFC/ADR template, required owner map and mandatory conformance payload.
- No-goals: no runtime code by process approval alone.

## Current Baseline

Backend owner proof can reach `AllowedProofOnlyNoExecution` only. The accepted Phase 09 privileged execution-state owner can reach `AllowedOwnerMaterializedProjectionClosed`; neither result is backend execution, projection or publication evidence.

## Authority Owner

The owner named by the RFC/ADR owns only the specific neutral fact or operation it defines. VMX, VMCS, `VmxCaps`, tests, telemetry and documentation never own the path.

## What Can Be Implemented

- a reusable RFC/ADR template;
- an owner map table;
- a pre-implementation checklist;
- a negative-test-first rule;
- a release-gate update rule.

## What Remains Denied/Future-Gated

Until the RFC/ADR is implemented in code and tests:

- backend execution remains closed;
- publication remains closed;
- compatibility projection remains denied;
- migration serialization remains denied for host-owned facts;
- product activation claims remain forbidden.

## Forbidden Shortcuts

- treating an approved RFC/ADR as implementation;
- using schema presence as value source;
- using a compatibility field as owner;
- using proof-chain acceptance as backend success;
- merging positive code before negative tests.

## Required RFC/ADR

Every positive path must name:

- canonical decode-owned operation identifier and taxonomy owner;
- descriptor registry and sole materialization/revocation owner;
- opaque descriptor binding carried instead of a full caller-supplied descriptor;
- SafetyVerifier as the sole issuer of an immutable admission certificate;
- certificate binding for operation/domain/VT/slot/epoch/effects identity;
- grant-ledger mint/revoke/reissue owner;
- named neutral effect owner;
- typed request/result shape;
- input and output authority classes;
- capability/grant requirements;
- evidence visibility class;
- migration/checkpoint class;
- completion fence;
- retire rule;
- compatibility projection rule;
- denial matrix;
- required tests and static scans.
- production composition root and reachability from decode through issue, named effect result, completion and retire;
- bounded rollback that disables only the named path and invalidates its bindings/certificates/grants.

## Code Anchors

- `SecureBackendOwnerDescriptor.cs`
- `SecureBackendOwnerAdmissionPolicy.cs`
- `RuntimeBoundaryAdmissionService.cs`
- `SecureGrantAuthorityPolicy.cs`
- `SecureEvidencePolicy.cs`
- `SecureMigrationAdmissionPolicy.cs`
- `SecureCompletionPublicationFence.cs`
- VMREAD projection services for compatibility-facing owners.

## Documentation Anchors

- `SecureComputerefactoringNew/20_positive_runtime_execution_rfc_gate.md`
- `SecureComputerefactoringNew/21_release_gate_and_activation_checklist.md`
- `VirtualiztionRefactoringNew/05_privileged_execution_state_owner_decision.md`
- `VirtualiztionRefactoringNew/16_external_audit_activation_readiness_addendum.md`

## Required Tests

- one test per denial branch before any positive test;
- static guard that approved RFC vocabulary cannot be read as activation;
- owner-specific matrix coverage;
- release-gate coverage for product-claim wording.

## Required Static/Source Scans

- `ProofChainAccepted.*authorizes`
- `approved RFC.*backend execution`
- `owner descriptor.*activation`
- `schema entry.*current value`
- `VMX.*owner`
- `VmxCaps.*grant`

## Migration/Evidence Classification

The RFC must classify every input and output as one of:

- host-owned denied;
- guest-visible;
- migration-serializable;
- recomputed-after-restore;
- compatibility projection only;
- debug-only;
- forbidden.

## Completion/Retire Implications

The RFC must state whether the path has:

- no backend result;
- internal backend result only;
- internal completion record;
- completion publication;
- retire publication.

These are separate states.

For a hypercall owner, the RFC must keep four identifiers distinct:

- transport opcode;
- decoded leaf register value;
- SecureCompute service/hypercall ID;
- neutral backend owner ID.

No one identifier may be inferred from another. Test fixture IDs are not ABI assignments.

The RFC must also decide versioning, request/result ownership, replay/idempotence, cancellation/failure semantics and whether the path is internal-only or guest-visible.

## SecureCompute Activation Implications

An RFC can open implementation work only. It cannot claim limited activation until `22_limited_securecompute_release_gate.md` passes.

## Exit Criteria

- template complete;
- owner map fields complete;
- no positive path can bypass negative tests;
- first owner RFC `ADR-SC-PES-GuestCr0Cr4` is accepted, implemented and linked to `09_privileged_execution_state_owner_rfc.md`;
- its follow-up projection remains isolated behind Phase 10.
- implementation merge remains blocked until the RFC names exact identifiers, typed contracts, negative tests, migration classes and rollback conditions.

## Dependency

Previous: `02_global_forbidden_regressions_and_release_guards.md`. Next: `04_no_effect_disabled_baseline_revalidation.md`.
