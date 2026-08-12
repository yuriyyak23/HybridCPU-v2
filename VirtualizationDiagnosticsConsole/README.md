# Virtualization Diagnostics Console

## PR-J exact release activation/rollback profile

`prj-exact-release-activation-rollback` executes the real default-disabled exact
profile, rejects an adjacent leaf, provisions the existing neutral domain and
typed capability contours, observes live E2, then runs the ordered kill switch
to transition/E2/E3/E5/E6 zero, revokes exact binding/grant and confirms the
deterministic fault-only fallback. It emits structured counters and NDJSON trace.

```powershell
dotnet run --project VirtualizationDiagnosticsConsole -- prj-exact-release-activation-rollback --iterations 50
dotnet run --project VirtualizationDiagnosticsConsole -- vmcall-denied --iterations 50
dotnet run --project VirtualizationDiagnosticsConsole -- e1-fault-transport --iterations 50
```

This scenario exercises runtime contracts but remains diagnostic evidence. It
does not issue release authority and cannot substitute for the immutable clean
subject plus later non-self-referential release record.

## PR-I drain/restore/determinism profile

`pri-drain-restore-determinism` executes the real E7 lifecycle for the exact
no-state probe. It demonstrates that live host-owned E5 blocks checkpoint,
cancel/drain returns all E2/E3/E5/E6 registries to zero, the checkpoint contains
policy identity only, restore advances generation once, duplicate restore is
denied, and repeated architectural traces have no writes.

```powershell
dotnet run --project VirtualizationDiagnosticsConsole -- pri-drain-restore-determinism --iterations 50
```

This is exact-slice migration/determinism evidence, not a general checkpoint
format, compatibility authority, compiler gate or release proof.

## PR-A D2 v2 governance/negative profile

`pra-d2-governance-negative` directly exercises the immutable SpecV2 and
AcceptanceRecordV2 contracts, the versioned binary canonical encoder, SHA-256
verification, exact-byte checks, fail-closed validator, zero-owner denial,
adjacent-leaf denial and missing-CODEOWNERS denial. It emits structured counters
and NDJSON trace tagged `governance-negative-only`.

The profile deliberately uses no completed review evidence and produces no
accepted instance, owner registry, O1, E2, executor, completion record or retire
permission. A passing diagnostic is testing/model evidence only. Run it together
with the production invariants:

```powershell
dotnet run --project VirtualizationDiagnosticsConsole -- pra-d2-governance-negative --iterations 50
dotnet run --project VirtualizationDiagnosticsConsole -- vmcall-denied --iterations 50
dotnet run --project VirtualizationDiagnosticsConsole -- e1-fault-transport --iterations 50
```

## PR-E exact executor/no-publication profile

`pre-exact-probe-executor-no-publication` executes the real default-off neutral
executor contract. With its exact owner review switch enabled, it consumes one
live E2 exactly once and returns one opaque E3 with canonical non-zero no-effect
and no-result digests. It also checks default-off rollback, duplicate execution
denial and restore invalidation. The scenario is service-level evidence only:
the executor is not connected to decode/VMX compatibility or the production
pipeline, and it cannot publish completion or retire.

```powershell
dotnet run --project VirtualizationDiagnosticsConsole -- pre-exact-probe-executor-no-publication --iterations 50
dotnet run --project VirtualizationDiagnosticsConsole -- vmcall-denied --iterations 50
dotnet run --project VirtualizationDiagnosticsConsole -- e1-fault-transport --iterations 50
```

## PR-F canonical E4 composition/no-publication profile

`prf-canonical-hypercall-composition` executes the real production-compiled
canonical scheduler/execute chain. A configured neutral binding lets only the
lane-7 E1 plus immutable operand seam issue E2; `VmxMicroOp.Execute` consumes its
sealed dispatch once and records E3. The scenario also proves default/no-binding
rollback, adjacent-leaf denial and disable-before-execute revocation. VMX retire
continues to fault, and completion/retire publication counters remain zero.

```powershell
dotnet run --project VirtualizationDiagnosticsConsole -- prf-canonical-hypercall-composition --iterations 50
dotnet run --project VirtualizationDiagnosticsConsole -- vmcall-denied --iterations 50
dotnet run --project VirtualizationDiagnosticsConsole -- e1-fault-transport --iterations 50
```

## PR-B attributable machine-D2 policy profile

## PR-G atomic completion/E5 profile

`prg-atomic-completion-e5` executes the real canonical E3-to-completion contour.
Only the neutral completion owner consumes a live exact E3 after split
completion-only route and pure fence policy, then atomically emits one neutral
`CompletionRecord` and one opaque record-bound E5. The scenario proves the
missing-owner rollback and keeps compatibility publication and retire counters
at zero; VMX retire remains fault-only.

```powershell
dotnet run --project VirtualizationDiagnosticsConsole -- prg-atomic-completion-e5 --iterations 50
dotnet run --project VirtualizationDiagnosticsConsole -- prf-canonical-hypercall-composition --iterations 50
dotnet run --project VirtualizationDiagnosticsConsole -- vmcall-denied --iterations 50
dotnet run --project VirtualizationDiagnosticsConsole -- e1-fault-transport --iterations 50
```

