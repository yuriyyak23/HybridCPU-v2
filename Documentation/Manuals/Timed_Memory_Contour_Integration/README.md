# Timed-memory contour integration

> Repository workflow, не architecture authority. Новый RF-10 memory request/completion, DMA progress или timed service contour сохраняет `MemoryCycleController` как single progress owner и selected retirement как publication owner там, где это определено paper [paper §7.7][paper-7] [RF-10 ledger][rf10].

## 1. Entry gate

Применять после того, как ISA/static memory shape и dynamic `MemoryCapability` уже разделены. Static plan не содержит resolved address/bank/readiness; capability для Stage A не является request, completion или timing authority [RF-06 contracts][rf06].

`STOP/ADR`, если требуется новый clock domain, второй tick owner, caller-local completion loop, новая store visibility point, coherence/cache/PTW theorem, новый queue progress contract или изменение fault timing [paper limitations][paper-10].

## 2. Contour classification

| Contour | Existing boundary | Дополнительный gate |
|---|---|---|
| Scalar read | Controller request → completion → MEM/WB result | sign/zero extension, request identity, failed completion |
| Scalar store | Readiness may complete; mutation only at selected retire | exact bytes/size, no eager write |
| Vector segment read | Bounded functional segment service | payload/length and current modeling limitation |
| Vector segment store | Нужен existing selected-retire byte carrier | Без carrier не мигрировать |
| Canonical vector transfer | Frozen source payload; destination publication at retire | exact source/destination identity |
| DMA | Bound agent advances once per controller edge | no recursive/caller loop; retained functional adapters explicit |
| Legacy/non-migrated queue | Existing legacy service owner invoked by controller edge | Не объявлять migrated без request/completion proof |

## 3. Workflow

| Шаг | Owner | Input → output | Valid | Fail-closed | Forbidden bypass | Tests/evidence | Rollback |
|---:|---|---|---|---|---|---|---|
| 1. Inventory | Memory owner | Callers/queues/ticks/publication → contour map | Every progress and mutation caller known | Unknown loop/callback blocks change | Search only request type name | Caller/loop inventory | No code change |
| 2. Static/dynamic split | Decoder + admission | Static shape + runtime topology → capability | Direction/bank/frozen footprint exact | Invalid geometry/bank has no value | Unresolved→bank0/default; readiness in capability | MemoryAdmission/Differential | Remove projection |
| 3. Request identity | Controller owner | Accepted operation → immutable request | ID, owner, size, binding/payload captured once | Invalid/capacity rejected before publication | Address re-resolution after accepted binding | Request/binding tests | Restore old adapter |
| 4. Enqueue/backpressure | Queue owner | Request → accepted/rejected/pending | Bounded capacity and explicit reason | Failed enqueue creates no pending state | Hidden unbounded queue | Capacity/cancellation tests | Revert queue adapter |
| 5. Tick | `MemoryCycleController` | One observed CPU edge → at most one service edge | Monotonic cycle; agents progress once | Double/recursive tick rejected by proof | Caller-local spin/completion loop | Cycle-delta tests [controller][controller] | Disable new service branch |
| 6. Completion/fault | Controller + pipeline fault owner | Service result → completion/fault | Exact request ID and terminal state | Failed completion not decoded as success | Bool false → retry guessing | Token/fault-tail tests | Restore previous completion route |
| 7. Capture publication | Retire/family owner | Completion/readiness → frozen effect | Loads publish result; stores only selected retire | Fault/denial/rollback changes no state | Service-time store/destination write | Visibility tests | Remove new effect carrier |
| 8. Compatibility | Adapter owner | Non-migrated path → explicit adapter | Owner/timing status documented | Adapter silently becomes second progress owner | Inline loop retained as fallback | Source scan + parity | Restore named adapter |

## 4. Required tests

- valid sizes, boundaries, sign/zero extension and exact byte truncation;
- invalid geometry, bank, size, address, capacity and binding generation;
- pending, completed success, completed failure, cancellation and duplicate completion;
- exactly one externally visible memory/DMA step per CPU cycle;
- no state mutation on denial/fault/rollback;
- store/vector destination invisibility before complete selected-retire prevalidation;
- existing legacy/functional adapter behavior explicitly retained.

```powershell
pwsh ./eng/validate.ps1 -Stage MemoryAdmission
pwsh ./eng/validate.ps1 -Stage MemoryDifferential
pwsh ./eng/validate.ps1 -Stage FaultInjection
pwsh ./eng/validate.ps1 -Stage Retire
pwsh ./eng/validate.ps1 -Stage MemoryCycle
```

## 5. Review record

```markdown
- Contour and existing authority:
- Static access contract / dynamic capability:
- Request identity, binding and queue:
- Tick/progress owner:
- Completion/fault owner:
- Publication point and architectural state owner:
- Invalid/denial/rollback behavior:
- Compatibility adapters:
- Tests, TRX/provenance, rollback, residual model limitation:
```

[paper-7]: ../../../../ResearchPaper/section/md%20base/7_Replay_Stable_Placement_Replay_Tokens_and_Execution_Boundaries.md
[paper-10]: ../../../../ResearchPaper/section/md%20base/10_Related_Work_Limitations_and_Conclusion.md
[rf10]: ../../../ArchitectureAuthorityRefactor/09_RF10/00_CURRENT_STATUS_AND_LEDGER.md
[rf06]: ../../../../HybridCPU_ISE/CloseToHSL/Core/Decoder/Rf06ExecutionContracts.cs
[controller]: ../../../../HybridCPU_ISE/CloseToHSL/Memory/Timing/MemoryCycleController.cs
