# HybridCPU ISE engineering manuals

> Этот каталог — набор repository workflows. Он не является ISA или microarchitecture authority. Архитектурные решения находятся только в Markdown paper; ArchitectureAuthorityRefactorSummary задаёт migration/retention/validation context [ArchitectureAuthorityRefactorSummary overview][overview] [target architecture][target].

## Выбор manual

| Изменение | Manual | Когда остановиться |
|---|---|---|
| Новая ISA-инструкция, alias или encoding contour | [ISA Instruction Addition](./ISA_Instruction_Addition/README.md) | Новый effect/owner/lane/timing contract требует architecture decision |
| Paper/owner contract недостаточен для изменения | [Architecture Decision and Authority Change](./Architecture_Decision_and_Authority_Change/README.md) | Нет однозначной paper semantics или затронут protected invariant |
| Новый execution outcome, retry/fault adapter или catch boundary | [Execution Outcome and Fault Integration](./Execution_Outcome_and_Fault_Integration/README.md) | Меняется fault winner, exception visibility или recovery owner |
| Новый retire-visible effect или publication carrier | [Retire Effect and Publication](./Retire_Effect_and_Publication/README.md) | Effect отсутствует в разрешённом contour или меняется publication order |
| Новый replay key factor, serving cache или invalidation reason | [Replay Identity and Invalidation](./Replay_Identity_and_Invalidation/README.md) | Предлагается token-only equality или backend-global rewind |
| Новый request/completion, DMA progress или memory timing contour | [Timed Memory Contour Integration](./Timed_Memory_Contour_Integration/README.md) | Появляется второй tick/progress/publication owner |
| Новое per-core state или изменение lifecycle/accessor/snapshot | [Core State Ownership and Lifecycle](./Core_State_Ownership_and_Lifecycle/README.md) | Containment начинает переносить semantic authority |
| Новый checked ID или миграция raw identifier API | [Checked Resource Identifiers](./Checked_Resource_Identifiers/README.md) | Не определены family, zero/absence, wire и invalid behavior |
| Удаление/перенос legacy adapter, table, fallback или test seam | [RF-13 Legacy Cleanup](./RF13_Legacy_Cleanup/README.md) | Нет closed-world caller proof или invalid/deletion decision |
| Формирование gates, evidence record и merge closure | [Validation and Evidence Closure](./Validation_and_Evidence_Closure/README.md) | Command/TRX/provenance неполны или worktree не соответствует claim |

## Общие правила

1. Paper определяет архитектуру; ArchitectureAuthorityRefactorSummary/evidence/tests не создают её [paper §3][paper-3] [ArchitectureAuthorityRefactorSummary governance][governance].
2. Один change может использовать несколько manuals, но один owner-changing decision оформляется отдельно от implementation slice.
3. Valid parity, invalid behavior, compatibility retention и deletion proof всегда раздельны.
4. `STOP/ADR` нельзя закрыть тестом или telemetry.
5. В manuals не переносится historical pass count или performance baseline как current fact.
6. RF-12 не переоткрывается; RF-13 cleanup не разрешает менять semantics под видом удаления [RF-13 ledger][rf13].

## Рекомендуемый порядок для сложного изменения

```text
Authority decision (если требуется)
  -> ISA/provider contract
  -> outcome/fault contract
  -> replay/timed-memory integration (если применимо)
  -> retire/publication
  -> compatibility assessment
  -> validation/evidence closure
```

[overview]: ../../ArchitectureAuthorityRefactor/00_Overview/00_README.md
[target]: ../../ArchitectureAuthorityRefactor/02_Authority/02_Target_Architecture_and_Authority.md
[governance]: ../../ArchitectureAuthorityRefactor/05_Governance/05_Invariants_Dependency_Risks_DoD.md
[rf13]: ../../ArchitectureAuthorityRefactor/12_RF13/00_CURRENT_STATUS_AND_LEDGER.md
[paper-3]: ../../../ResearchPaper/section/md%20base/3_Architectural_Overview_and_Frontend_Contract.md
