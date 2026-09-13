# AOT preflight result

## 2026-09-01 scheduler-through-guest-callee iterations (current)

Fresh immutable evidence is under `TempEnv/doom-loop-budget-20260901`,
`doom-symbolic-target-20260901`, `doom-abi-alias-20260901`, and
`doom-guest-callee-20260901`. Each completed cycle passed targeted/full
ManagedPortSmoke, Doom CoreCLR 26/26, Core/Windows/guest Release builds,
compiler/runtime/RuntimeKernel builds, a verified 217-file pack, and the unchanged
1937-owned/37-external/202-type closure (26 cctors, 2 EH methods, 20 throw, 1 rethrow).

Closed blockers, in reached order: already-rejected call-containing loops no longer
materialize a useless 20,100-edge distance DAG or overwrite `unsupported-loop-call`;
symbolic branch targets are encoded as byte addresses and preserve the exact CIL
target through allocation; helper argument/result ABI bridges publish x10 aliases
before dependency scheduling; linker-owned guest-service HCO symbols are admitted as
direct callees while unknown symbols remain rejected. Real-body probes proved
`GameController..ctor` allocation/543-bundle relocation and `CheatSequence..ctor`
x10 result-before-next-argument ordering.

The latest normal publish still exits 1 / adapter 25 at `HCPUB1004: unsupported
managed publish workstream(s): eh-unwind`. The latest diagnostic exits 1 /
publish-graph 31 / adapter 15 at `HCSCF-LINK4003`: a real intra-method branch from
instruction 27 to 188 exceeds the signed 16-bit byte displacement. An independent
fail-fast body-world scan confirms the same compiler-layer range blocker in
`DoomSharp.Core.GameLogic.Trigger.TeleportEvent`, assembly `DoomSharp.Core`, IR 310 /
231 values: CIL `IL_002a` to `IL_0196` becomes final IR 71 to 391. This now requires
exact long-branch relaxation; truncation or target rewriting is forbidden. No Doom
`.hcexe` exists and no ISE execution is claimed.

## 2026-08-31 scheduler iteration (supersedes checkpoints below)

Evidence and reproducible commands: `TempEnv/doom-scheduler-20260831/`:
`baseline.ps1`, `preflight.ps1`, `publish.ps1`. All new outputs are isolated there.
An initial invocation outside the Doom SDK-pinned directory selected SDK 11 preview
and correctly failed HCPUB1007 (adapter 25, publish 1). Running from Doom selects
its existing global.json / SDK 10.0.204; qualification was not relaxed.

Recovered-pack baseline backend PID 15660 was deliberately stopped after 327.197 s,
339.219 CPU s, 20,217,946,112 private bytes and 20,119,851,008 working-set bytes.
Fresh EventPipe `baseline-3.nettrace` / `baseline-profile-3.log` confirms
CompilePresentedImage -> Link -> CompileMethod -> ScheduleProgram ->
BuildTransitiveSuccessorCounts -> CollectDescendants. Its terminal publish exit 1
(adapter 31 / MSB3073 after forced backend stop) is NOT a semantic diagnostic.

The scheduler now computes exact descendant sets by reverse-topological bitset union.
Priorities and tie-breaking are unchanged; cycles, duplicate node identities and
invalid outgoing edges fail closed. Storage is V*ceil(V/64) words plus O(V);
work is O(V+E) topology, E membership checks, at most E*ceil(V/64) unions and
V*ceil(V/64) popcounts. Already included successors skip only redundant full unions.
Regression tests compare with an independent DFS oracle, cover shared/disconnected
and reordered graphs, malformed graphs, deterministic schedules and bounded work.
The 16384-node chain used 49149 edge visits / 4194048 word unions; a 1024-node
complete DAG used 16368 word unions. Targeted tests and full ManagedPortSmoke pass
(exit 0; 43 PASS lines in the current full log), Doom CoreCLR tests pass 26/26.
Core, Windows, guest, compiler, ManagedRuntime and RuntimeKernel Release builds pass.

New immutable `pack193-scheduler` has 217 verified files, freshly built compiler
binaries and byte-identical pinned ILCompiler/references from recovered pack193.
Fresh conservative closure: 1937 owned methods, 37 external members, 26 cctors,
2 EH methods, 20 throws and 1 rethrow. These are not completed HCO counts.
Normal publish still fails HCPUB1004: unsupported managed publish workstream(s):
eh-unwind (adapter 25, publish 1).

New-pack diagnostic terminated naturally at 22:21:24 +03:00 with backend exit 15,
publish-graph exit 31 (HCPUB2002) and dotnet publish exit 1 (MSB3073):
`HCSCF-LINK4003: Register allocation rejected method: BudgetExhausted: The
deterministic allocation input budget was exceeded.` Thus the scheduler DFS blocker
is passed, but allocation is not qualified for this method. The CLI omits method
identity; the separate read-only `BudgetProbe` rebuilds the same AdapterPresented
body world from the diagnostic DLL and exact pack metadata. It identifies the first
allocation-input budget failure in deterministic compilation order (ordinal 36):

- Assembly: `DoomSharp.Core,1.0.0.0,neutral,none`; module `DoomSharp.Core.dll`.
- Method: `DoomSharp.Core.GameLogic.MapObjectInfo.AddPredefinedTypes():System.Void`.
- Stable identity: `mmid:247d69086b2108b758c425152f06f8035c793a31d24dadbca959e4bcea625051:DoomSharp.Core.GameLogic.MapObjectInfo.AddPredefinedTypes():System.Void`.
- Input: 15038 IR instructions, 9557 values, one block; limits: 4096 / 8192.
- Layer: `HybridCpuScheduleAwareRegisterAllocatorV1.Allocate`, qualification options
  using `HybridCpuRegisterAllocationBudgetsV1.Production`.
- Faulting IL offset: not applicable to this whole-method budget gate. Reachability
  ingress is `MapObjectInfo..cctor():IL_000f` (ordinary direct call).

The probe reports 1913 admitted methods, NOT 1913 compiled HCOs. It does not run a
guest or bypass allocation. Budgets were not increased and no later blocker was
changed. The next iteration must establish bounded allocation capacity for the
actual input before changing admission. The diagnostic also reached approximately
26 GiB private memory during conservative scheduling-region materialization;
`diagnostic-profile-2.log` records that later path, not a new semantic error.
No Doom .hcexe, clean-publication hash pair or ISE execution exists. Loader-backed
WAD and production ECALL bridge remain gates. See TempEnv `Result.md` and logs.

## GUI continuation: separate AOT workbench, no execution qualification

`src/DoomSharp.HybridCpu.Windows` now supplies a .NET 10 WPF workbench referencing
production ISE, image-inspection, ManagedRuntime and RuntimeKernel assemblies. It
does not reference Doom Core/guest assemblies or execute their managed entrypoints.
Image/WAD inspection and real ISE entry-bundle decoding are implemented; native Doom
execution is not. Host gate `HCDOOMGUI1100` names the missing managed loader/ECALL
bridge. This is a host-integration diagnostic, not a new compiler preflight blocker.
The existing CoreCLR `DoomSharp.Windows` is unchanged. Host admission tests passed
14 checks; WPF startup/offscreen rendering was verified. No full AOT preflight or
Doom publish was restarted. See the new host README and `tools/Build-HybridCpuGui.ps1`.

## Current checkpoint: 2026-08-31 build-only pack recovery; preflight not rerun

At the user's explicit instruction, rebuilt compiler, ManagedRuntime/RuntimeKernel,
Doom Core, Windows and guest in Release under `TempEnv/doom-20260831-rebuild`.
All five build commands succeeded. Windows builds use a SHA-256-verified source copy
inside TempEnv because WPF can create a temporary project beside its input project.
The immutable `pack193` contains 217 verified files. Compiler binaries were rebuilt;
the pinned ILCompiler and runtime references were restored byte-for-byte from the
previously verified `TempEnv/doom-20260831-a/pack193`. No files were restored into
the partially deleted `Diagnostics/HybridCpuAotAddDemo/.hcpu-p193` directory.
The guest project now defaults to the recovered TempEnv pack.

No tests, diagnostic preflight or publish were launched after the build-only request.
The earlier diagnostic ended with MSB3073 / process exit 1 without an inner compiler
diagnostic; this is not evidence of a new semantic fail-closed blocker. Its sampled
stack was in `CompileMethod -> BuildTransitiveSuccessorCounts -> CollectDescendants`.
Do not claim the full backend, EH, loader or ECALL bridge qualified from these builds.
`eh-unwind` remains absent from `PublishQualifiedWorkstreams`. No new `.hcexe` or
pair of executable hashes exists. Build logs, source hashes and pack verification:
`TempEnv/doom-20260831-rebuild`; reproducible commands: `Rebuild-Only.ps1` there.

Before the build-only request, the fresh conservative CIL report in
`TempEnv/doom-20260831-a/closure.txt` counted 1937 owned methods, 37 external members,
26 cctors, two EH methods, 20 throws and one rethrow. These are conservative analyzer
counts, not proof of the compiler-admitted graph size. CoreCLR 26/26 and 44 reported
ManagedPortSmoke groups passed before recovery; they were not rerun after it.

## Historical checkpoint: installed pack 1.15.188; exact console-title service

p186 narrows token-free interface signature matching to metadata-owned primitive `byte[]` only;
this closes image-owned `IGraphics.ScreenReady(byte[])` candidate discovery without admitting
other arrays. p187 adds Graphics/FPRE over the exact initialized width*height read-only byte buffer;
kernel state rejects presentation before initialization or at any other length.

p188 reuses the bounded immutable String 16/20 projection for an exact Console/CT16 operation.
Both console operations retain the synchronous no-reentry/deferred-GC and read-only UTF-16 ABI;
odd, oversized and argument-bearing requests fail before the provider. ManagedPortSmoke, adapter
Release build, regenerated manifest, fresh `.hcpu-p188`, Doom 26/26 and Core/Windows/guest Release
builds pass. Diagnostic publish confirms `ConsoleSetTitle` is closed and exits 1 at the NEW blocker:
`DoomSharp.HybridCpu.Guest.HybridCpuGraphics.StartTicinstance(System.Object):System.Void`, IL_0006 ->
`runtime-external-interface:DoomSharp.HybridCpu.Guest.IHybridCpuGuestServices.PullInput(System.Object):System.Object`,
interface=7, slot=25, external=True. The exact source return is nullable sealed
`DoomSharp.Core.Input.InputEvent`; a generic host-provider integer is not a managed reference.
Loader/ISE ECALL execution and the loader-managed input registry remain unqualified. No `.hcexe`
or SHA-256 pair.

### Historical p185 checkpoint: trusted boot blob and non-trap process exit

p184 adds File/BLOB lookup through a dedicated trusted managed registry, never the generic host
provider. The registry accepts only an already materialized exact primitive byte[] extent, copies no
payload, registers a process-lifetime Handle root and returns a managed reference only for the exact
blob ID. Missing/malformed registrations fail closed. A one-ECALL native thunk returns that trusted
reference. Ordinary heap object limits were not widened to disguise the still-required large
loader-backed/frozen WAD allocation capability.

p185 binds exact guest `ProcessExit(int)` through a non-returning ABI wrapper: x11 is normalized to
x10 and tail-dispatched to the existing restricted process-exit helper/loader sentinel. Regression
proves the wrapper contains neither ECALL nor EBREAK. ManagedPortSmoke, adapter Release build,
regenerated manifest, fresh `.hcpu-p185`, Doom 26/26 and Core/Windows/guest Release builds pass.
Diagnostic publish confirms GetBootBlob and ProcessExit are closed and exits 1 at the NEW blocker:
`DoomSharp.Core.Graphics.Video.SignalOutputReadyinstance(System.Object,System.Int32):System.Void`,
IL_000e -> `runtime-external-interface:DoomSharp.Core.Graphics.IGraphics.ScreenReady(System.Object,System.Object):System.Void`,
interface=44, slot=686, external=True. The exact source signature is `ScreenReady(byte[])`; its
sealed image-owned implementation is `HybridCpuGraphics.ScreenReady`. The current primitive/String
interface matcher rejects the SZ-array metadata signature, so next is exact metadata-owned byte[]
candidate binding—not a host stub. Loader-backed large boot arrays and ISE ECALL execution remain
unqualified. No `.hcexe` or SHA-256 pair.

### Historical p183 checkpoint: exact Doom deadline park/wake

p183 adds Clock/WAIT with one positive Int32 Doom target. RuntimeKernel converts it to the least
source tick not earlier than the target (`ceil(target * bootFrequency / 35)`) using bounded UInt64
arithmetic, creates a deterministic kernel deadline and parks the context. ECALL results now carry
an explicit Resume/Parked disposition: a loader must retain the pending result and must not resume
the trap frame while Parked. Existing `AdvanceMonotonicTime` wakes the context exactly at deadline.
The native thunk emits one ECALL and only continues after the loader performs that wake/resume.

Regression at 1000 Hz proves target 44 -> source 1258, remains Parked at 1257, becomes Runnable at
1258, and an already-reached target returns Resume. It also checks the linkable one-ECALL HCO.
ManagedPortSmoke, adapter Release build, regenerated manifest, fresh `.hcpu-p183`, Doom 26/26 and
Core/Windows/guest Release builds pass. Diagnostic publish confirms WaitUntilDoomTic is closed and
exits 1 at the NEW first blocker:
`DoomSharp.HybridCpu.Guest.EntryPoint.Run():System.Int32`, IL_0029 ->
`runtime-external-interface:DoomSharp.HybridCpu.Guest.IHybridCpuGuestServices.GetBootBlob(System.Object,System.Int32):System.Object`,
interface=7, slot=19, external=True. The exact return type is byte[] despite the ObjectReference
stack carrier. Correct closure requires a loader-owned immutable boot-blob mapping plus an exact
managed byte[] object/header/length materialization and process-lifetime GC root. Loader/ISE ECALL
execution remains unqualified. No `.hcexe` or SHA-256 pair.

### Historical p182 checkpoint: GC-safe bounded UTF-16 console transition

p182 adds the Console/CW16 synchronous service contract over the exact immutable String layout
(`LengthOffset=16`, `DataOffset=20`, UTF-16LE). The native thunk rejects null, negative and over-1M
code-unit lengths, exposes only the payload as a read-only buffer, emits one ECALL and uses process
exit 255 on failure. RuntimeKernel admits only even, bounded, mapped read buffers (or canonical empty
buffer), no scalar arguments, before provider dispatch. The contract requires no reentry and deferred
GC; the V1 heap is non-moving, so the caller's live reference remains stable across the synchronous
transition. Exact importer binding is limited to the full Doom ConsoleWrite declaration.

Regression covers mapped success/provider trace, odd/oversized/argument payload rejection, one
encoded ECALL and valid HCO. ManagedPortSmoke, adapter Release build, regenerated manifest, fresh
`.hcpu-p182`, Doom 26/26 and Core/Windows/guest Release builds pass. Diagnostic publish confirms
ConsoleWrite is closed and exits 1 at the NEW first blocker:
`DoomSharp.HybridCpu.Guest.DeterministicDoomClock.WaitTicinstance(System.Object):System.Void`, IL_000e
-> `runtime-external-interface:DoomSharp.HybridCpu.Guest.IHybridCpuGuestServices.WaitUntilDoomTic(System.Object,System.Int32):System.Void`,
interface=7, slot=27, external=True. Correct closure requires ceil conversion from target Doom tic
to the boot timebase, kernel deadline parking and loader resume only after wake; no-op wait is invalid.
Loader/ISE ECALL execution remains unqualified. No `.hcexe` or SHA-256 pair.

### Historical p181 checkpoint: exact framebuffer initialization thunk

p181 adds the Graphics/FBIN service contract. Width and height retain their exact Int32 bit
patterns in one packed u64 register argument; RuntimeKernel accepts only positive dimensions up
to 16384, a single argument and no buffer before invoking the provider. The native HCO thunk emits
one ECALL and routes non-success to process exit 255. Importer binding is restricted to the full
exact `IHybridCpuGuestServices.InitializeFramebuffer(object,int,int):void` declaration.

Regression covers 320x200 success, provider trace, zero/negative/oversized dimensions, exact ECALL
count and valid HCO output. ManagedPortSmoke, adapter Release build, regenerated manifest, fresh
`.hcpu-p181`, Doom 26/26 and Core/Windows/guest Release builds pass. Diagnostic publish confirms
InitializeFramebuffer is closed and exits 1 at the NEW first blocker:
`DoomSharp.HybridCpu.Guest.HybridCpuConsole.Writeinstance(System.Object,System.Object):System.Void`,
IL_0007 -> `runtime-external-interface:DoomSharp.HybridCpu.Guest.IHybridCpuGuestServices.ConsoleWrite(System.Object,System.Object):System.Void`,
interface=7, slot=20, external=True. This next member requires exact managed String rooting/pinning,
UTF-16 payload bounds and a read-only service buffer; an object address cannot be exposed directly.
Loader/ISE ECALL callback execution remains unqualified. No `.hcexe` or SHA-256 pair.

### Historical p180 checkpoint: exact native Doom clock thunk

p180 makes the virtual-clock frequency a mandatory nonzero `HybridCpuBootInfoV1` input and adds
the exact Clock/DOOM operation `floor(monotonicTicks * 35 / bootFrequency)`. Its bounded UInt64
conversion rejects Int32 overflow. A native HCO thunk emits one fixed HCSV ECALL envelope, returns
the successful Int32 value, and routes every non-success status to managed process exit 255. The
importer binds only the full exact declaration identity and signature for
`IHybridCpuGuestServices.GetMonotonicDoomTics`; nearby external interface members remain closed.

Regression covers required timebase, exact 1000 Hz -> 35 Hz conversion, malformed gateway inputs,
one encoded ECALL and a linkable thunk symbol. ManagedPortSmoke, adapter Release build, regenerated
manifest, fresh `.hcpu-p180`, Doom 26/26 and Core/Windows/guest Release builds pass. Diagnostic
publish confirms the clock LINK4011 is closed and exits 1 at the NEW first blocker:
`DoomSharp.HybridCpu.Guest.HybridCpuGraphics.Initializeinstance(System.Object):System.Void`, IL_0010
-> `runtime-external-interface:DoomSharp.HybridCpu.Guest.IHybridCpuGuestServices.InitializeFramebuffer(System.Object,System.Int32,System.Int32):System.Void`,
interface=7, slot=22, external=True. Restricted loader/ISE ECALL callback execution is still not
qualified and must remain an image-startup gate before publication. No `.hcexe` or SHA-256 pair.

### Historical p179 checkpoint: trusted clock ECALL envelope

p179 adds a fixed user-mode ECALL register envelope for external services. Native code supplies
only service/operation/buffer and one bounded scalar argument; request signing remains inside the
trusted RuntimeKernel gateway. The gateway validates ECALL identity, user privilege, live context,
enum widths and argument count before constructing the existing signed service request. The exact
Clock/MONO request returns the kernel virtual tick value; malformed, machine-privilege and foreign-
context envelopes fail closed. The interop contract digest now includes this gateway ABI.

This is a real native-to-kernel boundary contract and kernel entry, but it is not yet wired into
the restricted-image loader/ISE system-event path and no guest native thunk is emitted. Therefore
the compiler intentionally retains the guest-service binding gate. ManagedPortSmoke, adapter
Release build, regenerated manifest, fresh `.hcpu-p179`, Doom 26/26 and Core/Windows/guest Release
builds pass. Diagnostic publish exits 1 at unchanged HCSCF-LINK4011:
`DoomSharp.HybridCpu.Guest.DeterministicDoomClock..ctorinstance(System.Object,System.Object):System.Void`,
IL_000f -> `runtime-external-interface:DoomSharp.HybridCpu.Guest.IHybridCpuGuestServices.GetMonotonicDoomTics(System.Object):System.Int32`,
interface=7, slot=26, external=True. Exact next work is the native clock thunk plus loader wiring
from the ISE ECALL event to `ExternalServiceEcallTransition`, including the explicit source-frequency
conversion to Doom's 35 Hz. No `.hcexe`, publication qualification or SHA-256 pair exists.

