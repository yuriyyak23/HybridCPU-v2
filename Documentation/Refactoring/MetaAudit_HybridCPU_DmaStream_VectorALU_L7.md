# Meta-audit of HybridCPU architecture audit

Scope: verification of `Documentation/Refactoring/Аудит архитектуры HybridCPU.md`
against current code, tests, and architecture documentation for
`DmaStreamCompute`, `StreamEngine`, `VectorALU`, `ExternalAccelerators/L7`,
memory ordering, cache/coherency, and compiler/backend lowering.

Method note: this is a source-level audit. Tests were inspected as evidence; no
test run was performed during this meta-audit.

## 1. Executive verdict

**Audit conclusions mostly confirmed with corrections.**

The previous audit correctly identifies the main architecture pressure points:
retire/publication is split by subsystem, DSC runtime compute is independent
from `VectorALU`, descriptor-backed DSC has no executable StreamEngine binding,
memory/addressing authority is fragmented, conflict/cache/coherency remain
future-gated, L7 model surfaces are rich enough to invite evidence inversion,
compiler/backend production lowering is blocked, progress diagnostics are
non-authoritative, and StreamEngine's DMA helper is synchronous.

The required correction is mostly one of precision. Several findings are not
current executable bugs because the repository actively preserves fail-closed
behavior, descriptor/execution separation, no hidden fallback, and conformance
gates. They should be classified as **future architecture risks before
executable DSC/L7/async DMA/coherent cache/compiler production lowering**, not
as proof that current ISA execution is already inconsistent.

## 2. Findings first

### High

#### A2 - DSC runtime compute is not just "potentially" duplicated

- **File/line references:** `HybridCPU_ISE/Core/Execution/DmaStreamCompute/DmaStreamComputeRuntime.cs:243`,
  `HybridCPU_ISE/Core/Execution/DmaStreamCompute/DmaStreamComputeRuntime.cs:297`,
  `HybridCPU_ISE/Core/Execution/DmaStreamCompute/DmaStreamComputeRuntime.cs:344`,
  `HybridCPU_ISE/Core/Execution/DmaStreamCompute/DmaStreamComputeRuntime.cs:387`,
  `HybridCPU_ISE/Core/Pipeline/MicroOps/VectorMicroOps.Compute.cs:144`,
  `HybridCPU_ISE/Core/Pipeline/MicroOps/VectorMicroOps.Compute.cs:453`,
  `HybridCPU_ISE/Core/Pipeline/MicroOps/VectorMicroOps.Compute.cs:654`,
  `HybridCPU_ISE/Core/Execution/Compute/VectorALU.cs:53`,
  `HybridCPU_ISE/Core/Execution/Compute/VectorALU.cs:145`,
  `HybridCPU_ISE/Core/Execution/Compute/VectorALU.FMA.cs:25`.
- **Previous audit claimed:** `DmaStreamComputeRuntime` and `VectorALU`
  potentially duplicate compute semantics.
- **Correctness assessment:** confirmed, and slightly understated. DSC runtime
  implements Copy/Add/Mul/Fma/Reduce itself, including integer `unchecked`
  arithmetic and floating FMA as `(a * b) + c`. Vector paths delegate to
  `VectorALU`, which tracks exception counters/modes and uses fused FMA helpers.
- **Why this matters:** if DSC becomes executable, the helper can become a
  second semantic source for integer overflow, FP flags, rounding, FMA, and
  reduction behavior.
- **Required correction or evidence:** keep it model/helper-only now. Before any
  executable DSC gate closes, add an ADR deciding whether `VectorALU` is the
  compute authority or add a DSC-vs-VectorALU conformance matrix with negative
  cases for FMA, overflow, NaN/Inf, reductions, and exception publication.

#### A1/A5/A7 - Publication, conflict, and L7 authority remain split despite rich model surfaces

- **File/line references:** `HybridCPU_ISE/Core/Execution/DmaStreamCompute/DmaStreamComputeRetirePublication.cs:15`,
  `HybridCPU_ISE/Core/Execution/DmaStreamCompute/DmaStreamComputeRetirePublication.cs:187`,
  `HybridCPU_ISE/Core/Execution/StreamEngine/StreamEngine.cs:103`,
  `HybridCPU_ISE/Core/Pipeline/MicroOps/SystemDeviceCommandMicroOp.cs:19`,
  `HybridCPU_ISE/Core/Execution/MemoryOrdering/GlobalMemoryConflictService.cs:682`,
  `HybridCPU_ISE/Core/Execution/MemoryOrdering/GlobalMemoryConflictService.cs:1058`,
  `Documentation/Stream WhiteBook/ExternalAccelerators/06_Backend_Staging_Commit_And_Rollback.md:43`,
  `Documentation/Stream WhiteBook/ExternalAccelerators/07_Memory_Conflict_Model.md:61`.
