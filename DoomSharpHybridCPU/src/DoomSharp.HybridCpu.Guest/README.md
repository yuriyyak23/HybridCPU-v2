# DoomSharp HybridCPU guest

2026-09-01 current checkpoint: four fresh full cycles under `TempEnv` closed the
loop-distance budget overwrite, symbolic target index/byte-address ambiguity,
pre-scheduling x10 ABI alias omission, and linker-owned guest-service direct-callee
gate. All requested tests/builds and the 1937/37 closure pass with a verified
217-file pack. Normal publish remains fail-closed at HCPUB1004 `eh-unwind`.
Diagnostic publication now reaches the native control-flow relocation layer and
stops because a real branch requires more than signed 16-bit byte displacement;
`Trigger.TeleportEvent` independently reproduces CIL `IL_002a` -> `IL_0196`
(final IR 71 -> 391). Long-branch relaxation is the one current blocker. No Doom
`.hcexe` or HybridCPU guest execution is claimed.

2026-09-01 superseding checkpoint: clean evidence is in
`TempEnv/doom-rootmap-loop-20260901`. Full compiler smoke, Doom CoreCLR 26/26, all requested
Release builds, verified immutable pack and unchanged 1937/37 closure pass. Normal publish
remains fail-closed at HCPUB1004 `eh-unwind`. Diagnostic publication now passes the scheduler,
large allocator/proof graph, 5617-safepoint metadata, ABI-ingress and ordinary-loop root-map
blockers, then stops in `GameController..ctor` (IR 579/value 496) because
`il_0018:null-check-arg-abi:value` cannot satisfy the native call/return location contract.
No Doom `.hcexe` or ISE execution is claimed; see `AotPreflight.md` for the exact identity.

2026-08-31 scheduler iteration supersedes the historical checkpoints below.
Exact reverse-topological descendant counting replaces per-instruction recursive DFS;
targeted regressions, full ManagedPortSmoke, Doom CoreCLR 26/26 and all requested
Release builds pass. Evidence, scripts and the new 217-file immutable pack are under
`TempEnv/doom-scheduler-20260831`. The project default still points to recovered
pack193; this iteration explicitly selects `pack193-scheduler` without mutating it.
Normal publish freshly confirms HCPUB1004 (`eh-unwind`, adapter 25 / publish 1).
New-pack diagnostic completed: HCSCF-LINK4003, register allocation BudgetExhausted
(backend 15 / publish-graph 31 / publish 1). A read-only exact body-world probe
attributes the first allocation-input failure to MapObjectInfo.AddPredefinedTypes
in DoomSharp.Core: 15038 IR instructions / 9557 values exceed 4096 / 8192.
This is a method-level budget gate, not an instruction IL fault; its cctor calls it
at IL_000f. Allocation limits remain unchanged; the scheduler DFS blocker is passed.
No Doom .hcexe or ISE execution is claimed; see AotPreflight.md for exact evidence.

Build-only recovery, 2026-08-31: the default pack is now
`TempEnv/doom-20260831-rebuild/pack193` (1.15.193), replacing the partially deleted
`Diagnostics/HybridCpuAotAddDemo/.hcpu-p193`. Compiler, runtime/kernel, Core, Windows
and guest Release builds passed. All 217 pack-file hashes were verified. The pinned
ILCompiler and reference assemblies were restored from the verified saved pack, not
rebuilt. No new diagnostic preflight, tests or publish were run after the user's
build-only instruction; no new `.hcexe` or capability qualification is claimed.
Outputs, staged WPF sources, logs and the build script are under
`TempEnv/doom-20260831-rebuild`. The following p188 narrative is historical.

This project is the managed guest boundary. It references `DoomSharp.Core` and the installed
RefPlan7 SDK pack; neither compiler nor ISE assemblies are referenced by the game core.

