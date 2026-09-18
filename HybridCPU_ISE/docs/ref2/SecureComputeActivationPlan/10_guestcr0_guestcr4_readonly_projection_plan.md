# GuestCr0 GuestCr4 Readonly Projection Plan

## Phase Metadata

- File name: `10_guestcr0_guestcr4_readonly_projection_plan.md`
- Phase goal: implement the post-RFC read-only compatibility projection path for `GuestCr0` and `GuestCr4`.
- Status: implemented, projection-only gate closed by production code, tests and source guards.
- Scope: VMREAD projection only after neutral privileged execution-state owner, value-source, visibility, migration and conformance gates succeed.
- No-goals: no VMCS store, no VMWRITE, no VMX authority, no SecureCompute backend activation and no category-wide VMREAD opening.

## Implemented Contract

`PrivilegedExecutionStateProjectionService` accepts a typed register selector and neutral `PrivilegedExecutionStateDescriptor`. It reruns `PrivilegedExecutionStateOwnerPolicy` and then requires:

1. materialized descriptor value source;
2. runtime domain-tag binding;
3. runtime address-space-tag binding;
4. current privileged execution-state epoch;
5. legal `GuestCr0` / `GuestCr4` values;
6. guest-visible evidence classification;
7. restore-revalidated migration classification;
8. secure visibility admission;
9. explicit conformance proof.

`VmcsReadOnlyValueProjectionService` maps only `VmcsField.GuestCr0` and `VmcsField.GuestCr4` to that service. Missing inputs remain denied as `PrivilegedExecutionStateProjectionDenied`.

## Field Matrix

| Field | Value source | Access | Side effects |
| --- | --- | --- | --- |
| `GuestCr0` | neutral descriptor `GuestCr0` value after all gates | read-only | none |
| `GuestCr4` | neutral descriptor `GuestCr4` value after all gates | read-only | none |
| any other guest privileged/control field | no Phase 10 contract | denied | none |
| all writes | none | denied | none |

## Authority Boundary

- the descriptor owns the values;
- the owner policy validates identity, epoch and legality;
- the projection service validates visibility, migration and conformance;
- VMREAD transports the admitted read-only value only;
- VMX, VMCS, schema metadata and `VmxCaps` own no SecureCompute authority.

Owner acceptance alone remains projection-closed. Phase 10 projection success is not backend success, mutation permission, completion publication, retire publication or migration authority.

## Production Code Anchors

- `PrivilegedExecutionStateProjectionService.cs`
- `PrivilegedExecutionStateDescriptor.cs`
- `PrivilegedExecutionStateOwnerPolicy.cs`
- `VmcsReadOnlyValueProjectionService.cs`
- `VmxCompatibilityAdmissionService.cs`
- `SecureComputeCompatibilityBoundaryMatrixPolicy.cs`

## Implemented Tests

- `GuestCr0Cr4Projection_DeniedBeforeOwnerMaterialization`;
- `GuestCr0Cr4Projection_DeniedWithoutReadOnlySource`;
- `GuestCr0Cr4Projection_DeniedWithoutVisibilityPolicy`;
- `GuestCr0Cr4Projection_DeniedWithoutMigrationClass`;
- `GuestCr0Cr4Projection_DeniedWithoutConformanceProof`;
- `GuestCr0Cr4Projection_AllowedReadOnlyAfterAllGates`;
- `GuestCr0Cr4Projection_DoesNotAuthorizeBackendSuccessMutationOrPublication`;
- `GuestCr0Cr4Projection_UnsupportedPrivilegedFieldRemainsDenied`;
- `GuestCr0Cr4VmWrite_RemainsDenied`;
- `GuestCr0Cr4Projection_SourceGuardIsFieldSpecificAndProjectionOnly`.

Existing tests also retain denial when the schema or generic execution read-only view exists without Phase 10 inputs.

## Static And Source Guards

- only `GuestCr0` and `GuestCr4` register kinds are accepted;
- no scalar compatibility fallback;
- no mutable VMCS store or runtime manager;
- no virtualization execution unit dependency;
- no `VmxCaps` authority source;
- no backend-success, mutation, completion or retire true flags;
- no generic execution snapshot as CR0/CR4 value or epoch source.

## Migration And Evidence Classification

The owner descriptor must use `GuestVisibleReadOnlyProjection` and `RevalidatedAfterRestore`. Projection output is compatibility-visible read-only data, not checkpoint or migration authority. Restore must revalidate owner identity, current epoch and bit legality before projection.

## Completion And Retire

The projection returns no backend result and authorizes neither completion nor retire publication.

## SecureCompute Activation Implications

Phase 10 closes one narrow compatibility projection path. It does not activate SecureCompute backend execution, a SecureCompute VMX mode or production SecureCompute workloads.

## Exit Criteria

- field-by-field matrix implemented;
- all denial branches covered;
- positive tests pass only after every gate;
- VMWRITE and broad field projection remain denied;
- source guards preserve VMX/VMCS/`VmxCaps` zero authority;
- release corpus states projection-only status.

Exit status: satisfied only for the narrow read-only projection/zero-authority claim. Phases 11 and 12 contain policy classes but remain open for production memory/IOMMU/device enforcement and canonical maps. The next implementation order is defined in `24_audit_revalidation_and_dependency_order.md`.

## Dependency

Previous: `09_privileged_execution_state_owner_rfc.md`. Next: `11_secure_memory_private_domain_policy_activation_plan.md`.
