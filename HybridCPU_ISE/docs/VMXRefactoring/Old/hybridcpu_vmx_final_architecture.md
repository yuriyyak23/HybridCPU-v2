# HybridCPU-v2 VMX Final Architecture

**Статус документа:** целевая окончательная архитектура VMX-модели HybridCPU-v2.  
**Назначение:** зафиксировать принципиальную модель, границы ответственности, запретные зависимости и критерии architectural freeze.  
**Не является:** описанием legacy VMX, Intel VMX-клоном, перечнем текущих временных workaround-ов или списком уже закрытых задач.

---

## 1. Ключевой тезис

VMX в HybridCPU-v2 не является архитектурной осью.

VMX — это:

- compatibility frontend;
- frozen ABI;
- набор CSR/VMCS/opcode aliases;
- generated projection surface;
- retire-owned compatibility publication protocol.

Source of truth — не VMX, не VMCS, не VmxCaps, не NPT/EPT/VPID, не Shadow VMCS.

Source of truth — generic runtime substrate:

- domain descriptors;
- capability grants;
- evidence policies;
- execution domains;
- memory domains;
- I/O domains;
- completion fabric;
- sideband records;
- migration/checkpoint policies;
- root-runtime authority.

VMX vocabulary допустим только на ABI/projection boundary.

---

## 2. Нормативная архитектурная пирамида

```mermaid
flowchart TB
    ISA["Small visible ISA<br/>32 integer architectural registers<br/>2048-bit / 256-byte VLIW bundle<br/>EPIC/VLIW execution model"]

    ABI["Frozen compatibility ABI<br/>VMX opcodes / CSR aliases / VMCS field aliases"]

    Projection["Generated compatibility projection<br/>VMCSv2 projection<br/>VmxCaps projection<br/>completion projection<br/>alias maps"]

    Runtime["Generic runtime substrate<br/>domains / descriptors / capabilities / evidence"]

    System["Generic system architecture<br/>MMU / TLB / IOMMU / IOTLB<br/>second-stage translation<br/>address-space tags<br/>DMA coherency"]

    Native["Native execution backends<br/>Lane6 DMA-stream<br/>Lane7 accelerators<br/>vector-stream runtime"]

    ISA --> ABI --> Projection --> Runtime --> System --> Native

    classDef compat fill:#fff4cc,stroke:#b89400,color:#111;
    classDef substrate fill:#e9f7ef,stroke:#1f7a3a,color:#111;
    classDef native fill:#eef2ff,stroke:#4455aa,color:#111;

    class ABI,Projection compat;
    class Runtime,System substrate;
    class Native native;
```

VMX не может ссылаться вниз как владелец состояния. VMX может только запросить projection/admission и получить retire-owned publication.

---

## 3. Запрещённая архитектура

```mermaid
flowchart TD
    VMX["VMX frontend"]
    VMCS["VMCS owns state"]
    Caps["VmxCaps owns capabilities"]
    MMU["VMX owns translation"]
    IO["VMX owns IOMMU/DMA"]
    Nested["VMX owns nested composition"]
    Lanes["VMX owns Lane6/Lane7"]
    Migration["VMX owns checkpoint/evidence"]

    VMX --> VMCS
    VMX --> Caps
    VMX --> MMU
    VMX --> IO
    VMX --> Nested
    VMX --> Lanes
    VMX --> Migration

    Bad["ARCHITECTURAL FAILURE:<br/>VMX becomes axis again"]

    VMCS --> Bad
    Caps --> Bad
    MMU --> Bad
    IO --> Bad
    Nested --> Bad
    Lanes --> Bad
    Migration --> Bad

    classDef bad fill:#ffe5e5,stroke:#aa0000,color:#111;
    class VMX,VMCS,Caps,MMU,IO,Nested,Lanes,Migration,Bad bad;
```

Любой путь, где VMX/VMCS/VmxCaps/NPT/VPID/ShadowVMCS становится authoritative owner, является axis regression.