- **Previous audit claimed:** no unified retire/publication authority;
  `GlobalMemoryConflictService` is conceptual; L7 APIs are rich and risk
  evidence inversion.
- **Correctness assessment:** mostly confirmed, but "conceptual" is too strong
  for `GlobalMemoryConflictService`. There is implemented foundation with
  absent/passive/enforcing modes. The correction is that it is **not installed
  as global CPU load/store/atomic authority** under the current contract.
- **Why this matters:** model token commit, StreamEngine scalar/predicate retire
  windows, VectorALU exception tracking, L7 commit coordinator, and conflict
  observations can look like publication authority unless the boundary remains
  explicit.
- **Required correction or evidence:** add an ADR for the future global
  retire/publication/conflict authority before executable DSC/L7 or overlap
  claims. Add negative tests that CPU load/store/atomic paths do not silently
  acquire global-conflict behavior until the gate explicitly changes.

#### A4/A6 - Address-space and cache claims are correctly blocked but are P0 prerequisites for device execution

- **File/line references:** `HybridCPU_ISE/Core/Execution/DmaStreamCompute/DmaStreamAcceleratorBackend.cs:38`,
  `HybridCPU_ISE/Core/Execution/DmaStreamCompute/DmaStreamAcceleratorBackend.cs:89`,
  `HybridCPU_ISE/Core/Execution/StreamEngine/StreamEngine.BurstIO.cs:19`,
  `HybridCPU_ISE/Core/Execution/BurstIO/IOMMUBurstBackend.cs:6`,
  `HybridCPU_ISE/Core/Cache/CPU_Core.Cache.cs:207`,
  `HybridCPU_ISE/Core/Cache/CPU_Core.Cache.cs:221`,
  `HybridCPU_ISE.Tests/tests/AddressingBackendResolverPhase06Tests.cs:118`,
  `HybridCPU_ISE.Tests/tests/AddressingBackendResolverPhase06Tests.cs:166`.
- **Previous audit claimed:** memory/addressing is fragmented; cache/coherency is
  explicit non-coherent observer/invalidation and dirty/writeback is not closed.
- **Correctness assessment:** confirmed. DSC helper and commit use physical main
  memory. StreamEngine has backend/IOMMU infrastructure. L7 has guarded model
  memory. Cache surfaces explicitly avoid claiming coherent DMA/cache hierarchy,
  and data-cache dirty proof currently fails closed/no-ops rather than proving
  writeback.
- **Why this matters:** executable DSC/L7 or async DMA would need one
  address-space, ordering, invalidation, dirty-line, and writeback contract.
- **Required correction or evidence:** no code fix for current fail-closed
  behavior. P0 before executable device paths: `AddressSpaceExecutionContract`
  ADR plus cache/dirty/writeback ADR or an explicit proof that device-visible CPU
  dirty data cannot exist.

### Medium

#### A3 - Missing DSC-to-StreamEngine binding is intentional isolation, not a current defect

- **File/line references:** `HybridCPU_ISE/Core/Pipeline/MicroOps/DmaStreamComputeMicroOp.cs:7`,
  `HybridCPU_ISE/Core/Pipeline/MicroOps/DmaStreamComputeMicroOp.cs:92`,
  `HybridCPU_ISE/Core/Execution/DmaStreamCompute/DmaStreamComputeRuntime.cs:38`,
  `HybridCPU_ISE/Core/Pipeline/MicroOps/DmaStreamComputeMicroOp.cs:170`.
- **Previous audit claimed:** no formal bridge between descriptor-backed DSC and
  live StreamEngine/VectorALU execution.
- **Correctness assessment:** confirmed with correction. There is no descriptor
  to `StreamExecutionRequest` path and no hidden StreamEngine/DMA fallback. That
  is a deliberate fail-closed design isolation under the current contract.
