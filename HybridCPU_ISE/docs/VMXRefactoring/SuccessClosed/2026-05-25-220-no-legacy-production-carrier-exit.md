# 2026-05-25 task 220 - no-legacy production carrier exit

## Scope

This step removed the remaining production dependency on physical `Legacy/VMX`.

The retained carrier trio was rehomed from `Legacy/VMX/Compatibility` into `Core/VMX/Compatibility`:

- `VmxInstructionPayload`
- `VmxRetireEffect`, `VmxRetireOutcome`, `VmxOperationKind`
- `ShadowVmcsNestedProjectionService`

No runtime authority was moved into VMX. These remain compatibility ABI/projection carriers.

## Code result

New production carrier paths:

- `Core/VMX/Compatibility/Frontend/Decode/VmxInstructionPayload.cs`
- `Core/VMX/Compatibility/Frontend/Retire/VmxRetireModel.cs`
- `Core/VMX/Compatibility/Generated/VmcsProjection/ShadowVmcsNestedProjectionService.cs`

Removed physical quarantine carrier paths:

- `Legacy/VMX/Compatibility/Frontend/Decode/VmxInstructionPayload.cs`
- `Legacy/VMX/Compatibility/Frontend/Retire/VmxRetireModel.cs`
- `Legacy/VMX/Compatibility/Generated/VmcsProjection/ShadowVmcsNestedProjectionService.cs`

`VmxRootDescriptorReference` now exposes `CompatibilityDefault`, so the rehomed Core sources do not need the old legacy-named default spelling.

`ShadowVmcsNestedProjectionService` remains fail-closed and evidence-independent:

- returns `NestedValidationResult.Fail`;
- uses `CompatibilityProjectionFailed`;
- returns `false`;
- owns no nested/checkpoint/runtime authority.

## Manifest and conformance

`LegacyVmxQuarantineManifest` now treats the three old physical paths as returned-to-Core compatibility vocabulary rather than `MustRemainQuarantined`.

`LegacyVmxRetainedCompatibilitySurfaceInventoryContract` and `LegacyVmxFreezeReadinessCertificationContract` now prove:

- `Legacy/VMX/Compatibility` contains no production `.cs`;
- the rehomed Core carriers exist;
- rehomed Core carrier sources contain no `legacy` marker;
- the old physical quarantine carrier paths are absent;
- production callers still bind to the same ABI symbols;
- removed heavy carriers remain absent.

## Move-away probe

The same physical probe was rerun:

1. Move `HybridCPU_ISE/Legacy/VMX` to `C:/Users/Yuriy Kurnosov/Desktop/New folder/VMX-carrier-exit-probe-*`.
2. Run production build.
3. Restore the directory in `finally`.

Result: production build passes while `Legacy/VMX` is absent from the project tree.

## Inventory

After this step:

- `Legacy/VMX/Compatibility`: `0` `.cs`.
- Total `Legacy/VMX`: `37` `.cs`, conformance/evidence only.
- Rehomed Core carrier count: `3`.
- `Core/VMX` legacy-marked `.cs`: `0`.
- `VmxExecutionUnit.cs`: absent.
- `VmcsManager.cs`: absent.
- `IVmcsManager.cs`: absent.

## Verification

- Production build with `Legacy/VMX` present: passed, existing `54` warnings.
- Tests build: passed, existing `93` warnings.
- Move-away production build with `Legacy/VMX` absent: passed, existing `54` warnings.
- `LegacyVmxRetainedCompatibilitySurfaceInventory|LegacyVmxFreezeReadinessCertification`: passed `2/2`.
- `VmxProjectionSchemaAndQuarantineTests`: passed `58/58`.
- `CoreVmxAuthorityBoundaryTests`: passed `1/1`.
- `RemovedLegacyVmxExecutionUnit|LegacyVmcsManager`: passed `19/19`.
- Dead shell removal bundle: passed `5/5`.
- Broad `FullyQualifiedName~Vmx`: `257/258` passed; the single failure is the known unrelated stale repository-shape path in `Phase09DirectFactoryCallerBoundaryTests` looking for `Core/Diagnostics/InstructionRegistry.Helpers.Core.cs` while the source lives under `NonRTL/Core/Diagnostics`.

## Freeze decision

VMX freeze is still not declared.

The physical production dependency blocker is closed. Remaining blocker is broad-filter debt and an explicit final freeze decision over the full compatibility/generated/conformance matrix.

## Next heavy step

Resolve the known broad `FullyQualifiedName~Vmx` stale-path failure for `InstructionRegistry.Helpers.Core.cs`, rerun broad VMX filters, and then perform the explicit final freeze declaration or final blocker list.
