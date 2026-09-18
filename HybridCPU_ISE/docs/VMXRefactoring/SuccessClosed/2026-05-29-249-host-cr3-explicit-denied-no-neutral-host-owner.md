# Closure 249: HostCr3 Kept Denied Without Neutral Host Owner

Date: 2026-05-29

## Selected Slice

Chose the fail-closed option for `HostCr3`.

No neutral host-address-space owner or read-only host-root value source exists in the runtime model. Therefore `HostCr3` remains denied, but now with an explicit projection decision instead of a generic missing-value-source result.

## Dependencies Checked

Checked the current VMREAD/memory projection path:

- `VmcsFieldProjectionSchema` marks `HostCr3` as generated/read-only and `MemoryDomainDescriptor`-owned compatibility alias vocabulary.
- `MemoryDomainDescriptor` exposes only neutral domain translation state through `TryCreateReadOnlyTranslationView()`.
- `MemoryDomainReadOnlyTranslationView` exposes guest/domain translation facts: `AddressSpaceRoot`, `SecondStageRoot`, `AddressSpaceTag`, and `AddressSpaceTargetCount`.
- No separate neutral host-address-space owner exposes a read-only host address-space root.

## Production Change

`VmcsReadOnlyValueProjectionService` now returns:

- `VmcsReadOnlyValueProjectionDecision.HostAddressSpaceOwnerMissing`

for `VmcsField.HostCr3` after runtime admission, generated schema lookup, and alias/evidence validation.

The denial happens before guest/domain translation view materialization, so invalid or valid guest translation state cannot accidentally decide the `HostCr3` result.

## Why HostCr3 Was Not Opened

`HostCr3` would require a neutral host-address-space owner. The existing `MemoryDomainDescriptor` translation view is not that owner; using `AddressSpaceRoot` would project the guest/domain root as host state and would turn compatibility vocabulary into authority.

## What Remains Projected

The admitted memory-owned VMREAD value slice remains:

- `GuestCr3`;
- `EptPointer`;
- `Vpid`;
- `Cr3TargetCount`.

## What Remains Denied

Still denied/fail-closed:

- `HostCr3`;
- execution-owned fields until `ExecutionDomainDescriptor` exposes explicit read-only values;
- compatibility-control fields until a separate neutral control-bit value contract exists;
- unknown fields;
- all writes.

## Why No VMCS Field Store Appeared

No VMCS scalar store, active VMCS pointer, mutable projection object, VMCS manager, or fallback read path was added. `HostCr3` is not mapped in `TryProjectMemoryField`.

## Why VMREAD Is Still Not Feature-Complete Backend Execution

This closure adds an explicit denial state, not a successful backend operation. VMREAD remains a generated/read-only compatibility projection only for fields backed by a neutral value source.

## Tests

Updated:

- `VmxMemoryOwnedVmReadValueProjectionTests`.

The tests prove:

- `HostCr3` returns `HostAddressSpaceOwnerMissing`;
- the result still occurs after runtime admission and generated schema owner lookup;
- invalid guest translation control does not become the reason for `HostCr3` denial;
- `HostCr3` is not mapped in the memory-field projection switch;
- no VMCS field store fallback markers are present.

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

- `VmxMemoryOwnedVmReadValueProjectionTests`: passed, 10 tests.
- `VmxGeneratedReadOnlyVmReadValueProjectionTests`: passed, 5 tests.
- `VmxFirstAdmittedCompatibilityPathTests`: passed, 1 test.
- `RuntimeBoundaryAdmissionTests`: passed, 4 tests.
- `VmxProjectionSchemaAndQuarantineTests`: passed, 1 test.
- `VmxCompatibilityProjectionInventoryTests`: passed, 1 test.

Broad VMX:

- `FullyQualifiedName~Vmx&FullyQualifiedName!~NonVmx`: passed, 241 tests.

Static:

- Production `CloseToHSL/Core/Virtualization` scan excluding conformance-only contracts found no `VmxExecutionUnit`, `VmcsManager`, `IVmcsManager`, VMX runtime manager, VMCS projection runtime manager, `ReadFieldValue`, `WriteFieldValue`, `HardwareWrite`, or `DirectWrite`.
- Full `CloseToHSL/Core/Virtualization` scan found only pre-existing conformance/static-evidence contract string literals that name forbidden symbols as deny-list entries.
- `HybridCPU_ISE.csproj` has no `Virtualization/Substrate` or `Virtualization\Substrate` entries.
- `TestAssemblerConsoleApps` has no `core.VmxUnit`, `core.Vmcs`, or `vmxUnit` references.
- No `VmxExecutionUnit.cs`, `VmcsManager.cs`, or `IVmcsManager.cs` files exist outside `CloseToHSL`.
- `Legacy/VMX` has no C# files.
- `rg -il "legacy" CloseToHSL/Core/Virtualization --glob "*.cs"` returned no production legacy markers.
- `git diff --check`: no whitespace errors; only repository line-ending warnings for LF to CRLF normalization.

## Residual Risk

Future work must not treat `MemoryDomainReadOnlyTranslationView.AddressSpaceRoot` as `HostCr3`. A future `HostCr3` projection needs a separate neutral host-address-space owner and conformance proof.

## Next Heavy Step

Continue VMREAD field-by-field only through existing neutral owners. The next useful work is likely execution-owned fields only if `ExecutionDomainDescriptor` exposes explicit read-only guest state; otherwise keep them denied.