- **Why this matters:** it prevents accidental execution claims. It becomes debt
  only when an executable DSC gate is proposed.
- **Required correction or evidence:** future ADR only: define whether DSC
  executes through StreamEngine/VectorALU, a separate DSC engine, or remains
  non-executable. Until then, no implementation fix is needed.

#### A8 - Compiler/backend capability model is sufficient for prohibition, coarse for future production

- **File/line references:** `HybridCPU_Compiler/Core/IR/Model/CompilerBackendLoweringContract.cs:6`,
  `HybridCPU_Compiler/Core/IR/Model/CompilerBackendLoweringContract.cs:100`,
  `HybridCPU_Compiler/Core/IR/Model/CompilerBackendLoweringContract.cs:161`,
  `HybridCPU_Compiler/Core/IR/Model/IrAcceleratorModels.cs:152`,
  `HybridCPU_Compiler/Core/IR/Model/IrAcceleratorModels.cs:195`,
  `HybridCPU_Compiler/Core/IR/Bundling/HybridCpuBundleLowerer.cs:33`,
  `HybridCPU_ISE.Tests/tests/Ex1Phase12ConformanceMigrationTests.cs:241`.
- **Previous audit claimed:** compiler/backend capability model is too coarse for
  future production lowering.
- **Correctness assessment:** partially confirmed. The current contract is not
  too coarse for its current job: it rejects non-production, parser-only,
  descriptor-only, model/test-helper, hardware-coherence, and partial-completion
  evidence. For future production lowering, however, the requirements will need
  finer decomposition across address space, ordering, cache, fault, result
  publication, token/rd semantics, cancel/fence/poll/wait, and FP determinism.
- **Why this matters:** sideband emission and carrier projection can otherwise
  be mistaken for executable lowering.
- **Required correction or evidence:** before production lowering, add a
  capability decomposition matrix and negative tests proving each future
  requirement cannot be satisfied by parser/model/fake/backend-infra evidence.

#### A9 - Progress diagnostics are closed now; the trap is future semantic drift

- **File/line references:** `HybridCPU_ISE/Core/Execution/DmaStreamCompute/DmaStreamComputeToken.cs:106`,
  `HybridCPU_ISE/Core/Execution/DmaStreamCompute/DmaStreamComputeDescriptorParser.cs:260`,
  `HybridCPU_ISE/Core/Execution/DmaStreamCompute/DmaStreamComputeToken.cs:898`,
  `Documentation/Stream WhiteBook/StreamEngine DmaStreamCompute/02_DmaStreamCompute.md:424`,
  `HybridCPU_ISE.Tests/tests/DmaStreamComputeAllOrNonePhase08Tests.cs`.
- **Previous audit claimed:** progress diagnostics may create a future trap
  around partial completion.
- **Correctness assessment:** partially confirmed. Current progress diagnostics
  explicitly state they are not authoritative and cannot publish memory. DSC1
  rejects non-AllOrNone policy; token validation requires AllOrNone. The danger
  is future drift if diagnostics or poll/wait/fence are promoted into memory
  publication without a new contract.
- **Why this matters:** partial-success language can invert all-or-none commit
  guarantees.
- **Required correction or evidence:** no current code fix. Add a future ADR and
  negative tests before any successful-partial-completion or progress-as-authority
  claim.

#### A10 - StreamEngine DMA helper is synchronous and should stay explicitly labeled

- **File/line references:** `HybridCPU_ISE/Core/Execution/StreamEngine/StreamEngine.BurstIO.cs:19`,
  `HybridCPU_ISE/Core/Execution/StreamEngine/StreamEngine.BurstIO.cs:328`,
  `HybridCPU_ISE/Core/Execution/StreamEngine/StreamEngine.BurstIO.cs:377`,
  `HybridCPU_ISE/Core/Execution/StreamEngine/StreamEngine.BurstIO.cs:538`,
  `HybridCPU_ISE/Core/Execution/StreamEngine/StreamEngine.BurstIO.cs:578`.
- **Previous audit claimed:** StreamEngine DMA helper looks like DMA but is
  architecturally synchronous and does not prove async overlap.
- **Correctness assessment:** confirmed. Reads and writes drive
  `DMAController.ExecuteCycle` synchronously before returning. The write path
  writes memory first for the DMA source buffer, then performs DMA bookkeeping.
