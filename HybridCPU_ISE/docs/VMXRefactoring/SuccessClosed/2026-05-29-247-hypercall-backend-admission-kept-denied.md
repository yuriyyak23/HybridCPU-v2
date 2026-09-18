# Closure 247: Hypercall Backend Admission Kept Denied

Date: 2026-05-29

## Selected Slice

Chose the clean fail-closed option: keep VMCALL admitted-denied until a real neutral hypercall backend owner exists.

This closure adds explicit neutral backend-admission evidence before route/fence publication, but does not open successful VMCALL backend execution.

## Dependencies Found

The current VMCALL path before this closure was:

- VMCALL decode;
- frozen opcode alias projection;
- `RuntimeBoundaryAdmissionService(ProjectCompatibilityTrap)`;
- neutral trap policy;
- `NeutralTrapResult`;
- `VmxTrapProjectionMapper`;
- `TrapCompletionRouteService`;
- `TrapCompletionPublicationFence`;
- backend-denied projection.

The missing piece was an explicit neutral backend admission result between neutral trap result and completion route authorization.

## Neutral Backend Admission Added

Added under `Core/Runtime/Events/Hypercalls`:

- `HypercallBackendDescriptor`;
- `HypercallBackendAdmissionRequest`;
- `HypercallBackendAdmissionResult`;
- `HypercallBackendAdmissionService`;
- `HypercallBackendAdmissionDecision`;
- `HypercallBackendAuthority`.

The admission service checks:

- runtime boundary admission;
- neutral trap result;
- runtime-owned backend authority;
- domain validation;
- typed capability requirement;
- neutral evidence requirement;
- materialized neutral backend owner semantics.

The service remains fail-closed. Even after capability and evidence gates pass, it returns `DeniedNeutralBackendOwnerMissing` unless a future closure materializes real neutral backend semantics.

## VMCALL Path Status

`VmxCompatibilityTrapAdmissionResult` now carries `HypercallBackendAdmissionResult BackendAdmission`.

Production VMCALL uses:

- `HypercallBackendAdmissionRequest.MissingNeutralOwner`;
- `HypercallBackendAdmissionService.Default.Admit`;
- `TrapCompletionRouteRequest.ProjectionOnlyDenied(..., backendAdmission.IsAllowed)`.

Therefore admitted VMCALL still returns:

- `HypercallBackendAdmissionDecision.MissingBackendDescriptor`;
- `TrapCompletionRouteDecision.DeniedBackendExecution`;
- `TrapCompletionPublicationDecision.DeniedBackendExecution`;
- no backend execution;
- no compatibility exit completion publication;
- no intercept retire publication.

## Why This Does Not Open Real VMCALL

No neutral hypercall execution owner, handler table, backend operation semantics, mutable state, or successful retire path was added.

The VMX compatibility frontend passes a missing-owner admission request. It never supplies a runtime-owned backend descriptor and never uses `TrapCompletionRouteDescriptor.RuntimeOwnedPublication`.

## Why No Legacy Authority Returned

No VMCS field store, active VMCS pointer, VMCS manager, VMX execution unit, VMX runtime manager, or compatibility-owned backend authority was added.

The neutral source has no VMX exit vocabulary, VMCS vocabulary, `TrapDecision`, or compatibility backend manager.

## Tests

Added:

- `VmxHypercallBackendAdmissionPolicyTests`.

Updated:

- `VmxAdmittedDeniedVmCallTrapPathTests`;
- `VmxTrapCompletionRouteOwnerTests`;
- `VmxTrapProjectionPublicationFenceTests`.

The tests prove:

- missing neutral hypercall backend owner keeps backend execution denied;
- runtime-owned design fence requires typed capability and evidence policy;
- compatibility-projection-owned backend authority is rejected;
- production VMCALL reports backend admission but remains admitted-denied;
- route/fence still deny completion and retire publication;
- neutral backend admission source contains no VMX exit or VMCS authority.

## Documentation

Updated:

- `audit3.md`;
- `audit4.md`;
- `audit5.md`;
- `2026-05-24-vmx-current-model-completion-audit.md`;
- `ОСНОВЫ и ПРАВИЛА VMX.md`.

## Verification

Builds:

- `dotnet build HybridCPU_ISE/HybridCPU_ISE.csproj --no-restore`: passed, 54 existing warnings.
- `dotnet build HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-restore`: passed, 93 existing warnings.
- `dotnet build TestAssemblerConsoleApps/TestAssemblerConsoleApps.csproj --no-restore`: passed, 2 existing obsolete-constructor warnings.

Focused tests:

- `VmxHypercallBackendAdmissionPolicyTests`: passed, 4 tests.
- `VmxAdmittedDeniedVmCallTrapPathTests`, `VmxTrapCompletionRouteOwnerTests`, and `VmxTrapProjectionPublicationFenceTests`: passed, 13 tests.
- `RuntimeBoundaryAdmissionTests`: passed, 4 tests.
- `VmxFirstAdmittedCompatibilityPathTests`: passed, 1 test.
- `VmxProjectionSchemaAndQuarantineTests`: passed, 1 test.
- `VmxCompatibilityProjectionInventoryTests`: passed, 1 test.

Broad VMX:

- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~Vmx&FullyQualifiedName!~NonVmx"`: passed, 238 tests.

Static:

- Production `CloseToHSL/Core/Virtualization` scan excluding conformance-only contracts found no `VmxExecutionUnit`, `VmcsManager`, `IVmcsManager`, VMX runtime manager, VMCS projection runtime manager, `ReadFieldValue`, `WriteFieldValue`, `HardwareWrite`, or `DirectWrite`.
- Full `CloseToHSL/Core/Virtualization` scan found only pre-existing conformance/static-evidence contract string literals that name forbidden symbols as deny-list entries.
- VMX compatibility production scan found no `RuntimeOwnedPublication`, `TrapCompletionPublicationDecision.Allowed`, explicit completion/retire publication `true`, `VmxRetireEffect.InterceptExit`, or `CompletionRecord.FromCompatibilityExit`.
- VMX compatibility production scan found no `HypercallBackendDescriptor`, `RuntimeOwnedDesignFence`, or `neutralBackendOwnerMaterialized: true`; production uses only `HypercallBackendAdmissionRequest.MissingNeutralOwner`.
- `HybridCPU_ISE.csproj` has no `Virtualization/Substrate` or `Virtualization\Substrate` entries.
- `TestAssemblerConsoleApps` has no `core.VmxUnit`, `core.Vmcs`, or `vmxUnit` references.
- No `VmxExecutionUnit.cs`, `VmcsManager.cs`, or `IVmcsManager.cs` files exist outside `CloseToHSL`.
- `Legacy/VMX` has no C# files.
- `rg -il "legacy" CloseToHSL/Core/Virtualization --glob "*.cs"` returned no production legacy markers.
- `git diff --check`: no whitespace errors; only repository line-ending warnings for LF to CRLF normalization.

## Residual Risk

Future work must not replace `MissingNeutralOwner` in production until real neutral hypercall semantics, typed capability contract, evidence policy, backend execution owner, completion route, publication fence, and retire publication are all admitted together.

## Next Heavy Step

Materialize a real neutral hypercall backend owner only if there is a concrete runtime operation semantics contract. Otherwise continue expanding VMREAD value projection only field-by-field through existing neutral owners.
