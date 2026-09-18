# SuccessClosed 237: admitted-denied VMCALL trap projection

Date: 2026-05-28

## Slice

Closed the admitted-denied VMCALL/trap/intercept projection slice:

- `VMCALL` can pass frozen decode and generated frozen alias projection;
- runtime admission goes through `RuntimeBoundaryAdmissionService`;
- runtime authority is neutral `DomainRuntimeOperationKind.ProjectCompatibilityTrap` plus neutral trap policy/bitmap;
- trap result is neutral `NeutralTrapResult`;
- VMX `TrapDecision` / `VmExitReason` projection is produced only by `VmxTrapProjectionMapper`;
- backend execution remains denied.

This is not a successful VMCALL backend and not a VMX intercept execution path.

## Path

Implemented path:

```text
VMCALL decode
-> CompatAliasMap Opcode/VMCALL -> VmxTrapProjectionMapper.Project
-> RuntimeBoundaryAdmissionService
-> DomainRuntimeOperationKind.ProjectCompatibilityTrap
-> TrapPolicyDescriptor + TrapPolicyBitmap
-> NeutralTrapResult
-> VmxTrapProjectionMapper
-> VmxCompatibilityTrapAdmissionDecision.TrapProjectionDeniedBackend
```

## Runtime authority

Neutral authority added or used:

- `DomainRuntimeOperationKind.ProjectCompatibilityTrap`
- `TrapPolicyDescriptor`
- `TrapPolicyBitmap`
- `TrapRequest.ForVmxOperation(...)` as compatibility alias over neutral `ForCompatibilityOperation(...)`
- `NeutralTrapResult`

The admitted path requires a runtime-authoritative trap policy descriptor and a neutral compatibility-operation intercept. If the neutral policy does not intercept VMCALL, the path denies after runtime admission with `TrapPolicyDenied`.

## VMX projection vocabulary

VMX vocabulary remains projection-only:

- `CompatAliasMap` now includes frozen `Opcode/VMCALL -> VmxTrapProjectionMapper.Project`.
- `VmxTrapProjectionMapper` maps VMCALL compatibility-operation intercepts to projected `VmExitReason.VmCall`.
- `TrapDecision` is returned only as projected compatibility vocabulary.

The admission service does not branch on `VmExitReason` and does not use `VmExitReason` as runtime authority.

## Denied / fail-closed state

Still denied or fail-closed:

- VMCALL backend execution.
- VMX intercept backend success.
- `VmxRetireEffect.InterceptExit`.
- `VmxRetireEffect.VmCall`.
- VMCS managers, active pointer state, field stores, runtime manager adapters.
- Production VMX dispatch/retire, which still retires typed fail-closed effects.

## Files changed

Production:

- `CloseToHSL/Core/Runtime/Domains/Services/DomainRuntimeOperation.cs`
- `CloseToHSL/Core/Virtualization/Compatibility/Generated/AliasMaps/CompatAliasMap.cs`
- `CloseToHSL/Core/Virtualization/Compatibility/Frontend/Handlers/VmxCompatibilityAdmissionService.Traps.cs`
- `CloseToHSL/Core/Virtualization/Compatibility/Frontend/Projection/Events/VmxTrapProjectionMapper.cs`
- `docs/VMXRefactoring/schemas/compat-alias-schema.v1.json`

Tests:

- `HybridCPU_ISE.Tests/VmxRefactoring/VmxAdmittedDeniedVmCallTrapPathTests.cs`
- `HybridCPU_ISE.Tests/VmxRefactoring/VmxNeutralTrapResultSplitTests.cs`

Docs:

- `docs/VMXRefactoring/audit3.md`
- `docs/VMXRefactoring/2026-05-24-vmx-current-model-completion-audit.md`
- `docs/VMXRefactoring/ОСНОВЫ и ПРАВИЛА VMX.md`
- `docs/VMXRefactoring/SuccessClosed/2026-05-28-237-admitted-denied-vmcall-trap-projection.md`

## Build and test results

- `dotnet build HybridCPU_ISE.csproj --no-restore`: passed, `0` warnings.
- `dotnet build HybridCPU_ISE.Tests.csproj --no-restore`: passed, `0` warnings.
- `dotnet build TestAssemblerConsoleApps.csproj --no-restore`: passed, `2` existing obsolete-overload warnings.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmxAdmittedDeniedVmCallTrapPathTests"`: passed, `5/5`.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmxNeutralTrapResultSplitTests"`: passed, `4/4`.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmxProjectionSchemaAndQuarantineTests"`: passed, `1/1`.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmxCompatibilityProjectionInventoryTests"`: passed, `1/1`.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~RuntimeBoundaryAdmissionTests"`: passed, `4/4`.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmxFirstAdmittedCompatibilityPathTests"`: passed, `1/1`.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~Vmx&FullyQualifiedName!~NonVmx"`: passed, `210/210`.

The VMX projection lineage verifier accepted the updated compat alias schema/map during builds.

## Static results

- `Core/VMX` path absent; Core/VMX legacy-marked C# count: `0`.
- `Legacy/VMX` path absent; Legacy/VMX C# count: `0`.
- `VmxExecutionUnit.cs`, `VmcsManager.cs`, `IVmcsManager.cs` file count: `0`.
- forbidden VMX manager/backend implementation marker count excluding `Conformance`: `0`.
- neutral runtime trap VMX vocabulary marker count: `0`.
- `TestAssemblerConsoleApps` stale `vmxUnit` / `core.VmxUnit` / `core.Vmcs` marker count: `0`.
- production VMX retire success-factory authority marker count: `0`.

## Known unrelated debt

- Raw `FullyQualifiedName~Vmx` can match `NonVmx`; broad VMX was run with `FullyQualifiedName!~NonVmx`.
- `TestAssemblerConsoleApps` still has unrelated obsolete constructor warnings.

## Residual risk

- `VmxRetireModel` still retains success-shaped VMX retire factories as compatibility/model vocabulary. Tests and static fences prove production callers do not use them as authority.
- `AdmitVmCallTrapProjection` is an explicit API surface; it is not wired into production dispatch. Any future wiring must preserve runtime admission and backend-denied behavior until a neutral owner exists.
- Trap projection currently maps VMCALL to `VmExitReason.VmCall` only inside `VmxTrapProjectionMapper`; future VMX exit reason additions must stay in the mapper layer.

## Next heavy step

Choose one:

- extend VMREAD beyond denied scalar ABI only when a neutral owner exposes generated read-only values; or
- add a neutral completion/retire-publication fence for admitted-denied trap projection without enabling backend success.

Do not restore VMCS managers, field stores, active pointers, or successful VMX backend execution.
