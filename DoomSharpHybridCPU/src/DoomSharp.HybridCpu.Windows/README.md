# DoomSharp HybridCPU AOT GUI

This is a separate .NET 10 x64 WPF host. `DoomSharp.Windows` remains the existing
CoreCLR reference application. This project references the real production ISE,
compiler image contracts, ManagedRuntime and RuntimeKernel projects; it does not
reference `DoomSharp.Core` or the guest assembly. No test hooks, reflection-based
guest invocation, CoreCLR fallback, automatic compiler run or publish is used.

## Implemented

- File selection for an actual `.hcexe` and a local IWAD/PWAD.
- Bounded file reads and the existing `HybridCpuRestrictedImageBuilderV1.Inspect`
  checks: package checksum, schema, startup/target/ABI contract identity.
- Entry bundle decoding with `HybridCPU_ISE.Arch.VLIW_Bundle`; this is decoding,
  not execution or instruction-legality qualification.
- WAD header, directory and lump-range validation, streaming SHA-256 and cancellation.
  Structural validity does not establish the presence of the required Doom assets.
- A viewport reserved for native framebuffer output, diagnostics and explicit
  fail-closed admission. No fake gameplay frames are displayed.

## Loader-backed execution wiring

`DoomGuestExecutionService` now connects this host to the existing production
`HybridCpuIseManagedImageLoaderV1` and `HybridCpuIseManagedGuestExecutionRunnerV1`.
It performs image inspection, runtime-owned image materialization and bounded ISE
pipeline execution without a CoreCLR fallback. A failed loader or CPU result is
returned as a diagnostic outcome and never treated as success.

The service copies validated WAD bytes through the production boot-blob binding into
a rooted runtime-owned guest array. It supports strict GC evidence and optional
owner-thread observation across initializer/Main segments. The published read-only
pipe endpoint is composed by Tools/HybridCpuBootBlobEvidence run-gc, not by this WPF UI.

The **Run ISE** button is disabled. `AdmissionReport.CanExecute` is always false:
inspection is not runtime authority. In particular, a valid scalar `.hcexe` does
not become a runnable Doom image. The admission diagnostic is separate from the
implemented loader/ECALL service path. Remaining full-game gates include
input/frame/palette transport, EH/safepoints and managed process exit.
The host neither qualifies `eh-unwind`
nor substitutes an architectural trap for managed exit.

The current diagnostic Doom HCEXE is available in HCEXEBULDS as
`DoomSharp.HybridCpu.Guest.unqualified.hcexe` (122618004 bytes), SHA256
`4a0f6886f9a9220a4cd0396f61230feb57394249665fe57808309aa5a6991092`.
This is an exact copy of diagnostic37 evidence, not a new publish or qualified build.
Its rejection sidecar preserves HCPUB1004: deterministic-allocation, eh-unwind and
managed-unmanaged-interop remain unsupported. Historical publish exit 1 / adapter 25.
CPU45 reached R_Init under a cycle budget; completion/GC/EH/process-exit qualification
remains open. The UI admission gate remains unchanged.

Future diagnostic runs must use Tools/HybridCpuBootBlobEvidence/Start-IndependentRun.ps1
with a frozen evidence Run.ps1 and verified SHA, so Task Scheduler owns the host lifetime.
See IndependentRun.md beside that script for validation and logon limitations.

## Build without touching source output directories

From the `DoomSharpHybridCPU` directory, using its pinned SDK:

```powershell
.\tools\Build-HybridCpuGui.ps1 -OutputRoot '..\TempEnv\doom-continuation-20260905\windows-host-build-NEW'
```

Choose a new output folder for each build. The script refuses existing folders
and paths outside this repository's `TempEnv`. It sets TEMP/TMP, stages WPF source
so even the transient WPF project remains under TempEnv, redirects bin/obj outputs,
and preserves ISE's existing lineage checks. It never publishes the guest.

Launch the produced executable:

```powershell
& '..\TempEnv\doom-continuation-20260905\windows-host-build-NEW\artifacts\bin\DoomSharp.HybridCpu.Windows\release\DoomSharp.HybridCpu.Windows.exe'
```

For IDE navigation, open `src/DoomSharp.HybridCpu.slnx`. Use the script above for
builds that must keep all transient files under TempEnv.

## Verification

The small `DoomSharp.HybridCpu.HostTests` console project checks valid WAD/image
inspection, malformed magic, directory/count/range errors, checksum tampering,
missing image, DLL rejection, scalar-image launch rejection and cancellation.
Run with TEMP/TMP and `--artifacts-path` under TempEnv. A scratch directory under
TempEnv is a required program argument. It does not invoke the AOT backend or ISE.

The GUI also supports `--ui-smoke <absolute-png-path>` to render its real WPF window
offscreen and exit, without starting a compiler, CPU or guest. Supply a PNG path
under TempEnv. This checks WPF startup and layout, not interactive/gameplay parity.
