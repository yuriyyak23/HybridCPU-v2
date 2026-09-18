# Phase 02 - Architecture Invariants And Closure Taxonomy

## Goal

Freeze the architecture rules that every later phase must obey. SecureCompute is a neutral runtime descriptor/admission discipline. It is not VMX-owned state, not a VMCS state store, not VmxCaps authority, not a CHERI-like ISA layer and not a tagged-memory model.

## Current Code Baseline

The baseline already supports this split:

- `SecureComputeDomainDescriptor` is a neutral root descriptor under `Runtime/Domains/SecureCompute`.
- `RuntimeBoundaryAdmissionService` in `Runtime/Services` owns Stage B runtime admission.
- `SecureComputeNoEmissionContract` denies new instruction encodings, new operand formats, capability-aware load/store/fetch behavior and VMX secure-mode emission.
- `SecureGrantAuthorityPolicy` validates runtime descriptor/grant provenance, bounds and epochs instead of accepting guest scalar authority.
- VMX compatibility policies deny activation, state mutation and backend success through compatibility surfaces.

## Already Closed / Must Not Reopen

- Stage A structural legality must not start requiring SecureCompute policy.
- Ordinary non-secure operations must not be over-denied by absent, disabled or no-effect SecureCompute state.
- VMX/VMCS/VmxCaps must remain compatibility/projection vocabulary only.
- Test, telemetry, conformance and evidence artifacts must remain proof surfaces, not runtime authority.

## Required Code/Doc Anchors

- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/SecureCompute/Conformance/NoEmission/SecureComputeNoEmissionContract.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Authority/SecureGrantAuthorityPolicy.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Plan/00-securecompute-refactoring-index.md`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Plan/13-future-capability-aware-isa-memory-layer.md`
- `Documentation/Virtualization WhiteBook/15_Security_Invariants.md`
- `HybridCPU_ISE/docs/VMXRefactoring/ОСНОВЫ и ПРАВИЛА VMX.md`

## Work Items

- Maintain a closure vocabulary shared by all phase files.
- Require each phase to state whether it is implemented, shell, no-effect, fail-closed, proof-only, admitted-denied, design-fence, release-gate or future.
- Keep compatibility projection distinct from authority, materialization, migration, checkpoint and publication.
- Require future ISA or memory-tag work to remain outside current Layer 1/Layer 2 activation.

## Explicit Non-Goals

- No VMX mode for SecureCompute.
- No VMCS-backed SecureCompute state.
- No VmxCaps grant or activation source.
- No decoder, encoder, ABI, register or VLIW bundle format changes.
- No LOAD/STORE/FETCH semantic changes for secure grants.

## Done Criteria

- Every later phase references this taxonomy.
- All closed baseline claims identify their closure class.
- Positive secure backend runtime execution appears only as future/RFC/ADR work until Phase 20 criteria are satisfied.

## Required Tests / Static Checks

- Source guards in `SecureComputePhase10ReleaseGateTests.cs`.
- VMX source guards in `SecureComputeVmxPhase10ReleaseGateTests.cs`.
- No-emission checks through `SecureComputeNoEmissionContract`.

## Residual Risk

Terms such as "admitted", "allowed", "projection" and "proof" can drift into authority language. Phase 15 and Phase 16 must keep those states separate from backend success.

## Next Phase Dependency

Phase 03 can inventory the existing baseline only after the closure classes are fixed.
