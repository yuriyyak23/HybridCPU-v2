# 2026-05-25-198 CapabilityDescriptorSet grant-first projection

Date: 2026-05-25

Status: closed

## Rule / basis

Audit2 identified the next blocker before VMX freeze as bitmap/cache pressure in `CapabilityDescriptorSet`: capability-bit publication must become grant-first/read-only evidence over neutral typed capability descriptors, without VMX-owned capability authority.

VMX principles require:
- VMX is a frozen compatibility frontend, not the virtualization architecture.
- `VmxCaps` is a pure compatibility alias over capability descriptors.
- Capabilities are typed grants, not raw bitmaps.
- Compatibility projection may read neutral descriptor state, but cannot own or mutate capability authority.

## Closed slice

This slice closes the descriptor-local capability mask authority:
- `CapabilityDescriptorSet` now stores `CapabilityGrantCollection` as primary state.
- `GlobalHardwareCaps`, `RuntimeEnabledCaps`, `DomainGrantedCaps`, `CompatibilityCapsProjection`, and `EffectiveCaps` are computed read-only projections over typed grants.
- `HasHardwareCapability(...)`, `IsRuntimeEnabled(...)`, `IsDomainGranted(...)`, and `HasEffectiveCapability(...)` now resolve typed grants by scope.
- Compatibility mask inputs are converted immediately into typed grants through `CapabilityGrantCollection.FromCompatibilityMasks(...)`.
- `CapabilityGrantCollection.FromCompatibilityMasks(...)` creates hardware/runtime/domain stage grants as host-only/non-projectable grants and creates guest-visible compatibility projection grants only for bits that pass all stages.
- VmxCaps bit schema now names `CapabilityGrantCollection.TypedGrant` / `NestedDomainCapability.TypedGrant` as the source, not `CapabilityDescriptorSet.*` bitmap state.

## Removed / denied authority

Removed as descriptor-local authority:
- stored hardware capability bitmap backing state;
- stored runtime-enabled capability bitmap backing state;
- stored domain-granted capability bitmap backing state;
- direct effective-capability computation from `global & runtime & domain` inside `CapabilityDescriptorSet`;
- VmxCaps schema claims that `CapabilityDescriptorSet.*` bitmap state is the typed grant source.

Denied by conformance:
- descriptor backing assignments for the former mask fields;
- compatibility projection that bypasses `TypedGrants.EffectiveCompatibilityMask`;
- schema entries that claim `CapabilityDescriptorSet.*` as typed grant source.
- the old `CapabilityGrantCollection.FromMasks(...)` alias, so compatibility ingress is named explicitly as `FromCompatibilityMasks(...)`.

## Not introduced

No new VMX owner was introduced:
- no `VmcsManager`;
- no `VmxExecutionUnit`;
- no capability manager/adapter replacement;
- no VMX-owned capability service;
- no VMCS field store;
- no runtime generator.

No behavior was moved into a renamed VMX/VMCS runtime owner.

## Remaining VMX / VmxCaps vocabulary

The following names remain compatibility vocabulary only:
- `VmxCapsProjection`;
- `VmxV2InstructionCaps`;
- `CapabilityDescriptorSetSchema.KnownVmxV2CompatibilityMask`;
- VmxCaps JSON schema rows.

They describe frozen ABI/projection metadata and generated compatibility publication, not capability authority.

## Live generic responsibilities

Live capability responsibility remains with:
- `CapabilityGrant`;
- `CapabilityGrantCollection`;
- `CapabilityDescriptorSet`;
- `CapabilityNegotiationService`;
- `CapabilityPublicationPolicy`;
- `RuntimeBoundaryAdmissionService`.

These are still physically under the current VMX substrate tree in part, so directory/namespace extraction remains future work. The important closure here is authority direction: VmxCaps reads grant-backed projection, not stored VMX-owned bit state.

## Conformance

Added / updated:
- `CapabilityDescriptorSetGrantFirstAuthorityContract`;
- `VmxCapsBitSchemaConformanceContract` now rejects `CapabilityDescriptorSet.*` as typed grant source;
- `VmxProjectionSchemaAndQuarantineTests.CapabilityDescriptorSet_ProjectsVmxCapsFromGrantFirstReadOnlyEvidence`;
- generated-lineage tests now require `CapabilityGrantCollection.TypedGrant` in the VmxCaps schema/generated source.

## Verification

Targeted verification run during closure:
- `dotnet build HybridCPU_ISE.csproj --no-restore`: passed; lineage verifier ran; 54 existing warnings, 0 errors.
- `dotnet build HybridCPU_ISE.Tests.csproj --no-restore`: passed with existing warnings.
- `dotnet test ... --filter "FullyQualifiedName~CapabilityDescriptorSet"`: passed.
- `dotnet test ... --filter "FullyQualifiedName~VmxCaps"`: passed.
- `dotnet test ... --filter "FullyQualifiedName~RuntimeBoundaryAdmission"`: passed.
- `dotnet test ... --filter "FullyQualifiedName~VmxProjectionSchemaAndQuarantineTests"`: passed, 39/39.
- `dotnet test ... --filter "FullyQualifiedName~GeneratedProjectionLineage"`: passed, 1/1.
- `dotnet test ... --filter "FullyQualifiedName~CoreVmxAuthorityBoundaryTests"`: passed, 1/1.
- `dotnet test ... --filter "FullyQualifiedName~RemovedLegacyVmxExecutionUnit"`: passed, 15/15.
- `dotnet test ... --filter "FullyQualifiedName~LegacyVmcsManager"`: passed, 4/4.
- `dotnet test ... --filter "FullyQualifiedName~VmcsV2"`: passed, 5/5.

Known unrelated broad-filter failures remain separate:
- `FullyQualifiedName~DmaStreamCompute`: existing 5 failures from missing `Core/Execution/DmaStreamCompute/DmaStreamComputeRuntime.cs` and native NonRTL shape scans.
- `FullyQualifiedName~Retire`: existing 4 failures from missing `Documentation/operational-semantics.md` and stream/compat/native-DMA boundary scans.

## Residual risk

This closes descriptor-local bitmap/cache authority. It does not close:
- production caller audit for `FromCompatibilityMasks(...)`;
- `PublishedVmxCaps` / `PublishedCapabilityWord` nested vocabulary;
- non-typed `HasEffectiveCapability(...)` admission fallback callers;
- memory translation NPT/VPID vocabulary;
- Lane6 host-token evidence migration/rebuild proof;
- generic substrate physical location under `Core/VMX/Substrate`.

## Next heavy step

Audit capability callers and nested capability publication: remove or quarantine `PublishedVmxCaps`, `PublishedCapabilityWord`, and any non-typed admission/security path that can seed or consume compatibility masks as authority instead of requiring typed grants.