- **Why this matters:** "DMA" naming can be used downstream to claim overlap,
  ordering, or coherence that is not implemented.
- **Required correction or evidence:** add or preserve claim-safety tests/docs
  that say this helper is synchronous and no async CPU/DMA overlap is current
  behavior.

### Low

#### Phase12/Phase13 already mitigate much of the claim drift risk

- **File/line references:** `Documentation/Refactoring/Phases Ex1/00_Index_And_Architecture_Baseline.md:9`,
  `Documentation/Refactoring/Phases Ex1/00_Index_And_Architecture_Baseline.md:70`,
  `Documentation/Refactoring/Phases Ex1/00_Index_And_Architecture_Baseline.md:117`,
  `Documentation/Refactoring/Phases Ex1/01_Current_Contract_Lock.md:26`,
  `Documentation/Refactoring/Phases Ex1/01_Current_Contract_Lock.md:63`,
  `HybridCPU_ISE.Tests/tests/Ex1Phase12ConformanceMigrationTests.cs:116`,
  `HybridCPU_ISE.Tests/tests/Ex1Phase13DependencyOrderTests.cs:58`,
  `HybridCPU_ISE.Tests/tests/Ex1Phase13DependencyOrderTests.cs:86`.
- **Previous audit claimed:** several areas risk downstream evidence inversion.
- **Correctness assessment:** confirmed, but with important mitigation already in
  place. Phase12 and Phase13 tests/documents explicitly block forbidden current
  claims and state that dependency graphs are planning only.
- **Why this matters:** the previous audit should not ignore existing
  claim-safety coverage when assigning severity.
- **Required correction or evidence:** maintain the claim-safety tests and extend
  them when new docs mention async DMA, coherent cache, executable DSC/L7,
  partial completion, or compiler production lowering.

## 3. Point-by-point validation matrix

