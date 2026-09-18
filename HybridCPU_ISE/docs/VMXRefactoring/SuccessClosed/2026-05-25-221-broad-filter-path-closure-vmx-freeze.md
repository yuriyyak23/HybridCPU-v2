# 2026-05-25 task 221 - broad filter path closure and VMX freeze

## Scope

This step closed the final stale broad-filter blocker recorded by task `220`.

The repaired path debt was test-side repository shape drift:

- stale expectation: `HybridCPU_ISE/Core/Diagnostics/InstructionRegistry.Helpers.Core.cs`;
- actual production source: `HybridCPU_ISE/NonRTL/Core/Diagnostics/InstructionRegistry.Helpers.Core.cs`.

The fix updates conformance/test path contracts to the real `NonRTL` source locations. No compatibility shim, duplicate file, or renamed runtime owner was introduced.

## Code result

- `Phase09DirectFactoryCallerBoundaryTests` now reads instruction-registry helpers from `NonRTL/Core/Diagnostics` and allows the current `NonRTL/Core/Decoder/DecodedBundleTransportProjector.cs` caller.
- Neighbor repository-shape contracts that read diagnostic, trace, vector helper, safety verifier, certificate, and typed-slot files now point at their actual `NonRTL` locations.
- `CompatFreezeGateCatalog` diagnostics allowlist entries now use `HybridCPU_ISE/NonRTL/Core/Diagnostics`.
- `LegacyVmxFreezeReadinessCertificationContract.CanDeclareFreeze` is now `true`.
- `LegacyVmxFreezeReadinessCertificationContract.KnownUnrelatedBroadFilterDebt` is empty for the VMX matrix.
- `LegacyVmxFreezeReadinessCertificationContract.OutOfScopeNonVmxBroadFilterDebt` records the remaining Phase12 VLIW/ISA compatibility-freeze debt separately.

## Freeze decision

VMX compatibility frontend freeze is declared for the current compiled VMX surface.

This is not a claim that VMX is the virtualization architecture. The frozen boundary remains:

- VMX is compatibility frontend vocabulary;
- VMX/VMCS names are ABI/projection/conformance vocabulary only;
- live authority remains under neutral runtime/domain owners;
- no `VmxExecutionUnit`, `VmcsManager`, `IVmcsManager`, field store, active pointer state, or renamed VMX runtime owner is restored;
- current opcode/retire behavior remains typed fail-closed unless a neutral runtime path separately admits an operation.

## Inventory at freeze

- `Core/VMX` legacy-marked `.cs`: `0`.
- `Legacy/VMX/Compatibility`: `0` `.cs`.
- Total `Legacy/VMX`: `37` `.cs`, conformance/evidence only.
- Rehomed Core compatibility carriers: `3`.
- `VmxExecutionUnit.cs`: absent.
- `VmcsManager.cs`: absent.
- `IVmcsManager.cs`: absent.

## Verification

- Tests build: passed with existing `93` warnings.
- `FullyQualifiedName~Vmx`: passed `258/258`.
- `FullyQualifiedName~VmxProjectionSchemaAndQuarantineTests`: passed `58/58`.
- `FullyQualifiedName~CoreVmxAuthorityBoundaryTests`: passed `1/1`.
- `FullyQualifiedName~RemovedLegacyVmxExecutionUnit|FullyQualifiedName~LegacyVmcsManager`: passed `19/19`.
- Repository-shape path repair bundle: passed `28/28`.
- Static inventory: `CoreVmxLegacyMarkedCount=0`, `LegacyVmxCsCount=37`, `LegacyVmxCompatibilityCsCount=0`.
- Final move-away probe: moving `Legacy/VMX` to `Desktop/New folder/VMX-final-freeze-probe-*` and building production passed; the directory was restored.

## Out-of-scope broad debt

`Phase12VliwCompatFreezeTests` remains separate repository/ISA compatibility-freeze debt:

- result: `23/28` passed, `5` failed;
- failures are stale repository-shape and VLIW/InstructionsEnum/Add_VLIW allowlist issues;
- this is not VMX compatibility frontend authority evidence and is not part of the VMX freeze decision.

## Residual policy

Future VMX work must treat the current VMX surface as frozen compatibility ABI/projection vocabulary. New execution, memory, I/O, lane/vector-stream, nested, capability, evidence, migration, completion, or retire authority must be added through the neutral runtime/domain substrate and then projected into VMX only through generated/read-only/fail-closed compatibility surfaces.
