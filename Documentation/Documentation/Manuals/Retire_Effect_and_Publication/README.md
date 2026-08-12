# Retire effect and architectural publication

> Repository workflow, не architecture authority. Он подключает effect только к уже разрешённому RF-08/ADR-009 bounded retirement protocol; enum membership, telemetry или execute success не дают publication authority [ADR-009][adr-009].

## 1. Entry gate

Применять для `RegisterWrite`, `PcWrite`, `CsrWrite`, store/atomic/system/vector/tile/accelerator effect или изменения capture/prevalidation/application. Сначала проверить RF-08 residual ledger: многие effects намеренно оставлены в independent/compatibility protocols и требуют новой architecture decision для миграции [RF-08 migration][migration] [effect vocabulary][effect-id].

`STOP/ADR`, если effect новый, меняется state owner, атомарность нескольких effects, fault precedence, selected-prefix order или момент видимости.

## 2. Frozen retirement model

Working slot order, затем lane order; authoritative current-window fault выбирает winner; только older prefix попадает в bounded batch; весь union effects prevalidate-ится до forwarding, counters, release и architectural mutation [paper §5][paper-5] [WB flow][wb-flow]. PRF/rename/commit/free-list остаются backend-owned; это не ROB/full precise-exception theorem [ADR-002][adr-002].

## 3. Workflow

| Шаг | Owner | Input → output | Valid | Fail-closed | Forbidden bypass | Tests/evidence | Rollback |
|---:|---|---|---|---|---|---|---|
| 1. Family decision | Architecture owner | Semantic effect → existing family/residual/new ADR | Один state/publication owner | Неясная family останавливает change | Создать generic effect по сходству | RF-08 family inventory | No code change |
| 2. Exact handoff | Stage-B/execution owner | `ScheduledOperation` + terminal result → capture input | Exact binding, source/working slot, lane, VT, attempt | Missing/mismatch/zero attempt rejected | Reconstruct ID from opcode/lane | Identity freeze tests | Remove projection |
| 3. Freeze effect | Family capture owner | Completed attempt → immutable effect + ordinal | Unique `(OperationId, ordinal)` | Duplicate/x0 mutation/mismatched VT rejected | Mutable carrier after capture | [`Rf08RetireEffectIdentityContracts.cs`][effect-id] tests | Restore prior carrier |
| 4. Select prefix | Fault/retire owner | WB window + winner → older prefix | Stable authoritative order | Faulting/younger lane excluded | Per-family early publish | Fault winner tests | Revert selection integration |
| 5. Prevalidate union | Retire + family owners | All records/effects → all-or-none validation | Capacity, owners, payloads and state prechecked | Any invalid member means zero batch mutation | Validate while applying | Selected-prefix union tests | Remove new validator |
| 6. Publish | Existing state owner | Prevalidated selected batch → state mutation | Existing order and owner used once | Denial/fault/rollback publishes nothing | `CoreRuntimeState.PublishAll()`; execute-time write | Publication/no-effect tests | Restore old owner route |
| 7. Cleanup | Backend/resource owner | Published terminal attempt → release/counters | Release exactly after valid boundary | Release/counter before prevalidation | Telemetry as commit | Ordering/source scans | Revert family cleanup hook |

## 4. Effect-specific gates

| Effect | Required proof |
|---|---|
| RegisterWrite | x0 discard; exact rd; PRF/RenameMap/CommitMap interaction; multi-write capacity |
| PcWrite | redirect/link coupling; winner order; committed-PC owner |
| CsrWrite | privilege/CSR owner; optional scalar readback separate |
| Store/atomic | no memory visibility before selected retire; reservation/result coupling |
| Vector/predicate/config | coupled state and optional rd treated explicitly; vector identity context |
| MatrixTile | independent MatrixTile capture/retire ABI; no generic scalar substitution |
| Accelerator/system/trap/event | independent owner, invalidation/order/fault contract; no enum-driven generic commit |

## 5. Tests and validation

- exact attempt and binding mutation negatives;
- duplicate ordinal and mismatched VT/slot/lane;
- x0 versus no-register;
- capacity overflow and complete prevalidation failure with zero mutation;
- winner lane, older-prefix-only publication and younger suppression;
- replay creates fresh attempt; rollback/denial leaves architectural state unchanged;
- family residual/independent-protocol guard.

```powershell
pwsh ./eng/validate.ps1 -Stage FaultInjection
pwsh ./eng/validate.ps1 -Stage Retire
pwsh ./eng/validate.ps1 -Stage ReplayIdentity
```

## 6. Review checklist

- [ ] Paper/ADR already authorizes family, owner and visibility timing.
- [ ] Exact issued attempt survives capture through publication.
- [ ] Complete union prevalidates before any mutation/counter/release.
- [ ] x0, denial, fault, rollback and replay have explicit tests.
- [ ] Independent MatrixTile/accelerator/system protocols remain independent.
- [ ] Residual limitation and rollback point recorded.

[adr-009]: ../../../ArchitectureAuthorityRefactor/02_Authority/ADR-009_VLIW_Retirement.md
[adr-002]: ../../../ArchitectureAuthorityRefactor/02_Authority/ADR-002_Backend_Target.md
[migration]: ../../../ArchitectureAuthorityRefactor/04_CoreMigration/04_RF07_RF13_Core_Migration.md
[paper-5]: ../../../../ResearchPaper/section/md%20base/5_Two_Stage_Admission_and_Bundle_Compositional_SMT_Packing.md
[effect-id]: ../../../../HybridCPU_ISE/CloseToHSL/Core/Architecture/Registers/Retire/Rf08RetireEffectIdentityContracts.cs
[wb-flow]: ../../../../HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/StageFlow/CPU_Core.PipelineExecution.cs
