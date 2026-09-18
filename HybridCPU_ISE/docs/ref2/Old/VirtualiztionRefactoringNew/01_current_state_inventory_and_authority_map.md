# Phase 01 - Current State Inventory And Authority Map

## Goal

Create the baseline inventory for the current virtualization boundary before any future work is planned. The phase freezes the owner map, projection map, denial map, source corpus, and known residual risks.

## Activation Authority

This phase creates review material only. It does not authorize runtime activation, VMX backend execution, VMCS mutation, SecureCompute backend execution, or production publication behavior.

## Current Code Baseline

- VMX compatibility admission is centered in `VmxCompatibilityAdmissionService`.
- Runtime authority admission is centered in `RuntimeBoundaryAdmissionService`.
- VMREAD value projection is centralized in `VmcsReadOnlyValueProjectionService`.
- Generated VMCS projection vocabulary is held in `VmcsFieldProjectionSchema`.
- `ExecutionDomainReadOnlyStateView` exposes only guest PC/SP/flags materialization.
- `MemoryDomainReadOnlyTranslationView` exposes neutral memory translation facts such as address-space root, second-stage root, address-space tag, and target count.
- `HypercallBackendAdmissionService` denies backend execution when no neutral backend descriptor or owner exists.
- `TrapCompletionRouteService` and `TrapCompletionPublicationFence` exist, but current VMX flow uses projection-only denied routing.

## Already Closed / Must Not Reopen

- VMX is frozen compatibility frontend vocabulary.
- Legacy VMX authority is absent and must remain absent.
- `VmxExecutionUnit`, `VmcsManager`, `IVmcsManager`, `VmcsManagerAdapter`, and VMX runtime manager names are forbidden return points.
- Active VMCS pointer and VMCS field store concepts are forbidden state models.
- VMCS/VMCSv2 mutable authority was removed or fenced.
- SecureCompute is not owned by VMX, VMCS, or `VmxCaps`.

## Required Code/Doc Anchors

- `Documentation/Virtualization WhiteBook/16_Current_State_And_Closure_Matrix.md`
- `Documentation/Virtualization WhiteBook/17_Roadmap_And_Residual_Risk.md`
- `HybridCPU_ISE/docs/VMXRefactoring/SuccessClosed/`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Frontend/Handlers/VmxCompatibilityAdmissionService.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Generated/VmcsProjection/VmcsFieldProjectionSchema.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Frontend/Projection/VmcsRead/VmcsReadOnlyValueProjectionService.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Services/RuntimeBoundaryAdmissionService.cs`

## Work Items

- Build a table of every current VMX compatibility surface and classify it as implemented, projection-only, denied, model/helper-only, future-gated, or forbidden.
- Build the owner map from neutral runtime owner to compatibility projection surface.
- Build the VMX name map from compatibility field/opcode to neutral owner or explicit denied reason.
- Capture the old research plan items that are now closed and prevent them from becoming new work.
- Record any conflict between old prompt text and live source corpus as a baseline correction.

## Explicit Non-Goals

- Do not change production code.
- Do not open new VMREAD fields.
- Do not create a hypercall backend owner.
- Do not introduce VMCS writes.
- Do not use inventory work as approval for runtime publication.

## Done Criteria

- The inventory names all current projected VMREAD fields.
- The inventory names all currently denied VMREAD categories.
- The inventory states that `TryReadScalarField` is not the admitted current VMREAD path.
- The inventory states that VMCALL is admitted-denied and has no current backend owner.
- The inventory captures SecureCompute, Stream, Lane6, and L7 boundaries without giving them VMX authority.

## Required Tests / Static Checks

- `rg -n "VmcsReadOnlyValueProjectionService|VmcsFieldProjectionSchema|RuntimeBoundaryAdmissionService" HybridCPU_ISE`
- `rg -n "VmxExecutionUnit|VmcsManager|IVmcsManager" HybridCPU_ISE`
- Existing VmxRefactoring tests that prove projection/denial behavior.
- `git diff --check -- "HybridCPU_ISE/docs/ref2/VirtualiztionRefactoringNew"`

## Residual Risk

Historical text still contains activation-oriented language and stale stream assumptions. This phase must classify such text as historical input, not current source of truth.

## External Audit Risk Update

The inventory is not activation-ready until it is a complete owner matrix. Each future positive path must name neutral owner, admission contract, evidence policy, migration classification, completion/retire rules, current result, denial reason, and test anchor. A list of anchors is not sufficient proof that no hidden authority path exists.

## Next Phase Dependency

Phase 02 depends on this inventory to define guard rails and static tripwires.

