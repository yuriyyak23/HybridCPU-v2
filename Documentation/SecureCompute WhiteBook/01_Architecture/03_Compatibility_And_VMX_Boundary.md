# Compatibility And VMX Boundary

## Zero-Authority Rule

VMX, VMCS, VMREAD, VMWRITE and `VmxCaps` are compatibility vocabulary only. They do not materialize SecureCompute descriptors, grants, evidence, backend owners or migration authority.

There is no secure VMCS and no VMCS-owned secure state.

## Privileged Execution-State Owner

Phase 09 introduces `PrivilegedExecutionStateDescriptor` and `PrivilegedExecutionStateOwnerPolicy` for `GuestCr0` and `GuestCr4`.

Owner admission validates:

- descriptor materialization;
- domain and address-space tags;
- current policy epoch;
- canonical register kind;
- allowed and required bit masks;
- evidence visibility class;
- restore revalidation class.

The accepted owner result remains projection-closed and keeps mutation, backend execution, completion and retire authority false.

## Read-Only Projection

Phase 10 adds `PrivilegedExecutionStateProjectionService`.

Projection is admitted only when:

- owner admission succeeds;
- the requested field is exactly `GuestCr0` or `GuestCr4`;
- a descriptor-owned read-only value source exists;
- visibility policy permits the projection;
- migration classification requires restore revalidation;
- conformance proof is present.

VMWRITE remains denied. Other privileged/control fields remain denied by this contract. Projection output is not runtime authority and is not migration authority.

## Compatibility Metadata

VMCS projection metadata, aliases and generated schemas may describe compatibility shape. Their existence does not establish current readable values or secure execution. Such metadata cannot be restored as SecureCompute authority.