---

## 4. Разрешённая архитектура VMX operation

```mermaid
flowchart LR
    Decode["Decode VMX opcode<br/>frozen ABI identity"]
    Alias["Compatibility alias lookup<br/>opcode / CSR / VMCS field"]
    Projection["Generated projection<br/>field/capability/completion schema"]
    Admission["Runtime admission<br/>descriptor + capability + evidence + root policy"]
    Operation["Generic domain operation<br/>execution / memory / I/O / nested / lane"]
    Retire["Retire-owned publication<br/>architectural effect boundary"]
    Result["VMX-compatible result projection<br/>success / fail / exit / denied"]

    Decode --> Alias --> Projection --> Admission --> Operation --> Retire --> Result

    Admission -. denied .-> Denied["Fail-closed<br/>no substrate mutation<br/>no host evidence exposure"]
    Projection -. unsupported .-> Denied

    classDef compat fill:#fff4cc,stroke:#b89400,color:#111;
    classDef runtime fill:#e9f7ef,stroke:#1f7a3a,color:#111;
    classDef deny fill:#ffe5e5,stroke:#aa0000,color:#111;

    class Decode,Alias,Projection,Result compat;
    class Admission,Operation,Retire runtime;
    class Denied deny;
```

Canonical path:

```text
VMX instruction / CSR / VMCS access
  -> compatibility alias/projection
  -> descriptor/capability/evidence/runtime validation
  -> generic domain operation
  -> retire-owned publication
  -> VMX-compatible result projection
```

Ни один compatibility opcode не имеет права напрямую:

- читать или писать authoritative VMCS state;
- читать CSR как authority;
- вызывать IOMMU/DMA/TLB backend;
- создавать nested state;
- публиковать completion/evidence;
- сериализовать checkpoint;
- раскрывать host-owned evidence.

---

## 5. Ownership matrix

| Область | Source of truth | VMX роль | Запрещено |
|---|---|---|---|
| VMX opcode ABI | Frozen ABI alias table | decode/projection label | opcode owns semantics |
| VMCS fields | Generated VMCS field projection schema | compatibility aliases | VMCS owns state |
| VmxCaps | Capability projection schema | CSR alias / read-only projection | VmxCaps owns grants |
| Capabilities | typed `CapabilityGrant` graph | projected bitmask only | mask-derived authority |
| Execution | `ExecutionDomainDescriptor` | VM-entry/exit vocabulary projection | VMLAUNCH/VMRESUME owns transition |
| Memory | `MemoryDomainDescriptor`, address-space tags, epochs | EPT/NPT/VPID aliases only | VMCS/EPT/VPID owns identity |
| I/O | `IoDomainDescriptor` | compatibility labels only | VMX binds IOMMU |
| DMA | `DmaAuthorityService`, windows, token/fence namespaces | no direct authority | VMX owns DMA tokens |
| Lane6/Lane7 | neutral lane descriptors and host evidence tables | exit labels / projection only | VMCS stores native handles |
| Nested | `NestedDomainDescriptor`, composition service | VMCS12/VMCS02 projection only | Shadow VMCS as owner |
| Completion | completion fabric / retire boundary | VM-exit projection | frontend publishes completion |
| Migration | domain checkpoint image | VMX metadata denied/recomputed | serializing host evidence |
| Observability | evidence policy + root access policy | guest-visible counters only | raw host counters via VMREAD |

---

## 6. VMCSv2 as generated projection

VMCSv2 is not a substrate object.

VMCSv2 is a generated compatibility projection over generic substrate fields.

