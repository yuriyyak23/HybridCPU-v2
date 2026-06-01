# StreamEngine And DmaStreamCompute Summary Pack

This directory is a source-backed summary pack for the three related memory/vector
contours requested after the Phase 12 validation baseline:

- `01_StreamEngine_SFR_SRF_VectorALU.md` - StreamEngine, the stream register file
  contour, and VectorALU execution.
- `02_DmaStreamCompute.md` - lane6 descriptor-backed `DmaStreamCompute`
  carrier, Phase 06 materialized runtime contour, explicit direct helper, and
  token/commit contour.
- `03_VDSA_Assist_Warming_Prefetch_SRF_DataIngress.md` - VDSA assist, warming,
  prefetch, SRF, and data-ingress behavior.

The codebase uses `SRF` and `StreamRegisterFile` for the stream register file.
If external notes say `SFR` in this context, this summary treats that as the same
stream-register-file contour unless a future document defines a different SFR
surface explicitly.

The current instruction-side closure and risk record live in
`Documentation/InstructionsRefactor/WhiteBook/`. Read that pack for the
current scalar, atomic, fence, and risk-closure baseline; keep this pack as the
separate stream-engine / DmaStreamCompute / assist summary.

## Authority Rules

These files are summaries, not a new semantic contract. Live code, tests, and the
current WhiteBook DmaStreamCompute contract remain authoritative.

Current anchors:

- `Documentation/InstructionsRefactor/WhiteBook/02_Runtime_Surface_Closure.md`
- `Documentation/InstructionsRefactor/WhiteBook/03_ABI_Decode_MicroOp_Retire_Contract.md`
- `Documentation/InstructionsRefactor/WhiteBook/04_Memory_Atomic_Fence_Model.md`
- `Documentation/InstructionsRefactor/WhiteBook/05_NonExecutable_And_Future_Gates.md`
- `Documentation/InstructionsRefactor/WhiteBook/06_Verification_And_Risk_Closure.md`
- `Documentation/Stream WhiteBook/DmaStreamCompute/01_Current_Contract.md`
- `Documentation/Stream WhiteBook/DmaStreamCompute/02_Phase_Evidence_Ledger.md`
- `Documentation/Refactoring/Phases Ex1/12_Testing_Conformance_And_Documentation_Migration.md`
- `Documentation/Refactoring/Phases Ex1/13_Dependency_Graph_And_Execution_Order.md`

Global invariants preserved by this summary:

- `W=8`: lanes 0-3 ALU, lanes 4-5 LSU, lane6 DMA/stream, lane7 branch/system.
- The active frontend is native VLIW only.
- `DmaStreamCompute` is the lane6 descriptor carrier path; direct micro-op
  execution is open only for the current DSC1 Phase 06 contour and remains
  fail-closed for unsupported descriptor shapes, VMX guest lane binding, DSC2,
  queue lifecycle, async overlap, and coherent DMA/cache.
- Descriptor payload travels as typed sideband metadata. Raw scalar fields,
  reserved bits, and raw VT hints are not ABI or authority.
- Owner/domain guards precede descriptor acceptance, replay/certificate reuse,
  helper admission, commit, and exception publication.
- Telemetry, replay evidence, certificates, and tokens are evidence surfaces, not
  authority.
- Custom accelerators, registry accelerators, MatMul fixtures, and accelerator
  DMA seams remain fail-closed.
- Scalar, ALU, vector, `GenericMicroOp`, and silent no-op fallback are rejected
  for lane6 descriptor-backed compute success.
- Compatibility modes are limited to `Compatibility`, `Strict`, and `Future`.
  Unknown modes reject.
- StreamEngine, SRF, assist warming, DMA helper paths, IOMMU warm helpers,
  prefetch/cache surfaces, and telemetry are downstream evidence or independent
  helper/runtime surfaces. They must not satisfy upstream DSC2, queue lifecycle,
  broad L7, async overlap, coherent DMA/cache, IOMMU-backed execution, or
  production compiler/backend lowering gates.
- Phase12 controls documentation migration. Phase13 is dependency planning only
  and does not approve implementation.

## Validation Anchor

The Phase 12 baseline on 2026-04-28 passed:

- `dotnet test ".\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" -v minimal`
  with 5650 passed and 2 skipped.
- `powershell -ExecutionPolicy Bypass -File ".\build\run-validation-baseline.ps1" -NoRestore`
  with 52/52 suites passed.
- `dotnet run --project ".\TestAssemblerConsoleApps\TestAssemblerConsoleApps.csproj" -- --iterations 200`
  with aggregate `Succeeded`.

The focused DmaStreamCompute/native VLIW/documentation checks also passed during
Phase 12. See
`Documentation/Stream WhiteBook/DmaStreamCompute/02_Phase_Evidence_Ledger.md`
for the compact evidence ledger.

Current live assembler-app risk closure is newer than the archived comparison
log: the observed profile is `250` iterations, `11` child runs, and
`stream-vector` passed. Keep comparing against
`Documentation/AsmAppTestResults.md`; the live profile drift is expected while
the aggregate status stays `Succeeded`.

## Reading Order

Read this package in dependency order:

1. StreamEngine/SRF/VectorALU, because it defines the older raw stream/vector
   execution and SRF warming surfaces.
2. DmaStreamCompute, because it explains the descriptor-backed lane6 carrier and
   explicit helper path, and why it must not fall back to StreamEngine or
   VectorALU success.
3. VDSA assist, because it shares lane6 and SRF carrier resources with stream
   execution but remains assist-only, non-retiring, and architecturally invisible.
4. `Documentation/InstructionsRefactor/WhiteBook/00_README.md`, because it is
   the current instruction-side closure reference for scalar, atomic, fence,
   and risk-closure boundaries.
