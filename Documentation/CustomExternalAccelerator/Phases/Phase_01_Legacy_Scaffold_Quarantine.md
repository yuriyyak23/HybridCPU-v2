# Phase 01 - Legacy scaffold quarantine confirmation

Status: closed.

Goal:

- Preserve `CustomAcceleratorMicroOp`, `MatMulAccelerator`, custom registry, and
  accelerator DMA seams as fail-closed or metadata-only surfaces.
- Make accidental production use of `ICustomAccelerator.Execute()` impossible
  for future L7-SDC paths.

ISE files likely touched:

- `HybridCPU_ISE/Core/Pipeline/MicroOps/MicroOp.Misc.cs`
- `HybridCPU_ISE/Core/Accelerators/MatMulAccelerator.cs`
- `HybridCPU_ISE/Core/Diagnostics/InstructionRegistry.Accelerators.cs`
- `HybridCPU_ISE/Core/Diagnostics/InstructionRegistry.Types.cs`
- `HybridCPU_ISE/Core/Execution/BurstIO/AcceleratorRuntimeFailClosed.cs`
- `HybridCPU_ISE/Core/Decoder/VliwDecoderV4.cs`
- `HybridCPU_ISE/Core/Decoder/DecodedBundleTransportProjector.cs`

Compiler files likely touched:

- none for execution behavior
- optionally add compiler negative tests proving legacy custom registry metadata
  is not a lowering target

Existing classes/methods to protect:

- `CustomAcceleratorMicroOp.Execute(ref CPU_Core core)`
- `CustomAcceleratorMicroOp.InitializeMetadata()`
- `CustomAcceleratorMicroOp.EmitWriteBackRetireRecords(...)`
- `MatMulAccelerator.Execute(...)`
- `MatMulAccelerator.GetLatency(...)`
- `MatMulAccelerator.GetResourceFootprint(...)`
- `InstructionRegistry.RegisterAccelerator(...)`
- `InstructionRegistry.GetAccelerator(...)`
- `InstructionRegistry.GetAllAccelerators()`
- `InstructionRegistry.CreateUnsupportedCustomAcceleratorException(...)`
- fail-closed accelerator runtime registration/transfer methods in
  `AcceleratorRuntimeFailClosed`

Implementation requirements:

- `CustomAcceleratorMicroOp.Execute` must continue to throw unsupported custom
  accelerator exceptions.
- `MatMulAccelerator.Execute` may remain only in tests, fixtures, or explicitly
  quarantined legacy compatibility code.
- No production L7-SDC path may call `ICustomAccelerator.Execute()`.
- Registry success must not grant decode, placement, execution, backend, token,
  or commit authority.
- Legacy writeback/retire metadata must stay unreachable from a successful
  production execution path.

Tests to add or extend:

- `Phase4ExtensibilityTests`
- `L7SdcLegacyQuarantineTests`
- `L7SdcNoLegacyExecuteBackendTests`

Test cases:

- registered custom accelerator opcode still rejects canonical decode
- direct `CustomAcceleratorMicroOp.Execute` throws
- registry lookup cannot create successful micro-op execution
- `MatMulAccelerator.Execute` does not write memory
- no backend adapter can call `ICustomAccelerator.Execute()` in production
- legacy accelerator DMA registration/transfer remains unsupported

Must not break:

- existing Phase4 extensibility negative controls
- DmaStreamCompute direct-execute fail-closed boundary
- native VLIW-only active frontend

Phase closure validation:

- Run focused tests:

```powershell
dotnet test --filter "Phase4Extensibility|L7SdcLegacyQuarantine|L7SdcNoLegacyExecuteBackend"
```

- Run affected baseline filters:

```powershell
dotnet test --filter "DmaStreamCompute|Phase09|Phase12"
```

- Run diagnostics console:

```powershell
dotnet run --project TestAssemblerConsoleApps/TestAssemblerConsoleApps.csproj -- matrix-smoke --iterations 200 --telemetry-logs minimal
```

- Compare with `Documentation/AsmAppTestResults.md`; any drift in issue width,
  stalls, legality rejects, or branch/system progress is a regression unless
  explained and recorded.

Definition of done:

- all legacy custom surfaces remain fail-closed or fixture-only
- tests fail if a production L7-SDC path calls `ICustomAccelerator.Execute()`

Rollback rule:

- if a quarantine test exposes an authority leak, disable the leaking path
  rather than activating legacy execution