The image loader must register an `IHybridCpuGuestServices` instance before invoking
`EntryPoint.Run`. That service owns the process-lifetime immutable WAD boot blob, synchronous
tic-boundary input, console, palette/frame presentation, virtual monotonic clock and
`process_exit`. The WAD is intentionally not embedded or copied into this project.

The pinned SDK is selected by the repository `global.json`:

```powershell
dotnet publish .\DoomSharp.HybridCpu.Guest.csproj -c Release -r hybridcpu `
  -p:PublishDir=.\publish\
```

The historical p188 pack was `1.15.188-refplan7-phase15`, installed at `.hcpu-p188`.
Its diagnostic carries exact EH homes into final GC maps, image-owned EH/unwind-v2 rows,
a link-resolved metadata index, startup x3 binding and a native non-returning
handler-transfer HCO, native bounded frame lookup, memory-safe unwind step, closed-world
type matching and fail-closed exact-catch selection. Catch-only EH is now production-linked;
any finally/endfinally closure remains gated.
All returning EH leaf HCOs use the exact bundled-call `pc+4+252` return contract.
The linked ancestry selector already resolves base-type catches while preserving the native ABI frame.
The linked catch walker completes stack-owned HCET transfer, rooted rethrow and non-trap process exit.
Package schema 1.3 keeps non-empty bootstrap metadata beyond the x3 startup fields.
The kernel exposes a checked user-mode ECALL envelope and p180 emits the exact native Doom-clock
thunk with a boot-required timebase. The restricted loader/ISE callback is still unqualified. The
clock/read/wait, framebuffer init/present, console write/title, trusted boot-blob and non-trap
process-exit compiler gates are closed. The current diagnostic stops at `HCSCF-LINK4011` in
`HybridCpuGraphics.StartTic`, IL_0006, for nullable managed `IHybridCpuGuestServices.PullInput()`.
The loader-managed input registry and actual ISE ECALL execution remain unqualified. Earlier gate advances do not
constitute end-to-end runtime or publication qualification.
No `.hcexe` is available. See `AotPreflight.md`.

Historical p115 checkpoint: pack `1.15.115-refplan7-phase15`, installed at the shorter
`Diagnostics/HybridCpuAotAddDemo/.hcpu-p115` root to avoid the Windows command-line limit.
Its qualified graph workstreams
reach concrete CIL/CoreLib closure; the required `eh-unwind` publication authority remains
withheld, so normal publication fails closed. The diagnostic body-world probe has passed all
recursive SCCs plus metadata-owned class allocation and now stops at exact scalar static-field
projection for static `Angle` fields, exact scalar value arguments and checked UInt32 division. It
now passes exact `State[]`, primitive `int[]`, outer `int[][]`, projected `Angle / uint` and
inherited exception layout, scalar `Fixed` fields, reachable literal bindings and exact
CoreLib-resolved `System.String[]`, then stops on a missing exact cast target binding in
`Trigger.VerticalDoorEvent` (`HCCIL1512`, IL_00e4), after fixing
its CFG termination, binding outer
`Patch[][]` allocation, correcting
the CIL stack carrier of string-indexer helper results and replacing the private
Boolean `_firstTime` with equivalent Int32 0/1 state and passing
one-byte `Player.UseDown` storage and enum array `DirectionType[]`. Literal image registration is
still explicitly gated (`HCSCF-LINK4013`), not qualified by this CIL progress. See
`AotPreflight.md` for details.
# AOT status (2026-09-01)

The diagnostic compiler now passes the closed infinite `DoomLoop` frame case and exact
guest-service HCO symbol construction. The current fail-closed blocker is image dispatch
metadata: `ManagedDispatchTypeObjectV1.Emit` reports `Dispatch type 7 lacks its exact
descriptor` (adapter exit `-532462766`, publish exit 1). Normal publish still stops at
`HCPUB1004: unsupported managed publish workstream(s): eh-unwind`. No Doom `.hcexe` has
been produced and no host/CoreCLR run is claimed as HybridCPU guest execution.
# AOT status: dispatch ownership checkpoint

Provider-local `IHybridCpuGuestServices` dispatch IDs are now kept out of image type and
vtable metadata, with mixed ownership rejected. The next diagnostic blocker is static
link `HCSCF-LINK4005 / HCLINK1003`: undefined
`__hybridcpu_managed_argument_exception_get_message`. An exact native implementation is
required; using the base exception getter would change `ArgumentException` semantics.
Normal publication remains gated by `HCPUB1004` for `eh-unwind`. No Doom `.hcexe` exists.
# AOT status: exception getter checkpoint

The exact native `System.Exception::_message` getter is now a linkable descriptor-bound
HCO object. Diagnostic compilation stops explicitly at `HCSCF-LINK4015`: the
`System.ArgumentException.get_Message` override still needs native string allocation,
concat and ParamName formatting. It is not replaced by the base getter. Normal publish
remains gated by `HCPUB1004: eh-unwind`; no Doom `.hcexe` exists.

The current fail-closed diagnostic is `HCSCF-LINK4016`. The allocating
`System.ArgumentException.get_Message` path cannot be linked until a production ISE/image
loader consumes the runtime-bootstrap descriptor and installs the managed heap, type
system, GC safepoints and allocating helper bindings. Host-side ManagedRuntime tests are
reference evidence only and are not HybridCPU guest execution.

The managed allocator now writes through an explicit guest-memory backing. ISE supplies a
bounded adapter for an explicitly provided `Processor.MainMemoryArea`; it never falls back
to the global memory instance. This closes host-private heap storage, but bootstrap
orchestration and allocating helper dispatch remain required before `HCSCF-LINK4016` can
be removed.

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
## Current AOT checkpoint (2026-09-01)

The production compiler/ISE path now contains the exact allocating
`ArgumentException.Message` runtime-helper thunk and managed-runtime ECALL
retirement bridge. A clean `TempEnv/d4020` preflight passed all smoke/tests and
Release builds and produced a verified 217-file pack. Diagnostic publication
advances past HCSCF-LINK4018 and the subsequently reached duplicate-module
HCLINK0002 blocker. It now stops at
`HCSCF-LINK4005 / HCLINK1003: Undefined global symbol
'__hybridcpu_managed_ensure_type_initialized'`. The aggregate diagnostic has no
method/assembly/IL-offset attribution. Normal publication remains fail-closed at
HCPUB1004 `eh-unwind`; no Doom `.hcexe` or guest execution exists.
## Current AOT checkpoint: static runtime bridge

Clean cycles `d4021` through `d4023` closed the exact native helper gaps for
type-initialization validation and 32-bit static field store/load. Every cycle
passed the full preflight and produced a verified 217-file pack. Diagnostic
publish now stops at `HCSCF-LINK4005 / HCLINK1003: Undefined global symbol
'__hybridcpu_managed_null_check'`. Exact handled NullReferenceException
semantics require a production trap-mapping/allocation/EH-transfer consumer;
process exit is not accepted as a substitute. Normal publish remains HCPUB1004
`eh-unwind`, and no Doom `.hcexe` has been emitted.

## Current AOT checkpoint: exact null-check gate

The clean `d4028` preflight passes all required tests/builds and verifies a new
217-file pack. Diagnostic publish `d4029` now fails explicitly at compiler/static-link
`HCSCF-LINK4019` for `DoomSharp.Core.GameLogic.Player.set_DidSecret`, assembly
`DoomSharp.Core,1.0.0.0,neutral,none`, `IL_0002`. Production ISE retirement still lacks the
complete precise-fault to RuntimeKernel authorization, `NullReferenceException` allocation,
GC-root, handled/unhandled EH-transfer, and managed-exit path. Normal publish `d4030` remains
`HCPUB1004: eh-unwind`. No Doom `.hcexe` or guest execution is claimed.

## Current AOT checkpoint: explicit null and i4 array store

The production path now implements explicit allocating `NullReferenceException` semantics and
exact guest-backed `stelem.i4`, including null/bounds exception allocation and native throw
transfer. Full clean preflights `d4035` and `d4039` pass. Diagnostic `d4040` reaches the next
linker blocker, `HCLINK1003: Undefined global symbol '__hybridcpu_managed_array_store_ref'`
(adapter 15, ILCompiler 1, publish 1). Normal publish remains `HCPUB1004: eh-unwind`; no Doom
`.hcexe` exists.

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

## Current AOT diagnostic boundary (2026-09-01)

The exact `array_load_ref`, checked UInt32 division, and `array_load_i4` runtime-helper blockers are closed with allocating/throwing ECALL implementations, exact exception image types, safepoints and native EH transfer. Managed ABI schema minor is 38. Full clean preflight `TempEnv/d4072` passes ManagedPortSmoke, Doom CoreCLR 26/26, all requested Release builds, immutable 217-file pack verification, and closure 1937/37/202.

Diagnostic publish `TempEnv/d4073` now stops at `HCSCF-LINK4005` / `HCLINK1003: Undefined global symbol '__hybridcpu_managed_array_empty'` (outer 1, ILCompiler 1, adapter 15; MSBuild wrapper 31). No method, assembly or IL offset is present in the aggregate diagnostic. Ordinary publish `TempEnv/d4074-normal` still exits through adapter 25 at `HCPUB1004: unsupported managed publish workstream(s): eh-unwind`. There is no Doom `.hcexe`, deterministic publish hash, or HybridCPU guest execution evidence.

`Array.Empty<T>` is subsequently closed with its cached, GC-rooted, allocating/throwing semantics and exact OOM transfer; ABI minor is 39. Full clean preflight `TempEnv/d4076` passes, and diagnostic `TempEnv/d4077` now stops at `HCLINK1003: Undefined global symbol '__hybridcpu_managed_array_length'` (outer/ILCompiler 1, adapter 15, wrapper 31; no method/assembly/IL offset). Normal `TempEnv/d4078-normal` remains fail-closed at `HCPUB1004: eh-unwind`. No `.hcexe` exists.

Array length is now closed with layout-aware length reads and exact null-to-NRE/native-EH transfer; ABI minor is 40. Full preflight `TempEnv/d4080` passes. Diagnostic `TempEnv/d4081` next stops at `HCLINK1003: Undefined global symbol '__hybridcpu_managed_array_store_i1'` (outer/ILCompiler 1, adapter 15, wrapper 31; no method/assembly/IL offset). Normal `TempEnv/d4082-normal` remains `HCPUB1004: eh-unwind`; no `.hcexe` exists.

Byte-array store is now closed with exact low-byte truncation, signed index, atomic write, NRE/bounds allocation and native EH; ABI minor is 41. Full preflight `TempEnv/d4084` passes. Diagnostic `TempEnv/d4085` advances to `HCLINK1003: Undefined global symbol '__hybridcpu_managed_string_char'` (outer/ILCompiler 1, adapter 15, wrapper 31; no method/assembly/IL offset). Normal `TempEnv/d4086-normal` remains `HCPUB1004: eh-unwind`; no `.hcexe` exists.

String character access is now closed in ABI minor 42 with exact raw UTF-16 results, signed-index validation, exact NRE/bounds allocation, required safepoint/native EH, and exact HCO definition. The failed `TempEnv/d4088` run exposed and led to correction of a runtime `-1` length-sentinel collision; it is not acceptance evidence. Full clean preflight `TempEnv/d4089` passes ManagedPortSmoke, Doom CoreCLR 26/26, all requested Release builds, immutable 217-file pack verification, and closure 1937/37/202. Diagnostic `TempEnv/d4090` advances to `HCSCF-LINK4005` / `HCLINK1003: Undefined global symbol '__hybridcpu_managed_string_length'` (outer/ILCompiler 1, adapter 15, wrapper 31; no method/assembly/IL offset). Normal `TempEnv/d4094-normal` with pack-qualified SDK `10.0.204` remains `HCPUB1004: eh-unwind`; the unpinned installed SDK is independently rejected by `HCPUB1007` in `TempEnv/d4092-normal`. No `.hcexe`, hashes or guest execution evidence exists.

String length is closed in ABI minor 43 with exact UTF-16 code-unit count, null status, NRE allocation, required safepoint/native EH, and exact HCO definition. The partial `TempEnv/d4096` orchestration is not evidence; clean `TempEnv/d4097` reruns the full preflight under `pwsh` and passes ManagedPortSmoke, CoreCLR 26/26, all builds, immutable 217-file pack verification with zero mismatches, and closure 1937/37/202. Diagnostic `TempEnv/d4098` advances to `HCSCF-LINK4005` / `HCLINK1003: Undefined global symbol '__hybridcpu_managed_exception_ctor_message'` (outer/ILCompiler 1, adapter 15, wrapper 31; no method/assembly/IL offset). Normal `TempEnv/d4099-normal` remains adapter 25 / `HCPUB1004: eh-unwind`. No Doom `.hcexe`, hashes or guest execution evidence exists.

The exact `Exception(string)` helper is now a layout-qualified native `_message` reference store with linker rejection when the slot contract is absent; it does not extend ECALL, ISA or ABI. Clean `TempEnv/d4101` passes the full preflight, immutable 217-file pack verification and closure 1937/37/202. Diagnostic `TempEnv/d4102` advances to `HCSCF-LINK4005` / `HCLINK1003: Undefined global symbol '__hybridcpu_managed_string_concat2'` (outer/ILCompiler 1, adapter 15, wrapper 31; no method/assembly/IL offset). Normal `TempEnv/d4103-normal` remains adapter 25 / `HCPUB1004: eh-unwind`. No Doom `.hcexe`, hashes or guest execution exists.

Two-string concatenation is closed in ABI minor 44 with exact UTF-16/null semantics, checked allocation, exact OOM/native-EH transfer and malformed-operand rejection. Clean `TempEnv/d4105` passes full smoke, CoreCLR 26/26, all builds, immutable 217-file pack verification and closure 1937/37/202. Diagnostic `TempEnv/d4106` advances to `HCSCF-LINK4005` / `HCLINK1003: Undefined global symbol '__hybridcpu_managed_array_load_u1'` (outer/ILCompiler 1, adapter 15, wrapper 31; no method/assembly/IL offset). Normal `TempEnv/d4107-normal` remains adapter 25 / `HCPUB1004: eh-unwind`. No Doom `.hcexe`, hashes or guest execution exists.

Unsigned byte-array load is closed in ABI minor 45 with exact zero-extension, byte/bool parity, signed-index null/bounds transfer, wrong-width rejection and exact HCO definition. Clean `TempEnv/d4109` passes full smoke, CoreCLR 26/26, all builds, immutable 217-file pack verification and closure 1937/37/202. Diagnostic `TempEnv/d4110` advances to `HCSCF-LINK4005` / `HCLINK1003: Undefined global symbol '__hybridcpu_managed_divide_i4_checked'` (outer/ILCompiler 1, adapter 15, wrapper 31; no method/assembly/IL offset). Normal `TempEnv/d4111-normal` remains adapter 25 / `HCPUB1004: eh-unwind`. No Doom `.hcexe`, hashes or guest execution exists.

Checked signed Int32 division is closed in ABI minor 46 with canonical signed carriers, exact quotient, divide-by-zero and MinValue/-1 overflow allocation/native-EH transfer, plus ABI/HCO checks. Clean `TempEnv/d4113` passes full smoke, CoreCLR 26/26, all builds, immutable 217-file pack verification and closure 1937/37/202. Diagnostic `TempEnv/d4114` advances to `HCSCF-LINK4005` / `HCLINK1003: Undefined global symbol '__hybridcpu_managed_divide_i8_checked'` (outer/ILCompiler 1, adapter 15, wrapper 31; no method/assembly/IL offset). Normal `TempEnv/d4115-normal` remains adapter 25 / `HCPUB1004: eh-unwind`. No Doom `.hcexe`, hashes or guest execution exists.

Checked signed Int64 division is closed in ABI minor 47 with full 64-bit carriers, exact quotient, divide-by-zero and MinValue/-1 overflow allocation/native-EH transfer, plus ABI/HCO checks. Clean `TempEnv/d4117` passes full smoke, allocation regression, CoreCLR 26/26, all builds, immutable 217-file pack verification and closure 1937/37/202. Diagnostic `TempEnv/d4118` advances to `HCSCF-LINK4005` / `HCLINK1003: Undefined global symbol '__hybridcpu_managed_math_abs_i8'` (outer/ILCompiler 1, adapter 15, wrapper 31; no method/assembly/IL offset). Normal `TempEnv/d4119-normal` remains adapter 25 / `HCPUB1004: eh-unwind`. No Doom `.hcexe`, hashes or guest execution exists.

Checked `Math.Abs(Int64)` is closed in ABI minor 48 with full-width magnitude, exact MinValue overflow allocation/native-EH transfer, corrected `MaySafepoint` effects and ABI/HCO checks. Clean `TempEnv/d4121` passes full smoke, allocation regression, CoreCLR 26/26, all builds, immutable 217-file pack verification and closure 1937/37/202. Diagnostic `TempEnv/d4122` advances to `HCSCF-LINK4005` / `HCLINK1003: Undefined global symbol '__hybridcpu_managed_array_load_i2'` (outer/ILCompiler 1, adapter 15, wrapper 31; no method/assembly/IL offset). Normal `TempEnv/d4123-normal` remains adapter 25 / `HCPUB1004: eh-unwind`. No Doom `.hcexe`, hashes or guest execution exists.

Signed Int16 array load is closed in ABI minor 49 with exact sign extension, signed-index null/bounds exception allocation/native-EH transfer, wrong-width rejection and ABI/HCO checks. Clean `TempEnv/d4125` passes full smoke, allocation regression, CoreCLR 26/26, all builds, immutable 217-file pack verification and closure 1937/37/202. Diagnostic `TempEnv/d4126` advances to `HCSCF-LINK4005` / `HCLINK1003: Undefined global symbol '__hybridcpu_managed_array_load_u2'` (outer/ILCompiler 1, adapter 15, wrapper 31; no method/assembly/IL offset). Normal `TempEnv/d4127-normal` remains adapter 25 / `HCPUB1004: eh-unwind`. No Doom `.hcexe`, hashes or guest execution exists.

Unsigned Int16/Char array load is closed in ABI minor 50 with exact zero-extension, signed-index null/bounds exception allocation/native-EH transfer, wrong-width rejection and ABI/HCO checks. Clean `TempEnv/d4129` passes full smoke, allocation regression, CoreCLR 26/26, all builds, immutable 217-file pack verification and closure 1937/37/202. Diagnostic `TempEnv/d4130` advances to `HCSCF-LINK4005` / `HCLINK1003: Undefined global symbol '__hybridcpu_managed_array_copy_all'` (outer/ILCompiler 1, adapter 15, wrapper 31; no method/assembly/IL offset). Normal `TempEnv/d4131-normal` remains adapter 25 / `HCPUB1004: eh-unwind`. No Doom `.hcexe`, hashes or guest execution exists.

Three-argument `Array.Copy` is closed in ABI minor 51 with atomic overlap-aware copying, primitive/value identity, reference assignability validation, exact managed exception allocation/native-EH transfer and ABI/HCO checks. Clean `TempEnv/d4133` passes full smoke, allocation regression, CoreCLR 26/26, all builds, immutable 217-file pack verification and closure 1937/37/202. Diagnostic `TempEnv/d4134` advances to `HCSCF-LINK4005` / `HCLINK1003: Undefined global symbol '__hybridcpu_managed_isinst'` (outer/ILCompiler 1, adapter 15, wrapper 31; no method/assembly/IL offset). Normal `TempEnv/d4135-normal` remains adapter 25 / `HCPUB1004: eh-unwind`. No Doom `.hcexe`, hashes or guest execution exists.

Managed `isinst` is closed in ABI minor 52 with exact image type-handle resolution, identity-preserving assignable results, null/non-assignable null results, fail-closed invalid metadata/object validation, ECALL operation 29 and defining-HCO/ABI regressions. Clean `TempEnv/d4137` passes full smoke, allocation regression, CoreCLR 26/26, all builds, immutable 217-file pack verification and closure 1937/37/202. Diagnostic `TempEnv/d4138` advances to linker-layer `HCSCF-LINK4005` / `HCLINK1003: Undefined global symbol '__hybridcpu_managed_remainder_i4_checked'` (outer/ILCompiler 1, adapter 15, wrapper 31; no method/assembly/IL offset). Normal `TempEnv/d4139-normal` remains adapter 25 / `HCPUB1004: eh-unwind`. No Doom `.hcexe`, hashes or guest execution exists.

Checked signed Int32 remainder is closed in ABI minor 53 with exact sign-extended results, zero and MinValue/-1 exception allocation/native-EH transfer, ECALL operation 30, linker gate `HCSCF-LINK4042` and defining-HCO/ABI regression. Clean `TempEnv/d4141` passes full smoke, allocation regression, CoreCLR 26/26, all builds, immutable 217-file pack verification and closure 1937/37/202. Diagnostic `TempEnv/d4142` advances to linker-layer `HCSCF-LINK4005` / `HCLINK1003: Undefined global symbol '__hybridcpu_managed_string_concat3'` (outer/ILCompiler 1, adapter 15, wrapper 31; no method/assembly/IL offset). Normal `TempEnv/d4143-normal` remains adapter 25 / `HCPUB1004: eh-unwind`. No Doom `.hcexe`, hashes or guest execution exists.

Three-string `String.Concat` is closed in ABI minor 54 with shared exact UTF-16/null-as-empty semantics, fail-closed malformed operands, checked OOM allocation/native-EH transfer, ECALL operation 31, linker gate `HCSCF-LINK4043` and defining-HCO/ABI regressions. Clean `TempEnv/d4145` passes full smoke, allocation regression, CoreCLR 26/26, all builds, immutable 217-file pack verification and closure 1937/37/202. Diagnostic `TempEnv/d4146` advances to linker-layer `HCSCF-LINK4005` / `HCLINK1003: Undefined global symbol '__hybridcpu_managed_array_store_i2'` (outer/ILCompiler 1, adapter 15, wrapper 31; no method/assembly/IL offset). Normal `TempEnv/d4147-normal` remains adapter 25 / `HCPUB1004: eh-unwind`. No Doom `.hcexe`, hashes or guest execution exists.

Checked 16-bit array store is closed in ABI minor 55 with exact low-16-bit truncation for Int16/Char arrays, null/signed-bounds exception allocation/native-EH transfer, wrong-width rejection, ECALL operation 32, linker gate `HCSCF-LINK4044` and defining-HCO/ABI regressions. Clean `TempEnv/d4149` passes full smoke, allocation regression, CoreCLR 26/26, all builds, immutable 217-file pack verification and closure 1937/37/202. Diagnostic `TempEnv/d4150` advances to linker-layer `HCSCF-LINK4005` / `HCLINK1003: Undefined global symbol '__hybridcpu_managed_string_from_utf16_array'` (outer/ILCompiler 1, adapter 15, wrapper 31; no method/assembly/IL offset). Normal `TempEnv/d4151-normal` remains adapter 25 / `HCPUB1004: eh-unwind`. No Doom `.hcexe`, hashes or guest execution exists.

Exact CoreLib `String(char[])` is closed in ABI minor 56 with raw UTF-16 copying, exact String/SZARRAY validation, null and OOM exception allocation/native-EH transfer, invalid-shape provider failure, ECALL operation 33, linker gate `HCSCF-LINK4045` and defining-HCO/ABI regressions. Clean `TempEnv/d4153` passes full smoke, allocation regression, CoreCLR 26/26, all builds, immutable 217-file pack verification and closure 1937/37/202. Diagnostic `TempEnv/d4154` advances to linker-layer `HCSCF-LINK4005` / `HCLINK1003: Undefined global symbol '__hybridcpu_managed_array_copy'` (outer/ILCompiler 1, adapter 15, wrapper 31; no method/assembly/IL offset). Normal `TempEnv/d4155-normal` remains adapter 25 / `HCPUB1004: eh-unwind`. No Doom `.hcexe`, hashes or guest execution exists.
The latest clean AOT preflight is `TempEnv/d4157`: managed ABI minor 57 closes the exact five-argument `Array.Copy` helper through a bounded 40-byte guest-memory argument block and the production ISE memory surface. All smoke/CoreCLR/build/pack/closure checks pass (217 pack files; closure 1937/37/202). Diagnostic `d4158` advances to linker `HCSCF-LINK4005` / `HCLINK1003` for undefined `__hybridcpu_managed_argument_null_ctor_param_name` (outer/ILCompiler 1, adapter 15, wrapper 31; no method/assembly/IL offset). Normal `d4159-normal` remains `HCPUB1004: eh-unwind`; there is no Doom `.hcexe`.
The latest clean preflight is `TempEnv/d4163`: ABI minor 58 closes exact `ArgumentNullException(string)` construction with ECALL 35, invariant UTF-16 field initialization, OOM managed transfer and linker gate `HCSCF-LINK4047`. Full smoke/CoreCLR/build/immutable-pack/closure checks pass (217 files; 1937/37/202). Diagnostic `d4164` advances to undefined `__hybridcpu_managed_argument_out_of_range_ctor_param_name` at linker `HCSCF-LINK4005/HCLINK1003` (outer/ILCompiler 1, adapter 15, wrapper 31; no method/assembly/IL offset). Normal `d4165-normal` remains `HCPUB1004: eh-unwind`; no Doom `.hcexe` exists.
The latest clean preflight is `TempEnv/d4167`: ABI minor 59 closes exact `ArgumentOutOfRangeException(string)` construction with ECALL 36 and linker gate `HCSCF-LINK4048`. Full smoke/CoreCLR/build/immutable-pack/closure checks pass (217 files; 1937/37/202). Diagnostic `d4168` advances to undefined `__hybridcpu_managed_string_not_equals` at linker `HCSCF-LINK4005/HCLINK1003` (outer/ILCompiler 1, adapter 15, wrapper 31; no method/assembly/IL offset). Normal `d4169-normal` remains `HCPUB1004: eh-unwind`; no Doom `.hcexe` exists.
The latest clean preflight is `TempEnv/d4170`: ABI minor 60 closes exact ordinal raw-UTF16 string inequality via ECALL 37 and linker gate `HCSCF-LINK4049`. Full smoke/CoreCLR/build/immutable-pack/closure checks pass (217 files; 1937/37/202). Diagnostic `d4171` advances to undefined `__hybridcpu_managed_array_clear` at linker `HCSCF-LINK4005/HCLINK1003` (outer/ILCompiler 1, adapter 15, wrapper 31; no method/assembly/IL offset). Normal `d4172-normal` remains `HCPUB1004: eh-unwind`; no Doom `.hcexe` exists.
