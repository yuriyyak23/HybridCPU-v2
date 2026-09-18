# Phase 03 - Owner Specific RFC ADR Process

Status: process gate. No runtime activation.

## 2026-06-11 Audit Contract

- File name: `03_owner_specific_rfc_adr_process.md`.
- Purpose: define the required RFC/ADR package before any denied/future-gated behavior can become implementation work.
- Status: process gate only; a draft RFC/ADR is not approval.
- Scope: owner map, domain boundary, capability/evidence/migration classes, backend execution, completion, retire, rollback, adjacent denials.
- No-goals: no runtime code permission, no broad VMX feature approval, no owner inference from VMX/VMCS/`VmxCaps`.
- Code anchors: `RuntimeBoundaryAdmissionService.cs`, `HypercallBackendAdmissionPolicy.cs`, `TrapCompletionRoutePolicy.cs`, `TrapCompletionPublicationFence.cs`, `VmxCompatibilityAdmissionService.cs`, `VmxCompatibilityAdmissionService.Traps.cs`.
- Authority owner: the accepted RFC/ADR must name a neutral runtime owner independent from the compatibility authority plane; repository-local placement is allowed, while docs/tests/schemas remain non-authoritative.
- Required RFC/ADR: mandatory for every future positive path with full owner map: field/operation, owner, value source, capability policy, evidence class, migration class, denial reason.
- Acceptance criteria: no positive path proceeds unless owner map and adjacent denials are complete; missing evidence is `не доказано` and `требует owner-specific RFC/ADR`.
- Tests/static scans: RFC marker tests, owner-map completeness tests, positive-test gating scans.
- Risks: treating approval of a document outline as execution permission.
- Next-gate dependency: owner-specific candidate phases 05, 06, and 11.

## Phase Goal

Define the minimum RFC/ADR package required before any future positive virtualization path can move from denied/future-gated to implementation.

## Historical Baseline (2026-06-11)

Phase 16 forbids converting audit recommendations into production work without a new owner-specific RFC/ADR. Existing closure files are not activation approval.

## Owner Of Authority

The RFC/ADR must name the neutral runtime owner that owns the fact or operation. Here `neutral` means outside and independently reviewed from the VMX/VMCS compatibility authority plane; it does not require an owner outside the repository or organization. VMX, VMCS, `VmxCaps`, tests, docs, generated schemas, telemetry, and migration images cannot be named as the authority owner.

Governance attribution and runtime authority are separate:

- D2 is an owner-approved architecture/ABI decision represented by an immutable `VirtualizationDecisionSpecV2` and a later separate `VirtualizationDecisionAcceptanceRecordV2` over the spec SHA+digest, with `DecisionOwner`, required reviewers/CODEOWNERS evidence, exact operation/leaf and full owner map;
- E2 is a live, operation-specific SafetyVerifier certificate bound to the accepted D2 inputs and the current attempt;
- E3 is an opaque execution receipt issued by the accepted backend owner;
- D2 does not execute, E2 does not execute, and E3 does not publish completion or retire.

A machine-readable Decision Spec plus Acceptance Record may validate D2 and drive generation after acceptance. Neither artifact is runtime authority. The acceptance record must not claim the SHA of its own containing commit, and the compatibility frontend cannot author, accept or self-review its own owner decision. The Phase 34 v1 single-manifest validator remains negative substrate and cannot close D2.

Phase 38 is the repository-owner architecture/ABI/policy ADR for the first slice. It accepts the neutral architecture role, stable `HCOWNR` allocation, exact namespace/leaf/ABI, capability bit and grant policy, execution-only domain, cancellation/replay, host-owned completion and drain/retire policies. PR-A supplies the v2 schemas, canonical encoders/digests and structural validation. Separately authorized PR-B supplies committed exact SpecV2/CODEOWNERS bytes, matching owner/architecture review receipts, the later AcceptanceRecordV2, allocation metadata and generated exact lookup. The subsequently authorized PR-C derives immutable O1 and canonical operand identity from that accepted D2. None is a live runtime capability or E2.

## What Can Be Implemented

An RFC/ADR template with mandatory sections:

- exact operation or field scope;
- neutral owner descriptor/service;
- explicit owner map with field or operation, owner, value source, capability policy, evidence class, migration class, denial reason, and current result;
- operation taxonomy and domain boundary;
- capability grant requirement;
- evidence visibility policy;
- backend execution semantics;
- completion publication route;
- retire publication rule;
- migration/checkpoint/restore class;
- rollback and failure semantics;
- denied adjacent states;
- conformance and static gates;
- documentation claim boundary.

