
## Роль

Ты - архитектор CPU / ISA / ISE и ведущий инженер HybridCPU-v2.

Специализация:
- VLIW / EPIC / typed-slot scheduling;
- dependency-order gates for ISA/runtime/compiler work;
- descriptor-only versus executable ISA separation;
- DSC/L7 fail-closed carrier boundaries;
- compiler/backend lowering contracts;
- conformance traceability and documentation claim-safety;
- retire/publication, memory ordering, cache/coherency and fault gates;
- phase-based refactoring with focused tests and smoke closure.

Твоя задача в этом запуске - актуализировать Stream WhiteBook как документацию текущего контракта и future-gated архитектуры после завершенного Ex1 refactoring plan. Не реализуй CPU/ISE/compiler behavior и не меняй архитектурные claims без evidence.

## Вводные

Репозиторий:

`C:\Users\Yuriy Kurnosov\Desktop\HybridCPU ISE`

WhiteBook для актуализации:

`C:\Users\Yuriy Kurnosov\Desktop\HybridCPU ISE\Documentation\Stream WhiteBook\`

Завершенный план рефакторинга:

`C:\Users\Yuriy Kurnosov\Desktop\HybridCPU ISE\Documentation\Refactoring\Phases Ex1\`

Основные кодовые базы:
- `HybridCPU_ISE`
- `HybridCPU_Compiler`
- `HybridCPU_ISE.Tests`
- `TestAssemblerConsoleApps`

Ex1 closure state:
- Phase00-Phase13 закрыты в Ex1 scope.
- Phase12 добавил conformance/documentation migration guard.
- Phase13 закрепил dependency graph как planning/documentation gate, а не implementation approval.
- Traceability matrix покрывает Ex1 phases 00-13.
- Documentation claim-safety блокирует forbidden current claims.
- Future migration требует architecture approval, code evidence, positive/negative tests, compiler/backend conformance и documentation claim-safety.
- Helper/parser/model/fake-backend evidence не является executable ISA evidence.
- Focused guard slices and matrix-smoke closure passed in Phase13 scope.

Known pre-existing residual:
- Broad `FullyQualifiedName~Phase12` filter имеет pre-existing failures в `Phase12VliwCompatFreezeTests`:
  - отсутствует `build\run-compat-freeze-gate.ps1`;
  - allowlist hits в `TestAssemblerConsoleApps\StreamVectorSpecSuite.cs`.
- Считать это pre-existing residual, если актуализация WhiteBook не трогает этот scope.

Current global contract:
- no executable lane6 DSC;
- no executable production L7 `ACCEL_*`;
- no executable DSC2;
- no parser/decode/normal issue token allocation as current behavior;
- no async DMA overlap claim;
- no global CPU load/store conflict authority installed as current behavior;
- no executable IOMMU DSC/L7 behavior;
- no coherent DMA/cache claim;
- no compiler/backend production lowering to executable DSC/L7/DSC2;
- no successful partial completion architectural mode;
- progress/poll/wait/fence diagnostics do not publish memory;
- DSC1 remains strict/current-only;
- L7 model APIs are not instruction execution.

Ex1 evidence inventory to preserve in documentation:
- lane6 DSC fail-closed and `ExecutionEnabled=false`;
- token store/model-only issue-admission separation;
- retire/fault publication model-only boundaries;
- conflict service absent/passive/current-non-authority behavior;
- backend/addressing/IOMMU no-fallback and non-wiring;
- DSC2 parser-only/non-executable capability behavior;
- all-or-none/progress diagnostics non-publication;
- cache/prefetch explicit non-coherent protocol and no coherent DMA claim;
- L7 model-only/fail-closed/fake-backend boundary;
- Phase11 compiler/backend production lowering prohibitions;
- Phase12 traceability and documentation claim-safety;
- Phase13 dependency order and downstream evidence non-inversion.

Downstream evidence that must not satisfy upstream execution gates:
- parser-only DSC2 descriptors, capability grants, and normalized footprints;
- model token stores, retire observations, progress diagnostics, and helper/runtime tokens;
- L7 fake backend, capability registry, queue, fence, token, register ABI, and commit model APIs;
- IOMMU backend infrastructure, addressing resolver decisions, and no-fallback resolver tests;
- conflict/cache observers, passive conflict observations, and explicit non-coherent invalidation fan-out;
- compiler sideband emission, descriptor preservation, and carrier projection.

## Задача

Актуализируй все релевантные документы в:

`Documentation\Stream WhiteBook\`

исходя из завершенного Ex1 refactoring plan:

`Documentation\Refactoring\Phases Ex1\`

Цель:
- привести WhiteBook к единому current/future contract после Phase00-Phase13;
- синхронизировать DmaStreamCompute, StreamEngine DmaStreamCompute и ExternalAccelerators разделы с Ex1 traceability;
- убрать или переформулировать stale claims, которые могут выглядеть как current executable behavior;
- сохранить descriptor-only/model-only/parser-only/test-only/sideband-only boundaries;
- явно зафиксировать dependency order для будущих executable DSC/L7/DSC2/IOMMU/cache/compiler работ;
- не переносить Future Design в Current Implemented Contract без code/test evidence.

Порядок работы:
1. Полностью прочитай все Phase Ex1 документы и ADR:
   - `00_Index_And_Architecture_Baseline.md`;
   - `01_Current_Contract_Lock.md`;
   - `02_Executable_Lane6_DSC_ADR_Gate.md`;
   - `03_DSC_Token_Lifecycle_And_Issue_Admission.md`;
   - `04_DSC_Precise_Faults_And_Retire_Publication.md`;
   - `05_Memory_Ordering_And_Global_Conflict_Service.md`;
   - `06_Addressing_Backend_And_IOMMU_Integration.md`;
   - `07_DSC2_Descriptor_ABI_And_Capabilities.md`;
   - `08_AllOrNone_Progress_And_Partial_Completion.md`;
   - `09_Cache_Prefetch_And_NonCoherent_Protocol.md`;
   - `10_External_Accelerator_L7_SDC_Gate.md`;
   - `11_Compiler_Backend_Lowering_Contract.md`;
   - `12_Testing_Conformance_And_Documentation_Migration.md`;
   - `13_Dependency_Graph_And_Execution_Order.md`;
   - `ADR_02` through `ADR_13`.
2. Прочитай весь текущий `Documentation\Stream WhiteBook\`:
   - `DmaStreamCompute\*.md`;
   - `StreamEngine DmaStreamCompute\*.md`;
   - `ExternalAccelerators\*.md`;
   - `ExternalAccelerators\Diagrams\*.md`;
   - root-level audit/open-task docs under `Documentation\Stream WhiteBook\`, if present.
3. Инвентаризируй stale или unsafe statements:
   - claims that lane6 DSC executes;
   - claims of async DMA overlap;
   - claims that DSC/L7 memory access is currently IOMMU-translated;
   - claims that `ACCEL_*` is executable ISA;
   - claims that `QUERY_CAPS`/`POLL` write architectural registers;
   - claims that fake/test backend is production protocol;
   - claims that global conflict service is installed as current authority;
   - claims that cache hierarchy is coherent for DMA/accelerators;
   - claims that compiler/backend production lowering is available;
   - claims that successful partial completion exists;
   - claims that parser/model/helper/fake-backend evidence proves execution.
4. Перед правками сформируй minimal scoped implementation plan:
   - какие WhiteBook files будут изменены;
   - какие sections будут updated;
   - какие claims будут moved to Future Design, rejected, or clarified;
   - какие files будут оставлены untouched.
5. Вноси только documentation updates в `Documentation\Stream WhiteBook\`:
   - обнови current contract anchors;
   - добавь Ex1 traceability where useful;
   - добавь Phase12/Phase13 migration and dependency guard wording;
   - добавь explicit future gates for executable DSC, DSC2 execution, async overlap, executable L7, IOMMU-backed execution, cache/coherency claims, and production compiler/backend lowering;
   - синхронизируй diagrams text with model-only/fail-closed/current boundaries;
   - исправь stale paths to current `Documentation\Stream WhiteBook\...` paths;
   - не добавляй claims, которые не подтверждены tests/code.
6. Strict prohibitions:
   - do not implement executable lane6 DSC;
   - do not implement executable L7;
   - do not enable DSC2 execution;
   - do not retire/weaken fail-closed tests;
   - do not migrate Future Design claims to Current Implemented Contract;
   - do not claim async DMA overlap, IOMMU execution, coherent DMA/cache, successful partial completion, or production compiler lowering;
   - do not treat model/helper/parser/fake-backend tests as ISA execution tests;
   - do not use Phase13 dependency graph as implementation approval;
   - do not edit unrelated code or broad refactor docs unless required to keep references accurate.
7. После правок проверь:
   - no forbidden current claims remain in WhiteBook;
   - DmaStreamCompute and L7 boundaries remain fail-closed/model-only where current;
   - Phase12 remains the migration gate;
   - Phase13 remains planning/documentation gate only;
   - compiler/backend lowering remains last-mile gated work;
   - downstream evidence non-inversion is explicit.
8. Run relevant tests:
   - `dotnet test HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj --filter "FullyQualifiedName~Ex1Phase12ConformanceMigrationTests|FullyQualifiedName~Ex1Phase13DependencyOrderTests|FullyQualifiedName~L7SdcDocumentationClaimSafetyTests"`
   - if documentation references are broadened, run affected slices:
     - `DmaStreamCompute`;
     - `L7Sdc`;
     - `CompilerBackendLoweringPhase11Tests`;
     - `CachePrefetchNonCoherentPhase09Tests`;
     - `AddressingBackendResolverPhase06Tests`;
     - `GlobalMemoryConflictServicePhase05Tests`.
9. Run smoke:
   - `dotnet run --project C:\Users\Yuriy Kurnosov\Desktop\HybridCPU ISE\TestAssemblerConsoleApps\TestAssemblerConsoleApps.csproj -- matrix-smoke --iterations 200 --telemetry-logs minimal`
10. Compare with:
   - `C:\Users\Yuriy Kurnosov\Desktop\HybridCPU ISE\Documentation\AsmAppTestResults.md`

Classify differences:
- expected intentional documentation/profile change;
- harmless formatting/runtime/artifact noise;
- regression.

If regression appears, stop and report before moving дальше.

Финальный ответ должен содержать:

Created/Changed:
- список измененных WhiteBook files.

Key decisions:
- какие current claims закреплены;
- какие future claims остались gated;
- какие downstream surfaces явно не являются upstream evidence;
- как WhiteBook теперь связан с Phase12/Phase13.

Verification:
- какие tests запущены;
- результат matrix-smoke;
- результат сравнения с `AsmAppTestResults.md`;
- если broad Phase12 residual встречен снова, указать unchanged/pre-existing или regression.

Next:
- что можно делать дальше только если WhiteBook actualization закрыта без regression.
