# SuccessClosed 236: neutral trap result split

Date: 2026-05-28

## Slice

Closed the pre-VMCALL trap hardening slice:

- runtime trap policy/timer now produce neutral `NeutralTrapResult`;
- VMX `TrapDecision`, `VmExitReason`, and exit qualification are projected only by `VmxTrapProjectionMapper`;
- no VMCALL backend, successful intercept execution, VMCS manager, VMCS field store, or active VMCS pointer was introduced.

This is a boundary split, not admitted VMX trap execution.

## Inventory and classification

Found trap/intercept/VMCALL dependencies:

- `TrapRequest`: neutral runtime request identity, now under `Core/Runtime/Events/Traps`.
- `NeutralTrapResult`: new neutral runtime trap outcome.
- `TrapPolicyBitmap`: neutral runtime trap policy owner; returns neutral results.
- `SchedulingBudgetTimer`: neutral runtime timer owner; returns neutral preemption-timer trap results.
- `DomainTrapRecord`: neutral runtime trap publication record; unchanged in this slice.
- `TrapPolicyService`: VMX-compatible projection boundary service; consumes neutral results and returns projection decisions.
- `NestedInterceptTranslator`: compatibility projection caller; now maps neutral results only at projection boundary.
- `VmExitReason`, `VmxExitQualification`, `TrapDecision`: VMX compatibility projection vocabulary only.
- `VMCALL` / `VmxOperationKind.VmCall`: frozen compatibility opcode vocabulary; no backend execution path admitted here.
- `VmxRetireEffect.InterceptExit`, `VmxRetireEffect.VmCall`, `VmxRetireEffect.VmFunc`: success-shaped retire model factories retained as vocabulary/model surface, not used by production VMX callers as authority.
- `RuntimeBoundaryAdmissionService`: still used only by the narrow admitted-denied VMREAD projection path; no VMCALL/trap admission was added in this closure.
- `CompletionRecord` / `CompletionProjectionService`: neutral completion and VMX-compatible completion projection surfaces; not changed.

No forbidden-authority leakage remains in the neutral trap runtime slice.

## Neutral runtime trap result

Added neutral runtime vocabulary:

- `Core/Runtime/Events/Traps/TrapRequest.cs`
- `Core/Runtime/Events/Traps/NeutralTrapResult.cs`

Updated neutral runtime owners:

- `TrapPolicyBitmap.Evaluate(...)` returns `NeutralTrapResult`.
- `SchedulingBudgetTimer.TryConsumeExpired(...)` returns `NeutralTrapResult`.
- `TrapPolicyClass.VmxOperation` was replaced by neutral `TrapPolicyClass.CompatibilityOperation`.
- runtime trap source has no dependency on `VmExitReason`, `VmxExitQualification`, `TrapDecision`, or `VmxOperationKind`.

## VMX-compatible projection

VMX exit projection is now explicit and late:

- `VmxTrapProjectionMapper.Project(...)` maps `NeutralTrapResult` to `TrapDecision`.
- `VmxTrapProjectionMapper.ProjectReason(...)` maps `NeutralTrapResultKind` to `VmExitReason`.
- `TrapRequest.ForVmxOperation(...)`, `TrapPolicyBitmap.EnableVmxOperation(...)`, and `InterceptsVmxOperation(...)` remain compatibility aliases in the projection mapper file.

`VmExitReason` is no longer a runtime trap authority because the runtime policy and timer select only `NeutralTrapResultKind`. The VMX reason is chosen only after neutral evaluation succeeds or denies at the compatibility frontend boundary.

## Denied / fail-closed state

Left denied or fail-closed:

- VMCALL backend execution.
- VMX intercept/trap backend success.
- VMCS scalar read materialization beyond the current admitted-denied VMREAD path.
- VMCS field store and active pointer state.
- VMX runtime manager / VMCS manager adapters.

Production VMX dispatch/retire callers still use fail-closed effects and do not call `VmxRetireEffect.InterceptExit`, `VmxRetireEffect.VmCall`, or `VmxRetireEffect.VmFunc`.

## Files changed

Production:

- `CloseToHSL/Core/Runtime/Events/Traps/TrapRequest.cs`
- `CloseToHSL/Core/Runtime/Events/Traps/NeutralTrapResult.cs`
- `CloseToHSL/Core/Runtime/Events/Traps/TrapPolicyBitmap.cs`
- `CloseToHSL/Core/Runtime/Events/Traps/SchedulingBudgetTimer.cs`
- `CloseToHSL/Core/Virtualization/Compatibility/Frontend/Projection/Events/TrapDecision.cs`
- `CloseToHSL/Core/Virtualization/Compatibility/Frontend/Projection/Events/TrapPolicyService.cs`
- `CloseToHSL/Core/Virtualization/Compatibility/Frontend/Projection/Events/VmxTrapProjectionMapper.cs`
- `CloseToHSL/Core/Virtualization/Compatibility/Frontend/Projection/Nested/NestedInterceptTranslator.cs`
- `CloseToHSL/Core/Virtualization/Compatibility/Frontend/Projection/Nested/NestedInterceptTranslator.Translate.partial.cs`
- `CloseToHSL/Core/Virtualization/Conformance/AuthorityBoundary/VmxCompletionTrapNestedRetireQuarantineContract.cs`
- `CloseToHSL/Core/Virtualization/Conformance/MemoryTranslation/EventTrapDomainIdentityAuthorityRemovalContract.cs`