```mermaid
flowchart TB
    Schema["Neutral projection schema<br/>vmcs-field-projection-schema"]
    Generator["Build-time generator<br/>schema -> generated C# projection tables"]
    Tables["Generated projection artifacts<br/>field aliases<br/>owner mapping<br/>access policy<br/>migration policy<br/>evidence visibility"]
    Runtime["Generic substrate owners<br/>execution domain<br/>memory domain<br/>completion record<br/>compat control descriptor"]
    VMREAD["VMREAD projection"]
    VMWRITE["VMWRITE projection"]

    Schema --> Generator --> Tables
    Tables --> Runtime
    VMREAD --> Tables
    VMWRITE --> Tables

    Tables --> Denied["Unsupported / denied fields<br/>explicit fail-closed rule"]

    classDef generated fill:#fff4cc,stroke:#b89400,color:#111;
    classDef runtime fill:#e9f7ef,stroke:#1f7a3a,color:#111;
    classDef deny fill:#ffe5e5,stroke:#aa0000,color:#111;

    class Schema,Generator,Tables,VMREAD,VMWRITE generated;
    class Runtime runtime;
    class Denied deny;
```

Каждый VMCS field обязан иметь:

- exact source;
- target substrate owner;
- access policy;
- evidence visibility class;
- migration policy;
- write behavior;
- unsupported/denied semantics.

Если field не имеет owner или explicit denied rule, он не существует.

---

## 7. VMREAD / VMWRITE final flow

```mermaid
sequenceDiagram
    participant Guest as Guest/domain code
    participant VMX as VMX compatibility frontend
    participant Schema as Generated VMCS projection schema
    participant Runtime as Generic runtime substrate
    participant Retire as Retire boundary

    Guest->>VMX: VMREAD / VMWRITE(field)
    VMX->>Schema: resolve field alias
    Schema-->>VMX: owner + access + evidence + migration policy

    alt unsupported or denied
        VMX->>Retire: fail-closed, no mutation
        Retire-->>Guest: VMX-compatible failure projection
    else read allowed
        VMX->>Runtime: read guest-visible projection only
        Runtime-->>VMX: projected value, no host evidence
        VMX->>Retire: publish read result
        Retire-->>Guest: VMX-compatible success
    else write allowed
        VMX->>Runtime: request descriptor-owned update
        Runtime-->>VMX: validation result
        VMX->>Retire: publish update or failure
        Retire-->>Guest: VMX-compatible result
    end
```

VMWRITE never mutates substrate directly. It requests a descriptor-owned update through runtime admission.

---

## 8. VmxCaps final model

VmxCaps is not a capability source.

VmxCaps is a CSR alias/projection of typed grants.

```mermaid
flowchart LR
    Grants["Typed capability grants<br/>canonical authority"]
    Policy["Capability publication policy<br/>guest-visible projection rules"]
    Schema["VmxCaps bit schema<br/>generated projection"]
    CSR["CSR alias: VmxCaps<br/>read-only compatibility surface"]
    Guest["Guest/domain VMX reader"]

    Grants --> Policy --> Schema --> CSR --> Guest

    Write["Write to VmxCaps"]
    Write --> Reject["Strict mode: reject<br/>Compatibility mode: no-effect only with no-emission proof"]

    classDef auth fill:#e9f7ef,stroke:#1f7a3a,color:#111;
    classDef compat fill:#fff4cc,stroke:#b89400,color:#111;
    classDef deny fill:#ffe5e5,stroke:#aa0000,color:#111;

    class Grants,Policy auth;
    class Schema,CSR,Guest compat;
    class Write,Reject deny;
```

Rules:

- no `ulong mask` may create authority;
- bitmap projections may exist only as guest-compatible read models;
- missing typed grant means fail-closed;
- `EffectiveCaps` is cache/projection only;
- `HasEffectiveCapability` is forbidden in security/admission decisions;
- VmxCaps write cannot mutate grants, descriptors, evidence, completion, memory epochs, lane state or migration state.

---

## 9. Runtime admission model

