# HybridCPU external runtime contracts 1.14

Versioned, semantic and hardware-opaque contracts for fail-closed HybridCPU
runtime integration. Consumers must validate contract version, manifest
generation, lease epoch and exact operation receipts.

Version 1.1 adds the additive `IHybridCpuChildDomainRuntimeV1` contract. It
binds every opaque child lease to an exact ordinary parent lease, admits only
an authority subset, and provides generation-bound child lifecycle, bounded
guest-memory mapping/unmapping, semantic event and trap receipts, and a
definitive terminal child-close receipt. No public type exposes VMX/VMCS,
physical addresses, provider-internal identities, or mutable hardware state.

The v1.1 feature families are advertised as `RuntimeAdmission`. Interface
presence does not claim production execution; a backend must independently
qualify before any family is advertised as `Executable`.

## H01 external-operation schema 1.4.0

Package 1.4.0 adds an immutable provider-neutral operation slice. Existing V1,
child-domain V1/V2/V3, operation identities and opaque ordinary domain leases
retain their semantics. The operation slice reuses `ExternalOperationIdentity`
(handle + attempt generation), `ExternalDomainLease` (handle + epoch), and
`HybridCpuExternalContractVersion`. It advertises no new feature family.

`ExternalOperationRequest` contains the exact operation, opaque scope, schema
version, opaque generation snapshot, typed immutable descriptor correlation,
effect class, visibility requirement and cancellation requirement. Providers
must bind correlation to the exact immutable work; callers must never reuse it
for different work. Every receipt carries that entire request by value equality.

The normal lifecycle is exactly:

```text
Prepared -> Admitted -> Submitted -> DeviceComplete -> Visible -> Published -> Released
```

Admission, completion, publication and release each have their own receipt type.
`ExternalOperationProgressReceipt` separately acknowledges Submitted or Visible.
The receipt stage is a claim about an attempted step; the outcome determines
whether it is accepted. Only Succeeded advances a non-release step, and only
Closed advances the exact release step. Completion is never visibility or
publication, and publication is never release. Even ReadOnly, Idempotent,
Coherent and Unsupported cancellation requirements do not bypass any step.
All operations require separate explicit publication and release receipts.

`ExternalOperationContract.Accept` is a pure fail-closed correlation/sequence
validator, not an issuer, source-of-truth registry, authentication mechanism or
permission to submit/publish. The trusted adapter must supply provider receipts
and a fresh authoritative current generation snapshot. The consumer must store
the returned stage atomically per exact operation, call Revalidate before each
external effect and publication, and mark Stale on invalidation notifications.
Do not pass compiler metadata, diagnostics or a cached admission snapshot as
fresh provider revalidation. No production caller is introduced by H01.

Absent receipts, Unknown, NotFound, faults, timeout/disconnect/cancellation
ambiguity and invalid sequencing never advance. Failed and Stale are sticky in
this slice; reconciliation/closure belongs to the provider and future adapter
work. A receipt replay after Released is rejected as Failed and must not be used
to undo an already proven release. Revalidate preserves an already terminal
stage; it does not prove a new release. Missing or unequal current snapshots and
cross-request receipts are Stale. Opaque tokens have no ordering: both an older
and a newer unequal snapshot are rejected. Empty/duplicate/invalid tokens and
unsupported versions fail construction/deserialization. Snapshot arrays are
copied, canonicalized and never exposed in the public object model. The strict
JSON snapshot envelope is transport-neutral; tokens remain uninterpretable.

### Version and consumer policy

Contract schema 1.4.0 is independent of any transport revision. This slice
supports exactly schema 1.4.0; reject missing, older, newer or incompatible
versions before effects. New schema support requires explicit negotiation and
qualification. Package SemVer: additive public APIs increment minor, breaking
changes increment major, implementation/documentation fixes increment patch.
A package patch need not change the supported operation schema triple. Existing
schemas and feature manifests keep their current version rules.

Future SingNextOS consumers target net11.0 and reference only this package:

```xml
<PackageReference Include="HybridCPU.ExternalRuntime.Contracts" Version="[1.14.0]" />
```

Build the package with:

```powershell
dotnet pack HybridCPU_ExternalRuntime.Contracts/HybridCPU_ExternalRuntime.Contracts.csproj -o artifacts/h01/packages
```

