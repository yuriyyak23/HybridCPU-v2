# Success Closure: 235 First Admitted VMREAD Projection Admission

Date: 2026-05-27

## Selected Slice

First admitted VMX compatibility path through `RuntimeBoundaryAdmissionService`, using the lowest-risk `VMREAD` compatibility projection path.

This is not successful VMCS execution. The admitted path stops at the existing denied/read-only scalar projection ABI.

## What Changed

- Added `Core/VMX/Compatibility/Frontend/Handlers/VmxCompatibilityAdmissionService.cs`.
- Added `VmxCompatibilityVmReadAdmissionRequest`, result, and decision types for the admitted projection path.
- The path is:

```text
VMREAD decode
-> frozen alias projection: Opcode/VMREAD -> VmcsFieldAliasProjection.Read
-> RuntimeBoundaryAdmissionService
-> DomainRuntimeOperationKind.ReadCompatibilityProjection
-> EvidenceBoundaryRequirement.GuestVisible(CompatibilityAlias)
-> VmcsV2Descriptor.TryReadScalarField
-> denied scalar compatibility ABI
```

- Added `HybridCPU_ISE.Tests/VmxRefactoring/VmxFirstAdmittedCompatibilityPathTests.cs`.
- Updated `VmxRetainedSurfaceAndFreezeEvidenceContracts.cs` so the new production caller is explicitly marker-checked.
- Updated `audit3.md`, `2026-05-24-vmx-current-model-completion-audit.md`, and `ОСНОВЫ и ПРАВИЛА VMX.md`.

## What Was Left Denied

- `TryReadScalarField` still returns `VmcsV2ValidationCode.AccessDenied`.
- No `VmxRetireEffect.VmcsRead` production caller was introduced.
- No destination-register writeback, VMCS backend path, field store, active pointer, or manager path was introduced.

## Production Authority Impact

Production authority remains neutral:

- runtime admission is handled by `Core/Runtime/Services/RuntimeBoundaryAdmissionService.cs`;
- domain operation is projection-only and cannot mutate authoritative state;
- capability requirement is `CapabilityBoundaryRequirement.None` for this alias-only projection admission;
- evidence exposure requires compatibility-alias policy approval;
- VMCSv2 scalar value publication remains denied until a generated read-only projection over neutral owners exists.

## Legacy/VMX Count

- `Legacy/VMX` C# sources remaining: `0`.
- Physical `Legacy/VMX` path: absent.
- `Core/VMX` legacy-marked C# sources: `0`.
- `VmxExecutionUnit.cs`, `VmcsManager.cs`, and `IVmcsManager.cs`: absent.

## Move-Away Probe

No new move-away probe was required for this slice because `Legacy/VMX` and `Legacy/VMX/Conformance` are already physically absent and were closed by the earlier full-folder probe.

## Verification

- `dotnet build HybridCPU_ISE/HybridCPU_ISE.csproj --no-restore`: passed.
- `dotnet build HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-restore`: passed.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmxFirstAdmittedCompatibilityPathTests"`: passed, `4/4`.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~RuntimeBoundaryAdmissionTests"`: passed, `4/4`.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmxProjectionSchemaAndQuarantineTests"`: passed, `58/58`.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmxCompatibilityProjectionInventoryTests"`: passed, `4/4`.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~Vmx&FullyQualifiedName!~NonVmx"`: passed, `266/266`.
- Static counts: `CoreVmxLegacyMarkedCs=0`, `LegacyVmxCs=0`, `ForbiddenManagerUnitFiles=0`, `LegacyVmxPathExists=False`.
- `git diff --check` on touched files: passed; line-ending warnings only.

## Known Unrelated Debt

Raw `FullyQualifiedName~Vmx` currently includes `NonVmxIteration04BDeferredTemplateSurfaceTests` because `NonVmx` contains the substring `Vmx`. That broad raw filter reported `565/567` passed and `2` failures:

- expected deferred template count `134`, actual `128`;
- expected remaining scalar template count `39`, actual `33`.

Those failures are in `CloseToHSL.Core.ISA.Instructions.NonVmx.*` inventory/count tests and are unrelated to this VMX compatibility admission slice.

## Residual Risk

- This is the first admitted compatibility projection path only. It is not feature-complete VMX execution.
- VMREAD scalar value publication remains denied until generated read-only projection over neutral runtime/domain owners exists.
- VMCALL/trap/intercept admission should not be attempted until neutral trap results are split from VMX-compatible `VmExitReason`/qualification projection.

## Next Step

Split neutral trap results from VMX-compatible trap/exit projection before admitting any VMCALL/trap/intercept path.
