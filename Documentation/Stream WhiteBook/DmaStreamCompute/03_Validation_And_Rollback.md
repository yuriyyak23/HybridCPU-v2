# Validation And Rollback

## Phase 12 Required Commands

```powershell
dotnet test ".\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" -v minimal
powershell -ExecutionPolicy Bypass -File ".\build\run-validation-baseline.ps1" -NoRestore
dotnet run --project ".\TestAssemblerConsoleApps\TestAssemblerConsoleApps.csproj" -- --iterations 200
```

## Useful Focused Commands

Run these when validating a fix or proving no Phase 10/11 drift:

```powershell
dotnet test ".\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --filter "FullyQualifiedName~DmaStreamCompute" -v minimal
dotnet test ".\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --filter "FullyQualifiedName~DmaStreamComputeCompilerContract|FullyQualifiedName~DmaStreamComputeIsaEncoding|FullyQualifiedName~Phase12CompilerContractHandshakeTests|FullyQualifiedName~CompilerV5ContractAlignmentTests|FullyQualifiedName~Phase12VliwCompatFreezeTests|FullyQualifiedName~CompilerParallelDecompositionCanonicalContourTests|FullyQualifiedName~Phase09NativeVliwBoundaryDocumentationTests" -v minimal
dotnet test ".\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --filter "FullyQualifiedName~Phase09LegacyTerminologyQuarantineDocumentationTests|FullyQualifiedName~Phase09ClaimSafetyDocumentationTests|FullyQualifiedName~Phase4ExtensibilityTests|FullyQualifiedName~Phase09ReviewerAuditBoundaryDocumentationTests|FullyQualifiedName~Phase09ReviewerRebuttalClaimBoundaryTests" -v minimal
```

Run these when validating Ex1 WhiteBook claim-safety or dependency-order drift:

```powershell
dotnet test ".\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --filter "FullyQualifiedName~Ex1Phase12ConformanceMigrationTests|FullyQualifiedName~Ex1Phase13DependencyOrderTests|FullyQualifiedName~L7SdcDocumentationClaimSafetyTests" -v minimal
dotnet test ".\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --filter "FullyQualifiedName~DmaStreamCompute|FullyQualifiedName~L7Sdc|FullyQualifiedName~CompilerBackendLoweringPhase11Tests|FullyQualifiedName~CachePrefetchNonCoherentPhase09Tests|FullyQualifiedName~AddressingBackendResolverPhase06Tests|FullyQualifiedName~GlobalMemoryConflictServicePhase05Tests" -v minimal
dotnet run --project ".\TestAssemblerConsoleApps\TestAssemblerConsoleApps.csproj" -- matrix-smoke --iterations 200 --telemetry-logs minimal
```

## Harness Comparison

Compare `TestAssemblerConsoleApps` output with:

```text
Documentation/AsmAppTestResults.md
```

Classify every delta as:

- allowed run artifact;
- expected additive diagnostic detail;
- real regression.

Real regressions block closure.

## Phase 12 Closure Evidence

Captured on 2026-04-28:

- `dotnet test ".\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" -v minimal`:
  `5650` passed, `2` skipped, `0` failed.
- `powershell -ExecutionPolicy Bypass -File ".\build\run-validation-baseline.ps1" -NoRestore`:
  `52/52` passed.
- `dotnet run --project ".\TestAssemblerConsoleApps\TestAssemblerConsoleApps.csproj" -- --iterations 200`:
  aggregate `Succeeded`, 10 child runs.
- Focused contours after validation drift fixes:
  `DmaStreamCompute` `107/107`, compiler/ISA/native VLIW `205/205`, legacy/quarantine docs
  `40/40`.

Harness deltas versus `Documentation/AsmAppTestResults.md` were classified as:

- allowed run artifacts: elapsed time, artifact roots, timestamps;
- expected additive diagnostic detail: replay fallback/warmup fields, assistant
  visibility/non-retirement detail, minimal telemetry logging, non-interactive final `Done.`;
- real regressions: none.

## Rollback Rules

- Do not revert unrelated dirty worktree files.
- Do not delete transitional fail-closed seams.
- Do not weaken owner/domain guards.
- Do not reinterpret telemetry/replay/certificates/tokens as authority.
- If validation exposes execution risk, disable the risky execution path while preserving
  descriptor parsing, lane6 typed-slot facts, guard checks, token commit shape, and fail-closed
  behavior.

## Global Fail Conditions

- Full test suite or smoke baseline fails without a classified reason.
- Harness aggregate status is not `Succeeded`.
- Lane6 behaves as ALU/vector/scalar fallback.
- `DmaStreamCompute` succeeds through custom accelerator registry.
- Raw reserved bits or raw VT hints become authority.
- Direct DMA destination writes become commit.
- `DmaStreamComputeMicroOp.Execute(...)` stops throwing fail-closed.
- `DmaStreamComputeRuntime.ExecuteToCommitPending(...)` is wired into the
  canonical micro-op execution path without an explicit architecture decision.
- Runtime/helper memory stops using exact physical main memory bounds checks.
- DSC2 parser-only evidence is described as executable DSC2, token issue,
  memory publication, IOMMU-backed execution, or production compiler/backend
  lowering.
- Phase13 dependency graph is treated as implementation approval.
- Helper/parser/model/fake-backend tests are treated as ISA execution tests.
- Public enum values or reject reasons are renumbered.
- Legacy docs/comments reintroduce MatMul/custom-accelerator/HRoT active-authority claims.