Use an exact version, restore with a committed packages.lock.json and locked
mode, and retain the package digest plus source commit pin. For source-based
consumption pin the reachable HybridCPU commit containing this slice and build
only Contracts; do not reference the ISE/runtime implementation to obtain the
contract. This environment has no accessible Git worktree, so no source SHA is
asserted. The owner must supply the real commit pin before cross-project use.
Contracts has zero project/package implementation dependencies. SingNextOS
adapter -> Contracts is the intended direction; Contracts never references
SingNextOS, ISE, GUI, diagnostics or transport.

PublicApi.1.3.0.txt is a lightweight full exported-member compatibility snapshot
from the available pre-H01 1.3.0.0 assembly, checked as a subset of current ABI by
the focused tests. PublicApi.Shipped.txt retains the existing inventory and adds
the H01-H15 additive surface. This is a regression check, not a replacement for
full binary compatibility tooling or signed/released package provenance.

## H14/H15 SecureCompute composition schema 1.0.0

Package 1.11.0 introduces, and package 1.12.0 extends, a separate SecureCompute schema `1.0.0`, independent of
the external-operation schema and all transport revisions. Its opaque secure
domain, region, evidence-context, execution and guest-region handles can be
correlated only by exact equality. `ExternalSecureRegionBinding` reuses the
existing `ExternalGuestMappingLease`; `ExternalSecureExecutionBinding` proves
the exact child/secure/parent lineage; and a close receipt requires an exact
operation plus terminal status. Missing, changed, or incompatible identities,
generations and schemas fail construction. These types neither create a secure
authority root nor assert production secure support.

`IExternalSecureDomainProvider` is an optional provider seam for secure-domain
creation and terminal close. Only a provider can issue its proof and receipts;
the runtime adapter rejects or quarantines malformed correlations and never
turns unavailable, faulted, or stale provider outcomes into creation or close.

`IExternalSecureVirtualEventProvider` authorizes an exact already-published
operation only after its own security and generation revalidation. The runtime
forwarder then calls the existing child `InjectChildEvent` contract. It has no
direct physical-completion-to-guest path, and stale, absent, malformed or
faulted authorization injects no event.

`ExternalSecureVirtualIoBinding` composes the exact existing bounded child
virtual-I/O receipt with the secure execution binding. `ExternalSecureIoAdmissionReceipt`
then requires a distinct opaque provider proof for the same operation. Neither
receipt replaces the other or grants physical-device authority.

The runtime feature manifest continues to report `SecureDomains` as
`Unavailable`. A future SingNextOS adapter must reference this package at an
exact pinned version and obtain all lifecycle, visibility, security and closure
facts from its provider. No CXL topology, endpoint, address, routing or
provider-private identity occurs in this ABI.

Provider/SingNextOS alone owns admission, submission acknowledgement,
completion, visibility, publication and release. CPU legality, runtime guards,
replay and certificates remain CPU evidence/authority within their existing
scope and cannot mint provider receipts. Constructible receipt DTOs are not
security credentials. The deterministic test provider proves contract behavior
only. H01 includes no real transport, provider integration, lane changes,
coherent-memory mapping or production/security/executable capability claim.

## H02 independent admission binding 1.5.0

The H02 binding gate combines two independent facts for one exact immutable
`ExternalOperationRequest`: a CPU-owned `ExternalOperationCpuGuardReceipt` and
an authoritative provider `ExternalOperationAdmissionReceipt`. A CPU guard may
be valid while provider admission rejects, and provider admission may succeed
while the CPU guard is stale, rejected, missing, or for another request. Neither
fact changes the meaning or authority source of CPU scheduling legality.

`ExternalOperationAdmissionBinding.Evaluate` is fail-closed: it returns
`SubmitEligible` only for an exact, current request with an Allowed CPU guard
and a Succeeded provider admission receipt. It returns Stale for generation or
correlation drift, and Rejected for missing or denied gates. It does not submit,
issue receipts, create a CPU legality decision, or make a provider call.
Callers must recreate CPU guard evidence after owner/domain/descriptor/replay
changes and revalidate the provider generation before each new provider effect.

## H03 replay effect and invalidation policy 1.6.0

`ExternalOperationReplaySnapshot` stores only the exact operation correlation,
semantic effect class, lifecycle stage, immutable generation snapshot, and a
provider-neutral invalidation reason. `ExternalOperationReplayKey` contains no
hardware addresses, topology, or provider-private identity.