| ID | Previous audit claim | Status | Evidence docs/code/tests | Overstatement? | Understatement? | Corrected wording | Required action |
|---|---|---|---|---|---|---|---|
| A1 | No unified retire/publication authority for DSC, StreamEngine, VectorALU, and L7. | **Partially confirmed** | DSC fail-closed carrier and future-retire seam: `HybridCPU_ISE/Core/Pipeline/MicroOps/DmaStreamComputeMicroOp.cs:92`, `HybridCPU_ISE/Core/Execution/DmaStreamCompute/DmaStreamComputeRetirePublication.cs:187`; StreamEngine scalar/predicate retire windows: `HybridCPU_ISE/Core/Execution/StreamEngine/StreamEngine.cs:103`; L7 fail-closed carrier: `HybridCPU_ISE/Core/Pipeline/MicroOps/SystemDeviceCommandMicroOp.cs:19`; Phase12/13 claim gates: `HybridCPU_ISE.Tests/tests/Ex1Phase12ConformanceMigrationTests.cs:185`, `HybridCPU_ISE.Tests/tests/Ex1Phase13DependencyOrderTests.cs:58`. | Yes, if read as "no retire model exists". Phase04 has a DSC future-retire model. | The real risk is cross-subsystem unification before executable device paths. | No installed unified executable retire/publication authority spans DSC, StreamEngine, VectorALU, L7, faults, and memory publication. | P0 ADR plus conformance matrix before executable DSC/L7 or async overlap. |
| A2 | `DmaStreamComputeRuntime` and `VectorALU` potentially duplicate compute semantics. | **Confirmed** | DSC operations: `HybridCPU_ISE/Core/Execution/DmaStreamCompute/DmaStreamComputeRuntime.cs:243`, `:297`, `:344`, `:387`; Vector paths delegate to `VectorALU`: `HybridCPU_ISE/Core/Pipeline/MicroOps/VectorMicroOps.Compute.cs:144`, `:453`, `:654`; VectorALU exception/FMA semantics: `HybridCPU_ISE/Core/Execution/Compute/VectorALU.cs:53`, `HybridCPU_ISE/Core/Execution/Compute/VectorALU.FMA.cs:25`. | No. | Yes. FMA and exception/rounding behavior are concretely divergent surfaces, not merely hypothetical. | DSC runtime is an independent model/helper compute implementation and must not be treated as executable compute authority. | P0 ADR or DSC-vs-VectorALU conformance tests before executable DSC. |
| A3 | No formal bridge from descriptor-backed DSC to live StreamEngine/VectorALU execution. | **Confirmed** | `DmaStreamComputeMicroOp.Execute` throws: `HybridCPU_ISE/Core/Pipeline/MicroOps/DmaStreamComputeMicroOp.cs:92`; runtime not wired to hidden StreamEngine/DMA path: `HybridCPU_ISE/Core/Execution/DmaStreamCompute/DmaStreamComputeRuntime.cs:38`; lane6 resource mask is metadata, not binding: `HybridCPU_ISE/Core/Pipeline/MicroOps/DmaStreamComputeMicroOp.cs:170`. | Slightly, if framed as current defect. | No. | There is intentionally no executable DSC-to-StreamEngine/VectorALU binding under the current fail-closed contract. | Future ADR only; no current code fix. |
| A4 | Memory/addressing model is fragmented across physical DSC helper, StreamEngine backend/IOMMU, and L7 guarded model memory. | **Confirmed** | Physical DSC backend: `HybridCPU_ISE/Core/Execution/DmaStreamCompute/DmaStreamAcceleratorBackend.cs:38`, `:89`; StreamEngine backend/IOMMU: `HybridCPU_ISE/Core/Execution/StreamEngine/StreamEngine.BurstIO.cs:19`, `HybridCPU_ISE/Core/Execution/BurstIO/IOMMUBurstBackend.cs:6`; resolver no-fallback tests: `HybridCPU_ISE.Tests/tests/AddressingBackendResolverPhase06Tests.cs:118`, `:166`; L7 authority docs: `Documentation/Stream WhiteBook/ExternalAccelerators/04_Authority_Model.md:5`. | No. | The address-space contract is P0 for executable device paths. | Current memory behavior is split by explicitly gated subsystem; no single executable device address-space contract exists. | P0 address-space/mapping/guard ADR before executable DSC/L7/IOMMU claims. |
| A5 | `GlobalMemoryConflictService` is conceptual; current managers are not global CPU load/store authority. | **Partially confirmed** | Implemented modes: `HybridCPU_ISE/Core/Execution/MemoryOrdering/GlobalMemoryConflictService.cs:682`; active/passive observations: `:691`, `:712`; unresolved address-space handling: `:833`; absent current behavior unchanged: `:1058`; L7 conflict docs: `Documentation/Stream WhiteBook/ExternalAccelerators/07_Memory_Conflict_Model.md:7`. | Yes. "Conceptual" understates implemented foundation. | No, but CPU load/store hook absence is the key point. | Global service exists as foundation, but it is not installed as current global CPU load/store/atomic authority. | Doc wording correction; P0 hook/negative tests before overlap/executable device ordering. |
| A6 | Cache/coherency is explicit non-coherent observer/invalidation; dirty/writeback story is open. | **Confirmed** | Data-cache dirty/writeback fail-closed: `HybridCPU_ISE/Core/Cache/CPU_Core.Cache.cs:207`; invalidation/flush stubs: `HybridCPU_ISE/Core/Cache/CPU_Core.Cache.cs:221`; Phase09 coverage listed by Phase12: `HybridCPU_ISE.Tests/tests/Ex1Phase12ConformanceMigrationTests.cs:210`; L7 invalidation model docs: `Documentation/Stream WhiteBook/ExternalAccelerators/06_Backend_Staging_Commit_And_Rollback.md:57`. | No. | The dirty-line gap is a P0 blocker for coherent device memory claims. | Current observer/invalidation fan-out is explicit non-coherent evidence only; it is not a coherent DMA/cache hierarchy. | P0 cache/coherency/writeback ADR and tests before coherent DMA/cache claims. |
| A7 | L7 ExternalAccelerators have rich model APIs creating evidence inversion risk. | **Partially confirmed** | Fail-closed carrier/no rd: `HybridCPU_ISE/Core/Pipeline/MicroOps/SystemDeviceCommandMicroOp.cs:19`, `:42`, `:74`; WhiteBook boundaries: `Documentation/Stream WhiteBook/ExternalAccelerators/00_README.md:33`, `:71`; fake backend warning: `Documentation/Stream WhiteBook/ExternalAccelerators/06_Backend_Staging_Commit_And_Rollback.md:6`; claim-safety tests: `HybridCPU_ISE.Tests/tests/L7SdcDocumentationClaimSafetyTests.cs:63`, `:136`, `:192`. | Somewhat. The repo already documents/tests non-inversion strongly. | The risk remains because model APIs are numerous and attractive as downstream evidence. | L7 model APIs are intentionally rich but remain model-only; the risk is future evidence inversion, not current executable behavior. | P2 maturity labels/doc clarity now; P0 production protocol/commit ADR before executable L7. |
| A8 | Compiler/backend capability model is too coarse for future production lowering. | **Partially confirmed** | Capability states/requirements: `HybridCPU_Compiler/Core/IR/Model/CompilerBackendLoweringContract.cs:6`, `:100`; rejection rules: `:161`; sideband/current emission: `HybridCPU_Compiler/Core/IR/Model/IrAcceleratorModels.cs:152`, `HybridCPU_Compiler/Core/IR/Bundling/HybridCpuBundleLowerer.cs:33`; Phase12 evidence inversion test: `HybridCPU_ISE.Tests/tests/Ex1Phase12ConformanceMigrationTests.cs:241`. | Yes, if applied to current prohibition. Current model is adequate to reject production lowering. | Future lowering needs finer feature decomposition. | Current gates are sufficient for forbidding production lowering; future production needs a finer capability matrix. | P1 capability decomposition and negative tests before production lowering. |
| A9 | Progress diagnostics may create a future partial-completion semantic trap. | **Partially confirmed** | Non-authoritative progress fields: `HybridCPU_ISE/Core/Execution/DmaStreamCompute/DmaStreamComputeToken.cs:106`; DSC1 rejects non-AllOrNone: `HybridCPU_ISE/Core/Execution/DmaStreamCompute/DmaStreamComputeDescriptorParser.cs:260`; token requires AllOrNone: `HybridCPU_ISE/Core/Execution/DmaStreamCompute/DmaStreamComputeToken.cs:898`; docs: `Documentation/Stream WhiteBook/StreamEngine DmaStreamCompute/02_DmaStreamCompute.md:424`. | Yes, if presented as current bug. | No. | Current diagnostics do not publish memory; the trap is future promotion of diagnostics/poll/wait/fence into publication authority. | P0 ADR/negative tests before any successful partial completion or progress-as-authority mode. |
| A10 | StreamEngine DMA helper is synchronous and does not prove async DMA overlap. | **Confirmed** | Current helper comment: `HybridCPU_ISE/Core/Execution/StreamEngine/StreamEngine.BurstIO.cs:19`; read helper cycles synchronously: `:328`, `:377`; write path writes memory before DMA bookkeeping: `:538`; write helper cycles synchronously: `:578`; doc claim-safety: `HybridCPU_ISE.Tests/tests/L7SdcDocumentationClaimSafetyTests.cs:235`. | No. | Naming risk is real and should remain guarded. | StreamEngine BurstIO DMA path is a synchronous helper; it is not evidence for architectural CPU/DMA overlap. | P2 doc wording/negative claim-safety; P0 async DMA ADR before overlap claims. |

