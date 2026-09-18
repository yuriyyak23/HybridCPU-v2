# Phase 14 - IO Lane Boundary

## Goal

Define the secure I/O boundary as explicit shared-buffer-only admission with neutral I/O owner proof, typed grants, direction, owner domain, lifetime epoch and evidence class. Raw private pointers remain denied.

## Current Code Baseline

`SecureIoDomainDescriptor` describes neutral I/O owner state and shared-buffer descriptors. `SecureIoHypercallAdmissionPolicy.AdmitIoDma` uses `SecureMemoryAdmissionPolicy`, checks neutral I/O ownership, shared-buffer binding, typed grants and completion/retire fences. Private DMA is denied by secure memory policy.

The external audit calls out a two-sided Stream/Lane6/L7 risk: some bounded contours are already executable, for example `DmaStreamComputeDescriptorParser.ExecutionEnabled`, but unsupported shapes remain fail-closed and those contours are not SecureCompute, VMX or virtualization authority.

## Already Closed / Must Not Reopen

- Secure I/O owner must be neutral runtime/I/O owner materialization.
- Shared-buffer admission is explicit and policy-defined.
- Raw private memory access and raw private DMA remain denied.
- Shared-buffer descriptors and hypercall `ExplicitSharedBuffer` arguments are descriptor IDs/ranges with current grants and evidence; they are not raw guest pointers, host/device pointer authority, backend execution proof or production activation evidence.
- A materialized shared-buffer descriptor under denied I/O policy remains denied; hypercall shared-buffer arguments require `SecureIoDmaPolicy.ExplicitSharedBuffersOnly`.
- VMX, VMCS, VmxCaps and compatibility aliases cannot act as I/O authority.
- Stream/Lane6/L7 bounded execution contours must not be widened into SecureCompute authority or virtualization backend authority.

## Required Code/Doc Anchors

- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Descriptors/Io/SecureIoDomainDescriptor.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Policies/Io/SecureIoHypercallAdmissionPolicy.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Policies/Memory/SecureMemoryAdmissionPolicy.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Publication/SecureCompletionPublicationFence.cs`
- `HybridCPU_ISE.Tests/SecureComputeRefactoring/SecureIoHypercallPolicyTests.cs`
- `HybridCPU_ISE.Tests/SecureComputeRefactoring/SecureMemoryDomainPolicyTests.cs`
- `HybridCPU_ISE/NonRTL/Core/Execution/DmaStreamCompute/DmaStreamComputeDescriptorParser.cs`

## Work Items

- Keep I/O admission tied to explicit shared-buffer descriptors.
- Require typed grants with current epoch for shared-buffer arguments.
- Separate I/O admission from hypercall backend execution.
- Treat completion and retire fences as publication prerequisites, not proof of execution.
- Classify Stream/Lane6/L7 as external bounded runtime/compiler contours unless a later SecureCompute RFC gives them a neutral owner and policy chain.

## Explicit Non-Goals

- No raw private pointer admission.
- No materialized shared-buffer ID as admission when I/O policy is denied.
- No hypercall shared-buffer argument as raw pointer, host pointer or device pointer authority.
- No device-side-effect success without neutral backend owner and execution semantics.
- No VMX/VMCALL path as secure I/O authority.
- No compatibility projection as typed grant.
- No Stream/Lane6/L7 contour as SecureCompute authority by implication.

## Done Criteria

- Secure I/O policy denies missing neutral owner, missing buffer binding, private memory access and missing typed grant.
- Secure I/O allowed policy remains narrow and explicit.
- Publication booleans remain blocked from implying backend success.

## Required Tests / Static Checks

- `SecureIoHypercallPolicyTests`
- `SecureMemoryDomainPolicyTests`
- `SecureAuthorityDisciplineTests`
- VMX source scans for forbidden authority tokens in secure I/O policy files.
- `SecureComputePhase10ReleaseGateTests` shared-buffer/raw-pointer wording and secure I/O source guard over `SecureComputerefactoringNew`, secure I/O descriptors/policies and memory admission.
- `SecureComputePhase10ReleaseGateTests` guard proving Stream/Lane6/Lane7 bounded execution is not SecureCompute, VMX or virtualization authority.

## Residual Risk

I/O admission and hypercall admission share policy code. Phase 15 must make hypercall recognition, backend execution, completion and retire states even more explicit.

The Stream/Lane6/L7 risk is bidirectional: saying everything is fail-closed is stale, while treating bounded execution as SecureCompute authority is unsafe.

## Next Phase Dependency

Phase 15 defines secure hypercall and trap policy semantics.
