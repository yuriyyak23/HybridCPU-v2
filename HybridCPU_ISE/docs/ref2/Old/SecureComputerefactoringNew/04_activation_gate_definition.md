# Phase 04 - Activation Gate Definition

## Goal

Define what "SecureCompute activation" means and what it explicitly does not mean. Activation requires real neutral runtime execution ownership, semantics, policy, completion and retire publication proof; it cannot be satisfied by docs, shells, proof-only gates, admitted-denied paths or VMX compatibility projection.

## Current Code Baseline

The current baseline is activation-ready in structure but not activated in the positive backend sense. `SecureBackendOwnerAdmissionPolicy` accepts a complete proof chain only as `AllowedProofOnlyNoExecution` and denies `RequestsBackendExecution`. `SecureIoHypercallAdmissionPolicy` can recognize a secure hypercall as `AllowedAdmittedDenied`, with `BackendExecutionAuthorized: false`, `CompletionPublicationAuthorized: false` and `RetirePublicationAuthorized: false`.

## Already Closed / Must Not Reopen

- Proof-only backend owner admission remains proof-only.
- Admitted-denied hypercall admission remains not backend success.
- VMX compatibility projection remains a non-authoritative read-only or denied surface.
- Existing release gates remain baseline guards, not activation proof.

## Required Code/Doc Anchors

- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Policies/Backend/SecureBackendOwnerAdmissionPolicy.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Descriptors/Backend/SecureBackendOwnerDescriptor.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Policies/Io/SecureIoHypercallAdmissionPolicy.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Plan2/SecureCompute RFC HybridCPU-v2.md`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Plan2/14-securecompute-open-decision-backlog.md`
- `HybridCPU_ISE.Tests/SecureComputeRefactoring/SecureBackendOwnerRfcGateTests.cs`

## Work Items

- Define activation as a product/runtime claim requiring all Phase 21 checklist items.
- Require a neutral backend owner with materialized identity and non-compatibility source.
- Require typed execution request/result semantics before any backend execution.
- Require evidence policy, capability/grant policy, completion fence and retire publication rule.
- Require negative conformance proving no VMX/VMCS/VmxCaps authority path.

## Explicit Non-Goals

- No activation by docs closure.
- No activation by shell/no-effect descriptors.
- No activation by proof-only owner evidence.
- No activation by admitted-denied hypercall recognition.
- No activation by VMREAD, VMWRITE, VMCS, VmxCaps or compatibility alias projection.

## Done Criteria

- Activation has a concrete checklist referenced by Phase 21.
- Any missing checklist item blocks activation.
- The maximum current result remains proof-only/admitted-denied where code says so.

## Required Tests / Static Checks

- `SecureBackendOwnerRfcGateTests`
- `SecureIoHypercallPolicyTests`
- `SecureComputePhase10ReleaseGateTests`
- `SecureComputeVmxPhase10ReleaseGateTests`

## Residual Risk

The admitted-denied hypercall publication-bit ambiguity is hardened in code and tests. Activation still remains blocked until Phase 20 and Phase 21 provide real neutral backend execution ownership, typed execution semantics, completion publication and retire publication proof.

## Next Phase Dependency

Phase 05 preserves the no-effect theorem that activation must never break.