### Historical p178 checkpoint: kernel-owned virtual-clock service

p178 defines the Clock service MONO read operation and exact interop signature
`hybridcpu.runtime!monotonic_ticks` -> u64, no arguments/buffer. Its contract digest is
included in the interop contract. RuntimeKernel handles this operation after existing
context/privilege/digest/buffer validation, directly from its explicit virtual clock;
it does not call a host provider. Unknown operations retain the existing provider path.
ManagedFixedRateClock now reads through this checked service transition and validates the
result digest instead of accessing MonotonicTicks directly. Source frequency remains an
explicit required input; no ISA changes or wall-clock/cycle fallback were introduced.

Regression checks kernel service selection even with a conflicting mock provider, rejects
foreign context/unsigned requests/payloads, and checks exact u64 native-symbol registration
versus an incompatible i32 signature. Existing rate-conversion tests now exercise this
service path. Full ManagedPortSmoke, adapter Release build, regenerated manifest, fresh
`.hcpu-p178`, Doom 26/26 and Core/Windows/guest Release builds pass.
`dotnet publish .\DoomSharp.HybridCpu.Guest.csproj -c Release -r hybridcpu -p:HybridCpuManagedRequirements= -p:PublishDir=.\publish-phase15-178-diagnostic\`
exits 1 at unchanged HCSCF-LINK4011:
`DoomSharp.HybridCpu.Guest.DeterministicDoomClock..ctorinstance(System.Object,System.Object):System.Void`,
IL_000f -> `runtime-external-interface:DoomSharp.HybridCpu.Guest.IHybridCpuGuestServices.GetMonotonicDoomTics(System.Object):System.Int32`,
interface=7, slot=26, external=True. Kernel/runtime-component evidence does not yet bind
that guest CIL call to a native thunk/loader service. No publish-workstream qualification,
Doom .hcexe or publication SHA-256 pair.

### Historical p177 checkpoint: virtual clock conversion component

Inspection found RuntimeKernel.MonotonicTicks/AdvanceMonotonicTime as the explicit virtual
clock seam, but no specified mapping of its tick unit to Doom's 35Hz unit and no native
IHybridCpuGuestServices provider. p177 adds ManagedRuntime HybridCpuManagedFixedRateClockV1:
source frequency is mandatory, ReadTics(35) reads kernel virtual time, and exact integer
conversion rejects Int32 overflow. Fraction scaling uses bounded 64-bit arithmetic, not
floating point, UInt128, wall time or CPU cycles. This component does NOT implement or
qualify the missing native service binding, and no default source frequency is invented.

VirtualClockSmoke checks real deterministic kernel reads against BigInteger arithmetic:
35Hz boundaries, UInt64 frequency/time limits, repeated reads, invalid rates, backward
kernel time rejection and Int32 overflow. Full ManagedPortSmoke, adapter Release build,
regenerated manifest, fresh `.hcpu-p177`, Doom 26/26 and Core/Windows/guest Release builds pass.
`dotnet publish .\DoomSharp.HybridCpu.Guest.csproj -c Release -r hybridcpu -p:HybridCpuManagedRequirements= -p:PublishDir=.\publish-phase15-177-diagnostic\`
exits 1 at the unchanged HCSCF-LINK4011:
`DoomSharp.HybridCpu.Guest.DeterministicDoomClock..ctorinstance(System.Object,System.Object):System.Void`,
IL_000f -> `runtime-external-interface:DoomSharp.HybridCpu.Guest.IHybridCpuGuestServices.GetMonotonicDoomTics(System.Object):System.Int32`,
interface=7, slot=26, external=True. Next remains native runtime/loader service binding with
an explicit timebase. No eh-unwind qualification, Doom .hcexe or publication SHA-256 pair.

### Historical p176 checkpoint: IConsole String binding advanced

p176 admits exact ELEMENT_TYPE_STRING in token-free interface signature matching and slot
identity. STRING is never equated with OBJECT despite the common stack carrier; regression
with Write(string)/Write(object) overloads admits only the String implementation bodies.
Non-sealed direct implementations are admitted only if they have no descendant definition
in the admitted or metadata-only world. This covers Core NullConsole without changing its
public source API. A metadata-only subclass regression invalidates that leaf proof and
retains external binding. Existing interface-inheritance/generic/MethodImpl gates remain.

Full ManagedPortSmoke, adapter Release build, regenerated manifest, fresh `.hcpu-p176`,
Doom 26/26 and Core/Windows/guest Release builds pass. Diagnostic command:
`dotnet publish .\DoomSharp.HybridCpu.Guest.csproj -c Release -r hybridcpu -p:HybridCpuManagedRequirements= -p:PublishDir=.\publish-phase15-176-diagnostic\`
exits 1 at the NEW first binding:
HCSCF-LINK4011, `DoomSharp.HybridCpu.Guest.DeterministicDoomClock..ctorinstance(System.Object,System.Object):System.Void`,
IL_000f -> `runtime-external-interface:DoomSharp.HybridCpu.Guest.IHybridCpuGuestServices.GetMonotonicDoomTics(System.Object):System.Int32`,
interface=7, slot=26, external=True; no image-owned implementation/guest-service binding.
IConsole.Write(string) is no longer the first blocker. This new member belongs to the
loader-owned guest services contract; an actual runtime/service binding is needed, not a
fabricated clock, CPU-cycle substitution or no-op provider.

Regenerated conservative AotPreflightClosure.txt from Release CIL: 1937 owned methods,
37 external members, 202 types (report algorithm remains conservative, not exact compiler
plan serialization). No new workstream/eh-unwind qualification or native execution proof.
No Doom .hcexe or two-publication SHA-256 pair exists.

### Historical p175 checkpoint: IDoomClock binding gate advanced

p175 classifies the narrow primitive MethodDef interface binding as image-owned only when
the discovered direct sealed-class candidates have exact class descriptors implementing
the descriptor's runtime interface TypeId. Their existing admitted method symbols are the
implementation targets; subsequent backend/link/runtime gates remain active. RuntimeExternal
is no longer retained merely as historical provenance after that binding has been proved.
Derived interfaces in metadata-only inputs now reject the entire direct candidate set;
regression covers this completeness guard, including alongside two direct implementations.
The primitive fixture advances past LINK4011 but still produces no image with missing runtime
support. No publish workstream or eh-unwind qualification was added.

Full ManagedPortSmoke, adapter Release build, regenerated manifest, fresh `.hcpu-p175`,
Doom 26/26 and Core/Windows/guest Release builds pass. Diagnostic command:
`dotnet publish .\DoomSharp.HybridCpu.Guest.csproj -c Release -r hybridcpu -p:HybridCpuManagedRequirements= -p:PublishDir=.\publish-phase15-175-diagnostic\`
exits 1 at the NEW first member:
HCSCF-LINK4011, `DoomSharp.Core.Graphics.RenderEngine.InitSpriteLumpsinstance(System.Object):System.Void`,
IL_0094 -> `runtime-external-interface:DoomSharp.Core.IConsole.Write(System.Object,System.Object):System.Void`,
interface=15, slot=136, external=True. The source declaration is `IConsole.Write(string message)`;
the diagnostic uses the ObjectReference stack carrier, not the exact String signature.
IDoomClock.GetTime is no longer the first unresolved binding. Native execution remains unproven.

Regenerated AotPreflightClosure.txt from current Release guest/Core: 1937 owned methods,
37 external/CoreLib members, 202 types, 26 cctors, 2 EH methods, 20 throw / 1 rethrow.
That report is explicitly conservative (all owned virtual/interface implementations), not
an exact serialization of the compiler's admitted call plans. No Doom .hcexe or publication
SHA-256 pair exists. Next work is the actual String-bearing IConsole.Write interface binding.

### Historical p174 checkpoint: bootstrap verifies installed types

p174 fixes ManagedBootstrapRuntimeV1 accepting registration declarations without checking
the installed type system. Before helper/initializer effects, each registration must resolve
to its exact TypeId/name/descriptor digest, with the installed descriptor digest recomputed.
Regression extends linked HCO -> runtime loader -> bootstrap: absent type system and a
rehashed bootstrap with a wrong type digest fail before the bootstrap helper is called;
the exact loaded type system succeeds. This remains CoreCLR runtime-component evidence,
not native startup or guest-service qualification. Native helper/dispatch and GC-root
installation are still unfinished; the production interface gate is unchanged.

Full ManagedPortSmoke (including runtime bootstrap regressions), adapter Release build,
regenerated manifest, fresh `.hcpu-p174`, Doom 26/26 and Core/Windows/guest Release builds pass.
`dotnet publish .\DoomSharp.HybridCpu.Guest.csproj -c Release -r hybridcpu -p:HybridCpuManagedRequirements= -p:PublishDir=.\publish-phase15-174-diagnostic\`
exits 1 at HCSCF-LINK4011, `DoomSharp.Core.DoomGame.GetTimeinstance(System.Object):System.Int32`,
IL_0005 -> `runtime-external-interface:DoomSharp.Core.IDoomClock.GetTime(System.Object):System.Int32`,
interface=12909664871142831107, slot=16319574553250112791, external=True.
No eh-unwind qualification, Doom .hcexe or publication SHA-256 pair.

### Historical p173 checkpoint: runtime image type-loader component

p173 adds ManagedRuntime HybridCpuManagedImageTypeLoaderV1 for class/interface v1 metadata.
It checks framing/ranges/counts, contract/descriptor digest, layout bounds/non-overlap,
dependencies and caller-supplied image TypeId-to-TypeHandle mapping before constructing the
runtime type system. TypeSystem accepts exact image handles rather than renumbering the
descriptor subset; ordinary declaration-builder callers retain their existing dense mapping.
Static storage is allocated only after descriptor validation, under a total byte budget.
GC root installation and cctor invocation are NOT implied by that allocation.

Regression exercises HCO -> static linked bytes -> loader -> real runtime TypeSystem,
checks assignability and exact handles with gaps, and rejects truncation, corrupt framing,
missing and duplicate handles without returning a partial type system. This is CoreCLR
execution of the runtime component, not native startup or Doom AOT execution. The loader
API is not yet wired into native startup; full shape support/combined type universe and
static-root/helper/dispatch binding remain obligations before qualification.

ManagedRuntime Release build, full ManagedPortSmoke, adapter Release build, regenerated
manifest, fresh `.hcpu-p173`, Doom 26/26 and Core/Windows/guest Release builds pass.
`dotnet publish .\DoomSharp.HybridCpu.Guest.csproj -c Release -r hybridcpu -p:HybridCpuManagedRequirements= -p:PublishDir=.\publish-phase15-173-diagnostic\`
exits 1 at HCSCF-LINK4011, `DoomSharp.Core.DoomGame.GetTimeinstance(System.Object):System.Int32`,
IL_0005 -> `runtime-external-interface:DoomSharp.Core.IDoomClock.GetTime(System.Object):System.Int32`,
interface=12909664871142831107, slot=16319574553250112791, external=True.
Same blocker remains. No eh-unwind qualification, Doom .hcexe or publication hash pair.

### Historical p172 checkpoint: dispatch descriptor HCO transport

p172 adds ManagedDispatchTypeObjectV1: exact class/interface descriptors for dispatch
receivers, bases and interface dependencies are encoded with the existing Core v1 metadata
encoder into read-only .hctypes HCO symbols. Shape-bearing types fail closed rather than
losing extensions absent from v1. Production linker now includes this object when interface
dispatch is admitted and constructs bootstrap ManagedTypes registrations from linked symbol
offsets/sizes/digests, retaining String registrations separately.

Regression links the metadata HCO, checks exact byte ranges against the existing encoder,
tests the production registration mapping, deterministic input reorder, missing dependency
and unsupported-shape rejection. This proves HCO transport/registration construction only:
the Doom gate runs before this path, and runtime installation, static storage/roots, native
helper binding and indirect-call execution remain unqualified.

Full ManagedPortSmoke, adapter Release build, regenerated manifest, fresh `.hcpu-p172`
installation, Doom 26/26 and Core/Windows/guest Release builds pass.
`dotnet publish .\DoomSharp.HybridCpu.Guest.csproj -c Release -r hybridcpu -p:HybridCpuManagedRequirements= -p:PublishDir=.\publish-phase15-172-diagnostic\`
exits 1 at HCSCF-LINK4011, `DoomSharp.Core.DoomGame.GetTimeinstance(System.Object):System.Int32`,
IL_0005 -> `runtime-external-interface:DoomSharp.Core.IDoomClock.GetTime(System.Object):System.Int32`,
interface=12909664871142831107, slot=16319574553250112791, external=True.
Same blocker remains; no eh-unwind qualification, Doom .hcexe or publication SHA-256 pair.

### Historical p171 checkpoint: retained receiver descriptors

p171 retains complete managed descriptors in compiler type-universe rows and includes each
descriptor digest in the universe digest. Dispatch object construction now checks receiver
TypeId/name/base consistency, descriptor digest and actual interface membership. Regression
rejects missing descriptors, changed layout with stale digest, and a correctly rehashed
descriptor which does not implement the interface; duplicate/conflicting-slot tests remain.
This supplies descriptor evidence for later image registration; it does not register types.
The existing Core HybridCpuManagedTypeMetadataEncoderV1 is the next format to evaluate for
class/interface descriptor transport (shape-bearing descriptors need separate coverage).

Full ManagedPortSmoke, adapter Release build, regenerated manifest, fresh `.hcpu-p171`
installation, Doom 26/26 and Core/Windows/guest Release builds pass.
`dotnet publish .\DoomSharp.HybridCpu.Guest.csproj -c Release -r hybridcpu -p:HybridCpuManagedRequirements= -p:PublishDir=.\publish-phase15-171-diagnostic\`
exits 1: HCSCF-LINK4011, `DoomSharp.Core.DoomGame.GetTimeinstance(System.Object):System.Int32`,
IL_0005 -> `runtime-external-interface:DoomSharp.Core.IDoomClock.GetTime(System.Object):System.Int32`,
interface=12909664871142831107, slot=16319574553250112791, external=True.
Same image-owned dispatch binding blocker remains. No new eh-unwind qualification,
Doom .hcexe or two-publication SHA-256 evidence.

### Historical p170 checkpoint: managed null check before dispatch

p170 corrects ordinary virtual/interface call lowering: the receiver null-check helper is
now emitted before both exact direct and resolver/indirect paths. Previously only the exact
direct path checked null; the native resolver can return zero, which must not turn a null
callvirt into JALR to zero. Regression checks the original receiver, exactly one check and
ordering before the interface resolver. This is a lowering/IR proof, not execution of a
native NullReferenceException path. Native throwing-helper binding remains an obligation.

Full ManagedPortSmoke, adapter Release build, regenerated manifest, fresh `.hcpu-p170`
installation, Doom 26/26 and Core/Windows/guest Release builds pass.
`dotnet publish .\DoomSharp.HybridCpu.Guest.csproj -c Release -r hybridcpu -p:HybridCpuManagedRequirements= -p:PublishDir=.\publish-phase15-170-diagnostic\`
exits 1: HCSCF-LINK4011, `DoomSharp.Core.DoomGame.GetTimeinstance(System.Object):System.Int32`,
IL_0005 -> `runtime-external-interface:DoomSharp.Core.IDoomClock.GetTime(System.Object):System.Int32`,
interface=12909664871142831107, slot=16319574553250112791, external=True.
Same blocker remains: image-owned dispatch binding is not qualified. Inspection confirms
bootstrap type registration currently covers String literals only, not interface candidates;
native helper/receiver registration and missing-slot guarantees still need completion.
No eh-unwind qualification, Doom .hcexe or two-publication SHA-256 evidence.

### Historical p169 checkpoint: primitive interface runtime identities

p169 binds the narrow discovered primitive MethodDef interface call to the descriptor's
runtime TypeId and the runtime builder's canonical slot hash (declaring TypeId, owner/member,
exact instance signature). The same binding is passed into method lowering and call plans;
RuntimeExternal remains true until native dispatch/image registration are qualified.
Call-plan digest now also includes declaration identity and external classification.
Regression compares IDs against the runtime builder, evaluates materialized IR operands
at the resolver call, checks metadata input order independence and retains LINK4011.

Full ManagedPortSmoke, adapter Release build, regenerated manifest, fresh `.hcpu-p169`
installation, Doom tests 26/26 and Core/Windows/guest Release builds pass.
`dotnet publish .\DoomSharp.HybridCpu.Guest.csproj -c Release -r hybridcpu -p:HybridCpuManagedRequirements= -p:PublishDir=.\publish-phase15-169-diagnostic\`
exits 1 at HCSCF-LINK4011: `DoomSharp.Core.DoomGame.GetTimeinstance(System.Object):System.Int32`,
IL_0005 -> `runtime-external-interface:DoomSharp.Core.IDoomClock.GetTime(System.Object):System.Int32`,
interface=12909664871142831107, slot=16319574553250112791, external=True.
This proves runtime IDs reached Doom's call plan; it does not close image-owned dispatch
binding or prove native execution. Next work remains this same member: receiver/null and
missing-slot semantics, full implementation/descriptor registration and native-call proof.
No new eh-unwind qualification, Doom .hcexe or two-publication SHA-256 results exist.

### Historical p168 checkpoint: conflicting interface targets rejected

p168 fixes a dispatch-object construction defect: DistinctBy previously silently retained
the first implementation when two call plans assigned different targets to the same
(runtime TypeId, interface TypeId, slot) key. Construction now rejects that conflict.
Regression accepts repeated identical plans and rejects conflicting plans in both orders.
This is metadata validation only, not runtime binding qualification or closure advancement.

Full ManagedPortSmoke, adapter Release build, fresh `.hcpu-p168` installation, Doom 26/26,
and Core/Windows/guest Release builds pass. The manifest was regenerated from the rebuilt
adapter before installation. Diagnostic command:

```powershell
dotnet publish .\DoomSharp.HybridCpu.Guest.csproj -c Release -r hybridcpu -p:HybridCpuManagedRequirements= -p:PublishDir=.\publish-phase15-168-diagnostic\
```

Exit 1, HCSCF-LINK4011: `DoomSharp.Core.DoomGame.GetTimeinstance(System.Object):System.Int32`
at IL_0005 requires `runtime-external-interface:DoomSharp.Core.IDoomClock.GetTime(System.Object):System.Int32`
(interface=16, slot=140, external=True); no image-owned implementation/guest-service binding.
The blocker remains open. No eh-unwind qualification was added. No Doom .hcexe was created;
two successful clean publications and publication SHA-256 comparison remain unavailable.

### Historical p167 checkpoint: native interface resolver ABI correction

p167 adds an emitted-instruction regression for interface lookup with nonempty method
and virtual prefixes and selection of the second interface row. It first failed because
the resolver clobbered callee-saved x8 (x9 was also used as scratch). Scratch now uses
caller-saved x28/x29. TypeId/interface/slot/code-address loads now form addresses explicitly
with ADDI before LD; native LD does not interpret Immediate as a byte displacement.
The regression verifies exact target address, missing interface/slot, null and out-of-range
TypeHandle, all callee-saved registers and the bundled-call return adjustment.

Full ManagedPortSmoke, adapter Release build, fresh `.hcpu-p167` installation, Doom 26/26,
and Core/Windows/guest Release builds pass. `publish-phase15-167-diagnostic` still fails
closed at HCSCF-LINK4011: DoomGame.GetTime IL_0005 -> IDoomClock.GetTime (row 16/slot 140).
The resolver correction does not qualify metadata-to-runtime binding, arbitrary corrupt
metadata handling, guest-service dispatch or full image execution. No Doom .hcexe or
publication SHA-256 results exist.

### Historical p166 checkpoint

p166 adds primitive-signature interface candidate discovery over admitted executable
modules, using metadata-only inputs solely for exact type resolution. Two concrete sealed
implementors are retained by regression; inherited, generic and explicit-implementation
shapes remain outside this narrow discovery rule. Crucially, candidate discovery retains
the external dispatch classification: metadata row IDs are not runtime TypeIds/slot IDs,
and candidate bodies alone cannot authorize a dispatch table.

Full ManagedPortSmoke (including candidate reachability and retained LINK4011 regression),
adapter build, fresh `.hcpu-p166` install, Doom tests 26/26 and Core/Windows/guest Release
builds pass. `publish-phase15-166-diagnostic` still fails at `HCSCF-LINK4011`,
`DoomGame.GetTime` IL_0005 -> `IDoomClock.GetTime`, interface row 16 / slot row 140.
This blocker is not closed. Native dispatch identity, implementation mapping and runtime
registration remain necessary. No `.hcexe` exists and no publication hashes are available.

### Historical p165 checkpoint

The current verified install is `.hcpu-p165-verified`, manifest version
`1.15.165-refplan7-phase15`. `publish-phase15-165-diagnostic` fails closed at:

```text
HCSCF-LINK4011: DoomSharp.Core.DoomGame.GetTimeinstance(System.Object):System.Int32
IL_0005 -> runtime-external-interface:DoomSharp.Core.IDoomClock.GetTime(System.Object):System.Int32
interface=16, slot=140, external=True; no image-owned implementation/guest-service binding.
```

p165 retains external dispatch call plans even when they have no candidate body. Their
caller, IL offset, declaration, interface/slot identities and external classification
are digest-bound; the linker rejects them before linking any unrelated dispatch table.
The new external-interface regression confirms both the retained plan and exact-member
fail-closed diagnostic. Prior p160-p164 diagnostics incorrectly displayed the first closed
interface declaration (`IComparable<Fixed>`), not the actual dispatch target; that text
must not be treated as proof that `Fixed.CompareTo` was called.

Full ManagedPortSmoke, adapter build/install, Doom tests 26/26 and Release builds of Core,
Windows and guest pass. The initial `.hcpu-p165` install was rejected by `HCPUB1008`
because its source manifest was stale; the manifest was regenerated and a separate fresh
verified install used. No file-integrity check was bypassed.

p157-p164 added literal-image and dispatch integration work and moved the diagnostic past
the earlier literal/receiver gates. Passing those gates is NOT full runtime qualification:
image-literal GC/loader integration, dispatch resolver instruction/ABI tests and complete
EH behavior still require end-to-end evidence. `eh-unwind` is not newly publish-qualified.
No Doom `.hcexe` or two-publication SHA-256 results exist.

Next: resolve the actual `IDoomClock.GetTime` implementation from the admitted cross-module
body world, preserving the separate external `IHybridCpuGuestServices` boundary.

### Historical p156 checkpoint

p156 production-links the catch-only EH world. `throw`, `rethrow`, and catch-scope leave
use per-method `gp` tail thunks so the signed-16 managed-call relocation never depends on
whole-image distance. The image contains the native frame lookup, exact/ancestry selectors,
unwind step, non-returning transfer, non-trap process exit, exception state and dispatch
index. Bootstrap registers every managed code/unwind/EH record and the current-exception
static root. Any reachable `finally/endfinally` remains fail-closed at `HCSCF-LINK4014`.

Full ManagedPortSmoke, adapter Release build, fresh `.hcpu-p156` install, Doom tests 26/26,
and Core/Windows/guest Release builds pass. Diagnostic `publish-phase15-156-diagnostic`
passes the EH gate and now exits first at `HCSCF-LINK4013`: the reachable
`ManagedStringLiteralPlanV1` still needs an image-owned UTF-16 object/table, exact runtime
String descriptor, `ldstr` lookup helper, and pre-cctor intern/static-root registration.
No `.hcexe` or hashes exist.

### Historical p155 checkpoint

p155 derives the exact reachable EH shape from PE metadata: `EntryPoint.Run` has two catch
regions and `WadFileCollection.Initialize` one catch with the sole rethrow; neither has a
finally. Dispatch-index schema v3 now points at a writable image-owned EH state whose first
word is the current-exception static GC root. Catch transfer stores reference/TypeHandle,
`__hybridcpu_managed_rethrow` tail-dispatches that root, and lowering inserts
`__hybridcpu_managed_eh_leave_catch` only for leaves that release a catch scope. Compiler
regressions verify the exact branch targets and HCO bodies.

p154 added the non-trap unhandled process-exit leaf: it preserves the exit code in x10 and
jumps non-returningly to the restricted-image return sentinel; emitted bundles contain no
`ECALL` or `EBREAK`.

Full ManagedPortSmoke, adapter Release build, fresh `.hcpu-p155` install, Doom tests 26/26,
and Core/Windows/guest Release builds pass. Diagnostic `publish-phase15-155-diagnostic`
still exits first at `HCSCF-LINK4014`; the next exact step is production-linking the
catch-only runtime object world while retaining the gate for any finally/endfinally closure.
No `.hcexe` or hashes exist.

### Historical p153 checkpoint

p153 fixes a restricted-image layout collision: package schema 1.2 wrote x3 at bytes
384..399 while non-empty bootstrap metadata also began at byte 384. The production header
is now 416 bytes and package schema 1.3; an encode/inspect regression proves that a real
non-empty managed bootstrap preserves both the EH-index global pointer and its descriptor
digest. This is required for the p152 native dispatcher components to remain addressable
in an actual `.hcexe`.

Full ManagedPortSmoke, adapter Release build, fresh `.hcpu-p153` install, Doom tests 26/26,
and Core/Windows/guest Release builds pass. Diagnostic `publish-phase15-153-diagnostic`
still exits first at `HCSCF-LINK4014`; finally/rethrow continuation state, rooted exception
lifetime and the non-trap process-exit body remain. No `.hcexe` or hashes exist.

### Historical p152 checkpoint

p152 closes full-world helper reachability without changing ISA. Dispatch-index schema v2
contains linker-relocated absolute addresses for frame lookup, ancestry selection, unwind,
handler transfer and process exit; the catch walker uses `LD` + indirect `JALR`, avoiding
the signed-16 direct-call overflow `HCLINK1005`. A production-linked instruction oracle
executes throw-site PC lookup through a base-type catch, stack-owned HCET construction and
the non-returning handler transfer, proving exact handler PC, exception x10, SP/RA and all
callee-saved registers.

Full ManagedPortSmoke, adapter Release build, fresh `.hcpu-p152` install, Doom tests 26/26,
and Core/Windows/guest Release builds pass. Diagnostic `publish-phase15-152-diagnostic`
still exits first at `HCSCF-LINK4014`; finally continuation, rethrow state/rooting and a
real non-trap process-exit body remain. No `.hcexe` or hashes exist.

### Historical p151 checkpoint

p151 adds `__hybridcpu_managed_eh_select_catch`, an ABI-framed ancestry selector. It walks
the compiler-validated TypeHandle base chain, calls the exact selector through a real
`ManagedCallRelativeSigned16` relocation and chooses the global `(trySize, ordinal)`
minimum. A production-linked image is instruction-executed with `JAL pc+4` semantics;
the oracle proves a base-type catch, balanced SP and preservation of every callee-saved
register.

Full ManagedPortSmoke, adapter Release build, fresh `.hcpu-p151` install, Doom tests 26/26,
and Core/Windows/guest Release builds pass. Diagnostic `publish-phase15-151-diagnostic`
still exits first at `HCSCF-LINK4014`; frame walking, finally continuation, rooted HCET
transfer and unhandled `process_exit` remain. No `.hcexe` or hashes exist.

### Historical p150 checkpoint

p150 closes a native bundled-call control defect exposed while composing the dispatcher.
Returning EH leaves now use the required `JALR x0,x1,+252`: HybridCPU `JAL` publishes
`pc+4`, while the next W=8 bundle begins at `pc+256`. Frame lookup, unwind step, type match
and exact-catch selection oracles execute and assert that real adjusted return path. The
non-returning handler-transfer tail remains an unadjusted jump to the final handler PC.

Full ManagedPortSmoke, adapter Release build, fresh `.hcpu-p150` install, Doom tests 26/26,
and Core/Windows/guest Release builds pass. Diagnostic `publish-phase15-150-diagnostic`
still exits first at `HCSCF-LINK4014`; dispatcher composition, finally continuation and
unhandled `process_exit` remain. No `.hcexe` or hashes exist.

### Historical p149 checkpoint

p149 centralizes the final `.hceh` byte offsets and adds the image-native
`__hybridcpu_managed_eh_select_exact_catch` leaf. It validates header/version/count,
exact payload size, final code/handler ranges, canonical ordinals and reserved bytes,
then selects the innermost exact-TypeId catch using half-open native-PC ranges. Its
bundle-level execution oracle covers nested selection, boundaries, type miss and corrupt
metadata; the result is a valid HCO definition.

Full ManagedPortSmoke, adapter Release build, fresh `.hcpu-p149` install, Doom tests 26/26,
and Core/Windows/guest Release builds pass. Diagnostic `publish-phase15-149-diagnostic`
still exits first at `HCSCF-LINK4014`; the remaining exact work is composing ancestry-wide
catch selection with finally ordering, unwind/HCET transfer and unhandled `process_exit`.
No `.hcexe` or hashes exist.

### Historical p148 checkpoint

p148 adds the exact closed-world managed type universe to `.hcehindex`: contiguous ordinal
type handles, stable TypeIds and compiler-validated base-type handles. The native
`__hybridcpu_managed_eh_type_match` leaf performs bounded catch assignability over that
immutable chain. Extending the header exposed and closed a real compatibility defect:
frame lookup now reads the adjacent method/type counts as separate 32-bit fields. Both
native leaves use caller-saved scratch registers and preserve the managed ABI callee set.

Full ManagedPortSmoke, adapter Release build, fresh `.hcpu-p148` install, Doom tests 26/26,
and Core/Windows/guest Release builds pass. Diagnostic `publish-phase15-148-diagnostic`
still exits first at `HCSCF-LINK4014`; exact remaining work is native EH clause selection,
dispatcher composition/transfer-record construction and unhandled `process_exit`. No
`.hcexe` or hashes exist.

### Historical p147 checkpoint

p147 makes unwind execution image-native and memory-safe. The immutable dispatch index now
contains a normalized, compiler-validated unwind-v2 plan plus exact guest stack bounds.
`__hybridcpu_managed_eh_unwind_step` restores a stack-saved return PC and saved registers
into an HCET snapshot, advances SP to CFA and rejects out-of-range/unaligned stack reads
before access. The regression oracle covers successful RA/x8 restoration and an atomic
stackEnd failure; the helper is a valid HCO definition.

Full ManagedPortSmoke, adapter Release build, fresh `.hcpu-p147` install, Doom tests 26/26,
and Core/Windows/guest Release builds pass. Diagnostic `publish-phase15-147-diagnostic`
still exits at `HCSCF-LINK4014`; remaining native work is clause/type selection, complete
dispatcher composition and unhandled `process_exit`. No `.hcexe` or hashes exist.

### Historical p146 checkpoint

p146 extends the image index to every managed frame. Handler-free methods carry code,
GC and unwind pointers with an exact zero EH pointer/size, so native stack walking can pass
through them without inventing clauses. Negative EH sizes fail closed.

`__hybridcpu_managed_eh_find_frame` is the first executable dispatcher component: a bounded
HybridCPU leaf reads the x3 index, searches half-open native code ranges and returns the
unique row address or zero. Its word-level oracle covers the second row, end boundary and
miss paths, and its bytes pass the HCO writer.

Full ManagedPortSmoke, adapter Release build, fresh `.hcpu-p146` install, Doom tests 26/26,
and Core/Windows/guest Release builds pass. Diagnostic `publish-phase15-146-diagnostic`
still exits at `HCSCF-LINK4014`; remaining native work is unwind-record interpretation,
clause/type selection and transfer-record construction. No `.hcexe` or hashes exist.

### Historical p145 checkpoint

p145 establishes an image-native address path for the dispatcher without changing ISA:
the restricted startup contract optionally resolves one bounded read-only symbol and loads
its final address into reserved native ABI register x3 (`gp`). The package schema records
and revalidates x3; regression coverage builds and inspects an image whose x3 points exactly
at `__hybridcpu_managed_eh_dispatch_table`. Invalid or non-read-only symbols fail closed.

Full ManagedPortSmoke, adapter Release build, fresh `.hcpu-p145` install, Doom tests 26/26,
and Core/Windows/guest Release builds pass. Diagnostic `publish-phase15-145-diagnostic`
still exits at `HCSCF-LINK4014`, precisely because the CPU dispatcher code that walks the
now-addressable table is not implemented. No `.hcexe` or hashes exist.

### Historical p144 checkpoint

p144 carries the post-RA EH root work through the managed metadata finalizer: fixed
object-reference homes are zero-initialized where CIL does not initialize them, and every
final call safepoint contains their exact stack-slot roots. Managed method HCOs now retain
`.hcgc`, unwind-v2 and `.hceh` payloads. The versioned image bootstrap descriptor/encoding
also transports final-PC EH registrations and unwind-v2 bytes; an encode/decode regression
constructs the exception runtime directly from those image-owned rows.

The exact handler-transfer tail is now a real global HCO definition,
`__hybridcpu_managed_exception_transfer`: it restores SP/RA/callee-saved registers and x10
from a validated HCET record, then jumps without a return link. Its bytes pass the object
writer and production static linker unchanged. Each method's `.hcgc`, `.hcunwind` and
`.hceh` sections now has a deterministic link-visible symbol. A new `.hcehindex` object
uses `Absolute64` relocations to bind method code and all three metadata payloads to their
final image addresses.

This does not fabricate native dispatcher execution. `__hybridcpu_managed_throw`,
`__hybridcpu_managed_rethrow` and `__hybridcpu_managed_endfinally` have exact non-returning
ABI identities, but their native metadata-walking/type-match/unwind bodies remain absent.
Therefore the `HCSCF-LINK4014` gate is intentionally retained.

The first `.hcpu-p144` directory was found to contain the historical p143 manifest; the
immutable installer correctly refused to overwrite it. The actual pack
`1.15.144-refplan7-phase15` is installed at fresh `.hcpu-p144b` and is the guest default.

Full ManagedPortSmoke, adapter Release build, Doom tests 26/26, and Core/Windows/guest
Release builds pass. Diagnostic `publish-phase15-144b-diagnostic` exits at the same exact
first blocker `HCSCF-LINK4014`, now narrowed to the image-native dispatcher body. No
`.hcexe` or hashes exist.

### Historical p143 checkpoint

p142 closes HCCIL1810. PE-bound handler roots, catch state in ABI register x10, `leave`
stack clearing, `rethrow`, and exact scalar/reference home LD/SD operations now map through
ScalarControlFlowV2. Structural evidence binds every reachable operation. Compiler smoke
also schedules and allocates an EH method, resolves symbolic homes against its final frame,
and emits a valid HCO/unwind section; unreachable compiler-generated leaves are not emitted.

p142 diagnostic then exposed HCCIL1207 for `System.String::Empty`. p143 closes it using the
exact CoreLib static/initonly string field signature, an object-reference static-root layout,
and an explicit runtime-preinitialized type binding. Missing CoreLib metadata still fails
closed; it is never projected to null or a host-side string.

Full ManagedPortSmoke, adapter Release build, fresh `.hcpu-p143` install, Doom tests 26/26,
and Core/Windows/guest Release builds pass. Diagnostic `publish-phase15-143-diagnostic`
now exits at the exact next blocker `HCSCF-LINK4014`: native throw/rethrow needs non-returning
runtime transfer, final root maps and image handler binding. No `.hcexe` or hashes exist.

### Historical p140 checkpoint

p140 extracts exact native-width constant materialization into one deterministic plan for
ordinary ScalarControlFlowV2 and EH-home paths, preserving all 64 bits via signed `ADDI` or
byte-wise `ADDI`/`SLLI`/`ORI`.

### Historical p139 checkpoint

p139 binds each home access to explicit value provenance: existing SSA value, zero
constant, nonzero constant or new reload definition. Reloads receive distinct `eh:reload:*`
identities. The resolver maps only architectural zero, an exact existing SSA operand, or a
fresh reload definition; nonzero constants remain gated until explicit materialization.

### Historical p138 checkpoint

p138 materializes the deterministic home-access plan into symbolic IR insertions only
through a mandatory value-operand resolver. It preserves the exact IL anchor and placement:
initialization/reload before the anchor, definition stores after it. Each instruction
retains the reserved slot identity for post-RA resolution; missing anchors or non-register
operands fail closed. This avoids pretending a string value identity is an executable
operand and avoids silently materializing constants with the wrong semantics.

Regression tests verify insertion count, anchor/placement, LD/SD direction, exact slot
identity and missing-anchor rejection. Full ManagedPortSmoke, adapter Release build,
fresh `.hcpu-p138` install, Doom tests 26/26 and Core/Windows/guest Release builds pass.

Diagnostic publish `publish-phase15-138-diagnostic` exits 1 at the unchanged first error:
`HCCIL1810` in `DoomSharp.Core.Data.WadFileCollection.Initialize(System.Object):System.Object`.
The real EH mapper still must provide the exact SSA operand resolver and splice these
instructions into its block stream. No `.hcexe` or determinism hashes exist.

### Historical p137 checkpoint

p136 introduced symbolic fixed-frame accesses in IR. They participate in value-flow,
scheduling and register allocation while retaining an exact slot identity; allocation
resolves both store/load to the final shared frame displacement. Bundle lowering rejects
any unresolved slot instead of silently encoding offset zero. The allocation input digest
binds instruction/slot identities. A full backend smoke covers schedule, allocation,
nonzero displacement, proof rebuild and final bundle encoding.

p137 adds a deterministic home-access plan derived from the PE-bound typed dataflow:
initial stores for initialized arguments/initlocals, one post-definition store per required
home and reloads at handler entries. Unreached instruction edges are excluded rather than
invented; multiple normal successors do not duplicate the same definition store. Accesses
retain exact type, object-root bit and value identity for subsequent IR/GC-map insertion.

Full ManagedPortSmoke passes, including pre/post-store exceptional state, EH-loop joins,
symbolic-frame resolution and home-access plan determinism. Adapter Release build, fresh
`.hcpu-p137` install, Doom tests 26/26 and Core/Windows/guest Release builds pass.

Diagnostic publish `publish-phase15-137-diagnostic` exits 1 at the unchanged first error:

```text
HCCIL1810: Managed EH metadata is admitted, but handler-entry state and throw/leave lowering are not yet integrated into ScalarControlFlowV2.
DoomSharp.Core.Data.WadFileCollection.Initialize(System.Object):System.Object
```

The plan is not yet inserted into the real EH method IR; handler branches, GC maps and
runtime transfer remain unqualified. No `.hcexe` or two-publication hashes exist.

### Historical p135 checkpoint

p135 adds checked fixed-home access construction from the final allocation witness.
It reuses the existing spill/frame stack-memory instruction factory, producing ordinary
LD/SD IR with the exact SP-relative displacement and read/write memory annotations.
Missing/allocator-reserved slots, non-word or out-of-frame storage, SP clobber and loads
to x0 fail closed. Store to x0 remains available for zero initialization.

Regression tests inspect opcodes, final offsets, memory effects and operands, plus invalid
slot/register rejection. These tests do not execute native EH or claim encoded-image
qualification. Full ManagedPortSmoke, adapter Release build, `.hcpu-p135` installation,
Doom tests 26/26 and Core/Windows/guest Release builds pass. The caller must still insert
these accesses into EH IR and rebuild dependencies, schedule and GC maps.

Diagnostic publish `publish-phase15-135-diagnostic` exits 1 at the unchanged first error:
`HCCIL1810: Managed EH metadata is admitted, but handler-entry state and throw/leave
lowering are not yet integrated into ScalarControlFlowV2.` Exact member:
`DoomSharp.Core.Data.WadFileCollection.Initialize(System.Object):System.Object`.
No `.hcexe` or two-publication SHA-256 evidence exists.

### Historical p134 checkpoint

p134 adds fixed-frame reservations to the actual schedule-aware allocator, alongside
spill and saved-register slots. Duplicate and allocator-reserved identities are rejected;
existing frame size/displacement/multi-function/terminal-exit checks still apply.
ManagedEhFrameHomesV1 maps validated scalar/reference homes to separate aligned 8-byte
ABI carrier slots; aggregate/byref homes remain fail-closed. The method-object bridge
passes these requests when managed EH analysis is present.

Regression tests inspect a real throw allocation witness containing an EH home and
saved x1, nonoverlapping aligned offsets and a changed frame digest; aliasing saved:x1
is rejected. Typed-home tests verify exact identities and byref rejection. Full compiler
ManagedPortSmoke passes, as do adapter Release build, fresh `.hcpu-p134` installation,
Doom tests 26/26 and Core/Windows/guest Release builds.

This reserves physical storage only: initialization, store/reload emission, handler SSA,
GC root maps and runtime transfer are not qualified by these tests.

Diagnostic publish `publish-phase15-134-diagnostic` exits 1 at the same first blocker:
`HCCIL1810: Managed EH metadata is admitted, but handler-entry state and throw/leave
lowering are not yet integrated into ScalarControlFlowV2.` Exact member:
`DoomSharp.Core.Data.WadFileCollection.Initialize(System.Object):System.Object`.
No `.hcexe` or two-publication SHA-256 evidence exists.

### Historical p133 checkpoint

p133 retains verified typed entries and edge states in ManagedEhTypedDataflow, including
constants, initialization, aggregate/receiver identity and function-pointer signature.
Joins are recomputed from current predecessor edge states rather than folding into stale
merged values; the method-entry state remains an input for backedges to the entry.
These are analysis identities and snapshots, not emitted native phi copies or GC homes.

New tests prove pre-store exceptional versus post-store normal local values, catch-stack
replacement, EH-loop convergence and deterministic typed-dataflow digest. Full compiler
ManagedPortSmoke passes. Adapter Release build, fresh `.hcpu-p133` install, Doom tests
26/26 and Core/Windows/guest Release builds pass.

Diagnostic publish `publish-phase15-133-diagnostic` exits 1 at the unchanged first error:
`HCCIL1810: Managed EH metadata is admitted, but handler-entry state and throw/leave
lowering are not yet integrated into ScalarControlFlowV2.` Exact member:
`DoomSharp.Core.Data.WadFileCollection.Initialize(System.Object):System.Object`.
Native EH remains unqualified; no `.hcexe` or two-publication SHA-256 evidence exists.

### Historical p132 checkpoint

p132 connects PE-validated EH instruction flow to the existing V2 typed instruction
transfer in ImportImage, before the native EH gate. A bounded fixpoint propagates
normal post-instruction state and exceptional pre-instruction locals/arguments; catch
entry replaces the evaluation stack with one ObjectReference. leave clears the stack;
rethrow/endfinally require an empty stack (HCCIL0819). These are verification identities,
not emitted SSA phi copies, physical state homes or native runtime transfers.

New regressions exercise catch-stack underflow (HCCIL1602), nonempty rethrow (HCCIL0819)
and successful stack-clearing leave verification followed by the retained native gate.
Full ManagedPortSmoke passes; adapter Release build/fresh `.hcpu-p132` install pass;
Doom CoreCLR 26/26 and Core/Windows/guest Release builds pass.

Diagnostic publish `publish-phase15-132-diagnostic` exits 1. The actual WadFileCollection
EH body passes the new typed verification and reaches the same native-lowering blocker:

```text
HCCIL1810: Managed EH metadata is admitted, but handler-entry state and throw/leave lowering are not yet integrated into ScalarControlFlowV2.
DoomSharp.Core.Data.WadFileCollection.Initialize(System.Object):System.Object
```

Native handler SSA, finally continuation, physical homes/GC and runtime transfers are
still unfinished. No EH authority, `.hcexe` or two-publication hashes are claimed.

### Historical p131 checkpoint

p131 constructs EH basic blocks at branch/terminator and try/handler boundaries.
Handler entry clause ordinals are retained. Block edges distinguish ordinary flow,
leave continuation and exceptional dispatch, preserving each exceptional source IL offset
even inside a block (pre-instruction state must not be replaced by block-exit state).
These blocks are retained in the PE-bound EH plan; native V2 SSA/IR emission is still pending.

New tests verify complete ordered instruction partition, exact handler starts,
preservation of all exceptional edges/source offsets, deterministic graph digests and
fail-closed MaximumBasicBlocks (HCCIL2804). Full ManagedPortSmoke, adapter Release build,
fresh `.hcpu-p131` install, Doom tests 26/26 and Core/Windows/guest Release builds pass.

Diagnostic publish `publish-phase15-131-diagnostic` exits 1 at the same first blocker:
`HCCIL1810: Managed EH metadata is admitted, but handler-entry state and throw/leave
lowering are not yet integrated into ScalarControlFlowV2.` Exact method:
`DoomSharp.Core.Data.WadFileCollection.Initialize(System.Object):System.Object`.
No native EH qualification, `.hcexe`, or two-publication hashes are claimed.

### Historical p130 checkpoint

p130 preserves the EH CFG/typed homes in the graph-owned method plan (previously only
the separate analysis result retained them). ImportImage now re-derives EH analysis
from the exact PE and verifies clauses/operations/prefix/digest, instead of trusting only
method identity, body size and clause count. A rejected native lowering result retains
the PE-validated ManagedEhAnalysis for the next lowering stage; it has no IR authority.

Regression tests reject modified handler ranges, removed operations, stale prefixes and
self-consistently rehashed forged plans with HCCIL0810. Supplied forged CFG/home data is
discarded and reconstructed from the PE. Full ManagedPortSmoke passes, including all
previous EH/GC/runtime tests. Adapter Release build, fresh `.hcpu-p130` installation,
Doom tests 26/26 and Core/Windows/guest Release builds pass.

Diagnostic publish `publish-phase15-130-diagnostic` exits 1 at:

```text
HCCIL1810: Managed EH metadata is admitted, but handler-entry state and throw/leave lowering are not yet integrated into ScalarControlFlowV2.
DoomSharp.Core.Data.WadFileCollection.Initialize(System.Object):System.Object
```

This fixes the plan handoff/validation gap, not native handler lowering. No `.hcexe` or
two-publication hashes exist. Next: consume validated handler entries in V2 CFG/SSA,
allocate physical state homes and bind GC/runtime leave/rethrow transfers.

### Historical p129 checkpoint

p129 binds required EH state homes to the exact projected method/local signatures,
including object-root classification and the method body's initlocals flag. Forward
definite-assignment analysis intersects incoming states and preserves pre-instruction
state on exceptional edges. It rejects undeclared slots (HCCIL0817), and fails closed
when the conservative graph cannot prove initialization (HCCIL1818, Unsupported rather
than a claim that all such CIL is invalid). This is analysis, not physical frame-home
allocation, GC-map emission or native handler-entry lowering.

New regressions cover absent local/argument slots, an uninitialized handler read,
initlocals scalar/reference homes, and explicit assignment before try without initlocals.
Full ManagedPortSmoke passes; adapter Release build and fresh `.hcpu-p129` install pass;
Doom CoreCLR tests remain 26/26; Core, Windows and guest Release builds pass.
Actual EH probes confirm no homes in WadFileCollection.Initialize and one ObjectReference
home `local:0` in EntryPoint.Run (initialized at method entry).

Diagnostic publish `publish-phase15-129-diagnostic` exits 1 at the unchanged first blocker:

```text
HCCIL1810: Managed EH metadata is admitted, but handler-entry state and throw/leave lowering are not yet integrated into ScalarControlFlowV2.
DoomSharp.Core.Data.WadFileCollection.Initialize(System.Object):System.Object
```

No `.hcexe` or two-publication hashes exist. EH publication authority is still withheld.
Next implementation work is consuming the validated state in handler-entry CFG/SSA and
physical homes, with exact GC roots and native leave/rethrow transfers.

### Historical p128 checkpoint

p128 emits `.hcunwind` in ScalarControlFlowV2 method HCO objects, including methods
without their own handlers. EH finalization and method-object compilation share the same
post-RA frame-to-CFA conversion. ThrowBoundarySmoke decodes the emitted record and checks
the exact saved x1 slot; a frameless leaf instead recovers x1 directly. The leaf still
links and repeated HCO hashes match. These are object-level tests, not native EH execution.
Native-PC registration, handler-state lowering, safepoint/GC and runtime transfer remain
unqualified; HCSCF-LINK4014 and the release eh-unwind gate are retained.

Verified: full ManagedPortSmoke (including new leaf/throw unwind checks), adapter Release
build, fresh `.hcpu-p128` install, Doom CoreCLR 26/26, Core/Windows/guest Release builds.
Diagnostic publish `publish-phase15-128-diagnostic` exits 1 with the unchanged first error:

```text
HCCIL1810: Managed EH metadata is admitted, but handler-entry state and throw/leave lowering are not yet integrated into ScalarControlFlowV2.
DoomSharp.Core.Data.WadFileCollection.Initialize(System.Object):System.Object
```

The expected `.hcexe` does not exist. No two-publication SHA-256 claim is made.
Normal Release publish to `publish-phase15-128-release` was also rerun: exit 1,
`HCPUB1004: unsupported managed publish workstream(s): eh-unwind.`

### Historical p127 checkpoint

p127 includes EH CFG/state-liveness work and a backend prerequisite: exact terminal
`__hybridcpu_managed_throw` calls now keep an unwindable frame. The allocator saves x1
and used callee-saved registers in the prologue, retains the frame on throw, and emits
epilogues only on return paths. ThrowBoundarySmoke verifies a real allocation witness
with `saved:x1` and no throw-path restore/epilogue; isolated HCO compilation succeeds.
The public linker still rejects native exception publication with HCSCF-LINK4014.

Verified p127: full compiler smoke, adapter build/install, Doom CoreCLR 26/26,
Core/Windows/guest Release builds. Diagnostic publish `publish-phase15-127-diagnostic`
still fails at HCCIL1810 in WadFileCollection.Initialize. This does not close the handler
SSA/local-home/native-transfer workstream. No `.hcexe` or image determinism hashes exist.

### Historical p126 checkpoint

p126 installed-pack diagnostic closes the cross-assembly `DoomTerminationException..ctor`
allocation failure. Its first actual failure is now:

```text
HCCIL1810: Managed EH metadata is admitted, but handler-entry state and throw/leave lowering are not yet integrated into ScalarControlFlowV2.
DoomSharp.Core.Data.WadFileCollection.Initialize(System.Object):System.Object
```

Verified: adapter Release build and fresh `.hcpu-p126` installation; full compiler smoke
(previous turn, same constructor change); Doom CoreCLR 26/26; Core, Windows and guest
Release builds; diagnostic publish to `publish-phase15-126-diagnostic` (exit 1, no `.hcexe`).
Guest csproj now selects p126. No EH publication authority was added.

Regenerated `AotPreflightClosure.txt`: 1937 owned methods, 37 external members, 202 types,
26 cctors, 2 EH methods, 20 throw and 1 rethrow. This tool reports a conservative
reflection/IL dispatch closure, not an exact successfully compiled native closure.
The next compiler work is exceptional handler-entry state plus leave/rethrow transfer,
followed by the existing runtime/link/image and GC qualification gates.

### EH transfer-contract implementation (not yet CFG/SSA lowering)

`ManagedEhMethodPlanV1` now derives handler entry stacks and ordered leave actions from
its clauses/operations. Catch entry has one ObjectReference from runtime transfer x10;
finally entry has an empty stack. Leave clears the evaluation stack and orders exited
finally invocations/catch-scope releases inner-first; regions containing the destination
are retained. These derived contracts do not themselves emit native transfers.
Full ManagedPortSmoke passes, including nested leave-order and entry-stack regressions.
Actual Release CIL inspection with `--eh-plan` reports:

- WadFileCollection.Initialize: catch IL_000e; leave IL_000c -> IL_002b, no cleanup actions.
- EntryPoint.Run: catches IL_0057/IL_0066; normal leave IL_0055 -> IL_008f;
  catch leaves IL_0064/IL_008d -> IL_008f release scope 0/1 respectively.

No new pack installed for this intermediate change. HCCIL1810 remains the installed
diagnostic. Next work must connect these contracts to exceptional CFG edges, local/argument
homes, handler SSA, GC maps and runtime transfer, not simply remove the gate.

### EH instruction CFG implementation

EH-plan import now constructs a deterministic bounded instruction CFG with distinct
normal, leave-continuation and exception-dispatch-candidate edges. Exceptional edges
conservatively retain each protected instruction as a potential dispatch source; they
do not prove a catch matches and cannot be lowered as ordinary branches. Leave edges
represent continuation after the ordered cleanup plan, not a cleanup bypass.
The edge budget is 65536 (`HCCIL2803` on exhaustion).
Full compiler smoke passes: exact instruction endpoints, clause/handler identities,
no ordinary throw/rethrow successor, and deterministic graph digests.
Actual Doom EH-plan probes pass: Initialize has 15 instructions/18 edges; Run has
54 instructions/114 edges. Handler-local SSA and native transfer are still not wired;
installed p126 HCCIL1810 remains authoritative. No pack qualification changed.

### Exceptional local/argument liveness

EH CFG now computes backward state-slot liveness, distinguishing pre-instruction
exceptional state from normal post-definition state. Short/long ldloc/stloc/ldarg/starg
operands are retained by the EH decoder. Address-taken state is conservatively retained;
endfinally retains method state pending explicit runtime-continuation SSA. Required homes
and live-entry sets are included in the graph digest. This is analysis, not native frame
allocation or GC-map qualification.
Full compiler smoke passes, including a raw-CIL handler reading a modified local plus
argument (CoreCLR returns 42), pre-store exceptional-state retention, and exclusion of
handler-defined result slots. Actual Doom probes: Initialize requires no state homes;
EntryPoint.Run requires `local:0` (services). Native storage/root-map integration is next.
Installed p126 still stops at HCCIL1810; this intermediate source change is not packed.

### Previous p125 checkpoint

EH implementation preparation after p126: `ManagedEhContractsV1.cs` now retains
decoded switch targets and rejects ordinary branch/switch edges across protected-region
boundaries (`HCCIL0816`) and ret inside protected regions (`HCCIL0815`). Expanded
`EhPlanSmoke.cs` verifies both failures and a valid branch inside try. Full compiler smoke
passes. `ManagedPortSmoke --eh-plan <assembly> <type> <method>` verifies actual Doom plans:
WadFileCollection.Initialize: 1 clause/2 operations; EntryPoint.Run: 2 clauses/5 operations.
Both succeed under the new validation. This change is not yet installed in a new pack;
the installed p126 diagnostic remains HCCIL1810. Handler SSA/lowering, runtime native
transfer and image qualification remain incomplete; no EH gate was relaxed.

The authoritative installed guest pack is `1.15.125-refplan7-phase15`, `.hcpu-p125`.
Latest actual diagnostic (`publish-phase15-125e-diagnostic`): `HCCIL1301`,
`DoomSharp.Core.DoomTerminationException..ctor`, consumer MemberRef `0x0a000010`:
no exact allocation binding in the guest module. No `.hcexe` was produced.

Subsequent sequential work since the historical p115 checkpoint:

- p116: metadata-owned TypeDef cast/type-test bindings.
- p117: checked Int32 remainder ABI/semantic helper; exact runtime helper callees retained in HCO relocation closure. This is not native helper execution.
- p118: exact Int32 constant propagation through `conv.i8` for remainder proof.
- p119: exact CoreLib `String(char[])` binding; equivalent Int32 wipe flag.
- p120: exact CoreLib invariant argument-exception constructor bindings.
- p121: UInt32 low-bit carrier accepted for `stelem.i4`.
- p122: exact UInt32-to-Int32 call-boundary normalization (`ADDIW`); closes `Patch.FromBytes:il_005e HCCIL0017`.
- p123: unsigned relational branch against a nonnegative I4 constant; closes `DoomMath.Tan:il_0010 HCCIL1111`. Dynamic mixed types and negative constants remain closed.
- p124 did NOT close `PlatformRaise:phi:b24:local_2 HCCIL1106`; refreshing existing phi incoming values was insufficient.
- p125 additionally removes obsolete phi when a late predecessor makes a local uninitialized, preserving the definite-assignment gate. Actual preflight passed `PlatformRaise`.
- Same-pack source iterations: scalar `Fixed.RawValue` for `ActionSaw` and then `Player.UseLines` closed their respective `HCCIL1820`; explicit guest runtime cctor closed `RequirePlatform HCCIL1430`; seven-argument Segment constructor plus private pre-publication BackSector initialization closed `HCCIL2002`.

Compiler smoke and Doom CoreCLR 26/26 plus Core/Windows/guest builds passed at these checkpoints.
Current uninstalled compiler change reuses exact managed-definition resolution to map consumer
constructor MemberRefs to already qualified class-allocation bindings. Full ManagedPortSmoke
passes, including new cross-assembly constructor reachability regression. It has not yet been
validated by a new installed-pack Doom diagnostic; `HCCIL1301` remains the last confirmed blocker.
Normal release authority still excludes `eh-unwind`; no deterministic image hashes exist.

## Historical compiler checkpoint (pack 1.15.115)

The guest now defaults to `1.15.115-refplan7-phase15` (manifest SHA-256
`3a55886ccc8d4df11e6aed3543b6c4379668ed07289e0613c16a29a13af7d8d5`).
The physical pack root is `Diagnostics/HybridCpuAotAddDemo/.hcpu-p115`.

The sequential diagnostic checkpoints after pack 1.15.101 were:

- 1.15.102 lowered typed `throw` to `__hybridcpu_managed_throw` and retained the native-transfer image gate (`HCSCF-LINK4014`); the next CIL blocker was `HCCIL1441`.
- 1.15.103 implemented exact I8-to-I4 `conv.i4` via `ADDIW`; the next blocker was checked signed division (`HCCIL1832`).
- 1.15.104 added the exact checked Int32 division helper contract. The next run exposed an adapter crash on qualified 1/2-byte instance-field loads.
- 1.15.105 completed exact `LB/LBU/LH/LHU` and `SB/SH` object-field mapping with the existing managed null-check boundary. The next blocker was Int64 division in `Fixed.Div2`.
- 1.15.106 added exact checked Int64 division semantics and ABI contract. The next blocker was scalar-projected `Fixed` local storage (`HCCIL0034`).
- 1.15.107 projected exact four-byte scalar value locals. The next blocker was object-reference `bne.un` (`HCCIL1111`).
- 1.15.108 admitted object references only for equality branches while ordered object branches remain fail-closed. The next blocker was projected argument identity at a CFG join (`HCCIL1104`).
- 1.15.109 propagated metadata-scoped scalar identities through managed calls and mutable arguments. A second `HCCIL1104` exposed owner/value identity confusion for projected fields.
- 1.15.110 derives projected field identity from the exact field signature, not the containing layout. The next blocker was a duplicate mapped definition (`HCCIL0024`).
- 1.15.111 separates mapped value discovery from actual SSA definition ownership; loop-carried forward uses no longer look like duplicate definitions. The next blocker was a UInt32 array index (`HCCIL1407`).
- 1.15.112 admits the exact I4 `UInt32` array-index carrier and normalizes it with `ADDIW`. The next blocker was unary `neg` over a UInt32 carrier (`HCCIL1306`).
- 1.15.113 preserves exact low-32-bit wrapping for UInt32 `neg`. Two subsequent `HCCIL2002` register-argument overflows were closed by splitting `ScheduleStateTransition` work payload overloads and removing invariant path-traversal arguments; no stack-argument ABI was claimed.
- 1.15.114 adds only the exact UInt32-to-Int32 local bit projection, with `ADDIW`; UInt32-to-Int64 remains `HCCIL0034`. A further line-attack argument overflow was closed by base/hitscan overloads within the eight-register ABI. The next blocker was `System.String[]` allocation (`HCCIL1401`).
- 1.15.115 synthesizes `System.String[]` only from an exact resolved CoreLib String token, retaining reference-element GC scanning and store checks. Diagnostic publication now stops at `HCCIL1512` in `DoomSharp.Core.GameLogic.Trigger.VerticalDoorEvent:il_00e4` because `castclass/isinst` lacks an exact runtime type binding.

Compiler smoke, Doom CoreCLR smoke (26/26), Core, Windows and guest builds pass at this checkpoint. No `.hcexe` is produced. Normal release publication remains gated by `HCPUB1004: eh-unwind`; checked division helpers are semantic/compiler/ABI contracts only and are not claimed as native runtime execution.
The longer phase15-100 directory caused Windows process launch error 206 before CIL import;
installing the same pack under this shorter fresh root restores the diagnostic probe.
This is an integration workaround, not general response-file support for arbitrary path lengths.
Standard publication remains fail-closed on the required `eh-unwind` workstream and emits
no image. A diagnostic run with requirements suppressed reaches the managed call graph and
now passes the former `HCSCF-RECURSION1001` boundary. Damage, BFG, pain-elemental, arch-vile radius, missile-spawn and
fat/brain/skel action continuations now run through an explicit state-action work stack. Typed
block iterators removed the false context-insensitive `ChangeSector -> CheckThing` edge.
Resumable telefrag and missile-placement frames now also preserve their exact block cursor and
captured `BlockNext`; passive position checks keep corpse-fit and newly spawned skull checks out
of missile/skull collision branches. The prior `P_TeleportMove/PIT_StompThing` and
`P_TryMove/PIT_CheckThing` SCC witnesses are gone. Hitscan actions use resumable damage transitions;
monster crossed-line triggers drain between thinkers, and spawn-fit/missile paths no longer expose
impossible trigger edges. Pack 1.15.83 adds metadata-owned class allocation bindings and an exact
single-field scalar value-constructor projection. The related static
field projection for `DoomSharp.Core.Angle::Angle0` is now exact and preserves its physical
layout, static storage, cctor and root metadata. Exact single-field scalar method signatures and
instance-field reads now preserve nominal identity while lowering the payload read to an SSA copy;
`HCCIL1202` is closed without object/null/byref semantics. Dynamic UInt32 `div.un` now targets an
exact runtime helper contract, with a semantic reference for managed `DivideByZeroException`;
native helper execution is not established by this CIL progress. Native
DIVU remains limited to proven nonzero divisors. Metadata TypeDef reference arrays now receive an
exact per-assembly SZARRAY descriptor and type handle, preserving element identity, GC scanning and
covariant store checks. Canonical CIL `int32` constants now satisfy exact `uint32` managed-call
parameters through the existing directed stack-compatibility rule. Nil interface base handles and
facade-forwarded CoreLib bases now produce exact inherited layouts, including
`DoomTerminationException.ExitCode`. Exact CoreLib primitive TypeRef arrays now receive their
width/alignment descriptors without GC scanning. Outer `int[][]` has an exact reference-element
binding. Pack 1.15.92 closes `HCCIL1204` for `_centerXFrac`: four-byte reference-free scalar
instance payloads retain physical layout, null checks and LW/LWU/SW accesses; GC-containing
aggregate copies remain fail-closed. Pack 1.15.93 adds reachable, ordinal-deduplicated UTF-16
literal bindings and a digest-bound graph literal plan, closing the CIL `HCCIL1411` boundary.
This is not image/runtime qualification: `HCSCF-LINK4013` explicitly rejects discarding the
literal plan until runtime String layout, intern roots and pre-cctor registration are emitted.
Exact UTF-16 code units (including NUL and unpaired surrogates) are retained in the plan digest.
The next observed failure was an emitter crash on a narrow field store. Packs 1.15.94/95 move
that check before emission, preserving the separate aggregate gate. Pack 1.15.96 closes
`HCCIL1210` for one-byte instance stores (`Player.UseDown`): the existing null-check/offset path
now emits SB, preserving layout and low-byte storage. Two-byte stores remain fail-closed.
Pack 1.15.97 closes the enum-array CIL binding boundary for `DirectionType[]` using its own
nominal array TypeId and primitive storage derived from the exact reference-free `value__`
payload; ordinary struct arrays remain fail-closed. The subsequent `HCCIL1433` for
`CheatSequence._firstTime` is bypassed by an equivalent private 0/1 Int32 source flag,
preserving lazy initialization order. This does not qualify Boolean static helpers.
Pack 1.15.98 closes `HCCIL1870` at `CheatSequence..ctor IL_0024`: small helper returns
are normalized to I4 on the CIL stack, with explicit signed/unsigned extension after the
unchanged narrow helper ABI. String-char-to-byte and char-to-I4 regression imports pass;
object operands and stack underflow at conv.u1 remain rejected.
Pack 1.15.99 closes `HCCIL1401` at `StatusBar..ctor IL_003c`: exact scoped
SZARRAY CLASS TypeSpecs bind the outer reference array to the inner array TypeId.
Reference-jagged success and unbound value-jagged fail-closed regressions pass.
Pack 1.15.100 corrects the false `HCCIL1101` for a trailing throw: throw has no normal
CFG successor, consumes an object-reference operand, and remains behind a pre-IR native-EH gate.
Normal return, genuine falloff, throw-null, non-reference throw and underflow regressions pass.
Current first blocker: `HCCIL1882`, `DoomSharp.Core.DoomGame.Error IL_0026`, `throw`.
This does not implement native EH or qualify `eh-unwind`.

### EH implementation progress after pack 1.15.100

The diagnostic probe was reproduced at `HCCIL1882` before runtime changes. New failing
regressions exposed defects in `HybridCpuManagedExceptionRuntimeV1.Dispatch`, now corrected:

- `rethrow` searches enclosing catch regions in the current frame, not only caller frames;
  a rethrow PC outside a catch handler is rejected.
- An exited finally nested inside the selected catch frame runs before catch transfer;
  an enclosing finally whose try still contains the destination handler does not run yet.
- An absent finally executor fails closed instead of silently reporting completion.
- A replacement exception from finally resumes search at that finally handler PC in the same
  frame, preserving the dispatch-wide finally budget and requiring a new GC callback checkpoint.

Changed: `Compilers/HybridCPU_ManagedRuntime/ManagedExceptionRuntimeV1.cs`, new
`Compilers/HybridCPU_Compiler/Tools/ManagedPortSmoke/EhDispatchSmoke.cs`, and its `Program.cs` registration.
Tests first reproduced the same-frame rethrow and finally-replacement failures, then passed;
CoreCLR nested EH fixtures provide identity/order oracles. Full ManagedPortSmoke, compiler/runtime
builds, Doom 26/26 and Core/Windows/guest builds passed. The repeated installed-pack preflight
still reports `HCCIL1882` at `DoomGame.Error:IL_0026`.

These are runtime dispatch semantics, not native execution, GC-root lifetime qualification or
process-exit execution. The installed compiler pack remains 1.15.100; its native EH gate is unchanged.
The actual EH-bearing methods are `WadFileCollection.Initialize` (WadFormatException catch)
and guest `EntryPoint.Run` (DoomTerminationException and System.Exception catches).
Native frame/state transfer, handler entry/leave lowering, exception-root ownership and
image/startup/process-exit integration remain required before `eh-unwind` qualification.

### Runtime-owned exception roots and rooted dispatch

`ManagedExceptionStateV1.cs` now owns a bounded LIFO stack of active exception references
for one managed execution context. Tokens are monotonic; stale and out-of-order pop/replace,
null, inactive heap addresses and non-exception objects are rejected without changing state.
Replacing the top root retains outer catch exceptions. The collector accepts this runtime owner
through `HybridCpuManagedNonMovingGcRequestV1.Exceptions` and rejects a different heap/type system.

`DispatchRooted` requires a live token and a GC checkpoint, resolves the actual exception type
from the heap, retains replacement exceptions before the checkpoint, and leaves the root alive
after returning a handler destination. The caller must release the token on handler leave/unwind;
native lowering has not yet emitted these operations. The low-level `Dispatch` remains available
for semantic dispatch tests and is not independently GC-qualified.

New `EhStateSmoke.cs` uses the actual heap and non-moving collector: nested catch roots survive,
replaced/left exceptions become reclaimable, invalid changes leave state unchanged, and rooted
dispatch through a throwing finally survives two real GC checkpoints plus handler lifetime.
Compiler smoke, compiler/runtime builds, Doom 26/26, Core/Windows/guest builds and diagnostic
preflight were repeated. The native blocker remains `HCCIL1882`, `DoomGame.Error:IL_0026`.
Pack 1.15.100 and publication qualifications are unchanged; no native EH/image claim is made.

Changed in this step: runtime `ManagedExceptionStateV1.cs` (new), `ManagedNonMovingGcV1.cs`,
`ManagedExceptionRuntimeV1.cs`; compiler smoke `EhStateSmoke.cs` (new), `EhDispatchSmoke.cs`,
`Program.cs`; this preflight log. Native handler-state ABI, process exit and two `.hcexe`
publications remain unfinished.

### Rooted unhandled dispatch to RuntimeKernel process exit

`HybridCpuManagedExceptionRuntimeV1.DispatchForProcess` now joins rooted dispatch with
the existing `IHybridCpuRuntimeKernelV1.ProcessExit` operation. It requests exit only after
an `UnhandledTermination` result, uses the supplied process policy exit code unchanged,
and retains the active exception scope through the kernel transition. Handled exceptions,
invalid dispatch and rejected GC checkpoints do not call process exit. A kernel failure
is preserved in the result and grants neither handler entry nor successful termination.

`EhStateSmoke` verifies handled/no-exit, GC-rejected/no-exit, unhandled exit code 255,
retained exception root, and rejection of another exit after the kernel has terminated.
No architectural trap is generated. This proves the runtime/kernel path, not a native
instruction transfer: `throw` lowering and the image-bound helper implementation remain gated.
The Doom-specific `DoomTerminationException.ExitCode` policy remains in guest EntryPoint.Run;
the runtime does not special-case Doom types or replace their catches.

Changed: `ManagedExceptionRuntimeV1.cs`, `Tools/ManagedPortSmoke/EhStateSmoke.cs`, this log.
Compiler/runtime builds, complete compiler smoke, Doom 26/26 and Core/Windows/guest builds
passed again. Diagnostic preflight still stops at `HCCIL1882`, `DoomGame.Error:IL_0026`.
The installed pack and `PublishQualifiedWorkstreams` are unchanged; no `.hcexe` was produced.

### Native-frame context reconstruction from unwind records

The runtime unwind decoder previously retained only CFA base/offset and discarded saved-register
rows and return-PC location. It now retains the complete v2 record. `TryUnwindFrame` reconstructs
a 32-register native context using SP- or FP-based CFA, register or stack return PC, and every saved
register row. It validates method range, aligned return PC, CFA arithmetic and each supplied stack
read before returning a new read-only register snapshot; failures return no partially restored context.

`EhUnwindSmoke.cs` verifies exact saved RA/FP/register recovery, unaffected registers, input immutability,
missing stack words, return-PC misalignment, CFA overflow, FP-based leaf unwind and unknown method/range.
This is context reconstruction, not a machine branch or execution of an `.hcexe`. Native handler
transfer and CIL lowering remain unimplemented and `HCCIL1882` remains at `DoomGame.Error:IL_0026`.

Changed: runtime `ManagedExceptionRuntimeV1.cs`, new compiler smoke `EhUnwindSmoke.cs`, smoke
`Program.cs`, this log. Compiler/runtime builds, full compiler smoke against Doom Core, Doom 26/26,
Core/Windows/guest builds and diagnostic preflight were repeated. Pack 1.15.100 remains unchanged.

### Encoded native handler-transfer fragment

Added private versioned HCET transfer-record encoder in Platform.Contracts and
`HybridCpuManagedExceptionTransferEmitterV1` in compiler Core. Its fixed native fragment accepts
an already validated, immutable readable record in x10, preserves process/thread pointers x3/x4,
uses caller-saved x5/x6/x7 as scratch, restores the native ABI callee-saved registers plus RA/SP,
loads the exception reference into x10 and ends with JALR x0 (no return link). Address calculation
and LD are separate bundles; no same-bundle forwarding is assumed. No ISA or ISE change was made.

`EhNativeTransferSmoke.cs` decodes the serialized bundles and checks all restored ABI values,
record read bounds, retained context pointers, final no-link branch, deterministic bytes and
misaligned handler rejection. This is a word-level semantic oracle, NOT ISE execution, a
scheduled/loader-qualified runtime artifact, or an AOT Doom image. The fragment is not yet connected
to throw lowering or the native runtime helper table; the record requires live root ownership,
address/lifetime validation and a non-safepoint transfer interval from that integration layer.

Changed: new `HybridCPU_Platform.Contracts/ManagedExceptionTransferV1.cs`, new compiler
`Core/Target/Managed/HybridCpuManagedExceptionTransferEmitterV1.cs`, new smoke
`EhNativeTransferSmoke.cs`, smoke `Program.cs`, this log. Compiler/runtime builds, full smoke,
Doom 26/26 and Core/Windows/guest builds passed. Installed-pack diagnostic preflight still reports
`HCCIL1882` at `DoomGame.Error:IL_0026`. No pack qualification or `.hcexe` publication was advanced.

### Connected unwind / dispatch / transfer-record preparation

`PrepareNativeHandlerTransfer` now validates native frame snapshots and reads/restores the full
caller chain before any managed callback. It then invokes rooted dispatch and encodes the selected
handler's restored registers and current exception into HCET. It snapshots input register/frame
collections and refuses invalid stack reads, frame/SP/FP mismatches or invalid handler alignment
without publishing a transfer record. Roots remain owned by the active token after preparation.

`EhStateSmoke` covers a two-frame transfer through real GC and checks encoded handler PC/SP/exception;
an unreadable saved return PC fails before callbacks and preserves exception state. Changed:
`ManagedExceptionRuntimeV1.cs`, `EhStateSmoke.cs`, this log. Full compiler smoke, compiler/runtime
builds, Doom 26/26 and Core/Windows/guest builds passed; installed-pack preflight remains
`HCCIL1882`, `DoomGame.Error:IL_0026`.

Still unqualified: caller return-PC versus EH lookup-PC convention at exact try boundaries,
native transfer record lifetime/placement, actual ISE transfer execution, CIL handler/leave lowering
and helper/image binding. This API is preparation, not evidence that those remaining contracts work.

### Caller return-PC versus EH lookup-PC

`HybridCpuManagedEhFrameSnapshotV1` now explicitly distinguishes a return address from a fault PC.
Native preparation marks reconstructed callers as return addresses. EH range lookup subtracts
exactly one 256-byte native bundle for those snapshots, while preserving architectural return PCs
for unwind-chain checks and context restoration. Real fault PCs are not biased. A finally replacement
resets the marker when continuing at an actual handler PC. Frame validation and standalone unwind
accept a marked return PC at method end only when the preceding bundle belongs to that method.

Regressions cover a call in the final try bundle, raw fault-PC exclusion at the same boundary,
return-PC underflow rejection, method-end lookup and two-frame rooted HCET preparation at try end.
Changed: Platform.Contracts `PlatformContractsV1.cs`, runtime `ManagedExceptionRuntimeV1.cs`,
smoke `EhDispatchSmoke.cs`, `EhUnwindSmoke.cs`, `EhStateSmoke.cs`, this log.
Runtime/compiler builds, full compiler smoke, Doom 26/26 and Core/Windows/guest builds passed.
Installed-pack preflight remains `HCCIL1882`, `DoomGame.Error:IL_0026`; CIL/image/native execution
qualification and real `.hcexe` publication remain unfinished.

Verified for this iteration: full ManagedPortSmoke (including reachable UTF-16 table, deterministic
handles, image-plan gate, byte/bool SB stores and two-byte-store negative), Doom CoreCLR 26/26 (including cheat first/repeated use and mismatch reset), Core/Windows/guest
Release builds and diagnostic publish. The
diagnostic publish exits fail-closed; it does not emit a guest `.hcexe`.

Pack 1.15.75 also fixes a compiler transport deadlock found by this closure: graph diagnostics are
bounded to 2048 UTF-8 bytes and put compact canonical cycle witnesses first. The prior unbounded
message filled the ILCompiler child stderr pipe while its parent waited for process exit.

The conservative closure report was regenerated after the source flag change; its counts
remain unchanged. It is not a successfully compiled or image-qualified closure.

## Source-of-truth pack

- Pack: `1.15.101-refplan7-phase15`
- Source commit: `94ea82652cdd4e0f8046b5bd5becbd11461482ca`
- SDK: `10.0.204` (pinned by `global.json`)
- Profile accepted by targets: `HybridCPU.DotNetAot.ScalarControlFlowV2`
- `PublishQualifiedWorkstreams`: `exact-aot-generics`, `gc-maps-safepoints`,
  `static-type-initialization`, `szarray-core`, `type-layout-object-references`,
  `utf16-string-literals`, `virtual-interface-dispatch`

This is the restricted scalar/body-world publication contour, not the production full-managed
graph profile described by RefPlan7. The guest declares every capability it actually needs and
therefore fails closed with:

```text
HCPUB1004: unsupported managed publish workstream(s): eh-unwind.
```

No `.hcexe` is emitted by this attempt.

## Reachable managed closure

`AotPreflightClosure.txt` is generated from the actual Release CIL rooted at
`DoomSharp.HybridCpu.Guest.EntryPoint.Run`. The analyzer recursively follows calls, constructors
and function pointers, registers cctors, reads EH clauses, and expands every owned implementation
of reachable virtual/interface calls. Current closure:

- 1,926 owned reachable methods;
- 37 external/CoreLib members;
- 202 referenced types;
- 25 reachable cctors;
- 2 methods containing EH;
- 8,787 reachable `callvirt` sites, 271 `newarr`, 16 `isinst`, 20 `throw`, one `rethrow`;
- no reachable `box`, `unbox` or `unbox.any` instruction in this current root closure.

The exact external member list includes `Array.Clear/Copy/Empty/Fill`, string construction,
comparison and concatenation, `Math.Abs/Max`, primitive formatting, exception constructors,
closed `Nullable<T>` members, `RuntimeHelpers.InitializeArray`, `ReadOnlySpan<char>` and the
Roslyn `DefaultInterpolatedStringHandler` constructor/append/to-string surface. `IConsole.WriteLine`
is now an ordinary required interface slot; there is no default-interface implementation left.

The report is a conservative dispatch closure: it intentionally includes every locally defined
implementation compatible with a reachable virtual/interface slot. That is the fail-closed set
the production compiler/runtime must admit; a points-to-aware compiler may prove a smaller set.

## Restricted diagnostic probe

Running the importer with the capability declaration deliberately suppressed (diagnostic only)
passes the manifest, call-graph recursion, class allocation, scalar-constructor, exact static
scalar-field, reference-SZARRAY allocation, projected `Angle / uint` call and inherited exception
layout, primitive-array and nested-array TypeSpec gates and reaches:

```text
HCCIL1882: throw requires native non-returning exception-state transfer, unwind/GC integration and managed unhandled process-exit lowering.
[cil:...:DoomSharp.Core.DoomGame.Error:il_0026]
```

Cycle witnesses remain rotation-canonical, compact and UTF-8-budgeted. Missing `newobj` bindings and
managed-byref gates now name their exact target/token/signature. The next implementation unit is
native exception-state transfer for DoomGame.Error, including root retention, unwind and managed process exit (not CPU trap). Runtime/image literal registration, stable intern
identity and pre-cctor GC roots remain unfinished and explicitly image-gated. Arbitrary aggregate
stores remain fail-closed.

## Latest verification commands

Files changed in the continuation from pack 1.15.97:

- Compiler: `Cil/RestrictedCilImporterV1.ControlFlowV2.cs`,
  `Cil/RestrictedCilImporterV1.MetadataBindingsV1.cs`,
  `NativeAot/HybridCpuSdkPackContractsV1.cs`, `NativeAot/Packaging/hybridcpu.runtime-pack.json`.
- Compiler tests: `Tools/ManagedPortSmoke/ByteArraySmoke.cs`, `MetadataReferenceArraySmoke.cs`,
  new `ThrowBoundarySmoke.cs`, and `Program.cs` in that same directory.
- Doom: `src/DoomSharp.Core/GameLogic/CheatSequence.cs`, `src/DoomSharp.Tests/Program.cs`,
  `src/DoomSharp.HybridCpu.Guest/DoomSharp.HybridCpu.Guest.csproj`, this document and guest `README.md`.
- `AotPreflightClosure.txt` was regenerated; closure counts remain unchanged.
- Fresh SDK installations: phase15-98, phase15-99, phase15-100, and the active `.hcpu-p100`.
  Previously installed packs and unrelated dirty files were not removed or overwritten.

From the compiler directory:

```powershell
dotnet run --project Tools/ManagedPortSmoke/ManagedPortSmoke.csproj -c Release -- "\HybridCPU ISE\DoomSharpHybridCPU\src\DoomSharp.Core\bin\Release\net8.0\DoomSharp.Core.dll"
dotnet build NativeAot/HybridCPU.Compiler.NativeAot.Adapter.csproj -c Release
```

From Doom `src` (all passed after each completed boundary above):

```powershell
dotnet run --project DoomSharp.Tests/DoomSharp.Tests.csproj -c Release
dotnet build DoomSharp.Core/DoomSharp.Core.csproj -c Release --no-restore
dotnet build DoomSharp.Windows/DoomSharp.Windows.csproj -c Release --no-restore
dotnet build DoomSharp.HybridCpu.Guest/DoomSharp.HybridCpu.Guest.csproj -c Release --no-restore
```

From the guest directory, diagnostic publish fails at HCCIL1882; normal publish fails at HCPUB1004:

```powershell
dotnet publish .\DoomSharp.HybridCpu.Guest.csproj -c Release -r hybridcpu -p:HybridCpuManagedRequirements= -p:PublishDir=.\publish-phase15-100-diagnostic\
dotnet publish .\DoomSharp.HybridCpu.Guest.csproj -c Release -r hybridcpu -p:PublishDir=.\publish-phase15-100-release\
```

No `.hcexe` exists under the guest directory. Two successful clean publications and image SHA-256
comparison have not been reached. Pack manifest hashes above are not executable hashes.

## Pack 1.15.101: compiler EH plan structural validation

The graph-owned EH importer now checks decoded instruction boundaries for clause starts/ends,
disjoint own try/handler ranges, enclosing catch for rethrow, enclosing finally for endfinally,
and leave transitions that do not enter a new protected region or exit a finally.
Nested leave remaining within the enclosing finally is not rejected by that rule.

New `EhPlanSmoke.cs` covers valid catch/rethrow and fail-closed HCCIL0812/0813/0814 paths.
Changed: `Cil/ManagedEhContractsV1.cs`, new `Tools/ManagedPortSmoke/EhPlanSmoke.cs`, smoke
`Program.cs`, pack version/manifest, guest pack path, README and this log. The fresh `.hcpu-p101`
preserves all previous packs. Full compiler smoke, Doom 26/26, compiler/Core/Windows/guest builds
passed. Actual new-pack diagnostic publish remains HCCIL1882 at `DoomGame.Error:IL_0026`;
normal publish remains HCPUB1004 (`eh-unwind`). No native EH qualification or `.hcexe` is claimed.
# 2026-09-01 current fail-closed preflight

The authoritative clean cycle is under `TempEnv/doom-rootmap-loop-20260901`.
Full ManagedPortSmoke, Doom CoreCLR 26/26, Core/Windows/guest Release builds and
compiler/ManagedRuntime/RuntimeKernel Release builds pass. The immutable 217-file
`pack193-control` was rebuilt and verified; closure remains 1937 owned methods,
37 external/CoreLib members, 26 cctors, 2 EH methods, 20 `throw` and 1 `rethrow`.
Normal publish terminates at HCPUB1004 (`eh-unwind`, adapter 25 / publish 1).

Closed in order: scheduler descendant DFS scalability; dense allocation proof graphs
and dependency digest materialization; allocator/liveness input capacity for
`MapObjectInfo.AddPredefinedTypes` (15038 IR / 9557 values); synchronized HCMG/compiler/runtime
safepoint capacity (5617 required, bounded at 8192); exact ABI-ingress lifetime for
`GameController.AddThinker`; and the root-map requirement that incorrectly treated an
optimization MII proof as GC-map evidence. GC maps still require current dependency,
liveness, pressure/resource, final allocation/frame and exact W=8 placement evidence.

The latest diagnostic publish terminates naturally with backend 15, publish-graph 31
and publish 1 at exactly:

```text
HCSCF-LINK4003: Register allocation rejected method
mmid:09efc4b6864e76261be3be25836af4e97deee17915701c19517e89c288f9774d:
DoomSharp.Core.GameLogic.GameController..ctorinstance(System.Object):System.Void
(assembly=DoomSharp.Core,1.0.0.0,neutral,none; IR=579; values=496):
Unsupported: Value 'cil:c0f0169c1772b22da0f112b7fc6c492c165872fbaf54190638436abad60bd5de:
DoomSharp.Core.GameLogic.GameController..ctor:il_0018:null-check-arg-abi:value'
cannot satisfy the native call/return location contract.
```

No Doom `.hcexe` exists. Diagnostic declaration suppression is not publication authority,
and no host/CoreCLR run is claimed as HybridCPU guest execution.
# 2026-09-01 closed-CFG and guest-service HCO checkpoint

Fresh workspaces `TempEnv/doom-closed-cfg-20260901`, `doom-object-detail-20260901`,
and `doom-guest-symbol-20260901` closed two independently reproduced backend blockers.
`DoomGame.DoomLoop` imports as an exact closed CFG (9 blocks, 232 IR instructions,
202 values, zero terminal blocks). The allocator now distinguishes that shape from a
terminal non-returning path: it does not require preservation of an unobservable entry
return address, while every actual terminal block still requires `ret` or an exact
exception transfer. The method allocates with zero spills and unchanged final IR.

The next diagnostic reached `HybridCpuConsole.WriteLine` and exposed an HCO relocation
to `__hybridcpu_managed_guest_console_write_utf16` without a matching undefined symbol.
Method objects now derive undefined symbols from their exact relocation targets as well
as managed direct callees, excluding linker-owned EH thunks. Object-writer diagnostics
now include method, section, target, relocation kind and offset. Full ManagedPortSmoke,
Doom CoreCLR tests (26/26), Release Core/Windows/guest builds, compiler/runtime/kernel
builds and immutable-pack verification passed. Closure remains 1937 owned methods,
37 external/CoreLib members and 202 referenced types.

The exact current diagnostic blocker is adapter exit `-532462766` / publish exit 1 in
`ManagedDispatchTypeObjectV1.Emit`: `Dispatch type 7 lacks its exact descriptor.` This
is an image dispatch-metadata closure failure, not HybridCPU guest execution. Normal
publish remains fail-closed with exit 25 at `HCPUB1004: unsupported managed publish
workstream(s): eh-unwind`. No Doom `.hcexe` or publish hashes exist.
# 2026-09-01 dispatch ownership checkpoint

Fresh `TempEnv/doom-dispatch-owner-20260901` evidence closes the prior
`Dispatch type 7 lacks its exact descriptor` failure. Type 7 is the provider-local
metadata row ID for `IHybridCpuGuestServices`, carried by exact direct runtime-service
plans (`RuntimeTypeIdentity=runtime-service`); it is not an image `TypeId`. Dispatch
type/table emission now excludes exactly those service-owned plans while retaining
image-owned virtual/interface plans, and rejects mixed ownership. Full ManagedPortSmoke,
Doom CoreCLR tests (26/26), all Release/compiler/runtime/kernel builds, immutable pack
verification and closure 1937/37/202 pass.

Normal publish still has outer exit 1 and adapter exit 25 at `HCPUB1004: unsupported
managed publish workstream(s): eh-unwind`. Diagnostic publish has outer exit 1 and
adapter exit 15 at `HCSCF-LINK4005 / HCLINK1003`: undefined global symbol
`__hybridcpu_managed_argument_exception_get_message`. The dispatch table requires the
exact `System.ArgumentException.get_Message` implementation, but the current compiler
contains only its symbol mapping; no native emitter/object exists. Mapping it to base
`System.Exception.get_Message` would violate CoreCLR message/ParamName semantics and is
not permitted. No Doom `.hcexe` or hashes exist.
# 2026-09-01 exception getter checkpoint

Fresh `TempEnv/doom-exception-getter-20260901` closes the latent base
`System.Exception.get_Message` object dependency. The compiler now emits a deterministic
HCO definition for `__hybridcpu_managed_exception_get_message`, bound to the exact
descriptor-owned aligned `_message` offset. Invalid, unaligned and out-of-range offsets
fail closed. Full ManagedPortSmoke, Doom CoreCLR 26/26, Release builds, compiler/runtime/
kernel builds, immutable pack verification and closure 1937/37/202 pass.

The former generic `HCLINK1003` is now an exact capability diagnostic. Diagnostic publish
has outer exit 1 / adapter exit 15 at `HCSCF-LINK4015`:
`System.ArgumentException.get_Message requires exact native string allocation/concat and
ParamName formatting; the base Exception message-field getter cannot substitute for this
override.` The managed reference model proves the required invariant format
`message + " (Parameter '" + paramName + "')"`, including null/empty/raw UTF-16 cases and
GC retention. Native concat/allocation objects are not yet present, so this override stays
fail-closed. Normal publish remains outer exit 1 / adapter exit 25 at `HCPUB1004 eh-unwind`.
No Doom `.hcexe` or hashes exist.

# 2026-09-01 image/bootstrap heap-consumer checkpoint

Fresh source and production-call-site inspection refines the former `HCSCF-LINK4015`
gate. `HybridCpuManagedHeapAllocatorV1`, `HybridCpuManagedNonMovingGcV1` and
`HybridCpuManagedBootstrapRuntimeV1` exist as host-side reference implementations, but
no production ISE/loader code consumes the image runtime-bootstrap descriptor or installs
the managed heap, type system, GC safepoints and allocating helper bindings. All observed
bootstrap invocations are smoke/xUnit code. Therefore defining only a native concat symbol
would still leave an unexecutable image and is rejected.

Fresh `TempEnv/doom-bootstrap-consumer-20260901` evidence passes full
ManagedPortSmoke, Doom CoreCLR 26/26, Release Core/Windows/guest and compiler/runtime/
kernel builds, a new verified immutable 217-file pack, and closure 1937/37/202. The
exact current compiler diagnostic is `HCSCF-LINK4016` for
`System.ArgumentException.get_Message`: allocating `String.Concat` has no production
image/bootstrap runtime consumer. Diagnostic publish has outer exit 1, ILCompiler exit 1,
adapter exit 15. Normal publish has outer exit 1, adapter exit 25 at `HCPUB1004: eh-unwind`.
No Doom `.hcexe` or clean-publish hashes exist.

# 2026-09-01 guest-visible heap backing checkpoint

Fresh `TempEnv/doom-guest-heap-backing-20260901` closes the private-host-arena defect in
`HybridCpuManagedHeapAllocatorV1`. The allocator now consumes an explicit bounded
`IHybridCpuManagedHeapMemoryV1`; its deterministic array implementation is component/test
only. Production ISE now provides `HybridCpuIseHeapMemoryV1`, bound to one explicit
`Processor.MainMemoryArea` with exact physical range checks and no fallback to the mutable
global `Processor.MainMemory`. A production-assembly probe materialized an allocated object
header at guest address `0x1000`, verified its TypeHandle and zero header word, and rejected
both underflow and end-exclusive overflow.

Full ManagedPortSmoke, Doom CoreCLR 26/26, Release Core/Windows/guest and compiler/runtime/
kernel builds, verified immutable 217-file pack, closure 1937/37/202, and Release ISE build
pass. The broad ISE xUnit project remains independently uncompilable because older tests
omit the now-required `HybridCpuBootInfoV1.VirtualClockTicksPerSecond`; this was not altered.
Diagnostic publish still stops at `HCSCF-LINK4016` (outer 1 / adapter 15), because no
production orchestration yet constructs bootstrap/type/heap/GC state and binds allocating
helpers. Normal publish remains `HCPUB1004: eh-unwind` (outer 1 / adapter 25). No Doom
`.hcexe` or hashes exist.

# 2026-09-01 production loader and CoreLib Message-body checkpoint

`HybridCpuIseManagedImageLoaderV1` now provides a production ISE consumer for an already
inspected managed image. It materializes bytes into one explicit `Processor.MainMemoryArea`,
boots RuntimeKernel, installs exact image type metadata, binds the guest-visible heap,
constructs string and non-moving GC runtimes, and runs the bootstrap contract. Its result
explicitly denies CPU/ECALL execution authority. The production probe under
`TempEnv/ise-managed-image-loader-probe-20260901` verifies exact image bytes, a guest-visible
allocated object header, and fail-closed image/heap overlap rejection.

The former `HCSCF-LINK4016` consumer gate is closed. The next two diagnostic runs exposed and
closed an incorrect synthetic dispatch symbol, then established the exact remaining blocker.
Fresh `TempEnv/doom-corelib-message-body-20260901` passes full ManagedPortSmoke, Doom CoreCLR
26/26, Core/Windows/guest Release, compiler/runtime/kernel builds, a verified immutable
217-file manifest pack (218 physical files including the manifest), and closure 1937 owned /
37 external / 202 referenced types. Diagnostic publish exits outer 1 / ILCompiler 1 / adapter
15 at `HCSCF-LINK4017`: virtual dispatch requires executable CoreLib
`System.ArgumentException.get_Message`, but the current CoreLib input is metadata-only and
supplies neither that managed body nor an exact allocating runtime implementation. The base
`System.Exception::_message` getter is not a semantic substitute because it omits parameter-name
formatting. Normal publish remains outer 1 / adapter 25 at `HCPUB1004: eh-unwind`. No Doom
`.hcexe` or clean-publish SHA-256 exists.

# 2026-09-01 exact ArgumentException.Message helper-contract checkpoint

A probe against the exact pack CoreLib identifies `System.ArgumentException.get_Message` as
method token `0x06000c8d`. Direct ScalarControlFlowV2 import first rejects its call to
`System.ArgumentException.SetMessageField`; importing the CoreLib body world reaches unsupported
`ldloca.s` in the formatting closure. Doom closure contains only
`ArgumentNullException(string)` and `ArgumentOutOfRangeException(string)`; their qualified
runtime constructors already install the invariant message, parameter name and HRESULT.

The exact complete Message behavior is now represented by managed ABI helper
`__hybridcpu_managed_argument_exception_get_message(object-ref):object-ref`, marked allocating
and `RequiredSafepoint`. Dispatch metadata binds the CoreLib virtual target to that helper, and
ManagedPortSmoke covers its ABI identity, GC transition, invariant UTF-16 formatting, null/empty
parameter behavior, GC retention and malformed receiver rejection. No executable thunk was
invented: fresh short-path diagnostic evidence at `TempEnv/d4018` exits outer 1 / ILCompiler 1 /
adapter 15 at `HCSCF-LINK4018`, because production ISE has no CPU-callable ECALL bridge that
transfers the object reference/result and preserves allocation safepoint roots. The host-side
ManagedRuntime method is reference evidence only. Normal publish remains outer 1 / adapter 25 at
`HCPUB1004: eh-unwind`. Full preflight in
`TempEnv/doom-argument-message-helper-contract-20260901` passed before publication; its longer
path also demonstrated Windows CreateProcess error 206, so terminal compiler evidence uses the
byte-identical verified pack copied to the short path. No Doom `.hcexe` or hashes exist.
## 2026-09-01 managed ArgumentException ECALL bridge checkpoint

The allocating `System.ArgumentException.get_Message` target now links to the
CPU-callable `__hybridcpu_managed_argument_exception_get_message` thunk and the
production ISE retirement path has an exact user-mode ManagedRuntime ECALL
envelope. The bridge transfers the receiver/result registers, contains invalid
requests and provider failures, advances exactly one 256-byte bundle, and falls
through to the architectural trap path for unknown services or an unrepresentable
next PC. Loader construction remains explicit and grants no execution authority.

A first diagnostic run exposed and then closed duplicate process-exit module
ownership (`HCSCF-LINK4005 / HCLINK0002`). The complete clean rerun under
`TempEnv/d4020` passed full ManagedPortSmoke, Doom CoreCLR 26/26, Core, Windows,
guest, compiler, ManagedRuntime, RuntimeKernel and ISE Release builds, verified a
217-file immutable pack, and reproduced the 1937-owned/37-external/202-type
closure. Diagnostic publish (SDK 10.0.204, declaration gate only removed) now
terminates at compiler/static-link layer:

```text
HCPUB2002: ILCompiler graph publication failed with exit 1
HybridCPU adapter exit 15
HCSCF-LINK4005: Static linker rejected the managed object world
HCLINK1003: Undefined global symbol '__hybridcpu_managed_ensure_type_initialized'.
```

The aggregate ILCompiler diagnostic provides no method, assembly or IL offset;
none is inferred. Normal publish remains exit 1 / adapter 25 at
`HCPUB1004: unsupported managed publish workstream(s): eh-unwind`. No Doom
`.hcexe`, deterministic publish hash pair, WAD materialization, or guest ISE
execution is claimed.
## 2026-09-01 static runtime ECALL checkpoint

Three successive clean fail-closed cycles under `TempEnv/d4021`,
`TempEnv/d4022`, and `TempEnv/d4023` closed the reached undefined symbols
`__hybridcpu_managed_ensure_type_initialized`,
`__hybridcpu_managed_static_store_i4`, and
`__hybridcpu_managed_static_load_i4`. Each cycle repeated full
ManagedPortSmoke, Doom CoreCLR 26/26, all requested Release builds, ISE build,
217-file immutable-pack verification, closure reporting, and diagnostic publish.

The production ManagedRuntime ECALL bridge now checks bootstrap-completed type
initialization by exact type handle and performs descriptor-validated 32-bit
static load/store through `HybridCpuManagedStaticFieldRuntimeV1`. The external
envelope was necessarily extended to three deterministic scalar arguments
(x16, x18, x19; x17 remains the ECALL number). Invalid type handles, offsets,
field kinds, argument counts and operations fail closed.

The exact next diagnostic is:

```text
HCPUB2002 / ILCompiler exit 1 / adapter exit 15
HCSCF-LINK4005: Static linker rejected the managed object world
HCLINK1003: Undefined global symbol '__hybridcpu_managed_null_check'.
```

No method, assembly or IL offset is present in the aggregate diagnostic.
`HybridCpuManagedTrapMappingRuntimeV1` has no production ISE/RuntimeKernel
consumer, so a zero-address fault cannot yet be claimed to allocate and dispatch
an exact `NullReferenceException`. A process-exit substitution was deliberately
not added. Normal publish remains HCPUB1004 `eh-unwind`; no Doom `.hcexe`
exists.

## 2026-09-01 null-check capability gate checkpoint

The undefined-symbol diagnostic is replaced by an explicit fail-closed linker gate. CIL
emissions now preserve their exact source IL offset in IR metadata, and
`StaticProjectionSmoke` verifies that an instance-field null check is attributed to its
actual owning constructor. The clean `TempEnv/d4028` cycle passed full ManagedPortSmoke,
Doom CoreCLR 26/26, Core/Windows/guest/compiler/ManagedRuntime/RuntimeKernel/ISE Release
builds, closure generation, and verification of a new 217-file immutable pack.

Diagnostic publish from `TempEnv/d4029` with SDK 10.0.204 exits publish 1 / ILCompiler 1 /
adapter 15 at:

```text
HCSCF-LINK4019: Method 'mmid:001b2e26b40f4ab33acfde8802cce4cbd7d96440aa7f299cc5ceb35a788b2369:DoomSharp.Core.GameLogic.Player.set_DidSecretinstance(System.Object,System.Boolean):System.Void'
(assembly=DoomSharp.Core,1.0.0.0,neutral,none) at IL_0002 requires exact
NullReferenceException semantics, but production ISE retirement does not yet connect a
precise zero-address managed-user fault to RuntimeKernel authorization, exception allocation,
GC roots, handled/unhandled EH transfer and managed process exit.
```

This is a compiler/static-link capability diagnostic. No no-op or process-exit substitute was
introduced. Normal publish in `TempEnv/d4030` remains exit 1 / adapter 25 at `HCPUB1004:
unsupported managed publish workstream(s): eh-unwind`. No Doom `.hcexe`, clean-publish hashes,
or HybridCPU guest execution exists.

## 2026-09-01 explicit null-check and i4 array-store checkpoint

The explicit null-check now returns a non-null receiver unchanged and, for null, allocates the
exact image-owned `System.NullReferenceException` through the production managed-runtime ECALL
before tail-entering native throw dispatch with the original managed caller return address.
This closes `HCSCF-LINK4019` without relying on an unimplemented implicit-fault retirement path.

The subsequently reached `__hybridcpu_managed_array_store_i4` linker blocker is also closed.
The production bridge uses guest-backed `HybridCpuManagedArrayRuntimeV1`; null and bounds failures
allocate exact `NullReferenceException` and `IndexOutOfRangeException` objects and enter native EH,
while malformed type/provider states fail closed. Clean cycles `d4035` and `d4039` each passed the
full preflight and verified a new 217-file pack. Diagnostic `d4040` now terminates at:

```text
HCPUB2002 / ILCompiler exit 1 / adapter exit 15
HCSCF-LINK4005: HCLINK1003: Undefined global symbol '__hybridcpu_managed_array_store_ref'.
```

The aggregate linker diagnostic contains no method, assembly, or IL offset. Normal publish
`d4041` remains `HCPUB1004: eh-unwind`. No Doom `.hcexe` or determinism hashes exist.

## 2026-09-01 reference-store, InitializeArray and newarr checkpoint

The reached `__hybridcpu_managed_array_store_ref`, `__hybridcpu_managed_initialize_array`, and
`__hybridcpu_managed_newarr` linker blockers are closed in order. Reference stores use exact
runtime assignability and allocate guest `NullReferenceException`, `IndexOutOfRangeException`,
or `ArrayTypeMismatchException` before native EH transfer. FieldRVA blobs now travel as bounded,
digest-covered immutable bootstrap rows into the production ISE loader; InitializeArray preserves
its atomic size/type validation. Newarr returns the guest-backed allocation and maps negative/size
overflow and heap exhaustion to exact guest `OverflowException` and `OutOfMemoryException`.
Malformed handles/provider failures remain fail closed.

Clean preflights `d4046` and `d4050` passed full ManagedPortSmoke, Doom CoreCLR 26/26, all requested
Release builds, closure (1937 owned / 37 external / 202 types), and verified 217-file packs.
Diagnostic `d4051` exits publish 1 / ILCompiler 1 / adapter 15 at the next exact compiler/linker
blocker:

```text
HCSCF-LINK4005: Static linker rejected the managed object world
(HCLINK1003: Undefined global symbol '__hybridcpu_managed_static_store_ref'.)
```

The aggregate diagnostic contains no method, assembly, or IL offset. Normal publish `d4052-normal`
exits 1 / adapter 25 at `HCPUB1004: unsupported managed publish workstream(s): eh-unwind`.
No Doom `.hcexe`, determinism hashes, or HybridCPU guest execution exists.

## 2026-09-01 static references and object allocation checkpoint

Three subsequently reached undefined helper blockers are closed in strict order:
`__hybridcpu_managed_static_store_ref`, `__hybridcpu_managed_static_load_ref`, and
`__hybridcpu_managed_alloc`. Static reference access is restricted to exact descriptor-owned
8-byte reference slots; malformed kind/offset requests fail atomically. The non-moving GC scans
those descriptor-declared static roots, with smoke evidence for retention and reclamation.
Object allocation now uses the guest-backed heap at a RequiredSafepoint; invalid type handles fail
closed and exhaustion allocates exact image-owned `System.OutOfMemoryException` before native EH
transfer. Managed ABI is minor 36.

Clean preflights `d4054`, `d4057`, and `d4061` each passed full ManagedPortSmoke, Doom CoreCLR
26/26, requested Release builds, closure (1937 owned / 37 external / 202 types), and a verified
217-file immutable pack. Diagnostic `d4062` exits publish 1 / ILCompiler 1 / adapter 15 at:

```text
HCSCF-LINK4005: Static linker rejected the managed object world
(HCLINK1003: Undefined global symbol '__hybridcpu_managed_array_load_ref'.)
```

The aggregate diagnostic supplies no method, assembly, or IL offset. Normal publish
`d4063-normal` exits 1 / adapter 25 at `HCPUB1004: unsupported managed publish workstream(s):
eh-unwind`. No Doom `.hcexe`, determinism hashes, or HybridCPU guest execution exists.

## 2026-09-01 fail-closed continuation: array loads and UInt32 division

The diagnostic sequence closed three exact static-link blockers without qualifying publication. `__hybridcpu_managed_array_load_ref` now uses a two-argument allocating/throwing ECALL, exact signed-I4 index validation, exact `NullReferenceException` / `IndexOutOfRangeException` image types, explicit success-versus-managed-throw state, GC safepoint metadata and native EH transfer. `__hybridcpu_managed_divide_u4_checked` now preserves the UInt32 carrier, returns the unsigned quotient, and allocates the exact `DivideByZeroException` on zero. `__hybridcpu_managed_array_load_i4` now uses the same exact bounds/null and managed-throw contract; the managed ABI schema minor is 38.

Clean preflight `TempEnv/d4072` passed the full ManagedPortSmoke, targeted allocation-capacity smoke, Doom CoreCLR tests (26/26), Core/Windows/guest/compiler/runtime/kernel/ISE Release builds, immutable 217-file pack verification, and closure reporting 1937 owned methods, 37 external/CoreLib members and 202 referenced types. Diagnostic publish `TempEnv/d4073` exited outer 1 / ILCompiler 1 / adapter 15 (MSBuild command wrapper 31) at `HCSCF-LINK4005` / `HCLINK1003`: undefined global symbol `__hybridcpu_managed_array_empty`. The aggregate diagnostic contains no method, assembly or IL offset, so none is inferred. Ordinary publish `TempEnv/d4074-normal` remains fail-closed at adapter exit 25 with `HCPUB1004: unsupported managed publish workstream(s): eh-unwind`.

No Doom `.hcexe` was produced. No publish hash or HybridCPU guest/ISE execution claim exists. The next and only confirmed diagnostic blocker is `__hybridcpu_managed_array_empty`; loader-backed WAD materialization, production ECALL/ISE execution and EH remain separate unopened gates.

### 2026-09-01 `Array.Empty<T>` continuation

`__hybridcpu_managed_array_empty` is now an exact one-argument ECALL over the existing per-type cached zero-length SZARRAY runtime. Its cache remains part of the non-moving GC root set. Invalid/non-array type handles fail as provider errors; allocation failure transfers an exact `System.OutOfMemoryException` through native EH. Because the first call can allocate and throw, the ABI row changed from `MayThrow=false` to `true` and schema minor is 39. Regressions cover cache identity, length zero, invalid type, ABI effects and exact HCO symbol definition.

Clean preflight `TempEnv/d4076` passed full ManagedPortSmoke, Doom CoreCLR 26/26, all requested Release builds, verified immutable 217-file pack, and closure 1937 owned / 37 external / 202 types. Diagnostic `TempEnv/d4077` exits outer 1 / ILCompiler 1 / adapter 15 (MSBuild wrapper 31) at `HCSCF-LINK4005` / `HCLINK1003: Undefined global symbol '__hybridcpu_managed_array_length'`; no method, assembly or IL offset is present. Normal publish `TempEnv/d4078-normal` remains at adapter exit 25 / `HCPUB1004: unsupported managed publish workstream(s): eh-unwind`. No Doom `.hcexe` or guest execution evidence exists.

### 2026-09-01 array length continuation

`__hybridcpu_managed_array_length` now reads the qualified runtime array layout and returns the non-negative native-uint length carrier. Null is mapped to an exact allocated `System.NullReferenceException` and native EH transfer; malformed/non-array references fail closed as provider errors. Its previously non-throwing ABI row was corrected to required safepoint / `MayThrow=true`, advancing ABI minor to 40. Regressions cover zero length, null status and exact ABI/HCO definition.

Clean preflight `TempEnv/d4080` passed full ManagedPortSmoke, Doom CoreCLR 26/26, all requested Release builds, verified immutable 217-file pack and closure 1937/37/202. Diagnostic `TempEnv/d4081` exits outer 1 / ILCompiler 1 / adapter 15 (wrapper 31) at `HCSCF-LINK4005` / `HCLINK1003: Undefined global symbol '__hybridcpu_managed_array_store_i1'`; no method, assembly or IL offset is present. Normal `TempEnv/d4082-normal` remains adapter 25 / `HCPUB1004: eh-unwind`. No Doom `.hcexe`, publish hashes or guest execution evidence exists.

### 2026-09-01 byte-array store continuation

`__hybridcpu_managed_array_store_i1` now preserves low-byte truncation, signed-I4 index validation and atomic runtime writes. Null and bounds failures allocate exact `NullReferenceException` / `IndexOutOfRangeException` objects and transfer through native EH; width/type mismatches fail as provider errors. Its ABI row is now required-safepoint / `MayThrow=true`, advancing ABI minor to 41. Existing exhaustive truncation/CoreCLR parity and atomic negative tests are supplemented by exact ABI/HCO definition checks.

Clean preflight `TempEnv/d4084` passed full ManagedPortSmoke, Doom CoreCLR 26/26, all Release builds, verified immutable 217-file pack and closure 1937/37/202. Diagnostic `TempEnv/d4085` exits outer 1 / ILCompiler 1 / adapter 15 (wrapper 31) at `HCSCF-LINK4005` / `HCLINK1003: Undefined global symbol '__hybridcpu_managed_string_char'`; no method, assembly or IL offset is present. Normal `TempEnv/d4086-normal` remains adapter 25 / `HCPUB1004: eh-unwind`. No Doom `.hcexe` exists.

### 2026-09-01 string character continuation

`__hybridcpu_managed_string_char` now returns the exact raw UTF-16 code unit for a valid signed-I4 index. Null and negative/out-of-range indices allocate exact `NullReferenceException` / `IndexOutOfRangeException` objects and transfer through native EH; malformed/non-string references fail as provider errors. The ABI row is required-safepoint / `MayThrow=true`, advancing schema minor to 42. Regressions cover raw UTF-16, null, both bounds directions, ABI effects and exact HCO definition. The first full attempt, `TempEnv/d4088`, failed the negative-index regression because the runtime shared the `-1` length sentinel with character access; it is not acceptance evidence. Length and character reads now use an explicit operation selector, so negative character indices reach the bounds check.

Clean preflight `TempEnv/d4089` passed full ManagedPortSmoke, Doom CoreCLR 26/26, all requested Release builds, verified immutable 217-file pack, and closure 1937 owned / 37 external / 202 types. Diagnostic `TempEnv/d4090` exits outer 1 / ILCompiler 1 / adapter 15 (MSBuild wrapper 31) at `HCSCF-LINK4005` / `HCLINK1003: Undefined global symbol '__hybridcpu_managed_string_length'`; the aggregate supplies no method, assembly or IL offset. Normal `TempEnv/d4094-normal`, with the pack-qualified SDK identity `10.0.204`, exits outer 1 / adapter 25 at `HCPUB1004: unsupported managed publish workstream(s): eh-unwind`. As an additional fail-closed check, unpinned current SDK `11.0.100-preview.1.26104.118` is rejected in `TempEnv/d4092-normal` by `HCPUB1007`. No Doom `.hcexe`, publish hashes or HybridCPU guest execution evidence exists.

### 2026-09-01 string length continuation

`__hybridcpu_managed_string_length` is now an exact one-argument ECALL over the immutable string runtime. It returns the non-negative UTF-16 code-unit count; null allocates an exact `System.NullReferenceException` and transfers through native EH, while malformed/non-string references fail as provider errors. Its ABI row is corrected to read/write, required-safepoint and `MayThrow=true`, advancing schema minor to 43. Regressions cover successful length, exact null status and ABI/HCO definition.

The first orchestration attempts are not acceptance evidence: `TempEnv/d4096` used legacy Windows PowerShell and stopped after tests because `Path.GetRelativePath` was unavailable; `TempEnv/d4097` reran the entire cycle under `pwsh`. That clean run passed full ManagedPortSmoke, Doom CoreCLR 26/26, all requested Release builds, verified an immutable 217-file pack with zero hash mismatches, and reproduced closure 1937/37/202. Diagnostic `TempEnv/d4098` exits outer 1 / ILCompiler 1 / adapter 15 (wrapper 31) at `HCSCF-LINK4005` / `HCLINK1003: Undefined global symbol '__hybridcpu_managed_exception_ctor_message'`; no method, assembly or IL offset is present. Normal `TempEnv/d4099-normal` exits outer 1 / adapter 25 at `HCPUB1004: unsupported managed publish workstream(s): eh-unwind`. No Doom `.hcexe`, hashes or guest execution evidence exists.

### 2026-09-01 exception message constructor continuation

`__hybridcpu_managed_exception_ctor_message` is now a layout-bound native helper that stores argument register `x11` into the exact aligned `System.Exception::_message` reference slot of receiver `x10`, then returns through the existing managed-call adjustment. It adds no ECALL, ISA, ABI or managed-semantic change. The linker fails closed with `HCSCF-LINK4033` when the exact inherited exception field layout is unavailable. Regression coverage verifies the existing CIL lowering and exact defining HCO.

Clean preflight `TempEnv/d4101` passed full ManagedPortSmoke, Doom CoreCLR 26/26, all requested Release builds, immutable 217-file pack generation/verification, and closure 1937/37/202. Diagnostic `TempEnv/d4102` exits outer 1 / ILCompiler 1 / adapter 15 (wrapper 31) at `HCSCF-LINK4005` / `HCLINK1003: Undefined global symbol '__hybridcpu_managed_string_concat2'`; its aggregate contains no method, assembly or IL offset. Normal `TempEnv/d4103-normal` remains outer 1 / adapter 25 at `HCPUB1004: unsupported managed publish workstream(s): eh-unwind`. No Doom `.hcexe`, determinism hashes or HybridCPU guest execution evidence exists.

### 2026-09-01 two-string concatenation continuation

`__hybridcpu_managed_string_concat2` now uses ECALL operation 21 over the existing immutable UTF-16 runtime. Null operands retain CoreCLR empty-string semantics; lengths and allocation sizes are checked, payloads are copied exactly, invalid/non-string operands fail as provider errors, and size/allocation failure allocates exact `System.OutOfMemoryException` for native EH transfer. ABI schema minor is 44. Regressions cover embedded NUL/non-ASCII content, left/right null, malformed operand rejection, required safepoint/throw effects, and exact defining HCO. The linker gate `HCSCF-LINK4034` requires exact OOM metadata and native handled/unhandled EH.

Clean preflight `TempEnv/d4105` passed full ManagedPortSmoke, Doom CoreCLR 26/26, all requested Release builds, immutable 217-file pack verification, and closure 1937/37/202. Diagnostic `TempEnv/d4106` exits outer 1 / ILCompiler 1 / adapter 15 (wrapper 31) at `HCSCF-LINK4005` / `HCLINK1003: Undefined global symbol '__hybridcpu_managed_array_load_u1'`; no method, assembly or IL offset is present. Normal `TempEnv/d4107-normal` remains outer 1 / adapter 25 at `HCPUB1004: unsupported managed publish workstream(s): eh-unwind`. No Doom `.hcexe`, publish hashes or HybridCPU guest execution evidence exists.

### 2026-09-01 unsigned-byte array load continuation

`__hybridcpu_managed_array_load_u1` now uses ECALL operation 22 and preserves exact zero-extension for byte/bool one-byte primitive SZARRAYs. Signed-I4 negative and upper-bound indices allocate exact `IndexOutOfRangeException`; null allocates exact `NullReferenceException`; malformed or wrong-width arrays fail as provider errors without an element read. Its ABI row is corrected to read/write, required-safepoint and `MayThrow=true`, advancing schema minor to 45. Regressions cover CoreCLR raw byte/bool parity, zero extension, null, both bounds, width mismatch, ABI effects and exact HCO. Linker gate `HCSCF-LINK4035` requires both exact exception types and native handled/unhandled EH.

Clean preflight `TempEnv/d4109` passed full ManagedPortSmoke, Doom CoreCLR 26/26, all requested Release builds, immutable 217-file pack generation/verification, and closure 1937/37/202. Diagnostic `TempEnv/d4110` exits outer 1 / ILCompiler 1 / adapter 15 (wrapper 31) at `HCSCF-LINK4005` / `HCLINK1003: Undefined global symbol '__hybridcpu_managed_divide_i4_checked'`; its aggregate has no method, assembly or IL offset. Normal `TempEnv/d4111-normal` remains outer 1 / adapter 25 at `HCPUB1004: unsupported managed publish workstream(s): eh-unwind`. No Doom `.hcexe`, deterministic hashes or HybridCPU guest execution evidence exists.

### 2026-09-01 checked signed Int32 division continuation

`__hybridcpu_managed_divide_i4_checked` now uses ECALL operation 23 with canonical sign-extended I4 carriers. It returns exact signed quotient, maps denominator zero to an allocated exact `DivideByZeroException`, and maps `Int32.MinValue / -1` to an allocated exact `OverflowException`; both transfer through native EH. ABI schema minor is 46. Existing CoreCLR boundary/runtime tests are supplemented by exact ABI/HCO checks. Linker gate `HCSCF-LINK4036` requires both exception image types and native handled/unhandled EH.

Clean preflight `TempEnv/d4113` passed full ManagedPortSmoke, Doom CoreCLR 26/26, all requested Release builds, immutable 217-file pack generation/verification, and closure 1937/37/202. Diagnostic `TempEnv/d4114` exits outer 1 / ILCompiler 1 / adapter 15 (wrapper 31) at `HCSCF-LINK4005` / `HCLINK1003: Undefined global symbol '__hybridcpu_managed_divide_i8_checked'`; no method, assembly or IL offset is present. Normal `TempEnv/d4115-normal` remains outer 1 / adapter 25 at `HCPUB1004: unsupported managed publish workstream(s): eh-unwind`. No Doom `.hcexe`, hashes or HybridCPU guest execution evidence exists.

`__hybridcpu_managed_divide_i8_checked` now uses ECALL operation 24 with full raw 64-bit carriers. It returns the exact signed Int64 quotient, maps denominator zero to an allocated exact `DivideByZeroException`, and maps `Int64.MinValue / -1` to an allocated exact `OverflowException`; both transfer through native EH. ABI schema minor is 47. Existing CoreCLR boundary/runtime tests are supplemented by exact ABI/HCO checks. Linker gate `HCSCF-LINK4037` requires both exception image types and native handled/unhandled EH.

Clean preflight `TempEnv/d4117` passed full ManagedPortSmoke, the allocation-capacity regression, Doom CoreCLR 26/26, all requested Release builds, immutable 217-file pack generation/verification, and closure 1937/37/202. Diagnostic `TempEnv/d4118` exits outer 1 / ILCompiler 1 / adapter 15 (wrapper 31) at `HCSCF-LINK4005` / `HCLINK1003: Undefined global symbol '__hybridcpu_managed_math_abs_i8'`; no method, assembly or IL offset is present. During the backend run, `compile-image` PID 26688 was observed at 174.5 CPU seconds and 1622.4 MiB working set; it then terminated naturally with the semantic diagnostic. Normal `TempEnv/d4119-normal` remains outer 1 / adapter 25 at `HCPUB1004: unsupported managed publish workstream(s): eh-unwind`. No Doom `.hcexe`, hashes or HybridCPU guest execution evidence exists.

`__hybridcpu_managed_math_abs_i8` now uses ECALL operation 25 and exact full-width Int64 carriers. Non-minimum inputs return the exact magnitude; `Int64.MinValue` allocates exact `OverflowException` and transfers through native EH. Because that path allocates, its ABI row is corrected from no transition to `MaySafepoint`; ABI schema minor is 48. Regression coverage retains CoreCLR boundary parity and false-CoreLib rejection and adds exact safepoint/throw effects plus a defining HCO check. Linker gate `HCSCF-LINK4038` requires exact OverflowException metadata and native handled/unhandled EH.

Clean preflight `TempEnv/d4121` passed full ManagedPortSmoke, allocation regression, Doom CoreCLR 26/26, all requested Release builds, immutable 217-file pack generation/verification, and closure 1937/37/202. Diagnostic `TempEnv/d4122` exits outer 1 / ILCompiler 1 / adapter 15 (wrapper 31) at `HCSCF-LINK4005` / `HCLINK1003: Undefined global symbol '__hybridcpu_managed_array_load_i2'`; no method, assembly or IL offset is present. The active backend PID 2440 was observed at 194.7 seconds elapsed, 201.1 CPU seconds and 1529.8 MiB working set before natural termination. Normal `TempEnv/d4123-normal` remains outer 1 / adapter 25 at `HCPUB1004: unsupported managed publish workstream(s): eh-unwind`. No Doom `.hcexe`, hashes or HybridCPU guest execution evidence exists.

`__hybridcpu_managed_array_load_i2` now uses ECALL operation 26 and sign-extends the exact two-byte `System.Int16[]` element into the canonical I4 result carrier. Null and signed-I4 bounds failures allocate exact `NullReferenceException` or `IndexOutOfRangeException` and transfer through native EH; wrong-width arrays fail as provider errors. Its ABI row is corrected to read/write, required safepoint and `MayThrow=true`; ABI schema minor is 49. Existing CoreCLR/runtime coverage is supplemented by exact ABI/HCO checks. Linker gate `HCSCF-LINK4039` requires both exception types and native handled/unhandled EH.

Clean preflight `TempEnv/d4125` passed full ManagedPortSmoke, allocation regression, Doom CoreCLR 26/26, all requested Release builds, immutable 217-file pack generation/verification, and closure 1937/37/202. Diagnostic `TempEnv/d4126` exits outer 1 / ILCompiler 1 / adapter 15 (wrapper 31) at `HCSCF-LINK4005` / `HCLINK1003: Undefined global symbol '__hybridcpu_managed_array_load_u2'`; no method, assembly or IL offset is present. Backend PID 22016 was observed at 193.2 seconds elapsed, 201.3 CPU seconds and 1849.6 MiB working set before natural termination. Normal `TempEnv/d4127-normal` remains outer 1 / adapter 25 at `HCPUB1004: unsupported managed publish workstream(s): eh-unwind`. No Doom `.hcexe`, hashes or HybridCPU guest execution evidence exists.

`__hybridcpu_managed_array_load_u2` now uses ECALL operation 27 and zero-extends the exact two-byte `System.Char[]`/unsigned element into the canonical I4 carrier. Null and signed-I4 bounds failures allocate exact `NullReferenceException` or `IndexOutOfRangeException` for native EH transfer; wrong-width arrays fail as provider errors. Its ABI row is corrected to read/write, required safepoint and `MayThrow=true`; ABI schema minor is 50. Regressions retain CoreCLR raw UTF-16 parity and add exact ABI/HCO checks. Linker gate `HCSCF-LINK4040` requires both exception types and native handled/unhandled EH.

Clean preflight `TempEnv/d4129` passed full ManagedPortSmoke, allocation regression, Doom CoreCLR 26/26, all requested Release builds, immutable 217-file pack generation/verification, and closure 1937/37/202. Diagnostic `TempEnv/d4130` exits outer 1 / ILCompiler 1 / adapter 15 (wrapper 31) at `HCSCF-LINK4005` / `HCLINK1003: Undefined global symbol '__hybridcpu_managed_array_copy_all'`; no method, assembly or IL offset is present. Backend PID 7672 was observed at 194.4 seconds elapsed, 200.6 CPU seconds and 1587 MiB working set before natural termination. Normal `TempEnv/d4131-normal` remains outer 1 / adapter 25 at `HCPUB1004: unsupported managed publish workstream(s): eh-unwind`. No Doom `.hcexe`, hashes or HybridCPU guest execution evidence exists.

`__hybridcpu_managed_array_copy_all` now uses ECALL operation 28 and delegates to the exact prevalidated atomic copy engine with overlap/memmove behavior, primitive identity, value-layout identity and reference assignability scanning. Null, negative length, invalid extent and incompatible array cases allocate exact `ArgumentNullException`, `ArgumentOutOfRangeException`, `ArgumentException` or `ArrayTypeMismatchException` and transfer through native EH; malformed runtime-owned shapes remain provider failures. Its ABI is corrected to required safepoint with `MayThrow=true`; ABI schema minor is 51. Linker gate `HCSCF-LINK4041` requires all four exception types and native handled/unhandled EH.

Clean preflight `TempEnv/d4133` passed full ManagedPortSmoke, allocation regression, Doom CoreCLR 26/26, all requested Release builds, immutable 217-file pack generation/verification, and closure 1937/37/202. Diagnostic `TempEnv/d4134` exits outer 1 / ILCompiler 1 / adapter 15 (wrapper 31) at `HCSCF-LINK4005` / `HCLINK1003: Undefined global symbol '__hybridcpu_managed_isinst'`; no method, assembly or IL offset is present. Backend PID 16940 was observed at 198.5 seconds elapsed, 205.8 CPU seconds and 2083.1 MiB working set before natural termination. Normal `TempEnv/d4135-normal` remains outer 1 / adapter 25 at `HCPUB1004: unsupported managed publish workstream(s): eh-unwind`. No Doom `.hcexe`, hashes or HybridCPU guest execution evidence exists.

`__hybridcpu_managed_isinst` now uses ECALL operation 29. The importer-supplied exact image type handle is resolved by the runtime; null returns null, assignable active objects preserve identity, non-assignable objects return null, and unknown target handles, inactive receivers or invalid object headers fail closed as provider failures. The helper allocates nothing and raises no managed exception, so its ABI remains read-only with no GC transition and `MayThrow=false`; ABI schema minor is 52. Regression coverage proves exact lowering, defining HCO, inheritance/null/non-assignable behavior and malformed input rejection.

Clean preflight `TempEnv/d4137` passed full ManagedPortSmoke, allocation regression, Doom CoreCLR 26/26, all requested Release builds, immutable 217-file pack generation/verification, and closure 1937/37/202. Diagnostic `TempEnv/d4138` exits outer 1 / ILCompiler 1 / adapter 15 (wrapper 31) at linker-layer `HCSCF-LINK4005` / `HCLINK1003: Undefined global symbol '__hybridcpu_managed_remainder_i4_checked'`; no method, assembly or IL offset is present. Backend PID 12172 was observed at 256.3 seconds elapsed, 264 CPU seconds, 1742.2 MiB working set and 1696.9 MiB private memory before natural termination. Normal `TempEnv/d4139-normal` remains outer 1 / adapter 25 at `HCPUB1004: unsupported managed publish workstream(s): eh-unwind`. No Doom `.hcexe`, hashes or HybridCPU guest execution evidence exists.

`__hybridcpu_managed_remainder_i4_checked` now uses ECALL operation 30 and returns the exact signed Int32 remainder in the canonical sign-extended carrier. A zero divisor or `Int32.MinValue % -1` allocates exact `DivideByZeroException` or `OverflowException` and transfers through native EH. ABI schema minor is 53; linker gate `HCSCF-LINK4042` requires both exception types and native handled/unhandled EH. Existing CIL/CoreCLR/runtime boundary tests now also require the defining HCO and exact allocating/throwing ABI.

Clean preflight `TempEnv/d4141` passed full ManagedPortSmoke, allocation regression, Doom CoreCLR 26/26, all requested Release builds, immutable 217-file pack generation/verification, and closure 1937/37/202. Diagnostic `TempEnv/d4142` exits outer 1 / ILCompiler 1 / adapter 15 (wrapper 31) at linker-layer `HCSCF-LINK4005` / `HCLINK1003: Undefined global symbol '__hybridcpu_managed_string_concat3'`; no method, assembly or IL offset is present. Backend PID 24224 was observed at 286.4 seconds elapsed, 292.6 CPU seconds, 2009.2 MiB working set and 1968.4 MiB private memory before natural termination. Normal `TempEnv/d4143-normal` remains outer 1 / adapter 25 at `HCPUB1004: unsupported managed publish workstream(s): eh-unwind`. No Doom `.hcexe`, hashes or HybridCPU guest execution evidence exists.

`__hybridcpu_managed_string_concat3` now uses ECALL operation 31 and the shared exact immutable UTF-16 concatenation engine. Null operands are empty, valid operands preserve all UTF-16 code units, invalid object/type/length metadata fails closed, checked size/allocation failures allocate exact `OutOfMemoryException` for native EH, and partial results are never published. ABI schema minor is 54; linker gate `HCSCF-LINK4043` requires exact OOM metadata and native handled/unhandled EH. Regressions cover CIL lowering, embedded NUL/Unicode, null operands, invalid operands, ABI effects and defining HCO.

Clean preflight `TempEnv/d4145` passed full ManagedPortSmoke, allocation regression, Doom CoreCLR 26/26, all requested Release builds, immutable 217-file pack generation/verification, and closure 1937/37/202. Diagnostic `TempEnv/d4146` exits outer 1 / ILCompiler 1 / adapter 15 (wrapper 31) at linker-layer `HCSCF-LINK4005` / `HCLINK1003: Undefined global symbol '__hybridcpu_managed_array_store_i2'`; no method, assembly or IL offset is present. Backend PID 22956 was observed at 273.6 seconds elapsed, 280.3 CPU seconds, 1689 MiB working set and 1642 MiB private memory before natural termination. Normal `TempEnv/d4147-normal` remains outer 1 / adapter 25 at `HCPUB1004: unsupported managed publish workstream(s): eh-unwind`. No Doom `.hcexe`, hashes or HybridCPU guest execution evidence exists.

`__hybridcpu_managed_array_store_i2` now uses ECALL operation 32 and the exact two-byte primitive SZARRAY store engine. The I4 value is truncated to its low 16 bits for `System.Int16[]`/`System.Char[]`; null and signed-I4 bounds failures allocate exact `NullReferenceException` or `IndexOutOfRangeException` and transfer through native EH, while wrong-width shapes fail as provider errors. Its ABI is corrected to required safepoint with `MayThrow=true`; ABI schema minor is 55. Linker gate `HCSCF-LINK4044` requires both exception types and native handled/unhandled EH. Existing exhaustive CoreCLR/runtime truncation and atomic negative coverage now also requires defining HCO and exact ABI effects.

Clean preflight `TempEnv/d4149` passed full ManagedPortSmoke, allocation regression, Doom CoreCLR 26/26, all requested Release builds, immutable 217-file pack generation/verification, and closure 1937/37/202. Diagnostic `TempEnv/d4150` exits outer 1 / ILCompiler 1 / adapter 15 (wrapper 31) at linker-layer `HCSCF-LINK4005` / `HCLINK1003: Undefined global symbol '__hybridcpu_managed_string_from_utf16_array'`; no method, assembly or IL offset is present. Backend PID 11636 was observed at 277.9 seconds elapsed, 283.7 CPU seconds, 1817.5 MiB working set and 1771.7 MiB private memory before natural termination. Normal `TempEnv/d4151-normal` remains outer 1 / adapter 25 at `HCPUB1004: unsupported managed publish workstream(s): eh-unwind`. No Doom `.hcexe`, hashes or HybridCPU guest execution evidence exists.

`__hybridcpu_managed_string_from_utf16_array` now uses ECALL operation 33 and the exact CoreLib `String(char[])` variable-size factory. It resolves the importer-supplied immutable UTF-16 String type handle, validates an exact runtime-owned two-byte primitive SZARRAY, copies all raw UTF-16 code units, maps null source to exact `NullReferenceException`, maps checked size/heap failure to exact `OutOfMemoryException`, and leaves invalid handles/shapes as provider failures. Its ABI is corrected to `MayThrow=true`; ABI schema minor is 56. Linker gate `HCSCF-LINK4045` requires both exception types and native handled/unhandled EH. Existing lowering/runtime/CoreCLR tests now also require exact ABI and defining HCO.

Clean preflight `TempEnv/d4153` passed full ManagedPortSmoke, allocation regression, Doom CoreCLR 26/26, all requested Release builds, immutable 217-file pack generation/verification, and closure 1937/37/202. Diagnostic `TempEnv/d4154` exits outer 1 / ILCompiler 1 / adapter 15 (wrapper 31) at linker-layer `HCSCF-LINK4005` / `HCLINK1003: Undefined global symbol '__hybridcpu_managed_array_copy'`; no method, assembly or IL offset is present. Backend PID 3080 was observed at 213.3 seconds elapsed, 219.8 CPU seconds, 1622.8 MiB working set and 1577.6 MiB private memory while active before natural termination. Normal `TempEnv/d4155-normal` remains outer 1 / adapter 25 at `HCPUB1004: unsupported managed publish workstream(s): eh-unwind`. No Doom `.hcexe`, hashes or HybridCPU guest execution evidence exists.
### 2026-09-02 — five-argument `Array.Copy` diagnostic iteration

`__hybridcpu_managed_array_copy` is closed in managed ABI minor 57. The HCO preserves the five native ABI arguments in an exact 40-byte, 16-byte-aligned stack block and uses the existing external-service buffer address/length/read-access envelope; it does not expand or repurpose the three scalar argument registers. The production ISE bridge reads that bounded block from its bound `MainMemoryArea`, validates exact length/access and canonical signed Int32 fields, then invokes the existing overlap-aware, prevalidated atomic array-copy runtime. ECALL operation 34, linker gate `HCSCF-LINK4046`, exception allocation/native-EH transfer, and ABI/defining-HCO regressions are included.

Clean `TempEnv/d4157` passes ManagedPortSmoke including allocation capacity, Doom CoreCLR 26/26, Core/Windows/guest Release builds, compiler/runtime/kernel/ISE builds, immutable 217-file pack verification, and closure 1937 owned / 37 external / 202 types. Diagnostic `TempEnv/d4158` ends naturally with outer/ILCompiler 1, adapter 15, wrapper 31 at linker-layer `HCSCF-LINK4005` / `HCLINK1003: Undefined global symbol '__hybridcpu_managed_argument_null_ctor_param_name'`; no method, assembly, IL offset, or narrower contract is reported. Normal `TempEnv/d4159-normal` remains wrapper 25 / `HCPUB1004: unsupported managed publish workstream(s): eh-unwind`. No Doom `.hcexe` exists.
### 2026-09-02 — `ArgumentNullException(string)` constructor iteration

`__hybridcpu_managed_argument_null_ctor_param_name` is closed in managed ABI minor 58 with ECALL operation 35. The CPU-callable HCO uses the exact two-argument native ABI and required-safepoint/managed-throw contour. The production loader binds the existing runtime that validates the allocated receiver and null-or-exact-String parameter, materializes the invariant UTF-16 default message, writes `_message`, `_paramName` and HRESULT, maps allocation failure to an image-owned `OutOfMemoryException`, and contains malformed operands as provider failure. Linker gate `HCSCF-LINK4047` requires exact String, ArgumentNullException, OutOfMemoryException and native handled/unhandled EH transfer. `System.String` remains on its shape-aware metadata path and is deliberately excluded from ordinary dispatch-type encoding.

Clean `TempEnv/d4163` passes full ManagedPortSmoke, allocation regression, Doom CoreCLR 26/26, all Release/compiler/runtime/kernel/ISE builds, immutable 217-file pack verification and closure 1937/37/202. Diagnostic `TempEnv/d4164` ends naturally at linker `HCSCF-LINK4005` / `HCLINK1003: Undefined global symbol '__hybridcpu_managed_argument_out_of_range_ctor_param_name'` (outer/ILCompiler 1, adapter 15, wrapper 31; no method/assembly/IL offset). Normal `TempEnv/d4165-normal` remains wrapper 25 / `HCPUB1004: eh-unwind`. No Doom `.hcexe` exists.
### 2026-09-02 — `ArgumentOutOfRangeException(string)` constructor iteration

`__hybridcpu_managed_argument_out_of_range_ctor_param_name` is closed in managed ABI minor 59 with ECALL operation 36 and linker gate `HCSCF-LINK4048`. It reuses the exact ArgumentException-family runtime with the ArgumentOutOfRange type identity, HRESULT, invariant UTF-16 message, `_paramName` initialization, required safepoint, image-owned OOM managed transfer, and provider failure for malformed operands. Clean `TempEnv/d4167` passes full smoke, allocation regression, Doom CoreCLR 26/26, all builds, immutable 217-file pack verification, and closure 1937/37/202. Diagnostic `TempEnv/d4168` advances to linker `HCSCF-LINK4005/HCLINK1003: Undefined global symbol '__hybridcpu_managed_string_not_equals'` (outer/ILCompiler 1, adapter 15, wrapper 31; no method/assembly/IL offset). Normal `TempEnv/d4169-normal` remains wrapper 25 / `HCPUB1004: eh-unwind`. No Doom `.hcexe` exists.
### 2026-09-02 — string inequality iteration

`__hybridcpu_managed_string_not_equals` is closed in managed ABI minor 60 with ECALL operation 37 and linker gate `HCSCF-LINK4049`. The production binding invokes the existing exact ordinal raw-UTF16 `AreNotEqual` runtime, preserves null/reference semantics, returns canonical Boolean, and rejects non-string heap objects as provider failure; no allocation, managed exception, ABI or ISA approximation was introduced. Clean `TempEnv/d4170` passes full smoke, allocation regression, Doom CoreCLR 26/26, all builds, immutable 217-file pack verification and closure 1937/37/202. Diagnostic `TempEnv/d4171` advances to linker `HCSCF-LINK4005/HCLINK1003: Undefined global symbol '__hybridcpu_managed_array_clear'` (outer/ILCompiler 1, adapter 15, wrapper 31; no method/assembly/IL offset). Normal `TempEnv/d4172-normal` remains wrapper 25 / `HCPUB1004: eh-unwind`. No Doom `.hcexe` exists.
