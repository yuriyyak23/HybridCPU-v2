# Replay identity and invalidation

> Repository workflow, не architecture authority. RF-09 semantic replay key, immutable entry, lookup/serving и invalidation изменяются только в границах paper; `ReplayToken` rollback и semantic replay cache — разные bounded surfaces [paper §7][paper-7].

## 1. Scope и non-goals

Применять, если decode semantics получает новый context/sideband, меняется replay lookup, появляется invalidation reason или новый carrier должен безопасно обслуживаться immutable replay entry.

Не использовать для выдачи старого `VliwOperationId`, восстановления rename/free-list/pipeline image или token-only equality. Paper ограничивает rollback explicitly captured register/memory surfaces [paper §10.2][paper-10].

## 2. Identity layers

| Layer | Owner | Содержит | Не содержит |
|---|---|---|---|
| Semantic key | Canonical decode/replay | Raw bytes + annotation hash + manifest/hash + extension/decoder/privilege/domain/address-space/vector/code epochs | Admission, lane, attempt |
| Frozen entry | Replay semantic owner | Key + canonical slots/bundle sidebands + validation fingerprint | Scheduler decision/runtime readiness |
| Issued attempt | Stage B | Fresh `VliwOperationId`, concrete lane, source provenance | Cache equality |
| Rollback token | Explicit capture owner | Только разрешённые captured architectural/memory surfaces | Backend-global state |

Текущие поля и raw-byte equality определены в [`CanonicalDecodedContracts.cs`][canonical]; immutable entry — в [`Rf09ReplayEntry.cs`][entry].

## 3. Workflow

| Шаг | Owner | Input → output | Valid | Fail-closed | Forbidden bypass | Tests/evidence | Rollback |
|---:|---|---|---|---|---|---|---|
| 1. Semantic delta | ISA/decoder owner | New fact → identity relevance decision | Любой execution-relevant delta назван | Неясный sideband блокирует serving | «Hash payload enough» | One-factor inventory | Keep serving disabled |
| 2. Freeze | Canonical owner | Bytes/context/payload → immutable key/bundle | Defensive copies and exact binding | Missing context non-replay-eligible | Mutable references in entry | Freeze/mutation tests | Remove new field |
| 3. Lookup equality | Replay owner | Candidate key → hit/miss | Full equality, включая raw bytes | Any mismatch → miss | Digest/token/PC-only hit | False-hit matrix | Disable lookup |
| 4. Validate content | Replay owner | Entry → integrity/semantic validation | Key and frozen content agree | Stale/mutated entry rejected | Fingerprint as sole equality | Entry validation tests | Fall back live decode |
| 5. Invalidate | Owning event source | State/code/config change → explicit reason/epoch | Invalidation before reuse | Unknown relevant mutation → miss/flush | Partial witness repair without contract | Reason/epoch tests | Disable serving cache |
| 6. Serve | Decode/replay owner | Valid hit → canonical carrier | No scheduler/ID allocation | Unbound/stale entry not served | Hit implies admission | Serving/live-fallback tests | Live decode remains fallback |
| 7. Issue | Scheduler Stage B | Served/live carrier → fresh attempt | Fresh ID only after lane materialization | Reject creates no ID | Reuse cached attempt/lane as authority | Freshness/routing tests | Revert serving integration |
| 8. Rollback | Token/state owner | Explicit capture → bounded restore | Owner/epoch/side-effect gates pass | Missing binding fails closed | Rename/free-list/global rewind | Rollback boundary tests | Disable token contour |

## 4. Mandatory mutation matrix

Менять по одному: raw byte, operand/immediate, canonical annotation, slot/bundle sideband, manifest version/hash, extension fingerprint, decoder version/epoch, privilege, domain, address space, vector configuration, executable invalidation epoch, code generation epoch. Semantic-changing mutation обязана miss; unchanged full identity может hit, но выдаёт fresh attempt [RF-09 tests][replay-tests].

Дополнительно проверить stale entry, corrupted fingerprint, mutable source after freeze, serving-disabled/unbound context, Stage-A/Stage-B reject without ID и live-decode fallback.

```powershell
pwsh ./eng/validate.ps1 -Stage ReplayIdentity
pwsh ./eng/validate.ps1 -Stage Routing
pwsh ./eng/validate.ps1 -Stage FaultInjection
```

## 5. Review checklist

- [ ] Sideband классифицирован как semantic, placement или dynamic state.
- [ ] Raw bytes сравниваются после digest equality.
- [ ] Missing context не aliases valid context.
- [ ] Invalidation owner и event order названы.
- [ ] Replay hit не даёт legality, lane или старый attempt.
- [ ] Rollback scope не расширен и failure atomicity limitation записана.

[paper-7]: ../../../../ResearchPaper/section/md%20base/7_Replay_Stable_Placement_Replay_Tokens_and_Execution_Boundaries.md
[paper-10]: ../../../../ResearchPaper/section/md%20base/10_Related_Work_Limitations_and_Conclusion.md
[canonical]: ../../../../HybridCPU_ISE/CloseToHSL/Core/Decoder/CanonicalDecodedContracts.cs
[entry]: ../../../../HybridCPU_ISE/CloseToHSL/Core/Decoder/Rf09ReplayEntry.cs
[replay-tests]: ../../../../HybridCPU_ISE.Tests/Architecture/Rf092ReplayContextAndInvalidationTests.cs