## What Remains Denied/Future-Gated

All positive behavior remains denied until the RFC/ADR is accepted and its negative tests exist. A draft RFC/ADR is not an implementation permit.

## Forbidden Shortcuts

- RFC/ADR that names a VMX field, VMCS block, `VmExitReason`, `TrapDecision`, `VmxCaps`, test artifact, or migration image as the owner.
- RFC/ADR that omits migration/evidence classification.
- RFC/ADR that opens positive behavior without preserving denied adjacent states.
- RFC/ADR that opens completion publication without explicit retire treatment.
- RFC/ADR that treats `AllowedProofOnlyNoExecution` or `AllowedAdmittedDenied` as backend success.
- RFC/ADR that reuses `SecureDomainAdmissionDecision.AllowedSecureOperation`, `SecureBackendOwnerAdmissionDecision.AllowedProofOnlyNoExecution`, SecureCompute descriptor materialization, or privileged read-only projection as proof of virtualization backend execution.
- RFC/ADR that names an operation class but does not freeze the exact leaf/field identifier consumed by code and tests.
- RFC/ADR whose acceptance is inferred from a draft, silence, handoff history, closed readiness gate or repository-local candidate owner name.
- RFC/ADR whose accepted provenance is self-referential, whose attestation does not reproduce the referenced immutable spec digest, or whose CODEOWNERS rule lacks matched review evidence.
- Runtime implementation that consumes a decision manifest as an authorization token instead of requiring a live E2 certificate.
- Runtime implementation that treats compatibility selector/qualification values as canonical operands, re-reads the register file after E2, or treats route booleans/`VmxRetireEffect` as E5/E6.

## Required RFC/ADR

Every positive path requires one. The recommended first RFC/ADR is Phase 06: neutral hypercall backend owner for a minimal VMCALL leaf.

PR-C is implemented and verified in the current worktree: common legality accepts an exact execution-only boundary without mutation privilege, O1 loads only from exact accepted D2, and the canonical E1 seam captures one immutable operand snapshot. No live grant, allowed backend decision, E2 issuer, executor, positive composition, completion or retire was added. Any E2 pool requires separate authorization.

## Code Anchors

- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Services/RuntimeBoundaryAdmissionService.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Events/Hypercalls/HypercallBackendAdmissionPolicy.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Completion/Routing/TrapCompletionRoutePolicy.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Completion/Records/TrapCompletionPublicationFence.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Frontend/Handlers/VmxCompatibilityAdmissionService.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Frontend/Handlers/VmxCompatibilityAdmissionService.Traps.cs`

## Documentation Anchors

- `HybridCPU_ISE/docs/ref2/Old/VirtualiztionRefactoringNew/16_external_audit_activation_readiness_addendum.md`
- `Documentation/Virtualization WhiteBook/04_Authority_Model.md`
- `Documentation/Virtualization WhiteBook/17_Roadmap_And_Residual_Risk.md`
- `Documentation/SecureCompute WhiteBook/SecureCompute HybridCPU-v2 WhiteBook.md`

## Required Tests

- Test that each new positive-path test file references an RFC/ADR identifier.
- Test that no positive test is enabled if the RFC/ADR owner map is incomplete.
- Test that denied adjacent states are asserted in the same PR as the first positive test.

## Required Static/Source Scans

- Scan for new constructors or services that use VMX names as owner names.
- Scan for new `Allowed` decisions in virtualization code that do not include owner/capability/evidence/migration references.
- Scan for positive test names without an RFC/ADR marker.

## Migration/Evidence Classification

The RFC/ADR must assign one of:

- `NoPayload`: no migration/checkpoint state.
- `DescriptorOwned`: neutral descriptor state may be serialized only through neutral migration policy.
- `RecomputedAfterRestore`: value must be recomputed and cannot be stored as authority.
- `HostOwnedNonMigratable`: host evidence or handle; never guest state.
- `Denied`: migration/checkpoint not permitted.

## Completion/Retire Implications

The RFC/ADR must separately answer:

- Can backend execution occur?
- Can completion publication occur?
- Can retire publication occur?
- What fence enforces the separation?
- What happens on backend success but completion/retire denial?
- What is the exact owner map for the chosen path?
- Which existing admission/proof/projection results are explicitly non-consumable as backend execution authority?

## Exit Criteria

- RFC/ADR template is complete.
- Future phases reference it explicitly.
- No positive path can be described as implementable without the template fields.

## Dependency On Previous/Next Phase

Depends on Phase 02. Phases 05, 06, and 11 are owner-specific RFC candidates.
