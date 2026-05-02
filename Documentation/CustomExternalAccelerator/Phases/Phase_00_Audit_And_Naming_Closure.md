# Phase 00 - Audit and naming closure

Status: closed.

Goal:

- Freeze the architectural name as `HybridCPU L7-SDC: Lane7 System Device
  Command Model for External Accelerators`.
- Record the current live-code and documentation surfaces before any code
  migration.
- Record that the old requested `Documentation/MemoryAccelerators` input name is
  provenance only; the live repository inputs are `Documentation/Stream WhiteBook/DmaStreamCompute`
  and `Documentation/Stream WhiteBook/StreamEngine DmaStreamCompute`.

ISE files to inspect:

- `HybridCPU_ISE/Core/Pipeline/Scheduling/SlotClassDefinitions.cs`
- `HybridCPU_ISE/Core/Pipeline/MicroOps/MicroOp.Misc.cs`
- `HybridCPU_ISE/Core/Accelerators/MatMulAccelerator.cs`
- `HybridCPU_ISE/Core/Diagnostics/InstructionRegistry.Accelerators.cs`
- `HybridCPU_ISE/Core/Pipeline/MicroOps/DmaStreamComputeMicroOp.cs`
- `HybridCPU_ISE/Core/Execution/DmaStreamCompute/*`
- `HybridCPU_ISE/Core/Execution/StreamEngine/*`
- `HybridCPU_ISE/Core/Pipeline/Assist/AssistMicroOp.cs`
- `HybridCPU_ISE/Core/Pipeline/Core/CPU_Core.Assist.cs`

Compiler files to inspect:

- `HybridCPU_Compiler/Core/IR/*`
- `HybridCPU_Compiler/Core/IR/Hazards/*`
- `HybridCPU_Compiler/Core/IR/Bundling/HybridCpuBundleLowerer.cs`
- `HybridCPU_Compiler/API/Threading/HybridCpuThreadCompilerContext.cs`

Documentation files to inspect:

- `Documentation/CustomExternalAccelerator/Ideas/*`
- `Documentation/Stream WhiteBook/DmaStreamCompute/00_README.md`
- `Documentation/Stream WhiteBook/DmaStreamCompute/01_Current_Contract.md`
- `Documentation/Stream WhiteBook/DmaStreamCompute/02_Phase_Evidence_Ledger.md`
- `Documentation/Stream WhiteBook/DmaStreamCompute/03_Validation_And_Rollback.md`
- `Documentation/Stream WhiteBook/StreamEngine DmaStreamCompute/00_README.md`
- `Documentation/Stream WhiteBook/StreamEngine DmaStreamCompute/01_StreamEngine_SFR_SRF_VectorALU.md`
- `Documentation/Stream WhiteBook/StreamEngine DmaStreamCompute/02_DmaStreamCompute.md`
- `Documentation/Stream WhiteBook/StreamEngine DmaStreamCompute/03_VDSA_Assist_Warming_Prefetch_SRF_DataIngress.md`

Classes and methods to cite in the audit:

- `CustomAcceleratorMicroOp.Execute`
- `CustomAcceleratorMicroOp.InitializeMetadata`
- `MatMulAccelerator.Execute`
- `InstructionRegistry.RegisterAccelerator`
- `InstructionRegistry.CreateUnsupportedCustomAcceleratorException`
- `DmaStreamComputeMicroOp.Execute`
- `DmaStreamComputeDescriptorParser.Parse`
- `DmaStreamComputeToken.Commit`
- `StreamEngine` BurstIO read/write helpers
- `StreamRegisterFile` window invalidation methods
- `AssistMicroOp` admission/visibility behavior

Required conclusions:

- L7-SDC is the final model name.
- `SystemSingleton` lane7 is the carrier class.
- The implementation must use `SetHardPinnedPlacement(SlotClass.SystemSingleton, 7)`.
- `BranchControl` is not external accelerator authority.
- `DmaStreamCompute` remains lane6 `DmaStreamClass`.
- Legacy custom accelerator surfaces remain quarantined.

Must not break:

- no runtime behavior changes
- no enum/opcode allocation
- no registry semantics changes
- no test baseline changes

Focused tests:

- no new runtime tests are required in this phase
- add documentation or claim-safety tests only if the test suite already checks
  architecture docs

Phase closure validation:

- Run documentation claim-safety tests if modified.
- Run affected baseline filters if any doc tests changed:

```powershell
dotnet test --filter "Phase09|Phase12|Phase4Extensibility"
```

- Run diagnostics console:

```powershell
dotnet run --project TestAssemblerConsoleApps/TestAssemblerConsoleApps.csproj -- matrix-smoke --iterations 200 --telemetry-logs minimal
```

- Compare diagnostics output with `Documentation/AsmAppTestResults.md`.
- Record any intentional documentation-only no-op result in
  `Documentation/AsmAppTestResults.md` only if the repository convention expects
  a fresh run record.

Definition of done:

- audit conclusions are recorded in the L7-SDC spec
- no document claims legacy custom execution is active architecture
- no document binds external accelerator authority to `BranchControl`

Rollback rule:

- revert only new L7-SDC documentation if naming is rejected
- do not touch runtime code or unrelated worktree changes
