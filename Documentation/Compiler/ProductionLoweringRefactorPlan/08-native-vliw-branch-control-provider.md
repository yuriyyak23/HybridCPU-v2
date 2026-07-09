# 08 — NativeVliwBranchControl production provider

## Goal

Introduce a production-lowering provider for `ExecutionContourKind.NativeVliwBranchControl` after scalar and load/store production packages are proven.

Branch/control lowering is authority-sensitive because control-flow selection, commit, retire, interrupt/event interaction, and architectural publication are runtime-owned. The compiler may only package branch/control carriers and structural relocation/target facts.

## What is produced

A branch/control production provider may produce:

- branch/control carrier words/bytes for an explicit opcode subset;
- branch target or relocation structural facts;
- typed-slot facts as structural evidence;
- no-fallback proof;
- evidence envelope;
- telemetry snapshot;
- runtime-authority-pending header.

It must not produce:

- completed control-flow transfer;
- commit or retire;
- architectural publication;
- event/interrupt authority;
- VMX execution authority;
- L7 system-device command authority;
- fallback into scalar or system singleton paths.

## Required gates

- Production profile explicitly enables `NativeVliwBranchControl`.
- Intent classifier covers branch/control subset.
- Branch target/label/relocation facts are structural and complete.
- ISE branch/control opcode identity is exact.
- Carrier golden artifacts exist.
- ISE decode/encode parity exists.
- Runtime commit/retire dependency is preserved.
- No-fallback proof forbids system singleton/L7/VMX fallback.

## Tests

### Positive

- conditional branch carrier snapshots;
- direct/indirect branch carrier snapshots where supported;
- label/target structural facts stay outside carrier execution authority;
- lane7 branch-control parity;
- runtime dependency includes Legality A/B, execution, commit, retire, publication.

### Negative

- system singleton opcodes rejected unless a separate contour exists;
- VMX opcodes rejected as projection/no-emission;
- L7 ACCEL_* opcodes rejected by branch/control provider;
- missing relocation/target facts reject;
- missing production gate rejects;
- helper/parser success cannot satisfy branch production gates.

## Caller migration

Run shadow compare against current branch carrier construction. Do not migrate raw branch/control callers until golden and ISE parity are green.

## Rollback

Disable `NativeVliwBranchControl` in the production profile. Existing compatibility carrier paths remain.

## Merge gate

- No commit/retire claim.
- No system singleton/L7/VMX fallback.
- Branch target facts remain structural evidence only.
