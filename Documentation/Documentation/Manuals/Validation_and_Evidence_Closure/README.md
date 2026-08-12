# Validation and evidence closure

> Cross-cutting workflow для выбора gates, получения воспроизводимых artifacts и формулировки bounded merge claim. Test/evidence подтверждают authority, но не создают ISA, legality, timing или publication semantics [paper §8][paper-8] [ArchitectureAuthorityRefactorSummary governance][governance].

## 1. Evidence model

| Слой | Что доказывает | Чего не доказывает |
|---|---|---|
| Source/authority scan | Отсутствие/наличие известных declarations/callers | Dynamic reachability без reflection/registration analysis |
| Unit/property/mutation test | Указанный contract и negative boundary | Repository-wide correctness |
| Differential/parity | Эквивалентность заданного corpus | Invalid closure вне corpus или deletion eligibility |
| TRX/JUnit + provenance | Фактическое завершение выбранного run | Архитектурную корректность невыбранных contours |
| Telemetry/trace | Наблюдаемое runtime событие/счётчик | Legality, semantic authority или architectural commit само по себе |
| External emulator matrix | End-to-end regression затронутого profile | PPA, official SPEC, universal performance baseline |

## 2. Gate selection

| Change contour | Минимальные focused gates |
|---|---|
| Static ISA/generator | `AuthorityInventory`, generator `--check`, `IsaParity` |
| Decoder/canonical payload | `DecoderDifferential`, `Materialization` |
| Compiler emission/annotations | `CompilerParity`, plus decoder/materialization |
| Scheduler/provider/resource | `Routing`, `FamilyDifferential`; memory gates if applicable |
| Execution outcome/fault | `FaultInjection` |
| Architectural effect | `Retire` + `FaultInjection` |
| Replay identity/serving | `ReplayIdentity` + `Routing` |
| Timed memory/DMA | `MemoryAdmission`, `MemoryDifferential`, `MemoryCycle`, `Retire` for publication |
| Legacy cleanup | `AuthorityInventory` + every gate owned by removed contour |
| Documentation-only | Link/reference checks; final repository `Full` on merge candidate according to team policy |

Эта матрица выбирает минимум, а не заменяет dependency analysis. Canonical commands и required results берутся только из текущего ArchitectureAuthorityRefactorSummary governance [validation table][governance].

## 3. Workflow

| Шаг | Owner | Input → output | Valid | Fail-closed | Forbidden bypass | Rollback |
|---:|---|---|---|---|---|---|
| 1. Claim map | Change owner | Diff → claims/non-claims/owners | Каждому claim назначен gate | Неясный claim блокирует closure | «Full covers everything» без mapping | Narrow claim/remove overclaim |
| 2. Dependency map | Component owners | Changed seams → transitive gates | Compiler/runtime/replay/retire/memory/compat dependencies включены | Unknown dependency требует inventory | Выбрать тест по filename | Add missing gate |
| 3. Negative matrix | Contract owner | Invalid/absence/mutation cases → expected failures | Fail-closed behavior проверено отдельно | Только happy path | Valid parity как invalid proof | Add negative tests |
| 4. Clean execution | CI/change owner | Commands → exit code + complete artifacts | Exit 0, no abort/timeout/hang, completed non-empty TRX, provenance | Missing/aborted artifact = failure | Pass text before crash | Re-run cleanly |
| 5. Interpret | Architecture/component reviewer | Results → bounded statement | Claim соответствует test scope | Metrics/counts не расширяют claim | Telemetry as authority | Narrow statement |
| 6. Compatibility/deletion | Cleanup owner | Caller/parity/invalid evidence → retain/remove | Четыре proof classes separate | Missing reflection/TestSupport/wire proof blocks deletion | Source count only | Retain adapter |
| 7. Record | Reviewer | Commands/artifacts/limitations → evidence record | Commit/worktree/toolchain/provenance указаны | Stale/historical result not current | Copy old pass totals | Mark current limitation |

## 4. Canonical commands

Из repository root [ArchitectureAuthorityRefactorSummary governance][governance]:

```powershell
pwsh ./eng/validate.ps1 -Stage Baseline
pwsh ./eng/validate.ps1 -Stage AuthorityInventory
dotnet run --project ./tools/HybridCPU.IsaGen -- --check
pwsh ./eng/validate.ps1 -Stage IsaParity
pwsh ./eng/validate.ps1 -Stage DecoderDifferential
pwsh ./eng/validate.ps1 -Stage Materialization
pwsh ./eng/validate.ps1 -Stage Routing
pwsh ./eng/validate.ps1 -Stage MemoryAdmission
pwsh ./eng/validate.ps1 -Stage MemoryDifferential
pwsh ./eng/validate.ps1 -Stage FamilyDifferential
pwsh ./eng/validate.ps1 -Stage CompilerParity
pwsh ./eng/validate.ps1 -Stage FaultInjection
pwsh ./eng/validate.ps1 -Stage Retire
pwsh ./eng/validate.ps1 -Stage ReplayIdentity
pwsh ./eng/validate.ps1 -Stage MemoryCycle
pwsh ./eng/validate.ps1 -Stage Full
```

External regression требуется для каждого RF-06 routing/admission slice и RF-07 behavior-fixing slice [ArchitectureAuthorityRefactorSummary governance][governance]:

```powershell
dotnet run --project ./TestAssemblerConsoleApps/TestAssemblerConsoleApps.csproj --configuration Debug --no-restore -- --iterations 100 --telemetry-logs minimal
```

Не переносить historical absolute counters в current baseline. Сравнивать только документированные profile coverage, success/zero-error invariants и разрешённую normalized shape; хранить fresh artifact directory в change evidence.

## 5. Reference validation

Для нового manual/evidence Markdown:

1. Извлечь inline/reference-definition destinations.
2. URL-decode relative paths и разрешить их от папки документа.
3. Проверить existence и exact case там, где CI case-sensitive.
4. Проверить internal anchors после render rules.
5. Убедиться, что source seam не переименован и test/command активен.
6. Не считать ссылку на evidence ссылкой на architecture authority.

## 6. Evidence record template

```markdown
# Validation record: <change>
- Commit / branch / dirty-worktree state:
- SDK/toolchain/configuration:
- Claims and explicit non-claims:
- Changed owners/seams:
- Gate-selection rationale:
- Commands and exact exit codes:
- TRX/JUnit/JSON/provenance paths:
- Positive results:
- Negative/mutation results:
- Compatibility retention result:
- Deletion proof result (if applicable):
- Documentation/reference result:
- Warnings, failures and interpretation:
- Residual limitation:
- Rollback point:
```

## 7. Merge checklist

- [ ] Gate set связан с dependency/owner map.
- [ ] Каждый command существует в current ArchitectureAuthorityRefactorSummary/script.
- [ ] Exit code, completion, non-empty results и provenance проверены.
- [ ] Abort/timeout/hang/missing artifact не назван pass.
- [ ] Negative behavior проверено отдельно от valid parity.
- [ ] Compatibility retention отдельно от deletion proof.
- [ ] Telemetry и test counts не превращены в architecture/performance theorem.
- [ ] Все Markdown references разрешаются.
- [ ] Final claim перечисляет ограничения и не заявляет RF completion сверх ledger.

[paper-8]: ../../../../ResearchPaper/section/md%20base/8_Telemetry_Validation_and_Evaluation_Methodology.md
[governance]: ../../../ArchitectureAuthorityRefactor/05_Governance/05_Invariants_Dependency_Risks_DoD.md