```mermaid
flowchart TB
    Request["RuntimeBoundaryAdmissionRequest"]
    Domain["Domain boundary<br/>execution + memory + I/O descriptors"]
    Capability["Capability boundary<br/>typed grant required"]
    Evidence["Evidence boundary<br/>guest-visible policy"]
    Root["Root runtime authority"]
    Mutation["Frontend mutation guard"]
    Admit["Admit generic operation"]
    Deny["Deny fail-closed"]

    Request --> Domain
    Domain -->|ok| Capability
    Domain -->|fail| Deny

    Capability -->|ok| Evidence
    Capability -->|fail| Deny

    Evidence -->|ok| Mutation
    Evidence -->|fail| Deny

    Mutation -->|compat frontend tries authoritative mutation| Deny
    Mutation -->|ok| Root

    Root -->|allowed| Admit
    Root -->|denied| Deny

    classDef runtime fill:#e9f7ef,stroke:#1f7a3a,color:#111;
    classDef deny fill:#ffe5e5,stroke:#aa0000,color:#111;

    class Request,Domain,Capability,Evidence,Root,Mutation,Admit runtime;
    class Deny deny;
```

Admission is runtime-owned legality.

Frontend legality is never sufficient for execution.

---

## 10. Execution domain model

```mermaid
flowchart LR
    VMEntry["VMLAUNCH / VMRESUME<br/>compatibility request"]
    Alias["VMX entry alias"]
    Admission["Runtime admission<br/>DomainEnter"]
    ExecDesc["ExecutionDomainDescriptor<br/>guest architectural state"]
    Bundle["2048-bit VLIW bundle carrier<br/>EPIC/VLIW scheduling"]
    Retire["Retire publication"]
    Exit["VMX-compatible exit/result projection"]

    VMEntry --> Alias --> Admission --> ExecDesc --> Bundle --> Retire --> Exit

    Admission -. denied .-> Fail["VMFail / security violation projection<br/>no architectural mutation"]

    classDef compat fill:#fff4cc,stroke:#b89400,color:#111;
    classDef runtime fill:#e9f7ef,stroke:#1f7a3a,color:#111;
    classDef deny fill:#ffe5e5,stroke:#aa0000,color:#111;

    class VMEntry,Alias,Exit compat;
    class Admission,ExecDesc,Bundle,Retire runtime;
    class Fail deny;
```

Execution domain owns:

- guest PC/SP/flags projection;
- architectural integer register state;
- domain execution policy;
- traps/intercepts as generic domain events.

VMX owns only compatibility names for entry/exit.

---

## 11. Memory architecture

Memory is not VMX-owned.

```mermaid
flowchart TB
    MemDesc["MemoryDomainDescriptor"]
    Root["AddressSpaceRoot"]
    Stage2["SecondStageRoot"]
    Tags["DomainTag / AddressSpaceTag"]
    Epochs["SecondStageEpoch<br/>AddressSpaceTagEpoch<br/>AddressSpaceGeneration"]
    TLB["TLB"]
    Projection["VMX compatibility projection<br/>EPT/NPT/VPID names"]

    MemDesc --> Root
    MemDesc --> Stage2
    MemDesc --> Tags
    MemDesc --> Epochs
    Root --> TLB
    Stage2 --> TLB
    Tags --> TLB
    Epochs --> TLB

    MemDesc -. projection only .-> Projection
    Projection -. no authority .-> MemDesc

    classDef runtime fill:#e9f7ef,stroke:#1f7a3a,color:#111;
    classDef compat fill:#fff4cc,stroke:#b89400,color:#111;

    class MemDesc,Root,Stage2,Tags,Epochs,TLB runtime;
    class Projection compat;
```

Final memory terms:

- `AddressSpaceRoot`;
- `SecondStageRoot`;
- `DomainTag`;
- `AddressSpaceTag`;
- `AddressSpaceGeneration`;
- `SecondStageEpoch`;
- `AddressSpaceTagEpoch`.

Compatibility-only terms:

