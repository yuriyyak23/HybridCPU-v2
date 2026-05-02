# Phase 03 - Lane7 system opcode surface

Status: closed.

Goal:

- Add canonical native L7-SDC opcode names as fail-closed system-device
  carriers.
- Ensure every carrier is hard-pinned to lane7 `SystemSingleton`.

ISE files likely touched:

- `HybridCPU_ISE/Core/Common/CPU_Core.Enums.cs`
- `HybridCPU_ISE/Arch/OpcodeInfo.Registry.Data.System.cs`
- `HybridCPU_ISE/Arch/InstructionClassifier.cs`
- `HybridCPU_ISE/Arch/IsaV4Surface.cs`
- `HybridCPU_ISE/Core/Decoder/VliwDecoderV4.cs`
- `HybridCPU_ISE/Core/Decoder/DecodedBundleTransportProjector.cs`
- `HybridCPU_ISE/Core/Pipeline/MicroOps/MicroOp.System.cs`
- `HybridCPU_ISE/Core/Pipeline/MicroOps/SystemDeviceCommandMicroOp.cs`
- `HybridCPU_ISE/Core/Pipeline/Scheduling/SlotClassDefinitions.cs`
- `HybridCPU_ISE/Core/Pipeline/Scheduling/MicroOpScheduler.Admission.cs`
- `HybridCPU_ISE/Core/Pipeline/Scheduling/DeterministicLaneChooser.cs`

Compiler files likely touched:

- `HybridCPU_Compiler/Core/IR/Model/IrInstruction.cs`
- `HybridCPU_Compiler/Core/IR/Model/IrHazardEnums.cs`
- `HybridCPU_Compiler/Core/IR/Hazards/HybridCpuOpcodeSemantics.cs`
- `HybridCPU_Compiler/Core/IR/Hazards/HybridCpuSlotModel.Analysis.cs`
- `HybridCPU_Compiler/Core/IR/Bundling/HybridCpuBundleLowerer.cs`

New opcode names:

- `ACCEL_QUERY_CAPS`
- `ACCEL_SUBMIT`
- `ACCEL_POLL`
- `ACCEL_WAIT`
- `ACCEL_CANCEL`
- `ACCEL_FENCE`

New ISE classes:

- `SystemDeviceCommandMicroOp`
- `AcceleratorQueryCapsMicroOp`
- `AcceleratorSubmitMicroOp`
- `AcceleratorPollMicroOp`
- `AcceleratorWaitMicroOp`
- `AcceleratorCancelMicroOp`
- `AcceleratorFenceMicroOp`

Constructor requirements:

- set `InstructionClass = InstructionClass.System`
- set conservative serialization per opcode
- call `SetHardPinnedPlacement(SlotClass.SystemSingleton, 7)`
- never call `SetClassFlexiblePlacement(...)`
- never use `SlotClass.BranchControl`
- never use `SlotClass.DmaStreamClass`
- default `Execute` path rejects until descriptor/guard/token phases are in
  place

Decoder requirements:

- decode only native `ACCEL_*` opcodes
- reject custom registry opcodes as L7-SDC carriers
- reject lane index not equal to 7
- reject any BranchControl classification
- reject any GenericMicroOp fallback

Tests to add:

- `L7SdcOpcodeSurfaceTests`
- `L7SdcHardPinnedPlacementTests`
- `L7SdcNoBranchControlAuthorityTests`
- compiler slot/legality alignment tests for L7-SDC

Test cases:

- every `ACCEL_*` opcode classifies as `System`
- every `ACCEL_*` micro-op has `HardPinned` pinning kind and pinned lane 7
- `ACCEL_SUBMIT` cannot occupy lane6
- `ACCEL_SUBMIT` cannot use `BranchControl`
- branch/system alias tests still reject illegal same-lane pressure
- direct execution remains unsupported/fail-closed

Must not break:

- W=8 instruction size and slot count
- existing `SystemSingleton` semantics
- `DmaStreamCompute` lane6 placement
- branch/control scheduling correctness

Phase closure validation:

- Run focused tests:

```powershell
dotnet test --filter "L7SdcOpcodeSurface|L7SdcHardPinnedPlacement|L7SdcNoBranchControlAuthority"
```

- Run affected baseline filters:

```powershell
dotnet test --filter "Phase09|CompilerV5ContractAlignment|DmaStreamComputeTypedSlot"
```

- Run diagnostics console:

```powershell
dotnet run --project TestAssemblerConsoleApps/TestAssemblerConsoleApps.csproj -- matrix-smoke --iterations 200 --telemetry-logs minimal
```

- Compare with `Documentation/AsmAppTestResults.md`; specifically inspect
  branch/control/system progress, pinned-constraint NOPs, and legality rejects.

Definition of done:

- native opcodes exist as fail-closed lane7 hard-pinned carriers
- tests fail if any L7-SDC carrier becomes class-flexible or BranchControl

Rollback rule:

- keep opcodes internal/fail-closed or remove compiler exposure if placement
  invariants fail

## Validation closure evidence - 2026-04-28

Focused gate:

```powershell
dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --filter "L7SdcOpcodeSurface|L7SdcHardPinnedPlacement|L7SdcNoBranchControlAuthority" --no-restore
```

Result: passed, `43/43`, `0` skipped.

Affected baseline:

```powershell
dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --filter "Phase09|CompilerV5ContractAlignment|DmaStreamComputeTypedSlot" --no-restore
```

Result: passed, `1244/1244`, `0` skipped.

Diagnostics:

```powershell
dotnet run --project TestAssemblerConsoleApps/TestAssemblerConsoleApps.csproj -- matrix-smoke --iterations 200 --telemetry-logs minimal
```

Result: succeeded, `3` child runs, artifacts under
`TestResults/TestAssemblerConsoleApps/20260428_114428_445_matrix-smoke`.

Diagnostics shape note:

- `matrix-smoke` emits `safety`, `replay-reuse`, and `assistant`, not the older
  10-child default SPEC-like matrix stored above in
  `Documentation/AsmAppTestResults.md`.
- Replay diagnostics include the newer `fallback-to-live-witness`,
  `warmup-misses`, and per-scenario fallback detail fields.
- Assistant diagnostics include the accepted-then-discarded replay invalidation
  scenario and visibility/non-retirement counters.
- This phase did not add descriptor ABI parsing, owner/domain admission, token
  lifecycle, backend execution, staged writes, commit, or compiler L7-SDC
  emission.
