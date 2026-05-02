# Phase 12 - Compiler emission path

Status: closed on 2026-04-28.

Goal:

- Add explicit compiler lowering for high-level accelerator intent to native
  lane7 `ACCEL_SUBMIT` plus typed sideband descriptor.
- Keep runtime rejection from silently falling back to scalar, ALU, vector,
  StreamEngine, or DmaStreamCompute execution.

ISE files likely touched:

- `HybridCPU_ISE/Core/Contracts/CompilerTransport/InstructionSlotMetadata.cs`
- `HybridCPU_ISE/Core/Pipeline/MicroOps/InstructionIR.cs`
- `HybridCPU_ISE/Core/Decoder/VliwDecoderV4.cs`
- `HybridCPU_ISE/Core/Decoder/DecodedBundleTransportProjector.cs`

Compiler files likely touched:

- `HybridCPU_Compiler/Core/IR/Model/IrInstruction.cs`
- `HybridCPU_Compiler/Core/IR/Model/IrSlotMetadata.cs`
- `HybridCPU_Compiler/Core/IR/Model/IrHazardEnums.cs`
- `HybridCPU_Compiler/Core/IR/Construction/HybridCpuIrBuilder.cs`
- `HybridCPU_Compiler/Core/IR/Hazards/HybridCpuHazardModel.cs`
- `HybridCPU_Compiler/Core/IR/Hazards/HybridCpuOpcodeSemantics.cs`
- `HybridCPU_Compiler/Core/IR/Hazards/HybridCpuSlotModel.Analysis.cs`
- `HybridCPU_Compiler/Core/IR/Bundling/HybridCpuBundleLowerer.cs`
- `HybridCPU_Compiler/API/Threading/HybridCpuThreadCompilerContext.cs`

New compiler types:

- `IrAcceleratorIntent`
- `IrAcceleratorCommand`
- `IrAcceleratorDescriptorSideband`
- `CompilerAcceleratorCapabilityModel`
- `CompilerAcceleratorLoweringDecision`
- `AcceleratorLoweringMode`

Methods to design:

- `HybridCpuThreadCompilerContext.CompileAcceleratorSubmit(...)`
- `HybridCpuIrBuilder.EmitAcceleratorSubmit(...)`
- `HybridCpuBundleLowerer.LowerAcceleratorCommand(...)`
- `HybridCpuHazardModel.GetAcceleratorCommandHazards(...)`
- `HybridCpuOpcodeSemantics.GetAcceleratorSystemSemantics(...)`
- `IrSlotMetadata.WithAcceleratorDescriptor(...)`
- `CompilerAcceleratorCapabilityModel.Supports(...)`

Compiler strategy:

```text
if capability exists and workload is large/coarse:
  emit ACCEL_SUBMIT with typed sideband descriptor
else if regular stream compute is appropriate:
  emit DmaStreamCompute
else:
  emit normal CPU lowering
```

Hard requirements:

- choice happens before code emission
- emitted accelerator command is native VLIW lane7 system command
- sideband descriptor is mandatory
- raw reserved bits stay zero
- raw VT hint stays zero
- unknown compatibility/adoption modes reject
- compiler must not promise runtime fallback after `ACCEL_SUBMIT` rejection
- compiler must model lane7 branch/system pressure and avoid submit/poll storms

Tests to add:

- `L7SdcCompilerEmissionTests`
- `L7SdcCompilerNoRuntimeFallbackTests`
- `L7SdcCompilerLane7PressureTests`

Test cases:

- coarse MatMul intent emits `ACCEL_SUBMIT` when capability model allows
- regular stream intent still emits DmaStreamCompute
- unsupported capability emits CPU lowering before accelerator emission
- once `ACCEL_SUBMIT` is emitted, runtime rejection remains rejection
- emitted carrier is lane7 hard-pinned SystemSingleton
- descriptor sideband survives compiler transport
- unknown adoption mode throws
- compiler avoids dense poll/submit storm in same bundle window

Must not break:

- existing DmaStreamCompute compiler contract
- `CompilerV5ContractAlignmentTests`
- bundle lowerer lane capacity invariants
- active frontend remains native VLIW only

Phase closure validation:

- Run focused tests:

```powershell
dotnet test --filter "L7SdcCompilerEmission|L7SdcCompilerNoRuntimeFallback|L7SdcCompilerLane7Pressure"
```

- Run affected baseline filters:

```powershell
dotnet test --filter "CompilerV5ContractAlignment|DmaStreamComputeCompilerContract|Phase09"
```

- Run diagnostics console:

```powershell
dotnet run --project TestAssemblerConsoleApps/TestAssemblerConsoleApps.csproj -- matrix-smoke --iterations 200 --telemetry-logs minimal
```

- Compare with `Documentation/AsmAppTestResults.md`; compiler changes must not
  regress default diagnostic workloads or unexpectedly change frontend profile
  behavior.

Definition of done:

- compiler can emit L7-SDC only through explicit accelerator intent
- runtime rejection has no silent fallback path

Rollback rule:

- disable compiler accelerator emission while preserving runtime fail-closed
  parser/guard tests

## Validation closure evidence - 2026-04-28

Focused gate:

```powershell
dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --filter "L7SdcCompilerEmission|L7SdcCompilerNoRuntimeFallback|L7SdcCompilerLane7Pressure" --no-restore
```

Result: passed, `9/9`, `0` skipped.

Regressions:

- Compiler/DmaStream/Phase09 affected baseline: passed, `1241/1241`.
- Phase 11 MatMul capability/provider/schema/no-legacy-execute baseline:
  passed, `13/13`.
- Phase 10 conflict-manager baseline: passed, `14/14`.

Diagnostics:

```powershell
dotnet run --project TestAssemblerConsoleApps/TestAssemblerConsoleApps.csproj -- matrix-smoke --iterations 200 --telemetry-logs minimal
```

Result: succeeded, `3` child runs, artifacts under
`TestResults/TestAssemblerConsoleApps/20260428_185152_611_matrix-smoke`.

Diagnostics shape note:

- `matrix-smoke` still emits `safety`, `replay-reuse`, and `assistant`.
- The diagnostics profile still does not exercise L7-SDC compiler emission or
  runtime accelerator backend paths.
- No diagnostics shape change was introduced by Phase 12 compiler emission, so
  `Documentation/AsmAppTestResults.md` was not updated for this phase.
