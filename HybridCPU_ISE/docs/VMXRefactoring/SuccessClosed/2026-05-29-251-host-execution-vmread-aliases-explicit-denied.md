# Closure 251: Host Execution VMREAD Aliases Explicitly Denied

Date: 2026-05-29

## Selected Slice

Chose the clean fail-closed path requested by the work order:

- keep `GuestCr0` and `GuestCr4` denied;
- do not invent neutral privileged execution-state semantics;
- close the next execution-owned branch by explicitly denying host execution aliases.

The fields covered by this closure are:

- `HostPc`
- `HostSp`
- `HostFlags`
- `HostCr0`

## Dependencies Checked

`VmcsFieldProjectionSchema` marks these fields as:

- owner: `ExecutionDomainDescriptor`;
- evidence: `CompatibilityAlias`;
- access: read-only;
- migration policy: projection-only.

The current neutral execution owner exposes only `ExecutionDomainReadOnlyStateView` for the guest architectural-state slice opened in closure `250`:

- `GuestPc`
- `GuestSp`
- `GuestFlags`

No neutral host-execution owner or read-only host PC/SP/flags/control-register source exists.

## Production Change

`VmcsReadOnlyValueProjectionService` now returns:

- `VmcsReadOnlyValueProjectionDecision.HostExecutionStateOwnerMissing`

for:

- `HostPc`
- `HostSp`
- `HostFlags`
- `HostCr0`

The denial happens after:

```text
VMREAD decode
-> frozen alias projection validation
-> RuntimeBoundaryAdmissionService(ReadCompatibilityProjection)
-> VmcsFieldProjectionSchema owner lookup
-> compatibility-alias evidence validation
```

and before `ExecutionDomainDescriptor.TryCreateReadOnlyStateView()` can materialize guest state.

## Why No New VMREAD Value Opened

There is no neutral host-execution owner. Reusing `ExecutionDomainReadOnlyStateView.GuestPc`, `GuestSp`, or `GuestFlags` as host state would turn a guest architectural projection into host authority.

This closure intentionally adds a precise denial state instead of projecting a value.

## What Remains Projected

The admitted execution-owned VMREAD value slice remains:

- `GuestPc`
- `GuestSp`
- `GuestFlags`

These still require neutral `ExecutionDomainReadOnlyStateView`, generated schema owner metadata, runtime admission, and `GuestArchitecturalState` evidence.

## What Remains Denied

Still denied/fail-closed:

- `GuestCr0`;
- `GuestCr4`;
- `HostPc`;
- `HostSp`;
- `HostFlags`;
- `HostCr0`;
- `HostCr3`;
- compatibility-control fields;
- unknown fields;
- all writes.

## Why No VMCS Field Store Appeared

No VMCS scalar store, active VMCS pointer, mutable projection object, VMCS manager, host fake value, host-state cache, or fallback read path was added.

The denial is not based on:

- `VmcsV2Descriptor`;
- `VmcsV2Blocks`;
- scalar cache;
- VMCS block;
- test-only behavior;
- legacy guest-state or host-state helpers.

## Why VMREAD Is Still Not Feature-Complete Backend Execution

This closure does not add a successful VMREAD backend. It only makes the host execution aliases fail closed with a more precise decision after neutral admission and evidence validation.

VMREAD remains generated/read-only compatibility projection only for fields backed by explicit neutral owner/value sources.

## Tests

Updated:

- `VmxExecutionOwnedVmReadValueProjectionTests`.

The tests prove:

- `HostPc`, `HostSp`, `HostFlags`, and `HostCr0` return `HostExecutionStateOwnerMissing`;
- the denial occurs after runtime admission and generated schema owner lookup;
- compatibility-alias evidence is required;
- materialized guest read-only state does not become a host source;
- missing guest read-only state is not the reason host aliases are denied;
- no VMCS field store or legacy helper fallback is present.

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
- `dotnet build HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-restore`: passed, 0 warnings.
- `dotnet build TestAssemblerConsoleApps/TestAssemblerConsoleApps.csproj --no-restore`: passed, 2 existing obsolete-constructor warnings.

Focused tests:

- `VmxExecutionOwnedVmReadValueProjectionTests`: passed, 8 tests.
- `VmxGeneratedReadOnlyVmReadValueProjectionTests`: passed, 5 tests.
- `VmxMemoryOwnedVmReadValueProjectionTests`: passed, 10 tests.
- `VmxFirstAdmittedCompatibilityPathTests`: passed, 1 test.
- `RuntimeBoundaryAdmissionTests`: passed, 4 tests.
- `VmxProjectionSchemaAndQuarantineTests`: passed, 1 test.
- `VmxCompatibilityProjectionInventoryTests`: passed, 1 test.

Broad VMX:

- `FullyQualifiedName~Vmx&FullyQualifiedName!~NonVmx`: passed, 249 tests.

Static:

- Full `CloseToHSL/Core/Virtualization` forbidden-authority scan found only pre-existing conformance/static-evidence contract string literals that name forbidden markers as deny-list entries.
- Production scan excluding `Conformance` found no forbidden authority markers.
- `HybridCPU_ISE.csproj` has no `Virtualization/Substrate` or `Virtualization\Substrate` entries.
- `TestAssemblerConsoleApps` has no `core.VmxUnit`, `core.Vmcs`, or `vmxUnit` references.
- No `VmxExecutionUnit.cs`, `VmcsManager.cs`, or `IVmcsManager.cs` files exist outside `CloseToHSL`.
- `Legacy/VMX` has no C# files.
- `rg -il "legacy" CloseToHSL/Core/Virtualization --glob "*.cs"` returned no production legacy markers.
- `git diff --check`: passed; Git reported only LF-to-CRLF normalization warnings.

## Residual Risk

Future work must not treat guest execution read-only state as host state.

`HostPc`, `HostSp`, `HostFlags`, and `HostCr0` require a separate neutral host-execution owner and conformance proof before any read-only value projection can open.

`GuestCr0` and `GuestCr4` still require neutral privileged execution-state semantics or must remain denied.

## Next Heavy Step

Continue VMREAD field-by-field only through explicit neutral owners. The likely next honest branch is one of:

1. keep remaining control-like fields denied and audit descriptor readiness policy; or
2. add a new read-only VMREAD value only if an existing neutral owner already exposes an explicit source.