Tests:

- `HybridCPU_ISE.Tests/VmxRefactoring/ActiveVmxCompatibilityConformanceTests.cs`
- `HybridCPU_ISE.Tests/VmxRefactoring/VmxNeutralTrapResultSplitTests.cs`
- `HybridCPU_ISE.Tests/VmxRefactoring/VmxCompatibilityProjectionInventoryTests.cs`
- `HybridCPU_ISE.Tests/VmxRefactoring/VmxRetainedSurfaceAndFreezeEvidenceContracts.cs`
- `HybridCPU_ISE.Tests/VmxRefactoring/VmxCapabilitySubstrateAndAliasEvidenceContracts.cs`

Docs:

- `docs/VMXRefactoring/audit3.md`
- `docs/VMXRefactoring/2026-05-24-vmx-current-model-completion-audit.md`
- `docs/VMXRefactoring/ОСНОВЫ и ПРАВИЛА VMX.md`
- `docs/VMXRefactoring/SuccessClosed/2026-05-28-236-neutral-trap-result-split.md`

## Static state

Baseline and post-change static evidence:

- `Core/VMX` path absent; Core/VMX legacy-marked C# count: `0`.
- `Legacy/VMX` path absent; Legacy/VMX C# count: `0`.
- `VmxExecutionUnit.cs`, `VmcsManager.cs`, `IVmcsManager.cs` file count: `0`.
- `TestAssemblerConsoleApps` stale `vmxUnit` / `core.VmxUnit` / `core.Vmcs` marker count: `0`.
- neutral runtime trap VMX vocabulary marker count: `0`.
- `Core/Runtime` `VmExitReason` marker count: `0`.
- production VMX retire success-factory authority marker count: `0`.
- VMX event projection `VmExitReason` mapping markers are present only in `TrapDecision.cs` and `VmxTrapProjectionMapper.cs`.

The broad forbidden-manager marker command over `CloseToHSL/Core/Virtualization` reports conformance deny-list string literals. Re-running the implementation scan with `!**/Conformance/**` reports:

- forbidden VMX manager/backend implementation marker count excluding Conformance: `0`.

## Build and test results

Baseline before changes:

- `dotnet build HybridCPU_ISE.csproj --no-restore`: passed.
- `dotnet build HybridCPU_ISE.Tests.csproj --no-restore`: passed.
- `dotnet build TestAssemblerConsoleApps.csproj --no-restore`: passed.

Final checks:

- `dotnet build HybridCPU_ISE.csproj --no-restore`: passed, `0` warnings.
- `dotnet build HybridCPU_ISE.Tests.csproj --no-restore`: passed, `36` existing warnings.
- `dotnet build TestAssemblerConsoleApps.csproj --no-restore`: passed, `2` existing obsolete-overload warnings.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmxProjectionSchemaAndQuarantineTests"`: passed, `1/1`.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmxCompatibilityProjectionInventoryTests"`: passed, `1/1`.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~RuntimeBoundaryAdmissionTests"`: passed, `4/4`.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmxFirstAdmittedCompatibilityPathTests"`: passed, `1/1`.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmxNeutralTrapResultSplitTests"`: passed, `4/4`.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~Vmx&FullyQualifiedName!~NonVmx"`: passed, `205/205`.

## Known unrelated debt

- Raw `FullyQualifiedName~Vmx` can still match `NonVmx` because the substring `Vmx` appears inside `NonVmx`; the broad VMX check was run with `FullyQualifiedName!~NonVmx`.
- Existing nullability/xUnit warnings in the tests project remain outside this slice.
- Existing obsolete constructor warnings in `TestAssemblerConsoleApps` remain outside this slice.

## Residual risk

- `VmxRetireModel` still exposes success-shaped VMX retire factories as compatibility/model vocabulary. Current conformance proves production VMX callers do not use them as runtime authority.
- `TrapPolicyService` still returns `TrapPolicyEvaluationResult` containing VMX `TrapDecision` because it is a VMX-compatible projection boundary service; this is acceptable only while neutral trap authority remains outside it.
- A future VMCALL/trap/intercept path could regress if it bypasses `RuntimeBoundaryAdmissionService` or projects `VmExitReason` before neutral `NeutralTrapResult` exists.

## Next heavy step

Design the admitted-denied VMCALL/trap/intercept compatibility path:

1. decode frozen compatibility opcode;
2. project frozen alias/request vocabulary;
3. admit through `RuntimeBoundaryAdmissionService`;
4. evaluate neutral runtime trap authority as `NeutralTrapResult`;
5. project to `TrapDecision` / `VmExitReason` only through `VmxTrapProjectionMapper`;
6. remain denied/fail-closed until a neutral owner supplies real behavior.

Do not create VMCS managers, active pointer state, field stores, or successful VMX backend execution.
