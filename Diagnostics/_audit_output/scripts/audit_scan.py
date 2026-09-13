from __future__ import annotations

import csv
import hashlib
import json
import os
import platform
import re
import sys
from collections import Counter, defaultdict
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable, Iterator


ROOT = Path(r"C:\Users\Yuriy Kurnosov\Desktop\HybridCPU ISE")
OUT = ROOT / "_audit_output"

EXCLUDED_DIR_NAMES = {
    ".git",
    ".vs",
    ".idea",
    "bin",
    "obj",
    "TestResults",
    "coverage",
    "packages",
    "node_modules",
}

INCLUDE_EXTS = {".cs", ".csproj", ".sln", ".slnx", ".props", ".targets", ".json", ".xml", ".md", ".txt", ".yml", ".yaml"}

SYMBOL_PATTERNS = {
    "SlotClass": re.compile(r"\b(?:enum|struct|class)\s+SlotClass\b"),
    "InstructionClass": re.compile(r"\b(?:enum|struct|class)\s+InstructionClass\b"),
    "MicroOpClass": re.compile(r"\b(?:enum|struct|class)\s+MicroOpClass\b"),
    "IrResourceClass": re.compile(r"\b(?:enum|struct|class)\s+IrResourceClass\b"),
    "BindingKind": re.compile(r"\b(?:enum|struct|class)\s+BindingKind\b"),
    "InstructionClassifier": re.compile(r"\bclass\s+InstructionClassifier\b"),
    "InstructionEncoder": re.compile(r"\bclass\s+InstructionEncoder\b"),
    "InstructionDecoder": re.compile(r"\bclass\s+.*Decoder\b"),
    "InstructionIR": re.compile(r"\b(?:class|struct|record)\s+InstructionIR\b"),
    "BundleMetadata": re.compile(r"\b(?:class|struct|record)\s+BundleMetadata\b"),
    "SlotMetadata": re.compile(r"\b(?:class|struct|record)\s+SlotMetadata\b"),
    "MicroOp": re.compile(r"\b(?:class|struct|record)\s+MicroOp\b"),
    "VectorMicroOp": re.compile(r"\b(?:class|struct|record)\s+VectorMicroOp\b"),
    "VectorTransferMicroOp": re.compile(r"\b(?:class|struct|record)\s+VectorTransferMicroOp\b"),
    "GatherMicroOp": re.compile(r"\b(?:class|struct|record)\s+GatherMicroOp\b"),
    "StoreScatterMicroOp": re.compile(r"\b(?:class|struct|record)\s+StoreScatterMicroOp\b"),
    "DmaStreamComputeMicroOp": re.compile(r"\bclass\s+DmaStreamComputeMicroOp\b"),
    "SystemDeviceCommandMicroOp": re.compile(r"\bclass\s+SystemDeviceCommandMicroOp\b"),
    "StreamEngine": re.compile(r"\bclass\s+StreamEngine\b|\bStreamEngine\b"),
    "VectorALU": re.compile(r"\bVectorALU\b"),
    "BurstIO": re.compile(r"\bBurstIO\b"),
    "StreamRegisterFile": re.compile(r"\bStreamRegisterFile\b|\b\bSRF\b|\bSFR\b"),
    "MemorySubsystem": re.compile(r"\bMemorySubsystem\b"),
    "VMX": re.compile(r"\bVMX\b|\bVmx\b"),
    "ACCEL": re.compile(r"\bACCEL_[A-Z0-9_]+\b"),
}

OPCODE_FAMILIES = {
    "VLOAD": ["VLOAD"],
    "VSTORE": ["VSTORE"],
    "VGATHER": ["VGATHER"],
    "VSCATTER": ["VSCATTER"],
    "DMA_STREAM": ["DmaStreamCompute", "DmaStreamComputeMicroOp", "DmaStreamComputeDescriptorParser"],
    "LANE7_ACCEL": ["SystemDeviceCommandMicroOp", "ACCEL_", "SystemSingleton"],
    "VMX": ["VMX", "VmxMicroOp", "Vmx"],
    "VECTOR_COMPUTE": ["VectorMicroOp", "VectorALU", "AluClass"],
    "VECTOR_MEMORY": ["VectorTransferMicroOp", "GatherMicroOp", "StoreScatterMicroOp", "LsuClass", "Memory"],
}

