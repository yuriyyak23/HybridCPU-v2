# Closure 246: Runtime-Owned Trap Completion Route Design

Date: 2026-05-29

## Selected Slice

Closed the design step required before any real VMCALL/intercept publication.

Added a neutral runtime-owned trap completion route layer, but did not open successful VMCALL, intercept completion publication, or retire publication.

## Dependencies Found

The existing path before this closure was:

- VMCALL decode;
- frozen compatibility alias projection;
- `RuntimeBoundaryAdmissionService(ProjectCompatibilityTrap)`;
- neutral trap policy;
- `NeutralTrapResult`;
- VMX-compatible `VmxTrapProjectionMapper`;
- `TrapCompletionPublicationFence`;
- backend-denied publication.

The missing layer was a neutral route owner that decides whether a neutral trap completion is publishable before the compatibility frontend can project it.

## Neutral Route Owner Added

Added under `Core/Runtime/Completion/Routing`:

- `TrapCompletionRouteDescriptor`;
- `TrapCompletionRouteRequest`;
- `TrapCompletionRouteResult`;
- `TrapCompletionRouteService`;
- `TrapCompletionRouteDecision`.

`TrapCompletionRouteService` authorizes trap completion publication only after:

- runtime boundary admission;
- a neutral trap result;
- runtime-owned route authority;
- domain validation when required by the route;
- backend execution authorization;
- completion publication permission;
- retire publication permission.

The runtime route source carries neutral trap/completion language only. It does not use `VmExitReason`, `TrapDecision`, `VmxExitQualification`, VMCS state, or VMX backend authority.

## VMCALL Path Status

`VmxCompatibilityTrapAdmissionResult` now carries `TrapCompletionRouteResult CompletionRoute`.

The VMCALL compatibility path uses:

- `TrapCompletionRouteRequest.ProjectionOnlyDenied`;
- `TrapCompletionRouteService.Default.Authorize`;
- `TrapCompletionRouteService.Default.EvaluateFence`.

Therefore the admitted VMCALL projection still returns:

- `TrapCompletionRouteDecision.DeniedBackendExecution`;
- `TrapCompletionPublicationDecision.DeniedBackendExecution`;
- no completion publication;
- no retire publication;
- no successful backend execution.

`TrapCompletionRouteDescriptor.RuntimeOwnedPublication` exists only as a neutral route contract and is not used by the VMX compatibility frontend.

## Why This Does Not Open Real Publication

No neutral hypercall backend owner exists yet. No backend execution authorization is granted to VMCALL/intercept. The compatibility frontend is wired only to `ProjectionOnlyDenied`.

The allowed route can be exercised in unit tests as neutral route semantics, but production VMX compatibility admission does not call it.

## Why No Legacy Authority Returned

No VMCS field store, active VMCS pointer, VMCS manager, VMX execution unit, VMX runtime manager, or compatibility-owned completion route was added.

The route authority check explicitly rejects `CompletionRouteAuthority.CompatibilityProjection`.

## Tests

Added:

- `VmxTrapCompletionRouteOwnerTests`.

Updated:

- `VmxTrapProjectionPublicationFenceTests`.

The tests prove:

- projection-only trap completion route denies publication before backend execution;
- runtime-owned route can authorize only a neutral trap after all neutral gates;
- compatibility-projection-owned route authority is rejected;
- VMCALL admission uses the neutral route layer but remains backend-denied;
- runtime route source has no VMX exit vocabulary;
- VMX compatibility frontend does not use `TrapCompletionRouteDescriptor.RuntimeOwnedPublication`.

## Documentation

Updated:

- `audit3.md`;
- `audit4.md`;
- `audit5.md`;
- `2026-05-24-vmx-current-model-completion-audit.md`;
- `ОСНОВЫ и ПРАВИЛА VMX.md`.

## Verification

Builds:

- `dotnet build HybridCPU_ISE/HybridCPU_ISE.csproj --no-restore`: passed, 0 warnings.
- `dotnet build HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-restore`: passed, 0 warnings.
- `dotnet build TestAssemblerConsoleApps/TestAssemblerConsoleApps.csproj --no-restore`: passed, 2 existing obsolete-constructor warnings.

Focused tests:

- `VmxTrapCompletionRouteOwnerTests`: passed, 4 tests.
- `VmxTrapProjectionPublicationFenceTests`: passed, 4 tests.
- `VmxAdmittedDeniedVmCallTrapPathTests`: passed, 5 tests.
- `RuntimeBoundaryAdmissionTests`: passed, 4 tests.
- `VmxFirstAdmittedCompatibilityPathTests`: passed, 1 test.
- `VmxProjectionSchemaAndQuarantineTests`: passed, 1 test.
- `VmxCompatibilityProjectionInventoryTests`: passed, 1 test.

Broad VMX:

- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~Vmx&FullyQualifiedName!~NonVmx"`: passed, 234 tests.

Static:

- Production `CloseToHSL/Core/Virtualization` scan excluding conformance-only contracts found no `VmxExecutionUnit`, `VmcsManager`, `IVmcsManager`, VMX runtime manager, VMCS projection runtime manager, `ReadFieldValue`, `WriteFieldValue`, `HardwareWrite`, or `DirectWrite`.
- Full `CloseToHSL/Core/Virtualization` scan found only pre-existing conformance/static-evidence contract string literals that name forbidden symbols as deny-list entries.
- `HybridCPU_ISE.csproj` has no `Virtualization/Substrate` or `Virtualization\Substrate` entries.
- `TestAssemblerConsoleApps` has no `core.VmxUnit`, `core.Vmcs`, or `vmxUnit` references.
- No `VmxExecutionUnit.cs`, `VmcsManager.cs`, or `IVmcsManager.cs` files exist outside `CloseToHSL`.
- `Legacy/VMX` has no C# files.
- `rg -il "legacy" CloseToHSL/Core/Virtualization --glob "*.cs"` returned no production legacy markers.
- `TrapCompletionRouteDescriptor.RuntimeOwnedPublication` appears only in the neutral runtime contract, tests, and documentation; VMX compatibility admission uses only `ProjectionOnlyDenied`.
- VMX compatibility production scan found no `RuntimeOwnedPublication`, `TrapCompletionPublicationDecision.Allowed`, explicit completion/retire publication `true`, or `VmxRetireEffect.InterceptExit`.
- `git diff --check`: no whitespace errors; only repository line-ending warnings for LF to CRLF normalization.

## Residual Risk

The neutral route contract now exists, so future work must not wire `RuntimeOwnedPublication` into VMX compatibility without first adding a real neutral hypercall/trap backend owner, capability grant, evidence policy, and backend execution authorization.

## Next Heavy Step

Design the runtime-owned VMCALL/hypercall backend owner and capability/evidence admission policy, or keep VMCALL as admitted-denied projection.