`ExternalOperationReplayPolicy` has no `ReplayToken` or certificate input and
always returns `AllowsDirectSubmit == false`. Before submit it requires fresh
admission. After submit it awaits the original operation or requires exact
cancellation before re-admission. DeviceComplete and Visible continue the
original lifecycle. Published and released effects are replay barriers. Direct
coherent and irreversible effects become replay barriers once submitted. Any
generation/owner/domain/descriptor/provider-capability/cancellation invalidation
prevents cached reuse. Before submit it requires fresh CPU guard and provider
admission; after submit it is a replay barrier until the original operation is
safely resolved.

## H04 provider-neutral adapter seam 1.7.0

`IExternalOperationProvider` is a narrow, asynchronous provider seam. A
CPU-side caller supplies `ExternalOperationSemanticRequest`, containing only
typed correlation and effect/visibility/cancellation semantics. The provider
creates the exact request with its opaque operation identity, lease scope and
generation snapshot, then remains the sole issuer of lifecycle receipts.

`ExternalOperationProviderPollResult` requires either an explicit `Pending`
status with no receipt, or a non-pending status with exactly one receipt. It
does not permit a missing result to mean completion, visibility, publication,
release, cancellation, or success. The seam exposes no descriptor bytes,
memory address, transport, topology, or hardware/provider identity.

The ISE opt-in `ProviderNeutralExternalAcceleratorBackend` maps a previously
validated lane-7 descriptor into a semantic staged-output request. It requires
both the current CPU guard and a valid provider admission before submission,
submits once, and retains provider lifecycle state privately. A completion
receipt leaves the L7 token Running; only an exact later Visible receipt moves
it to DeviceComplete, where the existing commit coordinator remains the sole
architectural publication path. Direct coherent output is never requested by
this backend. The existing reference backend remains the runtime default.

This seam is not a provider implementation, transport, CXL feature, or
production capability claim. Publication/release receipt integration and staged
payload qualification remain subsequent roadmap work.

## H09 semantic capability query and service binding 1.9.0

`ExternalOperationServiceBinding` accepts only a host-established opaque service
handle, exact opaque lease scope, contract version, and current opaque generation
set. Ordinary descriptor bytes cannot construct a usable binding in a provider.
`IExternalOperationCapabilityProvider` is an optional, additive provider
extension: it reports semantic service, DMA read/write, coherent-access, staged
publication, direct-output, and cancellation availability without topology or
hardware identity. Missing extension support, stale generations, provider fault,
contract mismatch, and transport unavailability carry explicit non-success
statuses and never imply a capability.

## H11 cancellation and invalidation taxonomy 1.10.0

`ExternalCancellationCapability` describes only provider-declared cancellation
support. `ExternalOperationInvalidationReason` classifies reset, generation,
domain/mapping, opaque service reconfiguration, session loss, visibility,
publication, and cleanup invalidations without exposing topology. Neither enum
is a cancellation acknowledgement, release receipt, or authority to retry an
ambiguous submitted effect.

`IExternalOperationCancellationProvider.RequestCancellation` returns an exact
`ExternalOperationCancellationReceipt`. Only confirmed outcomes can clear a
session operation for fresh admission; unsupported, ambiguous and stale outcomes
never imply release or a replacement submit.

## H06 staged-publication evidence gate 1.8.0

`ExternalOperationPublicationEvidence` binds one immutable request to its exact
DeviceComplete receipt, exact later Visible receipt, and the opaque generation
snapshot against which both were accepted. `ExternalOperationPublicationGate`
is pure and fail-closed: missing, cross-operation, out-of-sequence, stale,
direct-coherent, and read-only evidence is ineligible for CPU staged
publication. It neither calls a provider nor creates a publication or release
receipt.

The ISE opt-in provider-neutral lane-7 bridge retains validated completion and
visibility receipts privately and supplies this bundle to the existing CPU
commit coordinator only after an explicit current provider poll reports the
same opaque generation snapshot. That coordinator rejects the bridge operation
when the bundle is absent or ineligible. The reference backend does not opt in and keeps
its existing local staged-commit semantics. This remains a testable semantic
boundary only: no transport, provider implementation, payload qualification,
or production capability is added.
