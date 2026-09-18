# Task 215: dead CSR-backed capability stub removed

Date: 2026-05-25
Status: closed

## Selected slice

- Deleted without replacement: `Legacy/VMX/Compatibility/Generated/CsrProjection/LegacyCsrBackedVmxCapabilityDescriptorSource.cs`.
- Production reachability found no caller of the CSR-backed source or its empty-descriptor behavior; only manifest/conformance/test references existed.
- A fail-closed stub with no ABI caller is evidence history, not required production vocabulary.

## Authority result

- Capability authority remains typed-grant-first in `Core/Runtime/Capabilities/Descriptors/CapabilityDescriptorSet.cs` and `Core/Runtime/Capabilities/Grants/CapabilityGrant.cs`.
- The deletion does not permit CSR bits, VMX aliases, or generated compatibility masks to become capability grants.
- No host evidence, memory/I/O, lane, completion, checkpoint, nested, or retire authority was introduced.

## Conformance and inventory

- Added `LegacyCsrBackedVmxCapabilityDescriptorSourceRemovalContract`; its test proves absent source, `RemovedWithoutReplacement` status, and retained neutral typed-grant owners.
- After tasks `214`-`217`, only three production compatibility sources remain under `Legacy/VMX`; total evidence-quarantine count stays `38`.

## Verification

- Production and tests builds passed with `0` errors (`54` and `93` existing warnings).
- `VmxProjectionSchemaAndQuarantineTests`: `56/56`; new removal filters: `4/4`.
- Static scan keeps `Core/VMX` free of legacy-marked `.cs`; removed heavy carriers remain absent.

## Next step

- Final compiled-surface inventory must prove the remaining decode/retire/shadow projection sources are necessary and non-authoritative before freeze.