- `EPT`;
- `NPT`;
- `VPID`;
- `VMCS epoch`;
- `VMCS identity`.

These terms must not be canonical substrate identity.

Epoch wraparound is fail-closed and requires domain flush or rebuild.

---

## 12. Translation invalidation

```mermaid
flowchart LR
    VMXInv["INVEPT / INVVPID<br/>compat opcode"]
    Alias["Compatibility invalidation alias"]
    Admission["Runtime invalidation admission"]
    MemSvc["Memory-domain invalidation service"]
    TLB["TLB / second-stage cache"]
    Publish["Retire-owned completion projection"]

    VMXInv --> Alias --> Admission --> MemSvc --> TLB --> Publish

    Admission -. denied .-> Deny["fail-closed<br/>no epoch advance"]

    classDef compat fill:#fff4cc,stroke:#b89400,color:#111;
    classDef runtime fill:#e9f7ef,stroke:#1f7a3a,color:#111;
    classDef deny fill:#ffe5e5,stroke:#aa0000,color:#111;

    class VMXInv,Alias,Publish compat;
    class Admission,MemSvc,TLB runtime;
    class Deny deny;
```

Invalidation authority belongs to memory-domain/address-space-tag services.

VMX invalidation opcodes are aliases.

---

## 13. I/O, DMA, IOMMU, IOTLB

```mermaid
flowchart TB
    IoDesc["IoDomainDescriptor"]
    DmaWindow["DmaWindowDescriptor"]
    IommuBind["IOMMU domain binding<br/>generic I/O-domain identity"]
    Fence["Fence namespace<br/>coherency policy"]
    DmaAuth["DmaAuthorityService"]
    Iotlb["IOTLB"]
    Compat["VMX compatibility I/O aliases"]

    IoDesc --> DmaWindow --> DmaAuth
    IoDesc --> IommuBind --> DmaAuth
    Fence --> DmaAuth
    DmaAuth --> Iotlb

    Compat -. projection/request only .-> DmaAuth
    Compat -. cannot bind/unbind IOMMU .-> IommuBind

    classDef runtime fill:#e9f7ef,stroke:#1f7a3a,color:#111;
    classDef compat fill:#fff4cc,stroke:#b89400,color:#111;

    class IoDesc,DmaWindow,IommuBind,Fence,DmaAuth,Iotlb runtime;
    class Compat compat;
```

Rules:

- VMX cannot call `BindVmx`, `ApplyVmx`, `InvalidateVmx`, `UnbindVmx`;
- IOTLB invalidation is generic;
- dirty accounting is domain-owned;
- DMA windows are descriptor-owned;
- token/fence namespaces are not VMX-owned.

---

## 14. Lane6 / Lane7 / vector-stream

Lane state is split into guest-visible virtual state and host-owned evidence.

```mermaid
flowchart TB
    GuestView["Guest/domain-visible virtual state<br/>virtual queue id<br/>virtual token id<br/>guest fence id<br/>completion labels"]
    RuntimeDesc["Lane descriptors<br/>Lane6 / Lane7 / vector-stream"]
    HostEvidence["Host-owned evidence<br/>native token handles<br/>backend handles<br/>scheduler placement<br/>queue pressure"]
    Completion["Completion fabric"]
    Migration["Checkpoint image"]
    Rebuild["Evidence rebuild after restore"]

    RuntimeDesc --> GuestView
    RuntimeDesc --> HostEvidence
    RuntimeDesc --> Completion
    GuestView --> Migration
    HostEvidence -. forbidden .-> Migration
    Migration --> Rebuild --> HostEvidence

    classDef guest fill:#fff4cc,stroke:#b89400,color:#111;
    classDef runtime fill:#e9f7ef,stroke:#1f7a3a,color:#111;
    classDef evidence fill:#eef2ff,stroke:#4455aa,color:#111;
    classDef deny fill:#ffe5e5,stroke:#aa0000,color:#111;

    class GuestView guest;
    class RuntimeDesc,Completion,Migration,Rebuild runtime;
    class HostEvidence evidence;
```

