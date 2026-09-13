# Checked resource identifiers

> Repository workflow, не architecture authority. После закрытой RF-12 новый family или raw→checked migration является отдельным change, не переоткрывает RF-12 и сохраняет существующих legality/ownership/publication owners [ArchitectureAuthorityRefactorSummary overview][overview] [paper §3.7][paper-3].

## 1. Когда применять

Применять при добавлении checked ID/result/binding, миграции raw API, новом wire representation или устранении invalid→zero alias. Не применять механически ко всем `int`, `0`, `default` или nullable values.

`STOP/ADR`, если paper не определяет family, domain, valid range, zero/absence, wire width, generation lifetime или owner invalid behavior. Запрещён universal `ResourceId`, `ChannelId`, `DomainId`, `TokenId`; slot и physical lane, architectural и physical register, logical bank и physical bank index остаются разными families [RF-12 migration][migration].

## 2. Family decision card

Перед типом заполнить:

| Fact | Required answer |
|---|---|
| Semantic family | Что именно идентифицируется и каким owner |
| Valid set | Range/enum membership и configuration-dependent bounds |
| Zero | Valid identity, invalid/unissued или wire sentinel? |
| Absence | Nullable/result/occupancy/discriminated state, не implicit zero |
| Wire | Raw width, version, exact round trip и boundary validator |
| Lifecycle | Кто выдаёт/revokes; epoch/generation binding |
| Consumers | Index, shift, dictionary, owner lookup, completion/publication |
| Invalid behavior | Exception/reject/result/fault; существующая compatibility arm |

## 3. Workflow

| Шаг | Owner | Input → output | Valid | Fail-closed | Forbidden bypass | Tests/evidence | Rollback |
|---:|---|---|---|---|---|---|---|
| 1. Closed-world inventory | Family owner | Producers/consumers/wires/reflection → role map | Roles split by semantics | Unknown raw caller blocks migration | Text replace by type name | Caller + wire + TestSupport scan | No code change |
| 2. Authority decision | Architecture owner | Role map → family/zero/absence/wire contract | Paper already sufficient or revised | Ambiguous zero/default stops | Infer zero rule from another family | Decision tests/docs | Revert decision draft |
| 3. Zero-caller contract | Type owner | Contract → checked type/result/binding | Constructor/TryCreate exact; no callers | New type silently changes behavior | Implicit conversions/cross-family casts | Boundary/fuzz tests | Remove unused type |
| 4. Valid-input cutover | Boundary owner | Valid raw caller → checked signature/storage | Exact valid parity | Invalid arm unchanged | Combine invalid change | Valid parity/source scans | Restore one signature |
| 5. Invalid/absence decision | Existing invalid owner | Raw invalid cases → explicit result/reject | Invalid never aliases valid zero | Clamp/modulo/default substitution rejected | `?? 0`, `default(Id)` as absence | Negative/fuzz tests | Retain raw arm |
| 6. Wire bridge | Serialization owner | Raw↔checked↔raw | Exact width/value/version round trip | Unknown/reserved fails at owner boundary | Checked parse grants runtime legality | Wire parity/mutation | Restore bridge adapter |
| 7. Consumer cutover | Functional owner | Checked value → indexing/ownership/action | Owner revalidates config/lifecycle where needed | Stale generation/out-of-range rejected | Type existence as authority | Owner/differential tests | Revert consumer only |
| 8. Raw cleanup | Compatibility owner | Zero callers + invalid approval → remove overload | All call paths/reflection/tests closed | Valid parity alone insufficient | Remove by `Legacy`/`Raw` name | Deletion eligibility audit | Restore validating adapter |

## 4. Canonical examples and cautions

- `VtId`: `0..3`, zero is VT0; absence is outer state; VT order remains VT0→VT3 [`RegisterIdentity.cs`][register-id].
- `ArchRegId` and `PhysRegId`: different namespaces; register zero valid; no-register is separate [register identity source][register-id].
- `SlotId` and `LaneId`: both `0..7`, but source/working slot is not materialized lane [slot-id][slot-id] [lane-id][lane-id].
- `MemoryBankId` and `PhysicalMemoryBankIndex`: scheduler-visible logical identity differs from topology-local queue position; unresolved/invalid geometry has no bank zero [memory resolution][memory-resolution] [physical-index][physical-index].
- `PhysicalMemoryBankBinding`: accepted location must remain tied to immutable geometry generation; re-resolution can observe another topology [physical binding][physical-binding].
- DMA channel, stream engine, accelerator device/domain/token generations remain owner-specific families.

## 5. Test matrix and review

- minimum/maximum/zero valid values;
- below/above range, default, unknown enum and stale generation;
- absence distinct from zero;
- raw→checked→raw for every wire width/version;
- cross-family compile/runtime rejection;
- indexing/shift/dictionary paths accept only checked or owner-validated values;
- valid parity, invalid behavior, compatibility retention and deletion proof reported separately;
- reflection/TestSupport/JSON/checkpoint/trace bridges inventoried.

```powershell
pwsh ./eng/validate.ps1 -Stage AuthorityInventory
pwsh ./eng/validate.ps1 -Stage IsaParity
pwsh ./eng/validate.ps1 -Stage FamilyDifferential
pwsh ./eng/validate.ps1 -Stage Full
```

`IsaParity` обязателен только если family/change затрагивает ISA/generated/compiler surfaces; не использовать зелёный ISA gate как proof resource-owner correctness [ArchitectureAuthorityRefactorSummary governance][governance].

[overview]: ../../../ArchitectureAuthorityRefactor/00_Overview/00_README.md
[migration]: ../../../ArchitectureAuthorityRefactor/04_CoreMigration/04_RF07_RF13_Core_Migration.md
[governance]: ../../../ArchitectureAuthorityRefactor/05_Governance/05_Invariants_Dependency_Risks_DoD.md
[paper-3]: ../../../../ResearchPaper/section/md%20base/3_Architectural_Overview_and_Frontend_Contract.md
[register-id]: ../../../../HybridCPU_ISE/CloseToHSL/Core/Architecture/Registers/Architectural/RegisterIdentity.cs
[slot-id]: ../../../../HybridCPU_ISE/CloseToHSL/Core/Architecture/BinaryFormat/SlotEncoding/SlotId.cs
[lane-id]: ../../../../HybridCPU_ISE/CloseToHSL/Core/Pipeline/Scheduling/LaneId.cs
[memory-resolution]: ../../../../HybridCPU_ISE/CloseToHSL/Core/Decoder/MemoryBankResolution.cs
[physical-index]: ../../../../HybridCPU_ISE/CloseToHSL/Memory/Subsystem/PhysicalMemoryBankIndex.cs
[physical-binding]: ../../../../HybridCPU_ISE/CloseToHSL/Memory/Subsystem/PhysicalMemoryBankBinding.cs