REACHABILITY_TARGETS = {
    "legacy_vector_memory_carriers": ["VectorTransferMicroOp", "GatherMicroOp", "StoreScatterMicroOp", "LoadSegmentMicroOp", "StoreSegmentMicroOp"],
    "dsc_carriers": ["DmaStreamComputeMicroOp", "DmaStreamComputeDescriptorParser", "DmaStreamComputeRuntime"],
    "vmx_ops": ["VmxMicroOp", "VMXON", "VMXOFF", "VMLAUNCH", "VMRESUME", "VMREAD", "VMWRITE", "VMCLEAR", "VMPTRLD"],
    "legacy_accelerators": ["CustomAcceleratorMicroOp", "AcceleratorDescriptorParser", "FakeMatMulExternalAcceleratorBackend"],
    "stream_engine_paths": ["StreamEngine"],
    "compat_decoders": ["VliwCompatDecoder", "CompatDecoder", "Compatibility"],
    "direct_microop_constructors": ["new ", "=> new ", "return new "],
}

TEST_CATEGORIES = {
    "architecture-value": ["Assert.Equal(", "Expected architecture", "architecture-value"],
    "implementation-detail": ["typeof(", "private", "reflection", "Contains(", "DoesNotContain("],
    "self-consistency": ["actual_from_compiler", "actual_from_runtime", "roundtrip", "round-trip"],
    "golden snapshot": ["golden", "snapshot"],
    "negative/fail-closed": ["FailClosed", "fail closed", "FailClosed", "Throws", "Assert.Throws", "reject"],
    "reachability": ["reachable", "reachability", "reachability matrix"],
    "replay/rollback": ["Replay", "Rollback", "replay", "rollback"],
    "documentation guard": ["Documentation", "Docs", "commentary", "claim"],
}


@dataclass
class FileInfo:
    path: Path
    relpath: str
    ext: str
    category: str
    size: int
    sha256: str


def iter_files(root: Path) -> Iterator[Path]:
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if d not in EXCLUDED_DIR_NAMES]
        current = Path(dirpath)
        if current.name == "_audit_output":
            dirnames[:] = []
            continue
        for name in filenames:
            yield current / name


def categorize(path: Path) -> str:
    lower = str(path).lower()
    if "_audit_output" in lower:
        return "audit"
    if "\\tests\\" in lower or lower.endswith(".tests.csproj"):
        return "test"
    if path.suffix in {".sln", ".slnx", ".csproj", ".props", ".targets"}:
        return "project"
    if path.suffix in {".md", ".txt"}:
        return "doc"
    if path.suffix in {".json", ".xml", ".yml", ".yaml"}:
        return "config"
    if "\\generated\\" in lower or "\\generated\\" in lower or "generated" in path.parts[-2:].__str__():
        return "generated"
    if path.suffix == ".cs":
        return "source"
    return "other"


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def read_text(path: Path) -> str:
    try:
        return path.read_text(encoding="utf-8")
    except UnicodeDecodeError:
        return path.read_text(encoding="utf-8-sig", errors="replace")


def line_snippets(text: str, patterns: Iterable[re.Pattern[str]] | None = None, raw_substrings: Iterable[str] | None = None):
    lines = text.splitlines()
    for idx, line in enumerate(lines, start=1):
        matched = False
        if patterns:
            for pat in patterns:
                if pat.search(line):
                    matched = True
                    break
        if raw_substrings and not matched:
            for s in raw_substrings:
                if s in line:
                    matched = True
                    break
        if matched:
            yield idx, line.strip()


def project_references(csproj_text: str) -> list[str]:
    refs = []
    for m in re.finditer(r"<ProjectReference[^>]*Include=\"([^\"]+)\"", csproj_text, re.IGNORECASE):
        refs.append(m.group(1))
    return refs


