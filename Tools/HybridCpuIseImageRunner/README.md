# HybridCpuIseImageRunner: passive report diagnostics

The existing positional HCEXE execution mode is preserved. A separate mode imports a selected report without inspecting, loading or executing an image:

```powershell
dotnet <path-to-built-HybridCpuIseImageRunner.dll> --report 'C:\reports\selected.json'
dotnet <path-to-built-HybridCpuIseImageRunner.dll> --report 'C:\reports\selected.json' --image 'C:\images\selected.hcexe'
```

`--report` dispatches before the execution path. Both files are explicitly selected by the caller; the command never opens the `image` path inside the JSON and never scans directories. `--image` is optional and only supplies package bytes for SHA-256 checking. The report stream is passed directly to `CpuInterfaceBridge.Diagnostics.CompilerToolExample.ReadFailure`. The new bridge project reference supplies that API; it does not instantiate the legacy facade.

Stdout is one JSON object with schema `hybridcpu.ise-image-runner.report-diagnostics/v1`, mode `PassiveReportImport`, report source, diagnostic outcome, `executionStartedByThisCommand: false`, qualification `Unavailable`, and the failure card. Enum names are strings. The card retains the exact reported reason. The code location refers to the reported final PC, and code-record coverage may be partial. Unavailable per-ECALL reasons are not reconstructed from report-level text.

| Exit | Meaning |
| --- | --- |
| 0 | Valid report imported. Does **not** imply guest completion, blocker resolution, trusted execution evidence or image qualification. |
| 2 | Invalid report-mode arguments. |
| 3 | File access, JSON/schema or value validation failed; stderr contains the diagnostic and original exception message. |
| 4 | Explicit package bytes disagree with the report SHA. Stdout still contains the card with `Mismatch`; stderr explains the failure. |

Without report SHA, supplied bytes remain `BytesOnlyUnbound`; this is not claimed as a verified match. Without supplied bytes, SHA remains `ReportedOnly`. Report session identity is an import identity, not a runtime run ID. Import success is independent of the reported execution status. Qualification is never inferred.

From the repository root, use the isolated validation entry point (all build/dependency/intermediate/log artifacts under the bridge):

```powershell
powershell -NoProfile -File Compilers/CpuInterfaceBridge/Validation/Validate.ps1 -IncludeRunner
```

The script builds the runner sequentially, then runs the scope harness which compiles the actual `ReportCommand.cs` and Diagnostics/legacy sources. A nonzero integration build is preserved in the script exit even if the harness passes. No validation command runs the positional HCEXE mode. See `Compilers/CpuInterfaceBridge/Diagnostics/README.md` for evidence limits and the validation record.