## PR-H canonical retire/E6 profile

`prh-canonical-retire-e6` executes the real neutral retire-owner E5-to-E6
contract for the exact no-state probe. It issues one opaque E6 for a canonical
head/window/order identity, consumes it exactly once, denies duplicate use, and
reports zero register, memory, VM-state, or compatibility-success effects. This
is diagnostic evidence, not E7, migration, release, or broad activation proof.

```powershell
dotnet run --project VirtualizationDiagnosticsConsole -- prh-canonical-retire-e6 --iterations 50
dotnet run --project VirtualizationDiagnosticsConsole -- prg-atomic-completion-e5 --iterations 50
dotnet run --project VirtualizationDiagnosticsConsole -- vmcall-denied --iterations 50
dotnet run --project VirtualizationDiagnosticsConsole -- e1-fault-transport --iterations 50
```

`prb-d2-attributable-materialization` resolves the committed SpecV2 and
CODEOWNERS blob, validates the completed logical owner/architecture role receipts
and AcceptanceRecordV2, and checks the generated exact namespace/leaf lookup.
It is governance policy evidence only: no capability grant, O1, admission,
backend, completion or retire authority is created.

## PR-C O1/operand fault-only profile

`prc-o1-operand-fault-only` exercises the real execution-only common-legality
contract, immutable O1 loader and one-time full-value operand snapshot after
the existing E1 seam. It is fault-only evidence: E2, backend execution,
completion and retire publication counters remain zero.

```powershell
dotnet run --project VirtualizationDiagnosticsConsole -- prc-o1-operand-fault-only --iterations 50
```

## PR-D E2 admission/fault-only profile

`prd-e2-admission-fault-only` executes the real production-compiled
SafetyVerifier E2 schema and issuance/validation contracts with exact D2/O1,
canonical operands, a generation-bearing typed grant, runtime-root epoch and
execution-only common admission. It then proves duplicate, capability-revocation
and restore-generation denial. This is governance/admission evidence only:
there is no executor, backend execution, completion or retire publication, and
the production VMCALL carrier remains fault-only.

```powershell
dotnet run --project VirtualizationDiagnosticsConsole -- prd-e2-admission-fault-only --iterations 50
dotnet run --project VirtualizationDiagnosticsConsole -- vmcall-denied --iterations 50
dotnet run --project VirtualizationDiagnosticsConsole -- e1-fault-transport --iterations 50
```

Изолированный исследовательский стенд контура виртуализации HybridCPU ISE.

Он не объявляет production-активацию и не добавляет VMX backend. Реальные сценарии
вызывают текущие ISE-контракты, а модельные сценарии помечаются `ModelContract` и
служат эталоном для будущего положительного пути.

## Быстрый запуск

```powershell
dotnet run --project VirtualizationDiagnosticsConsole -- matrix --iterations 150
dotnet run --project VirtualizationDiagnosticsConsole -- e1-fault-transport --iterations 150
dotnet run --project VirtualizationDiagnosticsConsole -- research-canonical-composition --iterations 150
dotnet run --project VirtualizationDiagnosticsConsole -- list
```

Каждый сценарий матрицы исполняется в отдельном процессе. Артефакты сохраняются в
`TestResults/VirtualizationDiagnosticsConsole`: manifest, profile, stdout/stderr,
структурированный result и NDJSON trace. Модельный профиль использует фиксированный
seed и исчерпывающе перебирает комбинации полномочий; runtime trace при этом сохраняет
живые идентификаторы E1 и не выдаётся за побитово воспроизводимый артефакт.

## Профили

- `e1-fault-transport` — настоящий E1 SafetyVerifier, его отрицательные проверки,
  fault-only VMX execution и retire без register writeback.
- `guest-control-projection` — guarded read-only GuestCr0/GuestCr4 и fail-closed
  отрицательные варианты.
- `vmcall-denied` — admitted trap projection при гарантированно запрещённом backend.
- `research-runtime-probe` — положительный state-minimal/no-payload runtime probe:
  SafetyVerifier повторно проверяет живой E1 и типизированный runtime context, выдаёт
  непрозрачный prototype-допуск, а нейтральный runtime owner исполняет его ровно один
  раз. Профиль существует только в `TESTING`-сборке, не назначает numeric VMCALL leaf,
  проверяет отзыв E1 до и после допуска и не публикует completion/retire.
- `research-canonical-composition` — отдельный P2 runtime-профиль. Он явно включает default-off TESTING-only
  композицию только на канонической границе issue/materialization и за 150 итераций проверяет live/stale E1,
  replay/squash, source/working slot, VT/context/domain/address-space, capability/evidence/restore generations,
  foreign owner/context, stale policy/context, post-admission revocation, duplicate и concurrent exact-once
  consumption. Структурированные counters и NDJSON trace являются только testing/model evidence; профиль не
  назначает numeric leaf и не даёт production backend/completion/retire authority.
- `authority-state-model` — явно модельный oracle разделения result, completion и retire.

Успех модельного профиля не является доказательством реализации backend,
completion publication или retire publication.