## 4. Current vs future classification

### DmaStreamCompute

- **Current implemented behavior:** lane6 `DmaStreamComputeMicroOp` is a
  descriptor carrier and fails closed on `Execute`; `WritesRegister` remains
  false; `DmaStreamComputeDescriptorParser.ExecutionEnabled == false`.
- **Model/helper behavior:** `DmaStreamComputeRuntime`, token store, progress,
  all-or-none commit container, future-retire observations, and helper token
  lifecycle are explicit model/helper surfaces.
- **Parser-only behavior:** DSC1 strict/current-only; DSC2 parser/capability and
  normalized footprint evidence are parser/model-only.
- **Future-gated behavior:** executable DSC, token allocation through normal
  parser/decode/issue, IOMMU-backed DSC, formal retire/fault publication,
  StreamEngine/VectorALU binding, async DMA overlap.
- **Rejected/not allowed behavior:** hidden StreamEngine/DMA fallback,
  production compiler lowering to executable DSC, successful partial completion
  as current behavior.

### StreamEngine

- **Current implemented behavior:** live StreamEngine/VectorALU execution exists
  for current stream/vector paths; scalar/predicate retire-window publication is
  implemented for those paths.
- **Model/helper behavior:** BurstIO backend binding and DMA helper are
  synchronous execution helpers.
- **Parser-only behavior:** none relevant for DSC execution authority.
- **Future-gated behavior:** formal DSC descriptor-to-StreamEngine execution
  bridge and async overlap semantics.
