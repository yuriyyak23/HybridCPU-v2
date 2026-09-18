# Closure 248: CR3 Target Count VMREAD Value Projection

Date: 2026-05-29

## Selected Slice

Chose the honest VMREAD field-by-field expansion path instead of opening a real hypercall backend.

No concrete neutral hypercall runtime semantics exist yet, so production VMCALL remains admitted-denied through `HypercallBackendAdmissionRequest.MissingNeutralOwner`. The closure opens one additional generated/read-only memory-owned VMREAD value only where the neutral memory owner now exposes explicit semantics:

- `Cr3TargetCount`.

## Dependencies Found

The active VMREAD value path is:

```text
VMREAD(field)
  -> VMREAD decode
  -> frozen alias projection validation
  -> RuntimeBoundaryAdmissionService(ReadCompatibilityProjection)
  -> VmcsFieldProjectionSchema owner lookup
  -> neutral descriptor/completion owner
  -> field evidence policy
  -> generated/read-only compatibility value projection
```

The relevant dependencies are:

- `VmxCompatibilityAdmissionService.AdmitVmReadProjection`;
- `VmxCompatibilityVmReadAdmissionRequest`;
- `VmcsReadOnlyValueProjectionService`;
- `VmcsFieldProjectionSchema`;
- `VmcsFieldAliasProjection`;
- `VmcsFieldProjectionOwner.MemoryDomainDescriptor`;
- `MemoryDomainDescriptor.TryCreateReadOnlyTranslationView`;
- `MemoryDomainReadOnlyTranslationView`;
- `RuntimeBoundaryAdmissionService`;
- `EvidenceVisibilityClass.CompatibilityAlias`.

## Neutral Owner And Value Source

`MemoryDomainTranslationControl` now carries `AddressSpaceTargetCount` as neutral memory-domain translation metadata. The value is surfaced through `MemoryDomainReadOnlyTranslationView`.

The VMX compatibility frontend maps that neutral value to frozen ABI field `VmcsField.Cr3TargetCount` only after:

- runtime boundary admission succeeds;
- generated schema owner metadata says `MemoryDomainDescriptor`;
- compatibility-alias evidence is allowed;
- the memory descriptor materializes a valid read-only translation view.

`AddressSpaceTargetCount == 0` is a real neutral value meaning no address-space target roots are materialized. Values above `MemoryDomainTranslationControl.MaxAddressSpaceTargetCount` are rejected by the neutral translation-control validation gate and do not project.

## Fields Projected

The memory-owned generated/read-only VMREAD projection now includes:

- `GuestCr3` from neutral `AddressSpaceRoot`;
- `EptPointer` from owned neutral `SecondStageRoot`;
- `Vpid` from neutral `AddressSpaceTag` only when address-space tagging is materialized;
- `Cr3TargetCount` from neutral `AddressSpaceTargetCount`.

## Fields Still Denied

Still denied/fail-closed:

- `HostCr3`;
- execution-owned fields such as `GuestPc`, `GuestSp`, `GuestFlags`, `GuestCr0`, and `GuestCr4`;
- compatibility-control fields unless a future separate neutral control-bit value contract is admitted;
- unknown or ungenerated fields;
- all writes.

## Why No VMCS Field Store Appeared

The projection service reads from `MemoryDomainReadOnlyTranslationView`, not from VMCS scalar storage. No active VMCS pointer, mutable VMCS projection object, manager, field cache, or scalar fallback was introduced.

`VmcsV2Descriptor.TryReadScalarField` remains only a denied compatibility ABI and is not used by admitted VMREAD value projection.

## Why VMREAD Is Still Not Feature-Complete Backend Execution

This is a generated/read-only compatibility value projection after neutral authority, not a VMREAD backend. The frontend can project only the admitted fields with explicit neutral owner/value sources. Unsupported owners and fields remain denied with explicit value-projection decisions.

## Why VMCALL Remains Denied

Closure 248 does not replace `HypercallBackendAdmissionRequest.MissingNeutralOwner`. There is still no neutral hypercall backend operation semantics contract, typed capability contract, evidence policy, successful backend execution owner, allowed completion route, or retire publication path. VMCALL remains admitted-denied.

## Legacy Absence

No `VmxExecutionUnit`, `VmcsManager`, `IVmcsManager`, `VmcsManagerAdapter`, `VmxRuntimeManager`, `VmcsProjectionRuntimeManager`, `VmcsV2RuntimeManager`, active pointer state, or VMCS field store was introduced.

## Tests

Updated:

- `VmxMemoryOwnedVmReadValueProjectionTests`.

The tests prove:

- `Cr3TargetCount` projects only through the VMREAD path after runtime admission;
- projection source is `MemoryDomainReadOnlyTranslationView.AddressSpaceTargetCount`;
- generated schema owner metadata is `MemoryDomainDescriptor`;
- zero target count is a valid neutral value;
- invalid neutral target count is denied before projection;
- `HostCr3` remains denied;
- the source path contains no VMCS field store fallback markers.

## Documentation

Updated:

- `audit3.md`;
- `audit4.md`;
- `audit5.md`;
- `2026-05-24-vmx-current-model-completion-audit.md`;
- `ОСНОВЫ и ПРАВИЛА VMX.md`.

`audit3.md` now carries the active open backlog imported from both `audit4.md` and `audit5.md`; those two audit files remain historical/backlog evidence rather than competing current-state sources.

## Verification

Builds:

- `dotnet build HybridCPU_ISE/HybridCPU_ISE.csproj --no-restore`: passed, 0 warnings.
- `dotnet build HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-restore`: passed, 0 warnings.
- `dotnet build TestAssemblerConsoleApps/TestAssemblerConsoleApps.csproj --no-restore`: passed, 2 existing obsolete-constructor warnings.

Focused tests:

- `VmxMemoryOwnedVmReadValueProjectionTests`: passed, 9 tests.
- `VmxGeneratedReadOnlyVmReadValueProjectionTests`: passed, 5 tests.
- `VmxFirstAdmittedCompatibilityPathTests`: passed, 1 test.
- `RuntimeBoundaryAdmissionTests`: passed, 4 tests.
- `VmxProjectionSchemaAndQuarantineTests`: passed, 1 test.
- `VmxCompatibilityProjectionInventoryTests`: passed, 1 test.

Broad VMX:

- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~Vmx&FullyQualifiedName!~NonVmx"`: passed, 240 tests.

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

`Cr3TargetCount` now has a neutral count source, but there is still no broad CR3 target-list VMX backend and no mutable VMCS target store. Future expansion must keep target roots under neutral memory-domain semantics.

VMCALL remains blocked until real neutral hypercall backend semantics exist.

## Next Heavy Step

Continue VMREAD value projection field-by-field through explicit neutral owners. The next likely candidates are execution-owned fields only if `ExecutionDomainDescriptor` exposes a read-only value source, or `HostCr3` only if a neutral host-address-space owner exists. Otherwise keep them denied.
