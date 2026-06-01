# HybridCPU ISE Virtualization WhiteBook

Status date: 2026-05-31, after VMX refactoring closure 255.

This WhiteBook is the split architecture record for the current HybridCPU ISE virtualization layer. It is written against the live `CloseToRTL/Core/Runtime` and `CloseToRTL/Core/Virtualization` code contours plus the VMX refactoring audit corpus.

The central rule is simple: virtualization authority is neutral runtime authority. VMX is a frozen compatibility frontend. VMX vocabulary can describe compatibility ABI, generated projection, denied aliases, and VMX-facing completion vocabulary, but it must not own execution domains, trap policy, completion publication, memory authority, capability grants, migration payloads, secure-compute authority, or host evidence.

## Reading Order

1. `01_Executive_Summary.md`
2. `02_Principles_And_Non_Goals.md`
3. `03_Current_Implementation_Map.md`
4. `04_Authority_Model.md`
5. `05_Runtime_Domain_Owners.md`
6. `06_Capabilities_And_Evidence.md`
7. `07_Memory_IO_Lanes.md`
8. `08_Nested_Virtualization.md`
9. `09_VMX_Compatibility_Frontend.md`
10. `10_VMCS_Projection_And_Field_Access.md`
11. `11_Admission_Boundaries.md`
12. `12_Trap_Intercept_Completion_Retire.md`
13. `13_Compiler_ISA_Runtime_Contract.md`
14. `14_Conformance_Golden_Artifacts_NoEmission.md`
15. `15_Security_Invariants.md`
16. `16_Current_State_And_Closure_Matrix.md`
17. `17_Roadmap_And_Residual_Risk.md`
18. `18_Glossary.md`
19. `19_Source_References_And_Check_Commands.md`

## Source Corpus

This pack is grounded in:

- `HybridCPU_ISE/docs/VMXRefactoring/2026-05-24-vmx-current-model-completion-audit.md`
- `HybridCPU_ISE/docs/VMXRefactoring/audit3.md`
- `HybridCPU_ISE/docs/VMXRefactoring/audit4.md`
- `HybridCPU_ISE/docs/VMXRefactoring/audit5.md`
- `HybridCPU_ISE/docs/VMXRefactoring/SuccessClosed/**`
- `HybridCPU_ISE/docs/VMXRefactoring/deep-research-report (6).md`
- `HybridCPU_ISE/docs/VMXRefactoring/ОСНОВЫ и ПРАВИЛА VMX.md`
- `HybridCPU_ISE/docs/VMXRefactoring/Оценка рефакторинга VMX security-centric.md`
- `HybridCPU_ISE/CloseToRTL/Core/Runtime/**`
- `HybridCPU_ISE/CloseToRTL/Core/Virtualization/**`
- `HybridCPU_ISE.Tests/VmxRefactoring/**`
- `HybridCPU_ISE.Tests/SecureComputeRefactoring/**`

## Current Position

- VMX compatibility frontend freeze is declared.
- VMX is not the virtualization architecture.
- The neutral runtime owns domains, capabilities, evidence, memory, I/O, lanes, nested composition, trap policy, completion routing, retire publication, and secure-compute policy.
- VMCS/VMCSv2 is generated/read-only/denied projection vocabulary, not a state store.
- `VmExitReason`, `ExitQualification`, and `TrapDecision` are VMX-facing projection vocabulary only.
- VMREAD has partial generated/read-only value projection after runtime admission, but only for fields with explicit neutral owner/value sources.
- VMCALL has neutral trap, backend-admission, route, completion, and retire fences, but production backend execution remains denied.

## What This Pack Is Not

This is not a promise of successful VMX backend execution. It does not reintroduce `VmxExecutionUnit`, `VmcsManager`, `IVmcsManager`, active VMCS pointer state, a VMCS field store, or legacy VMX runtime authority.

The WhiteBook intentionally separates implemented behavior, compatibility vocabulary, conformance fences, and future heavy steps. Future architecture is labelled as future work, not current production behavior.
