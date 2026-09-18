# Phase 15 - Hypercall And Trap Policy

## Goal

Separate secure hypercall/trap recognition from backend success. A secure hypercall can be admitted-denied, proof-only or fail-closed, but it must not publish backend success unless Phase 20 and Phase 21 prerequisites are implemented and tested.

## Current Code Baseline

`SecureHypercallDescriptor` describes allowed hypercall IDs, required grants, argument descriptors, evidence approval, completion fence and retire rule requirements. `SecureIoHypercallAdmissionPolicy` denies raw private pointers, forged opaque handles, missing shared-buffer grants, missing neutral backend owner, missing evidence, missing completion/retire fence and backend success. It can return `AllowedAdmittedDenied` with `BackendExecutionAuthorized: false`, `CompletionPublicationAuthorized: false` and `RetirePublicationAuthorized: false`.

VMX VMCALL paths are compatibility trap projections and remain denied before backend success. VMX trap projection does not authorize SecureCompute runtime execution.

The external audit identifies VMCALL/hypercall backend as the highest execution risk. `TrapDecision`, `VmExitReason.VmCall`, route descriptors and compatibility projection names cannot authorize backend execution. `RuntimeOwnedPublication` must not be used before a real neutral backend owner, validated domain, capability/evidence proof, route authorization, completion fence and retire rule exist.

## Already Closed / Must Not Reopen

- Raw private pointers remain denied.
- Opaque handles require current epoch and provenance.
- Shared-buffer arguments require explicit shared-buffer descriptor and typed grant.
- `AllowedAdmittedDenied` is not backend success.
- VMCALL compatibility trap projection is not secure hypercall backend execution.
- `AllowedAdmittedDenied`, `TrapDecision`, `VmExitReason.VmCall`, VMCALL decode/projection and route/fence class presence are recognition/projection facts only; they do not authorize backend execution, completion publication, retire publication or production activation.
- `CompletionPublicationAuthorized` and `RetirePublicationAuthorized` must remain false on admitted-denied secure hypercall recognition, including when a completion/retire fence object is present.

## Required Code/Doc Anchors

- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Descriptors/Hypercalls/SecureHypercallDescriptor.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Policies/Io/SecureIoHypercallAdmissionPolicy.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Events/Hypercalls/HypercallBackendAdmissionPolicy.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Frontend/Handlers/VmxCompatibilityAdmissionService.Traps.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Completion/Routing/TrapCompletionRoutePolicy.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Completion/Records/TrapCompletionPublicationFence.cs`
- `HybridCPU_ISE/docs/VMXRefactoring/audit5.md`
- `HybridCPU_ISE.Tests/SecureComputeRefactoring/SecureIoHypercallPolicyTests.cs`
- `HybridCPU_ISE.Tests/VmxRefactoring/VmxAdmittedDeniedVmCallTrapPathTests.cs`
- `HybridCPU_ISE.Tests/VmxRefactoring/VmxHypercallBackendAdmissionPolicyTests.cs`

## Work Items

- Define separate state columns for recognition, backend execution authorization, completion publication authorization and retire publication authorization.
- Require proof that `AllowedAdmittedDenied` cannot be read as backend success.
- Require proof that proof-only owner admission cannot publish completion or retire success.
- Keep VMX VMCALL vocabulary as compatibility projection only.
- Require `RuntimeOwnedPublication` to stay blocked until neutral backend owner and publication proof are real.

## Explicit Non-Goals

- No backend execution through admitted-denied secure hypercall.
- No completion publication or retire publication through admitted-denied secure hypercall.
- No VMCALL-first SecureCompute activation path.
- No raw private pointer acceptance.
- No VMX exit reason as runtime authority.
- No `TrapDecision` or route descriptor as backend authorization.
- No VMCALL decode/projection, `TrapDecision`, route descriptor or publication fence as completion/retire publication authority.
- No `RuntimeOwnedPublication` before real neutral backend owner proof and Phase 20/21 completion.

## Done Criteria

- Hypercall policy docs and tests distinguish admitted-denied from backend success.
- Publication booleans are either denied on admitted-denied paths or renamed/split so they cannot imply backend completion.
- VMCALL compatibility remains admitted-denied or fail-closed.
- VMCALL path has explicit negative wording for backend execution, completion route, completion publication and retire publication.

## Required Tests / Static Checks

- `SecureIoHypercallPolicyTests`
- `SecureBackendOwnerRfcGateTests`
- `VmxAdmittedDeniedVmCallTrapPathTests`
- `VmxTrapProjectionPublicationFenceTests`
- `SecureComputePhase10ReleaseGateTests` hypercall/trap recognition wording and source guard over `SecureComputerefactoringNew`, secure hypercall policy, neutral hypercall backend admission, VMCALL trap projection and route/fence sources.
- Source scan for `AllowedAdmittedDenied`, `DeniedBackendSuccessClosed` and `BackendExecutionAuthorized`.

## Residual Risk

The admitted-denied result no longer carries completion or retire publication authorization while backend execution is closed. Backend execution remains closed, and any future route/fence permission still requires Phase 20/21 neutral owner proof and negative conformance.

The audit adds that route/fence classes can look like permission. Treat class existence as infrastructure only; authorization requires runtime admission and neutral owner proof.

## Next Phase Dependency

Phase 16 must harden completion and retire publication before any Phase 20 positive execution RFC can be implemented.
