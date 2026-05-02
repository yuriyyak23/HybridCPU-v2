# Phase 11 - MatMul capability provider migration

Status: closed on 2026-04-28.

Goal:

- Migrate MatMul from legacy fixture shape into a capability provider,
  descriptor schema, resource model, and fake backend test contour.
- Keep legacy `MatMulAccelerator.Execute` out of production L7-SDC paths.

ISE files likely touched:

- `HybridCPU_ISE/Core/Accelerators/MatMulAccelerator.cs`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Capabilities/MatMul*`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Descriptors/MatMul*`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Backends/FakeMatMul*`
- `HybridCPU_ISE/Core/Diagnostics/InstructionRegistry.Accelerators.cs`

Compiler files likely touched:

- compiler accelerator intent/type model if MatMul lowering is introduced

New ISE types:

- `MatMulCapabilityProvider`
- `MatMulDescriptor`
- `MatMulDescriptorValidator`
- `MatMulResourceModel`
- `MatMulLayoutFlags`
- `MatMulDatatypeTriple`
- `FakeMatMulExternalAcceleratorBackend`

Methods to design:

- `MatMulCapabilityProvider.GetCapabilities()`
- `MatMulDescriptorValidator.ValidateShape(...)`
- `MatMulDescriptorValidator.ValidateStrides(...)`
- `MatMulDescriptorValidator.ValidateDatatypes(...)`
- `MatMulDescriptorValidator.NormalizeFootprints(...)`
- `MatMulResourceModel.EstimateLatencyCycles(...)`
- `MatMulResourceModel.EstimateScratchBytes(...)`
- fake backend staged result generation method, explicitly not using
  `ICustomAccelerator.Execute()`

MatMul descriptor fields:

- `A_base`
- `B_base`
- `C_base`
- `M`
- `N`
- `K`
- `lda`
- `ldb`
- `ldc`
- `tile_m`
- `tile_n`
- `tile_k`
- `input_datatype`
- `accumulator_datatype`
- `output_datatype`
- `layout_flags`
- `partial_policy = AllOrNone`

Migration invariants:

- `MatMulAccelerator.Execute` may remain only in tests/fixtures/quarantine
- no production L7-SDC backend calls `ICustomAccelerator.Execute()`
- MatMul capability metadata is not execution authority
- fake backend writes staged buffers only
- old fixture result `matC_addr` is not proof of memory compute

Tests to add:

- `L7SdcMatMulCapabilityTests`
- `L7SdcMatMulDescriptorTests`
- `L7SdcMatMulNoLegacyExecuteTests`

Test cases:

- capability query advertises MatMul without granting submit authority
- valid shape descriptor accepted after guard
- unsupported shape rejects
- unsupported datatype triple rejects
- invalid stride/layout rejects
- partial policy other than `AllOrNone` rejects
- fake MatMul stages result only
- production path cannot call legacy execute

Must not break:

- `Phase4ExtensibilityTests`
- custom registry quarantine
- DmaStreamCompute stream compute path

Phase closure validation:

- Run focused tests:

```powershell
dotnet test --filter "L7SdcMatMulCapability|L7SdcMatMulDescriptor|L7SdcMatMulNoLegacyExecute"
```

- Run affected baseline filters:

```powershell
dotnet test --filter "Phase4Extensibility|DmaStreamCompute|CompilerV5ContractAlignment"
```

- Run diagnostics console:

```powershell
dotnet run --project TestAssemblerConsoleApps/TestAssemblerConsoleApps.csproj -- matrix-smoke --iterations 200 --telemetry-logs minimal
```

- Compare with `Documentation/AsmAppTestResults.md`; MatMul capability metadata
  should not change baseline diagnostics unless an explicit L7-SDC workload is
  added and documented.

Definition of done:

- MatMul is capability + descriptor + resource model
- legacy MatMul execute remains fixture-only and non-architectural

Rollback rule:

- unregister `MatMulCapabilityProvider` and keep legacy fixture quarantine if
  schema/backend tests fail

## Validation closure evidence - 2026-04-28

Focused gate:

```powershell
dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --filter "L7SdcMatMulCapability|L7SdcMatMulDescriptor|L7SdcMatMulNoLegacyExecute" --no-restore
```

Result: passed, `13/13`, `0` skipped.

Regressions:

- Phase 04/DmaStream/compiler affected baseline: passed, `268/268`.
- Phase 10 conflict-manager baseline: passed, `14/14`.
- Phase 08/09 commit, rollback, SRF/cache invalidation, poll/wait/cancel/fence,
  and fault-publication baseline: passed, `37/37`.
- Phase 09/Phase 12/compiler affected baseline: passed, `1451/1451`.

Diagnostics:

```powershell
dotnet run --project TestAssemblerConsoleApps/TestAssemblerConsoleApps.csproj -- matrix-smoke --iterations 200 --telemetry-logs minimal
```

Result: succeeded, `3` child runs, artifacts under
`TestResults/TestAssemblerConsoleApps/20260428_183420_259_matrix-smoke`.

Diagnostics shape note:

- `matrix-smoke` still emits `safety`, `replay-reuse`, and `assistant`.
- The diagnostics profile still does not exercise L7-SDC backend, descriptor
  ABI parser, token lifecycle, staged write commit, or compiler emission.
- No diagnostics shape change was introduced by Phase 11 MatMul metadata,
  descriptor validation, or fake-backend test contour, so
  `Documentation/AsmAppTestResults.md` was not updated.
