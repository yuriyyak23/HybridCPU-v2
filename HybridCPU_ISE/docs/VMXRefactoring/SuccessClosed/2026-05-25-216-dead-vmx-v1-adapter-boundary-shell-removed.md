# Task 216: dead VMX v1 adapter boundary shell removed

Date: 2026-05-25
Status: closed

## Selected slice

- Deleted without replacement: `Legacy/VMX/Compatibility/Adapters/LegacyVmxV1/LegacyVmxV1AdapterBoundary.cs`.
- Its validation DTOs and `CanAdapt` helper had no production caller; their only behavioral consumer was the old quarantine test.

## Authority result

- Frozen opcode handling remains in the current production dispatcher/retire route and publishes typed `SecurityPolicyViolation` fail-closed effects.
- Deleting the unused policy shell does not restore an execution unit, manager, host-evidence path, completion owner, or successful VMX backend.

## Conformance and verification

- Added `LegacyVmxV1AdapterBoundaryRemovalContract`; the focused test proves physical absence, manifest removal status, and the existing dispatcher/retire fail-closed markers.
- Shared removal-wave checks pass: production/test builds, `VmxProjectionSchemaAndQuarantineTests` `56/56`, new removals `4/4`, previous authority/heavy/I/O filters `1/1`, `19/19`, and `1/1`.
- Production compatibility quarantine has `3` sources after tasks `214`-`217`; total evidence quarantine remains `38`.

## Next step

- Retain no adapter name by assumption: complete final review of the actual caller-backed decode/retire/shadow projection set before freeze.