def package_refs(csproj_text: str) -> list[str]:
    refs = []
    for m in re.finditer(r"<PackageReference[^>]*Include=\"([^\"]+)\"[^>]*Version=\"([^\"]+)\"", csproj_text, re.IGNORECASE):
        refs.append(f"{m.group(1)}:{m.group(2)}")
    return refs


def detect_test_category(text: str) -> str:
    score = Counter()
    for category, tokens in TEST_CATEGORIES.items():
        for token in tokens:
            if token in text:
                score[category] += 1
    if not score:
        return "unclassified"
    return score.most_common(1)[0][0]


def classify_symbol_usage(symbol: str, path: str, line: str) -> str:
    if re.search(rf"\b(enum|class|struct|record)\s+{re.escape(symbol)}\b", line):
        return "definition"
    if "=>" in line or "new " in line or symbol in line:
        return "usage"
    return "mention"


def main() -> int:
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / "scripts").mkdir(parents=True, exist_ok=True)

    files: list[FileInfo] = []
    excluded_records: list[str] = []
    for path in iter_files(ROOT):
        rel = path.relative_to(ROOT).as_posix()
        ext = path.suffix.lower()
        category = categorize(path)
        try:
            data = path.read_bytes()
        except Exception as ex:
            excluded_records.append(f"{rel}\tERROR\t{ex}")
            continue
        files.append(FileInfo(path=path, relpath=rel, ext=ext, category=category, size=len(data), sha256=sha256_bytes(data)))

    files.sort(key=lambda f: f.relpath.lower())

    # source manifest and inventory
    with (OUT / "source-inventory.csv").open("w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        w.writerow(["relpath", "category", "ext", "size_bytes", "sha256"])
        for info in files:
            if info.category in {"audit"}:
                continue
            w.writerow([info.relpath, info.category, info.ext, info.size, info.sha256])

    manifest_lines = []
    for info in files:
        if info.category in {"audit"}:
            continue
        manifest_lines.append(f"{info.sha256}  {info.relpath}")
    (OUT / "source-manifest.sha256").write_text("\n".join(manifest_lines) + "\n", encoding="utf-8")

    # project inventory
    project_rows = []
    for info in files:
        if info.ext in {".sln", ".slnx", ".csproj", ".props", ".targets"}:
            text = read_text(info.path)
            refs = project_references(text)
            packages = package_refs(text)
            project_rows.append(
                {
                    "relpath": info.relpath,
                    "kind": info.ext.lstrip("."),
                    "project_refs": ";".join(refs),
                    "package_refs": ";".join(packages),
                    "size_bytes": info.size,
                    "sha256": info.sha256,
                }
            )
    with (OUT / "project-inventory.csv").open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=["relpath", "kind", "project_refs", "package_refs", "size_bytes", "sha256"])
        w.writeheader()
        w.writerows(project_rows)

    # excluded paths
    excluded = sorted(EXCLUDED_DIR_NAMES)
    (OUT / "excluded-paths.txt").write_text("\n".join(excluded) + "\n", encoding="utf-8")

    # symbol inventory
    symbol_rows = []
    for info in files:
        if info.ext != ".cs":
            continue
        text = read_text(info.path)
        for symbol, pattern in SYMBOL_PATTERNS.items():
            for idx, line in line_snippets(text, patterns=[pattern]):
                symbol_rows.append(
                    {
                        "symbol": symbol,
                        "kind": classify_symbol_usage(symbol, info.relpath, line),
                        "relpath": info.relpath,
                        "line": idx,
                        "snippet": line[:300],
                    }
                )
    with (OUT / "symbol-inventory.csv").open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=["symbol", "kind", "relpath", "line", "snippet"])
        w.writeheader()
        w.writerows(symbol_rows)

    # opcode/runtime evidence matrix
    op_rows = []
    for family, needles in OPCODE_FAMILIES.items():
        for info in files:
            if info.ext != ".cs":
                continue
            text = read_text(info.path)
            lines = text.splitlines()
            for idx, line in enumerate(lines, start=1):
                if any(needle in line for needle in needles):
                    op_rows.append(
                        {
                            "family": family,
                            "relpath": info.relpath,
                            "line": idx,
                            "evidence": line.strip()[:240],
                        }
                    )
    with (OUT / "opcode-runtime-matrix.csv").open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=["family", "relpath", "line", "evidence"])
        w.writeheader()
        w.writerows(op_rows)

    # reachability matrix
    reach_rows = []
    for target, needles in REACHABILITY_TARGETS.items():
        rows = []
        for info in files:
            if info.ext != ".cs":
                continue
            text = read_text(info.path)
            for idx, line in line_snippets(text, raw_substrings=needles):
                rows.append((info.relpath, idx, line))
        if not rows:
            reach_rows.append({"target": target, "status": "Unproven", "evidence": "", "notes": "no match"})
        else:
            # heuristic classification
            prod = any("/CloseToRTL/" in r[0] or "/NonRTL/" in r[0] or "HybridCPU_ISE/" in r[0] for r in rows)
            test = any(".Tests/" in r[0] for r in rows)
            status = "Production reachable" if prod and not test else ("Test-only" if test and not prod else "Reflection-reachable" if any("Activator" in r[2] or "GetType(" in r[2] for r in rows) else "Unproven")
            reach_rows.append(
                {
                    "target": target,
                    "status": status,
                    "evidence": " | ".join(f"{r[0]}:{r[1]}:{r[2][:120]}" for r in rows[:10]),
                    "notes": f"matches={len(rows)}",
                }
            )
    with (OUT / "reachability-matrix.csv").open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=["target", "status", "evidence", "notes"])
        w.writeheader()
        w.writerows(reach_rows)

    # test coverage matrix
    test_rows = []
    for info in files:
        if info.category != "test" or info.ext != ".cs":
            continue
        text = read_text(info.path)
        category = detect_test_category(text)
        tests = len(re.findall(r"\b(Fact|Theory|TestMethod|TestCase)\b", text))
        test_rows.append(
            {
                "relpath": info.relpath,
                "category": category,
                "test_count_hint": tests,
                "skip_count": text.count("Skip ="),
                "assert_equal_count": text.count("Assert.Equal("),
                "assert_true_count": text.count("Assert.True("),
                "assert_false_count": text.count("Assert.False("),
                "throws_count": text.count("Throws"),
            }
        )
    with (OUT / "test-coverage-matrix.csv").open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(
            f,
            fieldnames=[
                "relpath",
                "category",
                "test_count_hint",
                "skip_count",
                "assert_equal_count",
                "assert_true_count",
                "assert_false_count",
                "throws_count",
            ],
        )
        w.writeheader()
        w.writerows(test_rows)

    # file change map placeholder with baseline suggestion derived from categories
    change_rows = []
    for info in files:
        if info.category in {"source", "generated"} and info.ext == ".cs":
            change_rows.append(
                {
                    "relpath": info.relpath,
                    "symbol": "",
                    "problem": "",
                    "required_action": "review",
                }
            )
    with (OUT / "file-change-map.csv").open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=["relpath", "symbol", "problem", "required_action"])
        w.writeheader()
        w.writerows(change_rows)

    # open evidence gaps starter
    gaps = [
        "- Need exact build and test results after dotnet build/test baseline.",
        "- Need targeted line-level inspection for vector memory, lane6 DSC, lane7 ACCEL, and VMX contour reachability.",
        "- Need explicit confirmation of generated artifact provenance for any golden files used as evidence.",
    ]
    (OUT / "open-evidence-gaps.md").write_text("\n".join(gaps) + "\n", encoding="utf-8")

    # scan summary
    summary = {
        "root": str(ROOT),
        "total_files": len(files),
        "by_category": Counter(info.category for info in files),
        "by_ext": Counter(info.ext for info in files),
        "python": sys.version,
        "platform": platform.platform(),
    }
    (OUT / "scan-summary.json").write_text(json.dumps(summary, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")

    print(json.dumps(summary, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
