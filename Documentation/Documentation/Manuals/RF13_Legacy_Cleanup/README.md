# RF-13 legacy cleanup

> Repository workflow, не architecture authority. RF-13 удаляет только доказанно заменённые/неиспользуемые contours; он не меняет semantics, не завершает generated-source-only claim преждевременно и не переоткрывает RF-12 [RF-13 ledger][rf13].

## 1. Основное правило

Имя `Legacy`, `Compat`, `Raw`, `Fallback`, `Shadow`, `TestSupport` или handwritten switch не является deletion proof. Retained exact-slot scheduler, `InstructionIR`, runtime registry/materializer adapters, `DecodedBundleTransportProjector`, raw wire bridges, reflection и bounded fallbacks могут оставаться активными архитектурными/compatibility boundaries [ArchitectureAuthorityRefactorSummary removal matrix][governance].

Каждый cleanup slice отвечает отдельно:

1. valid callers migrated?
2. invalid behavior approved and preserved/changed explicitly?
3. compatibility retention owner still нужен?
4. deletion closed-world proof complete?

## 2. Artifact classification

| Класс | Default decision | Removal gate |
|---|---|---|
| Duplicate static ISA table/classifier fallback | Candidate for generated delegation/removal | Generated-source-first read path, valid+invalid closure, no fallback consumers |
| Runtime provider/materializer switch | Retain until provider callers migrate | Exact binding/provider parity and zero runtime/reflection/TestSupport callers |
| Canonical/legacy transport projector | Retain perimeter adapter | Parser/compiler/runtime/fallback/wire caller closure |
| Exact-slot scheduler path | Paper-protected retained contour | Separate paper revision; RF-13 name-based cleanup forbidden |
| Raw checked-ID overload | Retain validating adapter | Valid parity + invalid decision + wire parity + zero callers |
| TestSupport/reflection seam | Not production authority, but real caller | Explicit test migration/removal and source/IL/reflection proof |
| Evidence/report helper | May be retained as evidence | Prove it owns no runtime decision; deletion must not erase required provenance |
| Dead parameterless/obsolete adapter | Removal candidate | Zero callers across production/tests/reflection/generated code and rollback snapshot |

## 3. Workflow

| Шаг | Owner | Input → output | Valid | Fail-closed | Forbidden bypass | Tests/evidence | Rollback |
|---:|---|---|---|---|---|---|---|
| 1. Freeze candidate | Cleanup owner | Exact symbol/file/API → migration ID | One artifact/behavior class | Broad folder cleanup rejected | Delete by filename | RF-13 ledger row | Restore snapshot |
| 2. Authority classification | Architecture/component owner | Candidate → authority/adapter/evidence/test classification | Paper and current owner named | Unclear classification means retain | Evidence treated as authority | Authority inventory | No deletion |
| 3. Caller closure | Component owner | Production/compiler/parser/generated/tests/reflection/wire/fallback → inventory | Transitive callers and dynamic reachability closed | Unknown/reflection caller blocks | `rg` count alone as closed world | Source + compile + reflection/TestSupport tests | Retain adapter |
| 4. Valid parity | Replacement owner | Valid corpus old/new → equivalence | Exact output/binding/sideband/owner parity | Drift blocks removal | Telemetry similarity | Differential/parity tests | Revert consumer cutover |
| 5. Invalid behavior | Existing invalid owner | Invalid/absence corpus → explicit decision | Same behavior or separately approved change | Unknown→default/success prohibited | Assume no invalid callers | Mutation/fuzz/fault tests | Keep old invalid arm |
| 6. Fallback proof | Runtime owner | All fallback entrances → zero or named retention | No ownerless route | Hidden catch/reflection/raw path blocks | Delete primary, leave fallback | Reachability/source guards | Restore route |
| 7. Delete/relocate | Cleanup owner | Proven artifact → minimal removal | One reversible slice; no semantic diff | Build/test/doc reference break fails | Cleanup adjacent code opportunistically | Compile + focused gates | Revert this deletion |
| 8. Re-audit | Independent reviewer | Post-delete graph → no stale duplicate/reference | Ledger/current docs match code | Stale claims block closure | Mark RF-13 complete from local pass | Completion audit [RF-13 tests][rf13-tests] | Restore artifact/update ledger truthfully |

## 4. Special current limitations

- Generated C# and lock exist, но generator всё ещё строит rich fields через `legacy-*` defaults и читает static policies из `IsaV4Surface`; handwritten registry/readbacks ещё не дают generated-source-only claim [`HybridCPU.IsaGen`][generator] [RF-13 ledger][rf13].
- `InstructionClassifier` generated-first не означает invalid closure, пока живы handwritten fallbacks/permissive defaults.
- `InstructionIR`, `InstructionRegistry.CreateMicroOp`, `DecodedBundleTransportProjector` и mutable compatibility projections имеют retained callers/decisions.
- `InternalOpBuilder` и похожие helpers могут не иметь production callers, но оставаться достижимыми из tests/reflection.
- LoopBuffer compatibility methods могут быть TESTING-only при immutable production serving; test-only не равно автоматически removable.

## 5. Required evidence record

```markdown
# RF-13 cleanup: <artifact>
- Migration ID / ledger row:
- Classification and current owner:
- Replacement:
- Production callers:
- Compiler/parser/generated callers:
- Tests/TestSupport/reflection callers:
- Wire/serialization/checkpoint/trace callers:
- Fallback/catch/dynamic reachability:
- Valid parity:
- Invalid/absence behavior decision:
- Retain/remove decision and removal condition:
- Tests/commands/TRX/provenance:
- Rollback snapshot:
- Residual limitation / next eligible slice:
```

## 6. Validation

Выбирать component gates по затронутому contour, обязательно включая inventory и final clean-checkout gate [ArchitectureAuthorityRefactorSummary governance][governance].

```powershell
pwsh ./eng/validate.ps1 -Stage Baseline
pwsh ./eng/validate.ps1 -Stage AuthorityInventory
dotnet run --project ./tools/HybridCPU.IsaGen -- --check
pwsh ./eng/validate.ps1 -Stage Full
```

Добавлять `IsaParity`, `DecoderDifferential`, `Materialization`, `CompilerParity`, `Routing`, `FaultInjection`, `Retire`, `ReplayIdentity` или `MemoryCycle` только по затронутому dependency contour; отсутствие выбранного gate обосновать в record.

## 7. Merge checklist

- [ ] Один exact artifact и migration ID.
- [ ] Paper authority и current owner не изменены.
- [ ] Caller inventory включает generated/parser/compiler/runtime/test/reflection/wire/fallback.
- [ ] Valid parity и invalid behavior имеют разные evidence rows.
- [ ] Retention decision не выдан за deletion proof.
- [ ] No stale table/switch/doc reference после удаления.
- [ ] Rollback восстанавливает только adapter, не architecture.
- [ ] RF-13 ledger обновлён фактически, без overclaim completion.

[rf13]: ../../../ArchitectureAuthorityRefactor/12_RF13/00_CURRENT_STATUS_AND_LEDGER.md
[governance]: ../../../ArchitectureAuthorityRefactor/05_Governance/05_Invariants_Dependency_Risks_DoD.md
[generator]: ../../../../tools/HybridCPU.IsaGen/Program.cs
[rf13-tests]: ../../../../HybridCPU_ISE.Tests/Architecture/Rf1312LegacyCompletionAuditTests.cs