- **Rejected/not allowed behavior:** treating BurstIO DMA helper as proof of
  asynchronous DMA overlap or coherent DMA/cache hierarchy.

### VectorALU

- **Current implemented behavior:** `VectorALU` is the compute authority for
  vector micro-op paths that call it, including exception tracking and FMA
  helpers.
- **Model/helper behavior:** none of that makes it DSC authority unless an
  explicit DSC binding is introduced.
- **Parser-only behavior:** not applicable.
- **Future-gated behavior:** using `VectorALU` as DSC compute authority or
  conformance oracle.
- **Rejected/not allowed behavior:** treating DSC runtime results as proof of
  `VectorALU`-equivalent ISA semantics.

### ExternalAccelerators/L7

- **Current implemented behavior:** `ACCEL_*` carriers are lane7/system-device
  command carriers; direct `Execute` throws fail-closed; `WritesRegister=false`
  and no architectural `rd` writeback.
- **Model/helper behavior:** owner/domain guard, capability registry, token
  store, queue, poll/wait/cancel/fence, register ABI, fake/test backend, staging,
  commit coordinator, rollback, and telemetry are model/test surfaces.
- **Parser-only behavior:** descriptor parsing and carrier projection are not
  backend dispatch.
- **Future-gated behavior:** production backend protocol, executable L7,
  architectural result publication, CSR/rd semantics, globally ordered
  poll/wait/fence.
- **Rejected/not allowed behavior:** direct `ICustomAccelerator.Execute()` as
  production L7 path, fake backend as protocol evidence, silent runtime fallback.

### Memory/IOMMU/DMA

- **Current implemented behavior:** DSC helper uses physical main memory;
  StreamEngine has backend/IOMMU infrastructure and no-fallback resolver tests;
  DMA helper is synchronously driven.
- **Model/helper behavior:** L7 guarded memory, IOMMU/domain epoch validation,
  resolver selection, and conflict observations are model/foundation surfaces.
- **Parser-only behavior:** address footprints from DSC2/L7 descriptors do not
  prove executable memory behavior.
- **Future-gated behavior:** executable IOMMU DSC/L7, global address-space
  authority, async DMA overlap, and global conflict enforcement.
- **Rejected/not allowed behavior:** silent fallback to physical memory after
  IOMMU/backend rejection.

### Cache/SRF/prefetch

- **Current implemented behavior:** explicit invalidation/flush observer hooks,
  SRF/cache/prefetch fan-out and assist observations; no coherent hierarchy.
- **Model/helper behavior:** warmup/prefetch/assist and invalidation telemetry
  are downstream evidence.
- **Parser-only behavior:** not applicable.
- **Future-gated behavior:** coherent DMA/cache, dirty-line writeback, device
  visibility contract for CPU dirty data.
- **Rejected/not allowed behavior:** claiming explicit invalidation observers
  prove coherent DMA/cache.

### Compiler/backend

- **Current implemented behavior:** typed-slot/lane facts, sideband descriptor
  preservation, carrier projection tests, and capability gates that reject
  production lowering without full requirements.
- **Model/helper behavior:** compiler intent/capability metadata and sideband
  emission are evidence/correlation surfaces.
- **Parser-only behavior:** parser validation and descriptor preservation cannot
  satisfy executable lowering.
- **Future-gated behavior:** production executable DSC/L7 lowering after
  architecture approval, implementation, positive/negative tests, and
  documentation claim-safety.
- **Rejected/not allowed behavior:** production lowering based on parser-only,
  descriptor-only, model helper, fake backend, hardware-coherence assumption, or
  successful partial-completion assumption.

## 5. Evidence non-inversion audit

