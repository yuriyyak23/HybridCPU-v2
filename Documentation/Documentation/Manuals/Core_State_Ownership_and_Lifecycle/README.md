# Core state ownership and lifecycle

> Repository workflow, не architecture authority. RF-11 maintenance для нового per-core state, переноса storage, facade/accessor, snapshot или core-table lifecycle change сохраняет правило: containment не переносит semantic authority [ADR-010][adr-010].

## 1. Current boundary

Один sealed reference-type `CPU_Core` владеет одним readonly `CoreRuntimeState`; state разнесён по owner-oriented domains, но scheduler, execution provider, replay, memory, retirement и extension protocols сохраняют отдельную functional authority [RF-11 status][rf11] [`CoreRuntimeState.cs`][runtime-root]. PRF/RenameMap/CommitMap/FreeList остаются в backend domain и не являются ISA-visible architectural state [ADR-002][adr-002].

## 2. Куда помещать state

| State meaning | Storage domain | Authority остаётся у |
|---|---|---|
| ISA-visible committed context/CSR/vector config | `ArchitecturalState` | Existing architectural/retire owner |
| PRF/rename/commit/free-list | `BackendState` | Backend owners |
| Decode/admission/replay/retire latches | Соответствующий `Decode/Admission/Replay/RetireState` | Decoder/scheduler/replay/retire protocol |
| EX/MEM/WB-facing transient state | `ExecutionState` / `MemoryPipelineState` | Stage and memory/retire owners |
| Counters/trace | `TelemetryState` | Observation only, never control |
| MatrixTile/DSC/L7/etc. | Owner-specific `ExtensionState` child | Independent extension ABI/owner |
| Compatibility fields | `LegacyCompatibilityState` | Named retained adapter owner |

Если state не укладывается в существующий domain без объединения owners, требуется architecture decision, а не новый universal container.

## 3. Workflow

| Шаг | Owner | Input → output | Valid | Fail-closed | Forbidden bypass | Tests/evidence | Rollback |
|---:|---|---|---|---|---|---|---|
| 1. Meaning inventory | State + architecture owner | Readers/writers/reset/flush/recovery → owner map | Semantic and storage owners названы отдельно | Unknown writer blocks move | Классифицировать по filename | Closed-world reader/writer scan | No code change |
| 2. Domain selection | `CoreRuntimeState` maintainer | State meaning → one storage domain | One live location | Cross-domain dual storage | Add generic MiscState | ADR-010 map tests | Remove empty field |
| 3. Lifecycle | Platform/core owner | Construct/reset/replace rules → initialization | Non-null runtime identity, explicit reset | Lazy default root or absent live core | Snapshot/writeback as lifecycle | Lifecycle tests [core identity][core-identity] | Restore constructor/accessor |
| 4. Cutover | Domain owner | Old field → new storage + forwarding | Atomic one-location cutover | Dual-write or stale cached mutable copy | Broad mechanical move | Source/copy/reflection scans | Restore old storage for this group |
| 5. Cycle order | Pipeline owner | Reads/writes → frozen cycle mapping | Existing early return and stage order unchanged | Phase move/reorder hidden in extraction | Method name implies new timing | Stage/order differential | Revert group slice |
| 6. Accessors | Facade owner | Callers → identity-preserving access | Alias references same live core/root | Detached copy used for mutation | `GetCoreSnapshot` writeback | Accessor/snapshot tests | Restore forwarding |
| 7. Test/reflection | Compatibility owner | TestSupport/reflection callers → redirect/retain | Explicit non-production boundary | Test mutation promoted to production API | Delete hidden caller by name | Reflection/TestSupport inventory | Restore adapter |
| 8. Cleanup | State owner | Zero-reader/writer old location → removal | No dual declaration/read/write | Valid parity only as deletion proof | Remove other domain fields together | Final owner audit | Restore only this field group |

## 4. Lifecycle invariants

- Default/null facade is not an execution identity.
- A ready platform exposes populated configured core slots; absent live access fails.
- `GetCoreRef` preserves identity; snapshot is detached observation.
- `ReplaceCore` is explicit lifecycle-only operation, not cycle/replay/retire/recovery/publication.
- `CoreRuntimeState` must not expose universal `Execute`, `Commit`, `Publish`, `Rollback`, `Fallback`, `Checkpoint` or `Migrate` [ADR-012][adr-012] [core identity source][core-identity].

## 5. Validation and checklist

Use current `StateOwnership` stage only where the active ArchitectureAuthorityRefactorSummary script/ledger confirms the intended test set; final repository gate remains:

```powershell
pwsh ./eng/validate.ps1 -Stage Full
```

- [ ] One semantic owner and one storage location.
- [ ] Construction/reset/flush/recovery and serialization covered.
- [ ] No cycle-order, early-return or latch-semantics change.
- [ ] No snapshot/reflection/TestSupport mutation leak.
- [ ] Backend and ArchitecturalState remain distinct.
- [ ] Extension owners remain independent.
- [ ] Slice rollback does not revert another domain.

[adr-010]: ../../../ArchitectureAuthorityRefactor/02_Authority/ADR-010_CPU_Core_State_Ownership.md
[adr-002]: ../../../ArchitectureAuthorityRefactor/02_Authority/ADR-002_Backend_Target.md
[adr-012]: ../../../ArchitectureAuthorityRefactor/02_Authority/ADR-012_CPU_Core_Table_Lifecycle.md
[rf11]: ../../../ArchitectureAuthorityRefactor/10_RF11/00_CURRENT_STATUS_AND_LEDGER.md
[runtime-root]: ../../../../HybridCPU_ISE/CloseToHSL/Core/State/CoreRuntimeState.cs
[core-identity]: ../../../../HybridCPU_ISE/NonRTL/Processor/Core/Processor.CoreIdentity.cs