Rules:

- VMCS never stores Lane6/Lane7 authoritative state;
- native tokens and backend handles are never guest-visible;
- VMFUNC cannot bypass descriptor validation;
- lane completion routes through completion fabric;
- migration serializes only virtual/domain-visible state;
- restore rebuilds host evidence.

---

## 15. Nested virtualization

Nested VMX is compatibility projection over generic nested domain composition.

```mermaid
flowchart TB
    L1["L1 guest compatibility request<br/>VMCS12 vocabulary"]
    Alias["Generated nested compatibility projection<br/>VMCS12 / VMCS02 aliases"]
    NestedDesc["NestedDomainDescriptor"]
    Composition["Nested domain composition service"]
    Memory["Nested memory composition<br/>generic second-stage roots/tags/epochs"]
    Caps["Typed nested capability grants"]
    Evidence["Nested evidence policy"]
    Completion["Nested completion route policy"]
    Result["VMX-compatible nested result projection"]

    L1 --> Alias --> NestedDesc
    NestedDesc --> Caps
    NestedDesc --> Evidence
    NestedDesc --> Composition
    Composition --> Memory
    Composition --> Completion
    Completion --> Result

    Shadow["Shadow VMCS"]
    Shadow -. compatibility glossary only .-> Alias
    Shadow -. not owner .-> NestedDesc

    classDef compat fill:#fff4cc,stroke:#b89400,color:#111;
    classDef runtime fill:#e9f7ef,stroke:#1f7a3a,color:#111;
    classDef forbidden fill:#ffe5e5,stroke:#aa0000,color:#111;

    class L1,Alias,Result,Shadow compat;
    class NestedDesc,Composition,Memory,Caps,Evidence,Completion runtime;
```

Nested final rules:

- `VMCS12`, `VMCS02`, `ShadowVmcs` are projection vocabulary only;
- nested enablement requires typed grants;
- nested gates are runtime-validated evidence, not mask bits;
- nested memory composition uses generic memory domains;
- compatibility bridge cannot become permanent architecture;
- missing generic nested service means fail-closed.

---

## 16. Completion, sideband and retire-owned publication

```mermaid
flowchart LR
    RuntimeEvent["Generic runtime event<br/>trap / fault / completion / lane result"]
    Fabric["Completion fabric"]
    Sideband["Sideband records<br/>token/fence namespace<br/>evidence class"]
    Retire["Retire boundary"]
    GuestProj["Guest-visible projection<br/>VM-exit reason / qualification / status"]
    HostEvidence["Host-owned evidence"]

    RuntimeEvent --> Fabric
    Fabric --> Sideband
    Fabric --> Retire
    Retire --> GuestProj
    Sideband --> HostEvidence
    HostEvidence -. not published to guest .-> GuestProj

    classDef runtime fill:#e9f7ef,stroke:#1f7a3a,color:#111;
    classDef guest fill:#fff4cc,stroke:#b89400,color:#111;
    classDef evidence fill:#eef2ff,stroke:#4455aa,color:#111;

    class RuntimeEvent,Fabric,Sideband,Retire runtime;
    class GuestProj guest;
    class HostEvidence evidence;
```

Publication is retire-owned.

Frontend cannot publish:

- VM-exit state;
- VMFail state;
- trace events;
- VMX counters;
- dirty logs;
- completion records.

Frontend can only request projection after retire commits.

---

## 17. Evidence visibility classes