| Surface | Can be used as executable evidence? | Assessment |
|---|---:|---|
| DSC2 parser-only descriptors, capability grants, normalized footprints | **No** | They validate shape/capability/model footprints only. They do not enable lane6 execution. |
| DSC runtime/helper/token/progress/retire observations | **No** | They are explicit helper/future-retire/model surfaces. They do not prove normal parser/decode/issue execution. |
| StreamEngine/BurstIO/DMA helper evidence | **Mixed** | StreamEngine/VectorALU calls are executable evidence only for current stream/vector paths. BurstIO DMA helper is not evidence for async DMA overlap. |
| VectorALU compute evidence | **Mixed** | It is executable evidence for vector micro-op paths that call `VectorALU`; it is not executable DSC evidence. |
| L7 fake backend, capability registry, queue, fence, token, register ABI, commit APIs | **No** | WhiteBook and tests label these model/test-only; `ACCEL_*` carriers remain fail-closed and do not write architectural `rd`. |
| IOMMU/backend infrastructure and resolver no-fallback decisions | **No** | They prove infrastructure and no-hidden-fallback behavior, not executable DSC/L7 IOMMU memory execution. |
| Conflict/cache observers and passive observations | **No** | They are downstream observations/invalidation evidence, not global CPU load/store ordering authority or coherent cache proof. |
| Compiler sideband emission and carrier projection | **No** | Sideband and typed-slot facts preserve descriptors; they are not production executable lowering. |

## 6. Missing tests / missing ADRs

### P0 before executable DSC/L7, async DMA, or coherent DMA/cache

- ADR: unified retire/publication authority for DSC, L7, StreamEngine,
  VectorALU exceptions, DMA completion, faults, cancel/squash, and memory
  publication.
- ADR: DSC execution binding. It must choose and justify StreamEngine/VectorALU,
  a separate DSC engine, or continued non-execution; hidden fallback must remain
  rejected.
- ADR: executable address-space contract for CPU load/store, DSC helper/runtime,
  StreamEngine BurstIO, DMAController, IOMMU backends, and L7 guarded memory.
- ADR: global conflict service installation, including exact CPU
  load/store/atomic hooks, unresolved address-space policy, fences, poll/wait,
  and negative tests for absent/passive modes.
- ADR: cache/coherency/dirty-line/writeback contract, or a formal proof that CPU
  dirty data cannot be device-visible under the claimed mode.
- Conformance tests: DSC runtime vs `VectorALU` semantics for integer overflow,
  FP rounding, fused FMA, NaN/Inf, reductions, exception counters, and fault
  publication if DSC becomes executable.
- Negative tests: no progress/poll/wait/fence path may publish memory or mark
  successful partial completion without the new all-or-none/partial policy ADR.
- L7 production protocol ADR: backend identity, guard authority, staged commit,
  rollback, result publication, `rd`/CSR semantics, fault publication, and no
  fake-backend promotion.

### P1 before compiler/backend production lowering

- Capability matrix decomposed by executable carrier, address-space authority,
  IOMMU/backend choice, ordering/conflict, cache invalidation/writeback, fault
  publication, all-or-none commit, token/rd result publication, cancel/fence/
  poll/wait semantics, and FP/vector determinism.
- Negative tests proving parser-only, descriptor-only, model-helper, fake
  backend, resolver-only, cache-observer-only, and conflict-observer-only
  evidence cannot satisfy `ProductionExecutable`.
- Compiler tests proving sideband emission, typed-slot metadata, and carrier
  projection do not imply backend dispatch or architectural `rd` writeback.
- Claim-safety tests for docs that mention future production lowering,
  executable DSC/L7, coherent cache, async overlap, or partial success.

### P2 cleanup / claim-safety

- Documentation wording: keep "DMA helper" labeled as synchronous helper; avoid
  wording that suggests architectural overlap.
- Documentation maturity labels for L7 model APIs: carrier, parser, guard,
  token, fake backend, commit model, telemetry, and compiler sideband.
- Cross-reference matrix from each architecture claim to code surface, test
  coverage, and current/model/parser/future/rejected classification.
- Dedicated negative tests confirming no CPU load/store path is currently routed
  through `GlobalMemoryConflictService` as global authority.

## 7. Corrected final recommendation

Keep the previous audit's core conclusions, but reword them around evidence
boundaries:

1. Current HybridCPU behavior is fail-closed for lane6 DSC and L7 `ACCEL_*`
   execution; descriptor/model/helper/fake/backend-infra evidence must not be
   promoted to executable ISA evidence.
2. The real architectural risk is not that current executable DSC/L7 is already
   wrong; it is that the repository now has many useful model surfaces that can
   be misread as authority unless Phase12/Phase13-style gates continue to guard
   claims.
3. The safe next work is not executable DSC, executable L7, async DMA overlap,
   coherent DMA/cache, or compiler production lowering. The safe next work is
   ADRs, negative tests, documentation claim-safety, and conformance matrices
   that establish authority before any executable claim is made.
