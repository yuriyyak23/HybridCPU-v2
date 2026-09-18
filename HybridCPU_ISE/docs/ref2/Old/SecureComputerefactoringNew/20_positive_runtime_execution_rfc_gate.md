# Phase 20 - Positive Runtime Execution RFC Gate

## Goal

Define the gated path for future positive secure backend runtime execution without opening it in this plan. This phase is an RFC/ADR gate: execution remains closed until a real neutral backend owner, typed request/result model, capability/evidence policy, completion fence and retire publication rule exist in code and tests.

## Current Code Baseline

`SecureBackendOwnerDescriptor` can represent neutral runtime service, neutral device model or neutral migration service owners, but `SecureBackendOwnerAdmissionPolicy` accepts proof only as `AllowedProofOnlyNoExecution`. It denies non-neutral sources, missing RFC/ADR approval, missing proof chain, stale epoch, missing negative tests and any request for backend execution.

`SecureIoHypercallAdmissionPolicy` returns `DeniedBackendSuccessClosed` when a policy asks for backend execution. No `SecureBackendExecutionRequest` or `SecureBackendExecutionDecision` production class exists in the current baseline; those concepts are RFC input only.

The external audit recommends that the first future work order should not be hypercall backend execution. A safer first RFC/ADR candidate is a neutral privileged execution-state owner for `GuestCr0` and `GuestCr4`, because it is narrower, testable and currently denied for a concrete reason.

## Already Closed / Must Not Reopen

- Proof-only owner gate is not execution.
- `SecureBackendOwnerRfcGate` and `AllowedProofOnlyNoExecution` are proof-only evidence surfaces, not secure backend execution.
- Approved RFC/ADR state, `ProofChainAccepted`, `SecureBackendOwnerDescriptor` and Phase 20 subphase labels (`20A`-`20E`) are not typed execution request/result implementation, backend execution authorization, completion publication, retire publication or production activation evidence.
- Proof-only/no-effect evidence cannot publish completion, retire effects or backend success.
- Admitted-denied hypercall is not backend success.
- Compatibility projection cannot satisfy backend owner proof.
- VMX, VMCS and VmxCaps are denied as backend owner sources.
- `SecureBackendExecutionRequest`, `SecureBackendExecutionDecision`, `AllowedInternalExecutionNoPublication`, `AllowedCompletionRecordNoPublication`, `SecureCompletionRecord` and positive retire publication remain future vocabulary only unless introduced by a separate owner/evidence/test implementation chain.
- Phase 20 does not open nested execution; nested SecureCompute backend execution still requires a separate nested owner, evidence, monotonicity, checkpoint and publication test chain.
- Current positive secure backend runtime execution remains future work.

## Required Code/Doc Anchors

- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Descriptors/Backend/SecureBackendOwnerDescriptor.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Policies/Backend/SecureBackendOwnerAdmissionPolicy.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Policies/Io/SecureIoHypercallAdmissionPolicy.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Plan2/SecureCompute RFC HybridCPU-v2.md`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Plan2/14-securecompute-open-decision-backlog.md`
- `HybridCPU_ISE.Tests/SecureComputeRefactoring/SecureBackendOwnerRfcGateTests.cs`

## Work Items

- Split future work into subphases:
  - `20A`: approved RFC/ADR and naming.
  - `20B`: neutral backend owner implementation.
  - `20C`: typed execution request/result model.
  - `20D`: first minimal no-side-effect internal positive path.
  - `20E`: completion fence and retire publication path.
- Require negative tests for VMX/VMCS/VmxCaps owner denial, missing grant, stale epoch, raw private pointer, host evidence publication and missing publication fences.
- Prefer first positive path as minimal neutral runtime internal operation, not VMCALL-first.
- Keep proof-only closure as the maximum current executable state; `20A`/`20B` wording cannot be used as `20C`/`20D`/`20E` implementation evidence.
- Prefer `GuestCr0`/`GuestCr4` neutral privileged execution-state owner RFC before any VMCALL backend RFC:
  - owner placement;
  - CR0/CR4 bit semantics and reserved-bit legality;
  - visibility and evidence class;
  - migration classification;
  - missing/partial/stale owner denial;
  - negative conformance against VMCS scalar fallback, host alias leakage, compatibility-control inference and VMX authority.

## Explicit Non-Goals

- No positive secure backend runtime execution in this docs-only plan.
- No backend success through proof-only or admitted-denied result.
- No backend-owner wording as production activation claim.
- No completion publication or retire publication through `AllowedProofOnlyNoExecution`.
- No VMX-first positive path.
- No hypercall-backend-first work order.
- No CR0/CR4 opening without separate neutral privileged execution-state owner RFC.
- No publication path without typed result, completion fence and retire rule.
- No `20A`/`20B` RFC or owner proof as typed execution model, no-side-effect internal execution, completion record, retire publication or activation evidence.
- No nested execution under Phase 20; nested remains behind its own explicit owner/evidence/test chain.

## Done Criteria

- Future execution prerequisites are explicit and blocking.
- `AllowedProofOnlyNoExecution` remains maximum current owner-gate result.
- `DeniedBackendSuccessClosed` remains current hypercall backend result.
- RFC/ADR approval, `ProofChainAccepted`, owner descriptor materialization and Phase 20 subphase labels cannot be asserted as typed execution request/result, backend execution, completion record, retire publication, nested execution or production activation.
- Phase 21 blocks activation until Phase 20 subphases are implemented and tested.

## Required Tests / Static Checks

- `SecureBackendOwnerRfcGateTests`
- `SecureIoHypercallPolicyTests`
- `SecureComputePhase10ReleaseGateTests` backend-owner proof wording/source guard over `SecureComputerefactoringNew`, backend owner policy and hypercall admission policy.
- `SecureBackendOwnerRfcGateTests` reflection/source guard proving owner-gate result exposes no completion/retire publication or mutable nested state authority and no typed execution request/result classes exist in current backend-owner/hypercall sources.
- Future tests for typed execution request/result model.
- Future source guard proving no compatibility source can authorize execution.

## Residual Risk

The RFC can be mistaken for approval to implement. Keep RFC/ADR approval, code owner implementation, tests and release-gate wording separate.

The safest next RFC is not the same thing as an implementation task. Even the `GuestCr0`/`GuestCr4` owner RFC must keep values denied until owner semantics, visibility, migration and tests exist.

## Next Phase Dependency

Phase 21 turns the RFC gate and all previous phases into an activation checklist.