```mermaid
flowchart TB
    Evidence["Evidence"]
    Guest["GuestArchitecturalState<br/>serializable<br/>guest-visible"]
    Alias["CompatibilityAlias<br/>projection-only<br/>not authoritative"]
    Host["HostOwnedRuntimeEvidence<br/>not serializable<br/>not guest-visible"]
    Scheduler["SchedulerEvidence<br/>host-only"]
    Backend["BackendBindingEvidence<br/>host-only"]
    Token["NativeTokenEvidence<br/>host-only"]

    Evidence --> Guest
    Evidence --> Alias
    Evidence --> Host
    Evidence --> Scheduler
    Evidence --> Backend
    Evidence --> Token

    Host -. denied by VMREAD/checkpoint .-> Guest
    Scheduler -. denied .-> Guest
    Backend -. denied .-> Guest
    Token -. denied .-> Guest

    classDef guest fill:#e9f7ef,stroke:#1f7a3a,color:#111;
    classDef alias fill:#fff4cc,stroke:#b89400,color:#111;
    classDef host fill:#ffe5e5,stroke:#aa0000,color:#111;

    class Guest guest;
    class Alias alias;
    class Host,Scheduler,Backend,Token host;
```

Host-owned evidence includes:

- decode cache;
- micro-op cache;
- typed-slot proof cache;
- scheduler placement;
- native DMA tokens;
- native accelerator handles;
- TLB/IOTLB entries;
- backend binding pointers.

It is never guest architectural state.

---

## 18. Migration / checkpoint / restore

```mermaid
sequenceDiagram
    participant Domain as Domain runtime
    participant Checkpoint as DomainCheckpointImage
    participant Policy as MigrationValidationPolicy
    participant Store as Migration store
    participant Restore as RestoreValidationService
    participant Runtime as Runtime evidence rebuild

    Domain->>Checkpoint: serialize guest architectural state + guest-visible policy
    Checkpoint->>Policy: validate payload/evidence classes

    alt contains host-owned evidence
        Policy-->>Checkpoint: reject
    else valid image
        Checkpoint->>Store: persist image
    end

    Store->>Restore: load image
    Restore->>Policy: validate restore policy + epoch
    Restore->>Domain: restore guest architectural state
    Restore->>Runtime: rebuild host-owned evidence
```

Checkpoint image may contain:

- guest architectural state;
- guest-visible domain policy;
- migratable descriptor state;
- virtual/domain-visible lane state.

Checkpoint image must not contain:

- host-owned evidence;
- TLB/IOTLB entries;
- native tokens;
- native backend handles;
- scheduler placement;
- decode/micro-op cache;
- VMCS projection state as authority.

---

## 19. Observability and CSR/root-runtime access

```mermaid
flowchart LR
    Counters["Runtime observability counters"]
    Policy["Evidence/root access policy"]
    Root["Root-runtime reader"]
    Guest["Guest/domain reader"]
    Projection["Guest-visible compatibility projection"]

    Counters --> Policy
    Policy --> Root
    Policy --> Projection --> Guest

    Policy -. denies host-only counters .-> Guest

    classDef runtime fill:#e9f7ef,stroke:#1f7a3a,color:#111;
    classDef guest fill:#fff4cc,stroke:#b89400,color:#111;
    classDef deny fill:#ffe5e5,stroke:#aa0000,color:#111;

    class Counters,Policy,Root runtime;
    class Projection,Guest guest;
```

Observability rules:

- VMREAD cannot expose host evidence;
- CSR aliases are projection only;
- root-runtime access is policy-gated;
- counters do not become capability grants;
- trace data is never architectural guest state.

---

## 20. Directory and namespace ownership

Final layout principle:

```text
Core/VMX/
  Compatibility/
    FrozenAbi/
    Generated/
    Adapters/
  Conformance/
  Docs-facing projection contracts only

Core/Runtime/
  Domains/
  Capabilities/
  Evidence/
  Services/
  Completion/
  Migration/

Core/Memory/
  Translation/
  TLB/
  AddressSpaces/
  SecondStage/

Core/IO/
  Domains/
  DMA/
  IOMMU/
  IOTLB/

Core/Lanes/
  Lane6/
  Lane7/
  VectorStream/
```

