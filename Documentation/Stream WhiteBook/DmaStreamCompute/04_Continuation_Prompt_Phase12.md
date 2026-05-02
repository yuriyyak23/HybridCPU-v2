# Archived Prompt: Phase 12 Full Validation Baseline

Phase 12 is closed as of 2026-04-28. This file is retained as the execution prompt that produced
the closure evidence; it is not an active next-task prompt.

## Role

You are the CPU architect and lead HybridCPU ISE developer. Work at the ISA, native VLIW decode,
typed-slot scheduling, lane6 DMA/stream execution, descriptor ABI, replay/certificate contours,
retire/commit/exception modelling, owner/domain guards, telemetry, compiler contract, and
validation layers.

## Inputs

Workspace: `C:\Users\Yuriy Kurnosov\Desktop\HybridCPU ISE`

Plan: `Documentation\Stream WhiteBook\DmaStreamCompute\`

Runtime baseline: `Documentation\AsmAppTestResults.md`

Status:

- Phases 01-12 are closed.
- The Phase 12 `Full Validation Baseline` task has completed.
- Do not add new semantics. Only fix validation or documentation drift found during Phase 12.
- The worktree may contain unrelated dirty files. Do not revert changes you did not make.
- Read the actual code and tests before editing.

Hard constraints:

- `W=8` fixed: lanes 0-3 ALU, 4-5 LSU, lane6 DMA/stream, lane7 branch/system.
- Native VLIW path only.
- `DmaStreamCompute` is the canonical lane6 descriptor carrier path; direct
  micro-op execution remains fail-closed.
- Descriptor travels as typed sideband metadata; raw scalar fields and reserved bits are not ABI.
- Owner/domain guards run before descriptor acceptance, replay/certificate reuse,
  helper admission, commit, and exception publication.
- Raw `VirtualThreadId` is not authority.
- Telemetry/replay/certificate/token evidence is not authority.
- `CustomAcceleratorMicroOp`, registry accelerators, MatMul fixture, and accelerator DMA seams
  remain fail-closed.
- Scalar, ALU, vector, `GenericMicroOp`, and silent no-op fallback success are forbidden.
- Compatibility modes are explicit: `Compatibility`, `Strict`, `Future`; unknown mode rejects.

## Task

Execute Phase 12 full validation baseline and close evidence refresh.

Steps:

1. Read the current compact docs:
   - `Documentation\Stream WhiteBook\DmaStreamCompute\00_README.md`
   - `Documentation\Stream WhiteBook\DmaStreamCompute\01_Current_Contract.md`
   - `Documentation\Stream WhiteBook\DmaStreamCompute\02_Phase_Evidence_Ledger.md`
   - `Documentation\Stream WhiteBook\DmaStreamCompute\03_Validation_And_Rollback.md`
2. Run required validation:

```powershell
dotnet test ".\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" -v minimal
powershell -ExecutionPolicy Bypass -File ".\build\run-validation-baseline.ps1" -NoRestore
dotnet run --project ".\TestAssemblerConsoleApps\TestAssemblerConsoleApps.csproj" -- --iterations 200
```

3. If failures appear, fix only the root cause without expanding semantics.
4. Compare harness output with `Documentation\AsmAppTestResults.md` and classify every delta.
5. Update only compact docs under `Documentation\Stream WhiteBook\DmaStreamCompute\`
   when evidence or next-step state changes.

Focused checks when any boundary is touched:

```powershell
dotnet test ".\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --filter "FullyQualifiedName~DmaStreamCompute" -v minimal
dotnet test ".\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --filter "FullyQualifiedName~DmaStreamComputeCompilerContract|FullyQualifiedName~DmaStreamComputeIsaEncoding|FullyQualifiedName~Phase12CompilerContractHandshakeTests|FullyQualifiedName~CompilerV5ContractAlignmentTests|FullyQualifiedName~Phase12VliwCompatFreezeTests|FullyQualifiedName~CompilerParallelDecompositionCanonicalContourTests|FullyQualifiedName~Phase09NativeVliwBoundaryDocumentationTests" -v minimal
dotnet test ".\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --filter "FullyQualifiedName~Phase09LegacyTerminologyQuarantineDocumentationTests|FullyQualifiedName~Phase09ClaimSafetyDocumentationTests|FullyQualifiedName~Phase4ExtensibilityTests|FullyQualifiedName~Phase09ReviewerAuditBoundaryDocumentationTests|FullyQualifiedName~Phase09ReviewerRebuttalClaimBoundaryTests" -v minimal
```

Closure criteria:

- required validation commands pass;
- harness aggregate is `Succeeded`;
- no real regressions versus `AsmAppTestResults.md`;
- no lane6/custom-accelerator/scalar/vector/ALU fallback boundary regression;
- docs reflect final validation evidence without reintroducing legacy or misleading claims.
