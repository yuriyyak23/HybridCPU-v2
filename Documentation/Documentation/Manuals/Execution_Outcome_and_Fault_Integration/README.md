# Execution outcome and fault integration

> Практический RF-07 workflow для нового execute contour, retry/not-ready, typed fault или compatibility exception adapter. Manual не расширяет fault model paper [paper §7][paper-7] [RF-07 migration][migration].

## 1. Scope и stop conditions

Применять, когда provider возвращает новый вариант `ExecutionOutcome`, старый `bool/exception` adapter мигрирует на typed result или новый request completion должен попасть в существующий fault tail. Текущий vocabulary и invariants находятся в [`Rf07ExecutionOutcomeContracts.cs`][outcomes].

`STOP/ADR`, если меняются stage-aware fault winner, older-prefix publication, architectural exception kind/visibility, recovery/flush owner или предлагается universal rollback/precise-exception claim [ADR-009][adr-009].

## 2. Outcome classification

| Состояние | Допустимый смысл | Запрещённая подмена |
|---|---|---|
| Completed | Терминальный success с полностью сформированным result/effects | Частичный success, скрытая pending работа |
| Blocked / Retryable | Невыполненная попытка с явным readiness/retry owner | Page/alignment/device fault как retry; unknown exception как not-ready |
| Architectural fault | Typed, delivery-owned fault | Generic false или telemetry-only record |
| Fatal invariant violation | Нарушение internal contract/unknown exception | Продолжение execution или fallback success |

Точные enum members и factory invariants брать из текущего source, а не из этой сокращённой таблицы [outcome source][outcomes].

## 3. Workflow

| Шаг | Owner | Input → output | Valid behavior | Invalid/fail-closed | Forbidden bypass | Tests/evidence | Rollback |
|---:|---|---|---|---|---|---|---|
| 1. Inventory | Provider owner | Old returns/catches/callers → contour map | Все producers/consumers/catches найдены | Неизвестный caller блокирует cutover | Source-only happy-path inventory | Caller/catch/reflection/TestSupport scan | Старый adapter остаётся |
| 2. Classify | Fault architecture owner | Runtime states → exact typed outcomes | Retry, fault, fatal не пересекаются | Ambiguous false/null/default rejected | Boolean reconstruction downstream | Outcome contract tests | Откат classification до code |
| 3. Produce | Execution provider | Inputs/request state → one outcome | Provider формирует полный result/diagnostic | Unknown exception → fatal, не retry | Catch-all `return false` | Positive + injected failures | Revert one provider |
| 4. Transport | Pipeline/carrier owner | Outcome → stage latch/completion | Exact fault/result identity сохраняется | Missing/failed completion не становится success | Side channel/telemetry as carrier | Differential and token-failure tests | Restore prior adapter |
| 5. Arbitrate | Stage-aware fault owner | EX/MEM/WB faults → winner | Existing stage/lane winner and older-prefix rules | Faulting/younger effects suppressed | Direct throw before bounded prefix decision | Fault-tail/winner tests [fault source][faults] | Revert integration only |
| 6. Cleanup | Resource owner | Terminal decision → release/flush | Cleanup exactly once by existing owner | Retry releases committed resource; fatal continues | Provider-owned global cleanup | Architecture-preservation tests | Restore old cleanup route |
| 7. Remove adapter | Compatibility owner | Zero-caller proof → removal | Invalid behavior and public exception contract separately approved | Name/valid parity used as deletion proof | Raw fallback without ledger | RF-07 exit audits | Restore adapter under same owner |

## 4. Required tests and commands

- completed/retry/fault/fatal construction and illegal combinations;
- provider differential for valid and injected failure cases;
- pending versus completed-unsuccessful token;
- scalar/vector, single-lane/explicit-packet tails as applicable;
- exact fault winner, no premature state mutation and cleanup-once;
- retained public exception adapter parity or explicit removal proof.

```powershell
pwsh ./eng/validate.ps1 -Stage FaultInjection
pwsh ./eng/validate.ps1 -Stage Retire
```

Для behavior-fixing RF-07 slice также требуется документированный external regression gate [ArchitectureAuthorityRefactorSummary governance][governance].

```powershell
dotnet run --project ./TestAssemblerConsoleApps/TestAssemblerConsoleApps.csproj --configuration Debug --no-restore -- --iterations 100 --telemetry-logs minimal
```

## 5. Review record

```markdown
- Provider/callers/catches:
- Old state -> typed outcome mapping:
- Retry/readiness owner:
- Fault kind and delivery owner:
- Unknown exception behavior:
- Stage/lane winner interaction:
- Cleanup/release owner:
- Compatibility adapter retain/remove decision:
- Tests/TRX/provenance:
- Rollback point and residual limitation:
```

[paper-7]: ../../../../ResearchPaper/section/md%20base/7_Replay_Stable_Placement_Replay_Tokens_and_Execution_Boundaries.md
[migration]: ../../../ArchitectureAuthorityRefactor/04_CoreMigration/04_RF07_RF13_Core_Migration.md
[adr-009]: ../../../ArchitectureAuthorityRefactor/02_Authority/ADR-009_VLIW_Retirement.md
[governance]: ../../../ArchitectureAuthorityRefactor/05_Governance/05_Invariants_Dependency_Risks_DoD.md
[outcomes]: ../../../../HybridCPU_ISE/CloseToHSL/Core/Execution/Rf07ExecutionOutcomeContracts.cs
[faults]: ../../../../HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/Faults/CPU_Core.PipelineExecution.Faults.cs