`Core/VMX/Substrate` must not be the final home of generic substrate.

Acceptable in `Core/VMX`:

- ABI aliases;
- generated projection tables;
- compatibility adapters;
- conformance contracts that police VMX boundaries.

Not acceptable in `Core/VMX`:

- canonical capability authority;
- memory-domain authority;
- I/O/DMA authority;
- nested-domain composition;
- migration/checkpoint authority;
- native lane backend state;
- host-owned evidence stores.

---

## 21. Static boundary contracts

Required static contracts:

```mermaid
flowchart TB
    Static["Static conformance suite"]
    NoVMCS["No direct VMCS authority"]
    NoCSR["No direct CSR authority except frozen ABI/projection"]
    NoIOMMU["No direct VMX IOMMU/DMA binding"]
    NoShadow["No Shadow VMCS outside generated projection glossary"]
    NoLegacy["No legacy import without substrate rewrite"]
    NoMask["No mask-derived capability authority"]
    NoEvidence["No host evidence in guest projection/migration"]
    NoEpoch["Epoch wraparound fail-closed"]

    Static --> NoVMCS
    Static --> NoCSR
    Static --> NoIOMMU
    Static --> NoShadow
    Static --> NoLegacy
    Static --> NoMask
    Static --> NoEvidence
    Static --> NoEpoch
```

Static checks must be semantic enough to catch:

- renamed wrappers;
- facade authority;
- compatibility bridges becoming permanent;
- `Generated` path with handwritten authority;
- bitmap authority hidden under typed API names.

---

## 22. Negative conformance requirements

Before freeze, tests must prove:

1. missing typed grant is denied even if compatibility bit is present;
2. VmxCaps write is rejected or no-effect with no-emission proof;
3. VMREAD of host evidence is denied;
4. native token read is denied;
5. backend handle read is denied;
6. migration image with host evidence is denied;
7. restore rebuilds host evidence instead of trusting stale image data;
8. epoch wraparound is denied/fenced;
9. VMFUNC cannot bypass descriptor validation;
10. INVEPT/INVVPID route through generic memory-domain invalidation;
11. VMCS12/ShadowVMCS are projection-only;
12. compatibility adapter cannot call IOMMU/DMA backend directly;
13. completion publication is retire-owned;
14. dirty logging is descriptor/runtime-owned;
15. generated projection output matches schema source.

---

## 23. Freeze decision checklist

Architectural freeze is allowed only if all statements below are true:

| Requirement | Freeze status expectation |
|---|---|
| VMX frontend has no authoritative execution/memory/I/O/nested/lane state | required |
| VMCSv2 is generated projection, not state owner | required |
| VmxCaps is CSR alias/projection, not capability source | required |
| Typed grants are canonical source of capability authority | required |
| Memory identity is generic, not VMCS/EPT/VPID/NPT | required |
| IOMMU/IOTLB/DMA are generic system architecture | required |
| Lane6/Lane7 host evidence cannot leak to guest/migration | required |
| Nested uses generic nested-domain composition | required |
| VMCS12/VMCS02/ShadowVMCS are compatibility boundary only | required |
| Completion publication is retire-owned | required |
| Migration excludes host evidence and rebuilds it after restore | required |
| Generated artifacts are produced by executable/build-time generator | required |
| Static + negative conformance catches wrappers/renames/facades | required |
| `Core/VMX/Substrate` no longer owns generic substrate | required |

If any row is not proven, the system may be safe-by-quarantine or safe-by-fail-closed, but not architecturally frozen.

---

## 24. Final one-line model

```text
HybridCPU VMX =
  frozen compatibility ABI
  + generated projection surface
  + runtime-admitted generic domain operations
  + retire-owned publication
  - VMX-owned authority
  - VMCS-owned state
  - VmxCaps-owned capability
  - host-evidence leakage
```

VMX is a language spoken at the boundary.

HybridCPU runtime is the machine that owns truth.
