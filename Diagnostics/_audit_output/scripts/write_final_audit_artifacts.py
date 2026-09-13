from __future__ import annotations

import csv
import json
import subprocess
from datetime import datetime, timezone
from pathlib import Path
from textwrap import dedent


ROOT = Path(r"C:\Users\Yuriy Kurnosov\Desktop\HybridCPU ISE")
OUT = ROOT / "_audit_output"
PLAN = ROOT / "Documentation" / "ActualRefactoring" / "DeepResearch" / "Plan1"


def run_capture(args: list[str], cwd: Path = ROOT) -> tuple[int, str]:
    try:
        proc = subprocess.run(
            args,
            cwd=str(cwd),
            text=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            check=False,
        )
        return proc.returncode, proc.stdout
    except Exception as exc:  # pragma: no cover - defensive audit helper
        return -1, f"{type(exc).__name__}: {exc}"


def write_text(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text.replace("\r\n", "\n"), encoding="utf-8")


def write_json(path: Path, data: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps(data, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )


def write_csv(path: Path, rows: list[dict[str, str]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    fieldnames = list(rows[0].keys()) if rows else []
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(rows)


scan_summary_path = OUT / "scan-summary.json"
scan_summary = json.loads(scan_summary_path.read_text(encoding="utf-8")) if scan_summary_path.exists() else {}

dotnet_version_code, dotnet_version = run_capture(["dotnet", "--version"])
dotnet_info_code, dotnet_info = run_capture(["dotnet", "--info"])

baseline = {
    "build": {
        "command": "dotnet build \"HybridCPU v2.slnx\" -v minimal --no-restore -m:1 /p:UseSharedCompilation=false",
        "observed_result": "PASS",
        "observed_errors": 0,
        "observed_warnings": 73,
        "observed_elapsed": "36.32s",
        "note": "A prior parallel build/test attempt produced CSC CS2012 due shared compiler/output locking and is not treated as the baseline.",
    },
    "test": {
        "command": "dotnet test \"HybridCPU_ISE.Tests\\HybridCPU_ISE.Tests.csproj\" -v minimal --no-build",
        "observed_result": "FAIL",
        "total": 7875,
        "passed": 7862,
        "failed": 11,
        "skipped": 2,
        "duration": "3m54s",
    },
    "sdk_drift": {
        "global_json": "10.0.201",
        "observed_dotnet_version": dotnet_version.strip(),
        "risk": "Reproducibility drift: local CLI selected a newer SDK than global.json pins.",
    },
}

failed_tests = [
    "CompilerFacadeAbiPhase01Tests.AppFacadePublicAbi_StaysAtPhase01CompatibilityInventory",
    "Phase12FacadeFamilyDeprecationTests.FacadeInterfaces_NoNewProductionMentionsOutsideFacadeBoundary",
    "Phase09VectorPermute2ExecutableTests.Vperm2_CompilerSurfaceRemainsNoEmissionForDeltaHelpers",
    "Phase09VectorTransposeExecutableTests.Vtranspose_CompilerSurfaceRemainsNoEmissionForDeltaHelpers",
    "Phase09VectorSlideOneDownExecutableTests.Vslide1Down_CompilerSurfaceRemainsNoEmissionForDeltaHelpers",
    "Phase09VectorSlideOneUpExecutableTests.Vslide1Up_CompilerSurfaceRemainsNoEmissionForDeltaHelpers",
    "Phase03ScalarCarryLessClmulExecutableTests.Clmul_CompilerEmission_OpensCarryLessRowsWhileAdjacentContoursRemainClosed",
    "Phase03FallbackTransportSummaryTailTests.DecodeFullBundle_UnsupportedOptionalMatrixMemoryContours_FailClosedAsDecodeFaultTrap(MTILE_LOAD)",
    "Phase03FallbackTransportSummaryTailTests.DecodeFullBundle_UnsupportedOptionalMatrixMemoryContours_FailClosedAsDecodeFaultTrap(MTILE_STORE)",
    "Phase03FallbackTransportSummaryTailTests.DecodeFullBundle_UnsupportedOptionalMatrixContours_FailClosedAsDecodeFaultTrap(MTILE_MACC)",
    "Phase03FallbackTransportSummaryTailTests.DecodeFullBundle_UnsupportedOptionalMatrixContours_FailClosedAsDecodeFaultTrap(MTRANSPOSE)",
]

skipped_tests = [
    "Phase03CarrierProjectionTransportTailTests.LegacySlotCarrierMaterializer_VectorLoadProjection_PublishesTwoSurfaceTransferMemoryShape",
    "Phase03CarrierProjectionTransportTailTests.LegacySlotCarrierMaterializer_VectorStoreProjection_PublishesTwoSurfaceTransferMemoryShape",
]

findings = [
    {
        "id": "F-001",
        "severity": "High",
        "status": "Active",
        "category": "vector-memory-placement",
        "title": "VLOAD/VSTORE materialize memory side effects as Vector/ALU and IsMemoryOp=false",
        "evidence": "InstructionRegistry.Initialize.Vector.cs:21-24; InstructionRegistry.Helpers.Vector.cs:162-180; VectorMicroOps.cs:48-58; VectorMicroOps.Data.cs:727-755,773-812",
        "impact": "Scheduler/admission can see an ALU/vector carrier while the operation performs BurstIO read/write and publishes memory ranges.",
        "recommended_phase": "P1/P2",
        "confidence": "High",
    },
    {
        "id": "F-002",
        "severity": "High",
        "status": "Active",
        "category": "vector-memory-fallback",
        "title": "Vector segment and 2D memory micro-ops inherit ALU placement and contain fallback/no-op success paths",
        "evidence": "VectorMicroOps.cs:48-58; VectorMicroOps.Memory.cs:18-39,41-92,104-158,722-812,817-876",
        "impact": "Memory reads/writes can bypass the LSU slot taxonomy; store ops with missing buffers can complete successfully without architectural writes.",
        "recommended_phase": "P2",
        "confidence": "High",
    },
    {
        "id": "F-003",
        "severity": "High",
        "status": "Active",
        "category": "matrix-tile-authority",
        "title": "Matrix/tile contour is opened but its slot authority remains ALU for MTILE_LOAD/STORE",
        "evidence": "InstructionRegistry.Helpers.MatrixTile.cs:12-15,53-72,142-147; MatrixTileMicroOps.cs:71,128-143,476-484; Phase03FallbackTransportSummaryTailTests failed for MTILE_*",
        "impact": "Runtime materialization, compiler positive emission, and older fail-closed tests disagree on whether matrix/tile is executable and which physical class owns load/store.",
        "recommended_phase": "P3/P4",
        "confidence": "High",
    },
    {
        "id": "F-004",
        "severity": "Medium",
        "status": "Active",
        "category": "compiler-facade-abi",
        "title": "App facade phase-01 ABI tests conflict with newly published matrix helper methods",
        "evidence": "IAppAsmFacade.cs:96-113; AppAsmFacade.cs:684-721; CompilerFacadeAbiPhase01Tests.cs:288-354; Phase12FacadeFamilyDeprecationTests.cs:60-79",
        "impact": "Public compiler surface is neither a frozen compatibility facade nor a fully migrated runtime handoff boundary.",
        "recommended_phase": "P5",
        "confidence": "High",
    },
    {
        "id": "F-005",
        "severity": "Medium",
        "status": "Active",
        "category": "test-authority-drift",
        "title": "Test suite contains stale negative/skip assumptions for already registered/opened contours",
        "evidence": "Phase03CarrierProjectionTransportTailTests.cs:611,640; OpcodeInfo.Registry.Data.Vector.cs:69-74; Phase10MatrixTileRetirePublicationTests.cs:18-76; Phase12MatrixTilePositiveGoldenArtifactTests.cs:129-144",
        "impact": "Tests currently obscure whether VLOAD/VSTORE and MTILE are intentionally open or accidentally reopened.",
        "recommended_phase": "P0/P6",
        "confidence": "High",
    },
    {
        "id": "F-006",
        "severity": "Medium",
        "status": "Latent",
        "category": "memory-taxonomy",
        "title": "IsMemoryOp is overloaded and no longer matches memory ranges, ordering, or physical resource ownership",
        "evidence": "MicroOpAdmissionMetadata.cs:14-30,98-103; InstructionRegistry.Types.cs:159-192; VectorMicroOps.Data.cs:739-755; VectorMicroOps.Memory.cs:178-190,884-907; DmaStreamComputeMicroOp.cs:42-53",
        "impact": "Consumers can accidentally treat 'memory operation', 'memory footprint', 'ordering', and 'slot class' as the same predicate.",
        "recommended_phase": "P1/P3",
        "confidence": "High",
    },
    {
        "id": "F-007",
        "severity": "Medium",
        "status": "Latent",
        "category": "classifier-default",
        "title": "InstructionClassifier falls through to ScalarAlu/Free for unlisted opcodes",
        "evidence": "InstructionClassifier.cs:26-40,233-234,248-262,373-374; InstructionRegistry.Runtime.cs:70-87,106-107",
        "impact": "Registry materialization can fail closed, but classifier-only consumers still receive permissive defaults unless registry semantics are authoritative everywhere.",
        "recommended_phase": "P3/P6",
        "confidence": "Medium",
    },
    {
        "id": "F-008",
        "severity": "Medium",
        "status": "Active",
        "category": "compiler-test-anchor",
        "title": "CLMUL compiler test anchor expects contract text in the wrong source surface",
        "evidence": "Phase03ScalarCarryLessClmulExecutableTests expected CompilerDeferredScalarAbiContract; CompilerDeferredScalarAbiContract.cs:16-318",
        "impact": "The suite fails even though the named contract exists, suggesting test-source selection drift rather than a missing contract type.",
        "recommended_phase": "P6",
        "confidence": "Medium",
    },
    {
        "id": "F-009",
        "severity": "Low",
        "status": "Active",
        "category": "environment-reproducibility",
        "title": "global.json SDK pin differs from selected local dotnet SDK",
        "evidence": "global.json:3; dotnet --version observed 10.0.204",
        "impact": "Audit baseline is reproducible only if SDK roll-forward behavior is accepted or the pinned SDK is installed.",
        "recommended_phase": "P0",
        "confidence": "High",
    },
    {
        "id": "F-010",
        "severity": "Low",
        "status": "Confirmed-good",
        "category": "lane6-lane7-vmx",
        "title": "Lane6 DSC, Lane7 SDC, and VMX have strong hard-pinning evidence",
        "evidence": "DmaStreamComputeMicroOp.cs:42-53,121-149; SystemDeviceCommandMicroOp.cs:57-67,196-255; MicroOp.IO.cs:139-164; SlotClassDefinitions.cs:173-196",
        "impact": "These contours should be protected as anchors while vector/matrix taxonomy is repaired.",
        "recommended_phase": "P0/P6",
        "confidence": "High",
    },
]

reachability_rows = [
    {
        "surface": "Compiler App facade",
        "opcode_or_family": "MTILE_LOAD/STORE/MACC/MTRANSPOSE",
        "source": "IAppAsmFacade/AppAsmFacade",
        "decoder": "InstructionRegistry matrix materializer",
        "microop": "MtileLoadMicroOp/MtileStoreMicroOp/MtileMaccMicroOp/MtransposeMicroOp",
        "placement": "AluClass",
        "memory_effect": "Load/store true; macc/transpose capture/retire side effects",
        "tests": "Positive tests pass in areas, but baseline has ABI/fail-closed conflicts",
        "risk": "Open contour with stale closed-boundary tests",
    },
    {
        "surface": "OpcodeRegistry vector transfer",
        "opcode_or_family": "VLOAD/VSTORE",
        "source": "OpcodeInfo.Registry.Data.Vector",
        "decoder": "RegisterVectorTransferOp",
        "microop": "VectorTransferMicroOp",
        "placement": "Inherited AluClass",
        "memory_effect": "BurstIO read/write; memory ranges populated; IsMemoryOp false",
        "tests": "Two projection tests skipped with stale registry reason",
        "risk": "Memory side effects outside LSU taxonomy",
    },
    {
        "surface": "Vector memory helpers",
        "opcode_or_family": "LoadSegment/Load2D/StoreSegment/Store2D",
        "source": "InstructionRegistry vector helpers",
        "decoder": "Concrete vector factories",
        "microop": "LoadSegmentMicroOp/StoreSegmentMicroOp/etc.",
        "placement": "Inherited AluClass",
        "memory_effect": "MemorySubsystem or BurstIO; stores can no-op success on null buffer",
        "tests": "Partial/gap coverage",
        "risk": "Hidden fallback and silent success",
    },
    {
        "surface": "Indexed vector memory",
        "opcode_or_family": "VGATHER/VSCATTER",
        "source": "OpcodeRegistry + VectorLegalityMatrix",
        "decoder": "RegisterGatherOp/RegisterScatterOp",
        "microop": "GatherMicroOp/StoreScatterMicroOp",
        "placement": "LsuClass",
        "memory_effect": "Staged writeback/retire publication",
        "tests": "Executable and legality tests present",
        "risk": "Useful reference implementation for fixing VLOAD/VSTORE",
    },
    {
        "surface": "Lane6 runtime",
        "opcode_or_family": "DmaStreamCompute",
        "source": "InstructionRegistry.Initialize.Base",
        "decoder": "Dma descriptor guarded parser",
        "microop": "DmaStreamComputeMicroOp",
        "placement": "Hard-pinned DmaStreamClass lane 6",
        "memory_effect": "Descriptor-owned read/write ranges and retire commit",
        "tests": "Strong status/contract coverage",
        "risk": "Protect as source-of-truth pattern",
    },
    {
        "surface": "Lane7 runtime",
        "opcode_or_family": "ACCEL_* / system device command",
        "source": "OpcodeRegistry.Data.System",
        "decoder": "RegisterSystemDeviceCommandOp",
        "microop": "SystemDeviceCommandMicroOp",
        "placement": "Hard-pinned SystemSingleton lane 7",
        "memory_effect": "Control-plane side effects, no scalar/vector fallback",
        "tests": "Phase tests present",
        "risk": "Protect as source-of-truth pattern",
    },
    {
        "surface": "VMX",
        "opcode_or_family": "VMX8",
        "source": "VmxSpecTable + OpcodeRegistry.Data.System",
        "decoder": "InstructionRegistry core helpers",
        "microop": "VmxMicroOp",
        "placement": "Hard-pinned SystemSingleton lane 7",
        "memory_effect": "VMX retire effect, VmxSerial",
        "tests": "Metadata consistency and ABI snapshot tests present",
        "risk": "Confirmed anchor",
    },
]

coverage_rows = [
    {
        "area": "Build",
        "baseline": "PASS",
        "evidence": "dotnet build observed 0 errors / 73 warnings",
        "gap": "Warnings not classified in this audit.",
    },
    {
        "area": "Full test project",
        "baseline": "FAIL",
        "evidence": "7875 total; 7862 passed; 11 failed; 2 skipped",
        "gap": "Failing rows split between real contract drift and stale tests.",
    },
    {
        "area": "VLOAD/VSTORE",
        "baseline": "SKIPPED/STale",
        "evidence": "Phase03CarrierProjectionTransportTailTests.cs:611,640 vs registry rows at OpcodeInfo.Registry.Data.Vector.cs:69-70",
        "gap": "Need active placement/memory metadata assertions.",
    },
    {
        "area": "VGATHER/VSCATTER",
        "baseline": "Covered",
        "evidence": "Factories and LSU placement present in InstructionRegistry.Helpers.Vector.cs and VectorMicroOps.Memory.cs",
        "gap": "Use as reference for VLOAD/VSTORE normalization.",
    },
    {
        "area": "Matrix tile",
        "baseline": "Mixed",
        "evidence": "Positive retire/golden tests exist, while fail-closed and facade ABI tests fail",
        "gap": "Need one authoritative open/closed contract.",
    },
    {
        "area": "Lane6 DSC",
        "baseline": "Covered",
        "evidence": "Hard-pinned lane6 and guarded parser evidence",
        "gap": "Keep regression tests when refactoring taxonomy.",
    },
    {
        "area": "Lane7 SDC",
        "baseline": "Covered",
        "evidence": "Hard-pinned lane7 and fail-closed descriptorless submit paths",
        "gap": "Keep regression tests when refactoring taxonomy.",
    },
    {
        "area": "VMX",
        "baseline": "Covered",
        "evidence": "VmxInstructionMetadataConsistencyTests and Vmx8AbiSnapshotTests",
        "gap": "No production change requested.",
    },
]

file_change_rows = [
    {
        "phase": "P0",
        "file_or_area": "_audit_output/*, Documentation/ActualRefactoring/DeepResearch/Plan1/*",
        "change_type": "audit-only",
        "purpose": "Snapshot, evidence, manifests, report, phase plan.",
        "risk": "None to production source.",
    },
    {
        "phase": "P1",
        "file_or_area": "MicroOp taxonomy/admission helpers",
        "change_type": "planned",
        "purpose": "Introduce authoritative memory/placement invariant checks.",
        "risk": "Medium: shared scheduler contract.",
    },
    {
        "phase": "P2",
        "file_or_area": "VectorMicroOps.cs, VectorMicroOps.Memory.cs, VectorMicroOps.Data.cs, InstructionRegistry.Helpers.Vector.cs",
        "change_type": "planned",
        "purpose": "Normalize VLOAD/VSTORE and segment/2D memory placement, remove silent store success/fallback ambiguity.",
        "risk": "High: runtime-visible behavior.",
    },
    {
        "phase": "P3",
        "file_or_area": "InstructionClassifier.cs, InstructionRegistry.Types.cs, MicroOpAdmissionMetadata.cs",
        "change_type": "planned",
        "purpose": "Split memory footprint/effects/ordering/slot-class semantics.",
        "risk": "Medium: broad consumers.",
    },
    {
        "phase": "P4",
        "file_or_area": "MatrixTileMicroOps.cs, InstructionRegistry.Helpers.MatrixTile.cs, matrix tile tests",
        "change_type": "planned",
        "purpose": "Choose and enforce matrix/tile physical slot authority.",
        "risk": "High: current tests disagree.",
    },
    {
        "phase": "P5",
        "file_or_area": "HybridCPU_Compiler/API/Facade/*, ThreadCompilerContext.MatrixTile.cs, compiler ABI tests",
        "change_type": "planned",
        "purpose": "Resolve facade deprecation vs positive matrix emission handoff.",
        "risk": "Medium: public API.",
    },
    {
        "phase": "P6",
        "file_or_area": "HybridCPU_ISE.Tests/*",
        "change_type": "planned",
        "purpose": "Replace stale skips/negative tests with active authority assertions.",
        "risk": "Low/Medium: test-only but defines architecture truth.",
    },
]

refactoring_phases = dedent(
    """
    # Refactoring Phases

    ## P0 - Freeze the Audit Baseline
    Keep the generated manifests, inventories, and failing-test list as the local truth for this refactor. Do not mix functional edits with evidence regeneration.

    ## P1 - Add Runtime Invariant Guards
    Add a small authority check around micro-op materialization/admission: if a micro-op publishes memory ranges or memory-class semantics, its slot class, ordering, and memory taxonomy must be intentionally declared. Start with assertions/tests, then promote to guardrails.

    ## P2 - Normalize Vector Memory
    Fix VLOAD/VSTORE, segment loads/stores, and 2D vector memory so memory side effects use the same slot/resource vocabulary as VGATHER/VSCATTER or explicitly document a non-LSU runtime owner. Remove or fail-close silent store success when a store buffer is absent.

    ## P3 - Split Memory Semantics
    Replace the overloaded IsMemoryOp predicate with separate concepts: memory footprint, memory side effect, ordering class, physical slot owner, and backend/runtime owner. Keep compatibility shims until all call sites are migrated.

    ## P4 - Decide Matrix/Tile Physical Authority
    Choose whether MTILE_LOAD/STORE are LSU-owned memory operations or ALU-owned runtime-capture operations with explicit memory-domain resources. Then align MatrixTileMicroOps, InstructionRegistry descriptors, OpcodeInfo flags, fail-closed tests, and positive golden tests.

    ## P5 - Resolve Compiler Facade Handoff
    Either move matrix helpers out of the deprecated App facade surface or update the ABI/deprecation contracts to declare the new positive handoff. Keep runtime legality final; compiler must not reopen closed contours by helper naming alone.

    ## P6 - Repair Test Authority
    Update stale skips and negative tests after P2/P4 decisions. Add active tests for slot placement, memory ranges, IsMemoryOp replacement predicates, hidden fallback absence, and replay/retire publication.

    ## P7 - CI Hardening
    Pin or install the SDK requested by global.json, capture test logs as artifacts, and add an audit script target that regenerates inventories without touching production source.
    """
).strip() + "\n"

open_gaps = dedent(
    """
    # Open Evidence Gaps

    - Matrix/tile physical ownership requires an architecture decision: LSU-owned memory load/store vs ALU-owned runtime capture with explicit memory-domain resources.
    - VLOAD/VSTORE need an explicit current-status decision because tests still claim they are absent from OpcodeRegistry while registry rows exist.
    - BurstIO fallback needs a policy decision: legitimate backend abstraction or hidden fallback that must fail closed outside controlled tests.
    - IsMemoryOp consumers must be inventoried before replacing the predicate; the generated symbol inventory identifies call sites but not semantic intent.
    - The audit did not edit production source. Any future code phase should first re-run the baseline after resolving SDK drift.
    - Git history was intentionally not used as source authority; exact authorship/change chronology remains outside this audit.
    """
).strip() + "\n"

snapshot = {
    "generated_at_utc": datetime.now(timezone.utc).isoformat(),
    "root": str(ROOT),
    "authority": "Local filesystem state; git history intentionally not used as source authority.",
    "scan_summary": scan_summary,
    "baseline": baseline,
    "failed_tests": failed_tests,
    "skipped_tests": skipped_tests,
    "finding_counts": {
        "total": len(findings),
        "high_active": sum(1 for row in findings if row["severity"] == "High" and row["status"] == "Active"),
        "medium_active": sum(1 for row in findings if row["severity"] == "Medium" and row["status"] == "Active"),
        "latent": sum(1 for row in findings if row["status"] == "Latent"),
        "confirmed_good": sum(1 for row in findings if row["status"] == "Confirmed-good"),
    },
    "dotnet": {
        "version_exit_code": dotnet_version_code,
        "version": dotnet_version.strip(),
        "info_exit_code": dotnet_info_code,
    },
    "artifacts": [
        "audit-report.md",
        "audit-snapshot.json",
        "environment.txt",
        "findings.csv",
        "reachability-matrix.csv",
        "test-coverage-matrix.csv",
        "file-change-map.csv",
        "refactoring-phases.md",
        "open-evidence-gaps.md",
    ],
}

environment = (
    "# Environment\n\n"
    f"Repo root: {ROOT}\n"
    f"Audit output: {OUT}\n"
    f"Plan output: {PLAN}\n"
    "Authority: local filesystem state; git history was not used as source authority.\n\n"
    "## dotnet --version\n"
    f"Exit code: {dotnet_version_code}\n"
    f"{dotnet_version.strip()}\n\n"
    "## SDK Drift\n"
    "global.json pins: 10.0.201\n"
    f"observed dotnet --version: {dotnet_version.strip()}\n\n"
    "## Baseline Build\n"
    f"Command: {baseline['build']['command']}\n"
    "Observed result: PASS, 0 errors, 73 warnings, elapsed 36.32s.\n"
    "Note: a prior parallel build/test attempt failed with CSC CS2012 because output/compiler files were locked; it is not the baseline.\n\n"
    "## Baseline Test\n"
    f"Command: {baseline['test']['command']}\n"
    "Observed result: FAIL, total 7875, passed 7862, failed 11, skipped 2, duration 3m54s.\n\n"
    "Failed tests:\n"
    f"{chr(10).join('- ' + item for item in failed_tests)}\n\n"
    "Skipped tests:\n"
    f"{chr(10).join('- ' + item for item in skipped_tests)}\n\n"
    "## dotnet --info\n"
    f"Exit code: {dotnet_info_code}\n"
    f"{dotnet_info.rstrip()}\n"
)

scan_counts = scan_summary.get("by_category", {})
total_files = scan_summary.get("total_files", "unknown")
sections = [
    (
        "Scope",
        "Audit scope is the local tree at `C:/Users/Yuriy Kurnosov/Desktop/HybridCPU ISE`. I treated the filesystem as the source of truth and produced only audit artifacts under `_audit_output` and `Documentation/ActualRefactoring/DeepResearch/Plan1`.",
    ),
    (
        "Authority Model",
        "Source authority order used here: production code first, executable tests second, docs third, generated inventories last. Git history was not used as an authority, per request.",
    ),
    (
        "Excluded Paths",
        "The manifest excludes `.git`, IDE state, build outputs, packages, coverage, test result folders, and `_audit_output`. The full exclusion list is in `excluded-paths.txt`.",
    ),
    (
        "Snapshot",
        f"The scanner saw `{total_files}` files after exclusions. Category counts: `{scan_counts}`. Exact SHA-256 rows are in `source-manifest.sha256`.",
    ),
    (
        "Environment",
        "The repo pins SDK `10.0.201` in `global.json`, while `dotnet --version` selected `10.0.204`. Build passed under the observed SDK, but this is a reproducibility drift.",
    ),
    (
        "Baseline Build",
        "Sequential baseline command `dotnet build \"HybridCPU v2.slnx\" -v minimal --no-restore -m:1 /p:UseSharedCompilation=false` passed with 0 errors and 73 warnings. A prior parallel build/test lock failure is explicitly excluded from baseline evidence.",
    ),
    (
        "Baseline Tests",
        "Baseline test command `dotnet test \"HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj\" -v minimal --no-build` failed: 7875 total, 7862 passed, 11 failed, 2 skipped. The failures cluster around compiler facade ABI, matrix/tile open-vs-closed authority, CLMUL source anchors, and vector no-emission expectations.",
    ),
    (
        "Project Inventory",
        "The solution contains 29 `.csproj` files plus `HybridCPU v2.slnx`, `Directory.Build.props`, and `global.json`. Details are in `project-inventory.csv`.",
    ),
    (
        "Bundle Topology",
        "`BundleMetadata.BundleSlotCount = 8` (`HybridCPU_ISE/NonRTL/Core/Pipeline/Metadata/BundleMetadata.cs:19`) and `AbstractBundle.cs:7` describes `8 slots x 32 bytes = 256 bytes`; fetch uses a 256-byte buffer at `CPU_Core.PipelineExecution.StageFlow.cs:193`.",
    ),
    (
        "Carrier And Sideband",
        "`VliwBundleAnnotations.cs:10-16` carries sideband annotations, and `VliwBundleAnnotations.cs:45-63` builds `CoreBundleMetadata` only when explicit slot metadata exists. The raw carrier and scheduler metadata are therefore separate surfaces.",
    ),
    (
        "Slot Classes",
        "`SlotClassDefinitions.cs:17-35` defines ALU, LSU, DMA stream, branch, system, and unclassified classes. Lane masks at `SlotClassDefinitions.cs:173-196` map ALU to lanes 0-3, LSU to 4-5, DMA to 6, and branch/system to lane 7.",
    ),
    (
        "Lane 7 Alias",
        "`SlotClassDefinitions.cs:220-224` explicitly aliases `BranchControl` and `SystemSingleton` on lane 7. Hard-pinned system, trap, VMX, and SDC operations match this shape.",
    ),
    (
        "MicroOp Authority",
        "`MicroOp.cs:621` owns `Placement`; `MicroOp.cs:644-666` exposes flexible/hard-pinned placement helpers; `ApplyCanonicalDecodeProjection` at `MicroOp.cs:623-642` can overwrite class, serialization, placement, and memory predicates from decode projection.",
    ),
    (
        "Admission Metadata",
        "`MicroOpAdmissionMetadata.cs:14-30` captures memory/control/register/placement facts and `MicroOpAdmissionMetadata.cs:98-103` copies `IsMemoryOp` and `Placement` directly from the materialized micro-op. Bad micro-op taxonomy therefore propagates into scheduling evidence.",
    ),
    (
        "Instruction Class Taxonomy",
        "`InstructionClass.cs:8-33` has ISA-level classes including `Memory`, `System`, `SmtVt`, and `Vmx`. This is not equivalent to physical slot class; the audit found several places where those concepts are conflated.",
    ),
    (
        "IR Taxonomy",
        "`IrEnums.cs:24-33` defines compiler resource classes including `VectorAlu`, `LoadStore`, `DmaStream`, and `System`. The compiler/runtime bridge needs explicit mapping to `SlotClass`, not inference from names.",
    ),
    (
        "Opcode Registry",
        "`OpcodeInfo.Registry.Helpers.cs:64-80` publishes semantics from `OpcodeRegistry.Opcodes`. `OpcodeInfo.Registry.Data.Vector.cs:69-74` currently publishes VLOAD, VSTORE, and MTILE rows.",
    ),
    (
        "Classifier Defaults",
        "`InstructionClassifier.cs:26-40` consults the registry first, but `InstructionClassifier.cs:233-234` defaults unlisted instruction classes to `ScalarAlu`, and `InstructionClassifier.cs:373-374` defaults serialization to `Free`. This is a latent fail-open classifier path for classifier-only consumers.",
    ),
    (
        "Decoder Chain",
        "`DecodedBundleTransportProjector.cs:343-351` calls `InstructionRegistry.CreateMicroOp` and then applies canonical projection when required. This makes `InstructionRegistry` and concrete micro-op metadata the decisive runtime authority.",
    ),
    (
        "InstructionRegistry Chain",
        "`InstructionRegistry.Runtime.cs:70-87` applies registered factories/descriptors; `InstructionRegistry.Runtime.cs:106-107` throws for unknown opcodes. Registry materialization is fail-closed, but descriptor overrides can still preserve inconsistent placement/memory facts.",
    ),
    (
        "VLOAD VSTORE",
        "`InstructionRegistry.Initialize.Vector.cs:21-24` says legacy transfer opcodes now materialize concrete micro-ops. The factory at `InstructionRegistry.Helpers.Vector.cs:162-180` returns `VectorTransferMicroOp`; the class at `VectorMicroOps.Data.cs:727-755` sets memory ranges but `IsMemoryOp=false`, and inherits ALU placement from `VectorMicroOps.cs:48-58`.",
    ),
    (
        "Vector Segment Memory",
        "`LoadSegmentMicroOp`, `Load2DMicroOp`, `StoreSegmentMicroOp`, and `Store2DMicroOp` inherit ALU placement from the base vector class while executing memory through `MemorySubsystem` or `BurstIO` (`VectorMicroOps.Memory.cs:18-158,722-876`). Store segment/2D can return success with no store buffer.",
    ),
    (
        "Indexed Vector Memory",
        "`GatherMicroOp` and `StoreScatterMicroOp` are the good reference: constructors set `Class=MicroOpClass.Lsu`, `InstructionClass.Memory`, and `SetClassFlexiblePlacement(SlotClass.LsuClass)` (`VectorMicroOps.Memory.cs:168-190,884-907`).",
    ),
    (
        "IsMemoryOp Overload",
        "The same boolean means different things: `VectorTransferMicroOp` and indexed vector memory can publish memory ranges with `IsMemoryOp=false`, while DSC uses `IsMemoryOp=true` for lane6 descriptor runtime. This should be split into footprint/effect/ordering/slot-owner predicates.",
    ),
    (
        "Lane 6 DSC",
        "`DmaStreamComputeMicroOp.cs:42-53` hard-pins DSC to `DmaStreamClass` lane 6, sets `InstructionClass.Memory`, and marks side effects. Runtime execution and retire commit are separated at `DmaStreamComputeMicroOp.cs:121-149`. This is a confirmed-good anchor.",
    ),
    (
        "DSC Guarding",
        "`DmaStreamComputeDescriptorParser.cs:139-204` fail-closes unguarded DSC1 parsing and requires guard-plane/owner evidence for guarded parse. This is the pattern to preserve while refactoring memory taxonomy.",
    ),
    (
        "Lane 7 SDC",
        "`SystemDeviceCommandMicroOp.cs:57-67` pins SDC to lane 7 system singleton. `SystemDeviceCommandMicroOp.cs:196-255` contains fail-closed paths for descriptorless submit and guest-lane compatibility. This is another confirmed-good anchor.",
    ),
    (
        "VMX",
        "`MicroOp.IO.cs:139-164` defines `VmxMicroOp` as `InstructionClass.Vmx`, `SerializationClass.VmxSerial`, hard-pinned to lane 7. VMX tests assert registry/classifier/materializer consistency.",
    ),
    (
        "Matrix Tile Registry",
        "`InstructionRegistry.Helpers.MatrixTile.cs:12-15` registers MTILE_LOAD, MTILE_STORE, MTILE_MACC, and MTRANSPOSE; `InstructionRegistry.Helpers.MatrixTile.cs:53-72` publishes descriptors; `InstructionRegistry.Helpers.MatrixTile.cs:142-147` creates typed MTILE micro-ops.",
    ),
    (
        "Matrix Tile Placement",
        "`MatrixTileMicroOps.cs:71` sets ALU flexible placement for every matrix/tile micro-op; `MatrixTileMicroOps.cs:128-143` later marks MTILE_LOAD/STORE as `InstructionClass.Memory`, `IsMemoryOp=true`, and builds memory resources. This is the central matrix/tile authority conflict.",
    ),
    (
        "Matrix Tile Tests",
        "Positive retire/golden tests prove the contour is open (`Phase10MatrixTileRetirePublicationTests.cs:18-76`, `Phase12MatrixTilePositiveGoldenArtifactTests.cs:129-144`), while baseline fail-closed tests still expect traps for MTILE rows. One side must become authoritative.",
    ),
    (
        "Compiler Facade",
        "`IAppAsmFacade.cs:96-113` and `AppAsmFacade.cs:684-721` publish matrix helper methods. `CompilerFacadeAbiPhase01Tests.cs:288-354` expects the old phase-01 inventory; `Phase12FacadeFamilyDeprecationTests.cs:60-79` forbids new production mentions outside facade boundaries.",
    ),
    (
        "Compiler Positive Emission",
        "`ThreadCompilerContext.MatrixTile.cs:10-62` contains compile helpers; `CompilerMatrixTilePositiveEmissionAbiContract.cs:282-300` names them as positive emission helpers. This conflicts with older no-emission assumptions but may represent intended Phase13 handoff.",
    ),
    (
        "CLMUL Anchor Drift",
        "The failed CLMUL test expects the compiler source under inspection to contain `CompilerDeferredScalarAbiContract`; the contract type exists in `CompilerDeferredScalarAbiContract.cs:16-318`. This looks like test-source selection drift rather than a missing contract.",
    ),
    (
        "Skipped Tests",
        "`Phase03CarrierProjectionTransportTailTests.cs:611,640` skip VLOAD/VSTORE tests claiming they are absent from OpcodeRegistry, but `OpcodeInfo.Registry.Data.Vector.cs:69-70` now registers them. These skips are stale.",
    ),
    (
        "Reachability",
        "Curated opcode-to-runtime reachability is in `reachability-matrix.csv`. The highest-risk rows are VLOAD/VSTORE, segment/2D vector memory, and MTILE_LOAD/STORE.",
    ),
    (
        "Coverage",
        "Coverage summary is in `test-coverage-matrix.csv`. The suite is large and mostly passing, but the failing and skipped tests sit exactly on architecture-boundary truth claims.",
    ),
    (
        "Findings",
        "`findings.csv` contains 10 rows: 3 high active findings, 4 medium active/latent findings, 1 SDK reproducibility issue, and 1 confirmed-good anchor row for Lane6/Lane7/VMX.",
    ),
    (
        "Refactoring Plan",
        "`refactoring-phases.md` proposes P0-P7: freeze baseline, add invariant guards, normalize vector memory, split memory semantics, decide matrix/tile authority, resolve compiler facade handoff, repair tests, and harden CI.",
    ),
    (
        "Residual Risk",
        "No production files were edited. The main residual risk is architectural ambiguity: whether vector/matrix memory operations should be LSU-owned, runtime-capture-owned, or explicitly exempted with tests and docs.",
    ),
]

assert len(sections) == 40

finding_table = "\n".join(
    f"| {row['id']} | {row['severity']} | {row['status']} | {row['title']} | {row['recommended_phase']} |"
    for row in findings
)
report = (
    "# HybridCPU ISE Deep Audit Report\n\n"
    f"Generated: {snapshot['generated_at_utc']}\n\n"
    "## Executive Summary\n\n"
    "Baseline build passes, but the test project fails on 11 architecture-boundary tests. "
    "The strongest active defects are vector-memory placement drift, hidden vector memory "
    "fallback/no-op paths, and a matrix/tile authority conflict between opened runtime/compiler "
    "surfaces and older fail-closed/facade ABI tests. Lane6 DSC, Lane7 SDC, and VMX are "
    "comparatively well anchored and should be preserved as reference patterns.\n\n"
    "## Findings Summary\n\n"
    "| ID | Severity | Status | Title | Phase |\n"
    "|---|---|---|---|---|\n"
    f"{finding_table}\n\n"
)

for index, (title, body) in enumerate(sections, start=1):
    report += f"## {index}. {title}\n\n{body}\n\n"

report += dedent(
    """
    ## Appendix A - Failed Baseline Tests

    """
)
report += "\n".join(f"- {item}" for item in failed_tests) + "\n\n"
report += "## Appendix B - Skipped Baseline Tests\n\n"
report += "\n".join(f"- {item}" for item in skipped_tests) + "\n"


write_json(OUT / "audit-snapshot.json", snapshot)
write_json(PLAN / "audit-snapshot.json", snapshot)
write_text(OUT / "environment.txt", environment)
write_text(PLAN / "environment.txt", environment)
write_csv(OUT / "findings.csv", findings)
write_csv(PLAN / "findings.csv", findings)
write_csv(OUT / "reachability-matrix.csv", reachability_rows)
write_csv(PLAN / "reachability-matrix.csv", reachability_rows)
write_csv(OUT / "test-coverage-matrix.csv", coverage_rows)
write_csv(PLAN / "test-coverage-matrix.csv", coverage_rows)
write_csv(OUT / "file-change-map.csv", file_change_rows)
write_csv(PLAN / "file-change-map.csv", file_change_rows)
write_text(OUT / "refactoring-phases.md", refactoring_phases)
write_text(PLAN / "refactoring-phases.md", refactoring_phases)
write_text(OUT / "open-evidence-gaps.md", open_gaps)
write_text(PLAN / "open-evidence-gaps.md", open_gaps)
write_text(OUT / "audit-report.md", report)
write_text(PLAN / "audit-report.md", report)

print("Wrote final audit artifacts:")
for path in [
    OUT / "audit-report.md",
    OUT / "audit-snapshot.json",
    OUT / "environment.txt",
    OUT / "findings.csv",
    OUT / "reachability-matrix.csv",
    OUT / "test-coverage-matrix.csv",
    OUT / "file-change-map.csv",
    OUT / "refactoring-phases.md",
    OUT / "open-evidence-gaps.md",
    PLAN / "audit-report.md",
    PLAN / "refactoring-phases.md",
]:
    print(path)
