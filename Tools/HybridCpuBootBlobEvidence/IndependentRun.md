# Independent Windows launch

Use Start-IndependentRun.ps1 for future Doom evidence Run.ps1 scripts. Supply the absolute
frozen script path under TempEnv/doom-continuation-20260905 and its verified SHA256.
The run script owns image/binary/WAD verification, competing-process and memory preflight,
strict GC, positive cycle budget, manifest, redirected output and native exit persistence.
The launcher does not replace those checks. Never use it to duplicate an active Doom run.

```powershell
./Tools/HybridCpuBootBlobEvidence/Start-IndependentRun.ps1 -RunScript <absolute-new-evidence-Run.ps1> -ExpectedScriptSha256 <verified-sha256>
```

Task Scheduler starts the script using the current interactive user token, with no stored
password, elevated run level, triggers or automatic retries. No scheduler execution timeout;
the guest cycle budget remains authoritative. Launch claim prevents repeated dispatch from
the same evidence directory. Registration identity/XML are persisted before dispatch.
Read scheduler-registration.json to inspect the exact task after a Codex disconnect.
Remove only that registration after verified terminal completion; never stop an active task.
Interactive logon is required; surviving logout/reboot is outside this contract.

## Reproducible tiny launcher acceptance

`Test-IndependentRun.ps1 -EvidenceRoot <new-continuation-directory> -Runner <isolated-Release-HybridCpuBootBlobEvidence.dll>`
creates a frozen runner with CreateNew, freezes the tiny fixture image before dispatch,
and calls this launcher exclusively. Use Windows PowerShell and a PATH containing dotnet.
Build the tool with `DefineTestSupport=false`, the existing bridge `Validation/Isolation.props`,
and a new `BridgeValidationRoot`; isolate TEMP, TMP, DOTNET_CLI_HOME, NUGET_PACKAGES and
HybridCpuTestArtifactsRoot beneath the same new iteration. Do not reuse an evidence directory.

The test reuses ObservationLoaderPipeRegression (20,000 cycles per execution segment,
two initializers and Main, baseline/observed parity and an independent client). The frozen
script owns native output/exit persistence and atomic terminal publication. Its terminal
validator checks exact input and artifact hashes, freshness and the finished marker.
Only the test's own registration is deleted, after verified terminal completion.

Current evidence: `launcher-acceptance-20260908-v1/acceptance-04`, 17 passed, 0 failed,
0 skipped; native/dispatcher/scheduler exit 0. This proves the bounded fixture's dispatcher
independence, not Codex application exit survival or Doom qualification. Attempt 01 exposed
a test harness process-handle/ExitCode issue, corrected in the test scripts. Attempt 02
has scheduler result 0xC000013A and no terminal report; cause and native outcome remain
Unavailable. Attempts 03/04 do not erase that failure. Its registration is retained.

Historical artifacts `diagnostic47-independent-100m-v1` and
`launcher-independent-loader-v3` are unavailable in this session; the historical statements
below are provenance notes, not current proof.

Validation: launcher-independent-v2 under the continuation root. Actual launcher dispatch
exit 0; probe alive after dispatcher exit, parent svchost.exe; probe and Task Scheduler exit 0.
Own completed test registration removed. This proves dispatcher independence, not an observed
Codex application shutdown. Loader-backed validation also passed in launcher-independent-loader-v3:
the dispatcher exited before the fixture completed; two initializers/Main and independent client
preserved baseline outcome/GC/retired identity, native and scheduler exit 0.
Actual Codex application exit still needs verification. CPU47 was already active before this
launcher and has not been migrated/restarted.
