# Phase 16 - Completion And Retire Publication

## Goal

Make backend result, completion publication and retire publication three distinct states. Completion publication requires an explicit completion fence; retire publication requires an explicit retire rule. Proof-only and admitted-denied paths must not publish success.

## Current Code Baseline

`SecureCompletionPublicationFence` has `SecureCompletionFenceState` and `SecureRetirePublicationRule`. `CanPublishCompletion` and `CanPublishRetire` are distinct. `SecureIoHypercallAdmissionPolicy` checks completion and retire fences, and admitted-denied hypercall recognition now clears completion and retire publication booleans even when a fence is present.

The VMX trap publication work already separates neutral trap result, completion route, VMX projection and retire publication. That discipline must be applied to SecureCompute activation language.

The external audit sharpens this rule: the presence of `TrapCompletionRouteService`, `TrapCompletionRouteDescriptor.RuntimeOwnedPublication` or `TrapCompletionPublicationFence` does not mean publication is permitted. Route/fence classes are infrastructure; permission is a decision produced only after runtime admission, neutral trap/backend authorization, domain validation, completion publication approval and retire publication approval.

Activation-safe publication matrix:

| State / Surface | Backend execution | Completion publication | Retire publication | Activation consequence |
|---|---:|---:|---:|---|
| Proof-only owner accepted | no | no | no | proof evidence only |
| Admitted-denied hypercall recognized | no | no | no | recognition only |
| VMCALL decode/projection or `TrapDecision` | no | no | no | compatibility projection only |
| `RuntimeOwnedPublication` descriptor present without backend execution | no | no | no | infrastructure only |
| Internal backend result before completion fence | internal only | no | no | not guest-visible success |
| Completion fence only | requires backend execution | yes | no | completion is not retire |
| Explicit retire fence after completion | requires backend execution | yes | yes | still not production activation by itself |

## Already Closed / Must Not Reopen

- Compatibility projection is post-publication only.
- Completion record is not retire publication.
- Retire publication is not evidence visibility.
- Proof-only and admitted-denied decisions are not backend success.
- VMX exit vocabulary is not completion authority.
- Completion publication, retire publication and production activation are not interchangeable states.
- `RuntimeOwnedPublication` and fence objects are infrastructure until all neutral owner, backend execution, route authorization, completion and retire gates pass.

## Required Code/Doc Anchors

- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Publication/SecureCompletionPublicationFence.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Policies/Io/SecureIoHypercallAdmissionPolicy.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Completion/`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Events/Hypercalls/`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Completion/Routing/TrapCompletionRoutePolicy.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Completion/Records/TrapCompletionPublicationFence.cs`
- `HybridCPU_ISE/docs/VMXRefactoring/audit4.md`
- `HybridCPU_ISE/docs/VMXRefactoring/audit5.md`
- `HybridCPU_ISE.Tests/SecureComputeRefactoring/SecureEvidencePublicationPolicyTests.cs`
- `HybridCPU_ISE.Tests/VmxRefactoring/VmxTrapProjectionPublicationFenceTests.cs`

## Work Items

- Add an activation-safe publication matrix:
  - proof-only owner accepted: no backend execution, no completion publication, no retire publication;
  - admitted-denied hypercall recognized: no backend execution, no success publication;
  - internal backend result: internal only until completion fence;
  - completion publication: visible only after completion fence;
  - retire publication: visible only after retire rule and retire ownership.
- Require result type names to avoid success ambiguity.
- Route any positive backend path through Phase 20 typed request/result semantics.
- Add a route/fence infrastructure-not-permission guard to the release checklist.

## Explicit Non-Goals

- No completion publication based only on proof-only admission.
- No retire publication based only on admitted-denied recognition.
- No compatibility projection as publication owner.
- No VMCS or VMX exit field as completion storage.
- No route descriptor or publication fence class as permission by existence.
- No completion fence as retire publication authority.
- No retire fence, completion record or route authorization as production activation evidence by itself.

## Done Criteria

- Completion and retire publication are separately named in docs/tests.
- Proof-only/admitted-denied paths cannot publish success.
- Phase 21 checklist blocks activation if publication semantics are ambiguous.
- Route authorization, completion publication and retire publication must each have independent allow/deny evidence.

## Required Tests / Static Checks

- `SecureEvidencePublicationPolicyTests`
- `SecureIoHypercallPolicyTests`
- `SecureBackendOwnerRfcGateTests`
- `SecureComputePhase10ReleaseGateTests` route/fence infrastructure-not-permission wording guard over `SecureComputerefactoringNew`.
- `SecureComputePhase10ReleaseGateTests` publication-matrix wording guard over `SecureComputerefactoringNew`.
- `VmxTrapProjectionPublicationFenceTests`
- Focused tests: admitted-denied/proof-only paths cannot publish completion or retire success.

## Residual Risk

Publication naming remains activation-sensitive. The admitted-denied booleans are hardened to false, and any future positive path must still prove independent backend execution, completion publication and retire publication decisions.

The audit adds a related residual risk: developers may see route/fence production classes and assume permission. Release gates should scan for wording that equates class presence with publication authorization.

## Next Phase Dependency

Phase 17 must ensure checkpoint/restore never serializes publication or host evidence as authority.
