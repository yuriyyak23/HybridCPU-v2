# Phase 06 - Compiler ISA VLIW No-Emission Boundary

## Goal

Preserve the compiler/ISA/VLIW boundary: SecureCompute refactoring must not introduce new instruction encodings, operand formats, register classes, ABI changes, VLIW bundle changes or capability-aware LOAD/STORE/FETCH behavior.

## Current Code Baseline

`SecureComputeNoEmissionContract` reports violations for new instruction encoding, new operand format, capability-aware load/store/fetch and VMX secure-mode emission. `SecureComputeDomainDescriptorNoEffectTests` instantiate this contract and the Phase 10 release gates scan for forbidden claims.

The existing SecureCompute plan quarantines future ISA/memory work in `Plan/13` and `Plan2/14`; current Layer 1/Layer 2 work is runtime descriptor/grant discipline only.

The external audit also notes an active compiler/refactoring worktree outside this SecureCompute plan. SecureCompute phases must not absorb unrelated compiler work or use existing Stream/Lane6/L7 compiler contours as hidden SecureCompute preparation.

## Already Closed / Must Not Reopen

- Existing decoder, encoder, ABI, register file and VLIW carrier formats stay unchanged.
- SecureCompute checks belong to Stage B/runtime admission, not Stage A structural legality.
- Layer 2 grants are not scalar guest-visible capabilities.
- Future ISA work remains separate RFC/ADR work.

## Required Code/Doc Anchors

- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/SecureCompute/Conformance/NoEmission/SecureComputeNoEmissionContract.cs`
- `HybridCPU_ISE.Tests/SecureComputeRefactoring/SecureComputeDomainDescriptorNoEffectTests.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Plan/13-future-capability-aware-isa-memory-layer.md`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Docs/Decoder update изменения и правила.md`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Docs/CHERI-монотонность-и-влияние-на-ISA.md`

## Work Items

- Keep no-emission contract referenced by activation gates.
- Require any future compiler-facing change to prove it is not hidden SecureCompute activation.
- Keep capability-aware ISA, tagged memory and future capability operand work outside this plan.
- Ensure documentation does not imply SecureCompute changes existing LOAD/STORE/FETCH legality.
- Treat Stream/Lane6/L7 bounded execution and compiler helper availability as separate non-SecureCompute evidence unless a future RFC explicitly binds them to neutral runtime authority.

## Explicit Non-Goals

- No new SecureCompute instruction encoding.
- No new operand or register class.
- No ABI or VLIW bundle format change.
- No capability-aware LOAD/STORE/FETCH in current activation phases.
- No VMX secure-mode emission.
- No compiler emission creep through unrelated active refactoring changes.
- No Stream/Lane6/L7 expansion as SecureCompute authority preparation.

## Done Criteria

- Compiler/ISA/VLIW no-emission boundary is referenced from Phase 21.
- Any positive runtime execution RFC in Phase 20 remains non-ISA-first.
- SecureCompute remains a runtime descriptor/admission model.

## Required Tests / Static Checks

- `SecureComputeNoEmissionContract` coverage.
- Source scans for SecureCompute imports in Stage A decoder/projector paths.
- Release-gate doc scans for forbidden compiler/ISA claims.
- `SecureComputePhase10ReleaseGateTests` Stream/Lane6/Lane7 overclaim guard over `SecureComputerefactoringNew`.

## Residual Risk

Future examples or helper APIs may imply compiler support before runtime support exists. Keep examples and facade text aligned with no-emission status.

Existing bounded Stream/Lane6/L7 execution can be overclaimed as a secure backend path. Keep it documented as separate runtime/compiler evidence unless a later RFC supplies neutral SecureCompute ownership, policy and publication semantics.

## Next Phase Dependency

Phase 07 can discuss domain descriptor materialization only after no-emission boundaries are fixed.
