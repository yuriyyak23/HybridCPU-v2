# SecureComputeDiagnosticsConsole

## Expanded console results

Detailed mode now prints the diagnostic evidence directly to the terminal in
addition to preserving JSON/NDJSON artifacts. Per scenario it includes worker
exit/timeout state, completion percentage, assertions per iteration, finding
severity totals, trace event distribution, the last structured trace
observation, zero/non-zero counter totals, and direct paths to result, trace and
worker logs.

The final report includes a scenario matrix, aggregate counters and an open
blocker/warning register. `Harness result` reports whether the diagnostic code
ran successfully; `Architecture gate` separately reports whether the collected
findings still block SecureCompute activation. A green harness result therefore
cannot be mistaken for runtime or release authority. Use `--compact` when only
one line per scenario and the compact final totals are needed.

Изолированная консоль диагностики слоя SecureCompute HybridCPU-v2. Проект построен по модели `VirtualizationDiagnosticsConsole`: каждый сценарий запускается в отдельном дочернем процессе с тайм-аутом, heartbeat, NDJSON trace и атомарно записываемыми JSON-артефактами.

Главное отличие — подробный вывод непосредственно в консоль. Для каждого сценария показываются:

- тип исследуемой поверхности (`RuntimeContract`, `PolicyClassifier` или `StaticInspection`);
- число итераций и проверенных инвариантов;
- диагностические счётчики;
- находки уровней `INFO`, `WARNING` и `BLOCKER`;
- точный предел полномочий результата;
- SHA-256 трассы и путь к полному набору артефактов.
- commit SHA, dirty-state, .NET SDK/runtime и ОС для воспроизводимости запуска.

## Запуск

```powershell
dotnet run --project SecureComputeDiagnosticsConsole -- matrix
dotnet run --project SecureComputeDiagnosticsConsole -- list
dotnet run --project SecureComputeDiagnosticsConsole -- descriptor-maps --iterations 25
```

Подробный вывод включён по умолчанию. `--compact` оставляет одну строку на сценарий. Дополнительные параметры:

- `--iterations N` — число повторов каждого сценария;
- `--timeout-ms N` — тайм-аут дочернего процесса;
- `--artifacts PATH` — другой корень артефактов;
- `--fail-fast` — остановка матрицы после первой технической ошибки;
- `--seed N` — воспроизводимый seed профиля.

По умолчанию артефакты сохраняются в `TestResults/SecureComputeDiagnosticsConsole/<timestamp>_<command>/`. Batch содержит `manifest.json`, а каталог сценария — `profile.json`, `heartbeat.json`, `result.json`, `trace.ndjson`, `stdout.log` и `stderr.log`.

## Сценарии

| Сценарий | Что проверяется | Что результат не доказывает |
|---|---|---|
| `admission-boundary` | ordinary baseline, missing/disabled descriptor, generic positive policy decisions | CPU Stage-B reachability, SafetyVerifier certificate, execution |
| `descriptor-carriers` | context/request carriers и context-first precedence | lifecycle owner или registry authority |
| `grant-authority` | текущая проверка handle/source/epoch/revoked inputs | mint/revoke ledger |
| `descriptor-maps` | порядок разрешения overlapping memory/shared-buffer entries | memory-controller, DMA/IOMMU enforcement |
| `checkpoint-payload` | forbidden и unknown payload classification | checkpoint/restore protocol |
| `fail-closed-boundary` | Phase 20/22, VMX zero-authority и compiler no-emission | positive runtime или release authority |
| `static-reachability` | ограниченный inventory точных owner names и известных shortcut patterns | исполнимость или достижимость |

Успешный (`PASS`) сценарий означает, что диагностический контракт отработал и наблюдения сохранены. Наличие `BLOCKER` при `PASS` ожидаемо: это подтверждённый открытый архитектурный разрыв, а не технический сбой самой диагностики.

Текущая граница утверждения: positive SecureCompute runtime execution, secure completion/retire publication, compiler secure emission, nested execution и limited/production release остаются запрещёнными. VMX допускается только как read-only zero-authority projection.
