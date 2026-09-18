# Phase 02 - Non Regression Baseline And Guard Rails

## Goal

Turn the current authority model into non-regression rules. The output is a guard-rail plan that prevents old VMX authority, mutable VMCS state, false backend claims, and compatibility aliases from returning through documentation or code.

## Activation Authority

This phase creates review material only. It does not authorize runtime activation, VMX backend execution, VMCS mutation, SecureCompute backend execution, or production publication behavior.

## Current Code Baseline

- `VmcsFieldProjectionSchema.CanWrite(...)` returns `false`.
- `RuntimeBoundaryAdmissionService` denies compatibility frontend authoritative mutation except the explicit frontend activation/deactivation operation kinds.
- `VmcsReadOnlyValueProjectionService` returns explicit denial decisions for missing owners, privileged execution state, host execution state, host address-space state, compatibility controls, unknown fields, and denied source views.
- `HypercallBackendAdmissionService` denies when no neutral backend owner is materialized.
- `TrapCompletionPublicationFence` separates completion publication from retire publication and denies projection-only paths.

## Already Closed / Must Not Reopen

- Do not restore VMX backend authority.
- Do not add an active VMCS pointer.
- Do not add a VMCS field store.
- Do not make `VmcsManager`, `IVmcsManager`, `VmxExecutionUnit`, or related manager adapters production code.
- Do not treat conformance/test strings as implementation.
- Do not treat evidence, telemetry, tokens, or diagnostics as authority.

## Required Code/Doc Anchors

- `Documentation/Virtualization WhiteBook/16_Current_State_And_Closure_Matrix.md`
- `Documentation/Virtualization WhiteBook/19_Source_References_And_Check_Commands.md`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Services/RuntimeBoundaryAdmissionService.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Generated/VmcsProjection/VmcsFieldProjectionSchema.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Events/Hypercalls/HypercallBackendAdmissionPolicy.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Completion/Records/TrapCompletionPublicationFence.cs`

## Work Items

- Define static scans for forbidden legacy type names and state-store vocabulary.
- Define claim scans for phrases that imply complete backend behavior where only projection/denial exists.
- Define doc-lint rules for SecureCompute non-authority through VMX/VMCS/`VmxCaps`.
- Define stream and L7 claim rules that distinguish current bounded contours from virtualization authority.
- Add a review checklist requiring every future positive claim to name owner, admission, capability, evidence, route, publication, and tests.

## Explicit Non-Goals

- Do not add product source guards in this docs-only phase.
- Do not delete historical documents.
- Do not hide forbidden names when they are used as absent/must-not-return tripwires.
- Do not convert guard-rail documentation into runtime activation.

## Done Criteria

- Forbidden names are documented only as absent, forbidden, or static-check vocabulary.
- VMREAD is described as field-by-field projection, not as complete backend behavior.
- VMCALL is described as admitted-denied without a materialized neutral backend owner.
- SecureCompute is described as neutral runtime descriptor/admission discipline.
- DmaStreamCompute and L7 are kept out of VMX authority and SecureCompute authority.

## Required Tests / Static Checks

- Run the forbidden-name `rg` scan over this folder.
- Run the overclaim `rg` scan over this folder.
- Run the anchor-presence `rg` scan over this folder.
- Run `git diff --check`.

## Residual Risk

Some forbidden names appear intentionally in tripwire prose. Reviewers must read the surrounding line and classify it as absent/must-not-return language.

## External Audit Risk Update

Guard rails must be executable, not only descriptive. Forbidden-name scans, overclaim scans, anchor-presence scans, markdown skeleton checks, and path hygiene checks must be command-ready and review/CI-bound before any activation work order can proceed.

## Next Phase Dependency

Phase 03 depends on these guard rails to consolidate runtime admission without reopening frontend authority.

