# Architecture decision and authority change

> Workflow для изменения paper/ADR boundary. Это руководство не принимает архитектурное решение само. Единственная architecture authority — Markdown paper [ArchitectureAuthorityRefactorSummary overview][overview].

## 1. Когда применять

Применять до реализации, если изменение вводит новый architectural effect/state owner, lane topology, runtime legality rule, fault precedence, replay envelope, rollback scope, timed-memory/publication owner, checked-ID family/zero rule или меняет protected backend [target architecture][target] [ADR-002][adr-002] [ADR-009][adr-009].

Не применять для обычного добавления инструкции, полностью укладывающейся в существующие contracts; использовать [ISA manual][isa-manual]. Не создавать ADR для исправления опечатки, тестовой fixture или evidence refresh, не меняющего claim.

## 2. Decision tree

```text
Current paper однозначно определяет semantics и owner?
├─ Да -> implementation manual; paper не переписывать.
└─ Нет
   ├─ Это новое ISA-visible поведение/состояние/порядок? -> paper revision + ADR.
   ├─ Это выбор migration/cutover при неизменной semantics? -> ADR + ArchitectureAuthorityRefactorSummary ledger update.
   └─ Это только evidence gap? -> validation/evidence record, не ADR semantics.
```

## 3. Workflow

| Шаг | Owner | Input → output | Valid | Fail-closed / запрещено | Evidence | Rollback boundary |
|---:|---|---|---|---|---|---|
| 1. Claim inventory | Architecture owner | Request + paper/code → exact affected claims/owners | Каждая новая фраза классифицирована | Evidence/telemetry выдаётся за claim | Links на paper и live seams | Удалить draft inventory |
| 2. Protected-boundary check | Backend/scheduler/memory owners | Claims → список protected invariants | PRF/rename, Stage A/B, exact-slot, VT order, retirement, tick owners сохранены либо явно пересмотрены | Скрытый semantic change под refactor/cleanup | [ArchitectureAuthorityRefactorSummary invariants][governance] | Остановить change до code edits |
| 3. Alternatives | Architecture owner | Problem → минимум current-preserving option и explicit alternative | Trade-offs и non-goals записаны | Выбор по имени legacy или file count | Caller/owner/risk inventory | Отбросить alternatives без runtime changes |
| 4. Paper revision | Paper owner | Selected semantics → normative paper diff | State, inputs, outputs, invalid behavior, ordering и limitations определены | ArchitectureAuthorityRefactorSummary/evidence становится ISA authority | Paper reference review | Revert paper diff |
| 5. ADR | Decision owner | Paper decision → implementation/migration decision | Scope, owners, compatibility, validation, rollback указаны | ADR расширяет paper claim | ADR review | Revert ADR + не начинать slice |
| 6. Slice plan | Component owner | ADR → independently reversible slices | Один owner/behavior class на slice | Valid, invalid и deletion объединены | Dependency/risk/DoD map | Revert один slice |
| 7. Closure | Reviewer | Code/tests/evidence → bounded claim | Fresh gates и residual limitation | «Tests green» без artifacts | Validation manual | Revert affected slice, не соседние owners |

## 4. Минимальная ADR-карточка

```markdown
# ADR: <decision>
- Status / owner / date:
- Paper authority changed: <exact relative links>
- Problem and current boundary:
- Decision:
- Rejected alternatives:
- Inputs / outputs / state owner:
- Valid / invalid / absence behavior:
- Ordering, fault, replay, rollback and publication:
- Compatibility perimeter and expiry:
- Implementation slices:
- Tests and evidence:
- Rollback:
- Residual non-claims:
```

## 5. Merge checklist

- [ ] Paper изменён раньше implementation и остаётся единственной architecture authority.
- [ ] ADR не заявляет больше paper.
- [ ] Новый owner не дублирует scheduler, memory, replay, retire или extension owner [ADR-010][adr-010].
- [ ] Compatibility retention и removal eligibility раздельны.
- [ ] Rollback восстанавливает предыдущего owner без cross-domain revert.
- [ ] Documentation links и актуальные ArchitectureAuthorityRefactorSummary gates проверены.

[overview]: ../../../ArchitectureAuthorityRefactor/00_Overview/00_README.md
[target]: ../../../ArchitectureAuthorityRefactor/02_Authority/02_Target_Architecture_and_Authority.md
[adr-002]: ../../../ArchitectureAuthorityRefactor/02_Authority/ADR-002_Backend_Target.md
[adr-009]: ../../../ArchitectureAuthorityRefactor/02_Authority/ADR-009_VLIW_Retirement.md
[adr-010]: ../../../ArchitectureAuthorityRefactor/02_Authority/ADR-010_CPU_Core_State_Ownership.md
[governance]: ../../../ArchitectureAuthorityRefactor/05_Governance/05_Invariants_Dependency_Risks_DoD.md
[isa-manual]: ../ISA_Instruction_Addition/README.md
