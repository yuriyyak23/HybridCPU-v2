using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using YAKSys_Hybrid_CPU.Core.Registers;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.4 decision-only inventory guards. These tests freeze the existing
/// checked VtId and raw pipeline/scoreboard identity contour without approving
/// a migration or changing any invalid-input behavior.
/// </summary>
public sealed class Rf124VtPipelineScoreboardIdentityInventoryTests
{
    private const string ThisFile =
        "Rf124VtPipelineScoreboardIdentityInventoryTests.cs";
    private const string Rf124aGuardFile =
        "Rf124aVtRoleContourArchitectureDecisionTests.cs";
    private const string Rf124bGuardFile =
        "Rf124bMicroOpOwnerVtProjectionValidInputContractTests.cs";
    private const string Rf124cGuardFile =
        "Rf124cAtomicExecuteOwnerVtInventoryDecisionTests.cs";
    private const string Rf124dGuardFile =
        "Rf124dAtomicExecuteOwnerVtValidInputCutoverTests.cs";
    private const string Rf124eGuardFile =
        "Rf124eScalarAluExecuteOwnerVtInventoryDecisionTests.cs";
    private const string Rf124fGuardFile =
        "Rf124fScalarAluExecuteOwnerVtValidInputCutoverTests.cs";
    private const string Rf124gGuardFile =
        "Rf124gScalarAluRetireOwnerVtInventoryDecisionTests.cs";
    private const string Rf124hGuardFile =
        "Rf124hScalarAluRetireOwnerVtValidInputCutoverTests.cs";
    private const string Rf124iGuardFile =
        "Rf124iBranchRetireOwnerVtInventoryDecisionTests.cs";
    private const string Rf124jGuardFile =
        "Rf124jBranchRetireOwnerVtValidInputCutoverTests.cs";
    private const string Rf124kGuardFile =
        "Rf124kLoadRetireOwnerVtInventoryDecisionTests.cs";
    private const string Rf124lGuardFile =
        "Rf124lLoadRetireOwnerVtValidInputCutoverTests.cs";
    private const string Rf124mGuardFile =
        "Rf124mCsrRetireOwnerVtInventoryDecisionTests.cs";
    private const string Rf124nGuardFile =
        "Rf124nCsrRetireOwnerVtValidInputCutoverTests.cs";
    private const string Rf124oGuardFile =
        "Rf124oCsrExecutionSourceOwnerVtInventoryDecisionTests.cs";
    private const string Rf124pGuardFile =
        "Rf124pCsrExecutionSourceOwnerVtValidInputCutoverTests.cs";

    private const string FamilyPattern =
        @"\b(?:VtId|VtID|VirtualThreadId|VirtualThreadID|OwnerThreadId|OwnerVirtualThreadId|CarrierVirtualThreadId|DonorVirtualThreadId|TargetVirtualThreadId|SourceVirtualThreadId|ActiveVirtualThreadId|OriginalThreadId|ThreadId)\b";


    [Fact]
    public void CheckedSurfaceAndTypedCallerManifestRemainFrozen()
    {
        Assert.Equal(0, VtId.MinValue);
        Assert.Equal(3, VtId.MaxValue);
        Assert.Equal(4, VtId.SmtWayCount);
        Assert.Equal(typeof(byte), typeof(VtId).GetProperty(nameof(VtId.Value))!.PropertyType);
        Assert.Single(typeof(VtId).GetConstructors(BindingFlags.Public |
            BindingFlags.Instance | BindingFlags.DeclaredOnly));
        Assert.NotNull(typeof(VtId).GetMethod(nameof(VtId.Create),
            BindingFlags.Public | BindingFlags.Static, [typeof(int)]));
        Assert.NotNull(typeof(VtId).GetMethod(nameof(VtId.TryCreate),
            BindingFlags.Public | BindingFlags.Static,
            [typeof(int), typeof(VtId).MakeByRefType()]));
        Assert.NotNull(typeof(VtId).GetMethod(nameof(VtId.FromRawValue),
            BindingFlags.Public | BindingFlags.Static, [typeof(byte)]));

        string root = FindRepositoryRoot();
        Dictionary<string, int> createCallers = FindCallers(root,
            @"\bVtId\.Create\s*\(");
        Assert.Equal(new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["HybridCPU_Compiler/API/Threading/HybridCpuThreadCompilerContext.cs"] = 1,
            ["HybridCPU_Compiler/Core/IR/Bundling/HybridCpuBundleLowerer.cs"] = 1,
            ["HybridCPU_ISE/CloseToHSL/Core/Contracts/CompilerTransport/InstructionSlotMetadata.cs"] = 1,
            ["HybridCPU_ISE/CloseToHSL/Core/Pipeline/Core/CPU_Core.TestSupport.cs"] = 1,
            ["TestAssemblerConsoleApps/SimpleAsmApp.cs"] = 1,
            ["TestAssemblerConsoleApps/SimpleAsmApp.Emit.cs"] = 1
        }, createCallers);
        Assert.Equal(new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["HybridCPU_ISE/CloseToHSL/Core/Architecture/ExceptionsAndTraps/InvalidVirtualThreadException.cs"] = 1,
            ["HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Types/MicroOp.cs"] = 1
        }, FindCallers(root, @"\bVtId\.TryCreate\s*\("));
        Assert.Empty(FindCallers(root, @"\bVtId\.FromRawValue\s*\("));
        Assert.Empty(FindCallers(root, @"\bVtId\.ToRawValue\s*\("));
        Assert.Empty(FindCallers(root, @"\bVtId\.IsRepresentable\s*\("));
    }

    [Fact]
    public void RawStorageWireAndAbsenceSeamsRemainFrozen()
    {
        string root = FindRepositoryRoot();
        string microOp = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Types", "MicroOp.cs");
        string packed = Read(root, "HybridCPU_ISE", "NonRTL", "Arch", "Compat",
            "VLIW_Instruction.Layout.cs");
        string sideband = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Contracts", "CompilerTransport", "InstructionSlotMetadata.cs");
        string ir = Read(root, "HybridCPU_Compiler", "Core", "IR", "Model",
            "IrSlotMetadata.cs");
        string former = Read(root, "HybridCPU_Compiler", "Core", "IR",
            "Bundling", "HybridCpuBundleFormer.cs");
        string descriptor = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Decoder", "DecodedBundleDescriptor.cs");
        string diagnostic = Read(root, "TestAssemblerConsoleApps",
            "DiagnosticExecutionCheckpoint.cs");

        Assert.Contains("public int OwnerThreadId { get; set; } = 0", microOp,
            StringComparison.Ordinal);
        Assert.Contains("public int VirtualThreadId { get; set; } = 0", microOp,
            StringComparison.Ordinal);
        Assert.Contains("[49:48] VirtualThreadId (2 bits, 0-3)", packed,
            StringComparison.Ordinal);
        Assert.Contains("if (value > 3)", packed, StringComparison.Ordinal);
        Assert.Contains("VtId VirtualThreadId", sideband, StringComparison.Ordinal);
        Assert.Contains("VtId.Create(0)", sideband, StringComparison.Ordinal);
        Assert.Contains("byte VirtualThreadId", ir, StringComparison.Ordinal);
        Assert.Contains("VirtualThreadId { get; set; } = -1", former,
            StringComparison.Ordinal);
        Assert.Contains("microOp?.VirtualThreadId ?? 0", descriptor,
            StringComparison.Ordinal);
        Assert.Contains("microOp?.OwnerThreadId ?? 0", descriptor,
            StringComparison.Ordinal);
        Assert.Contains("trapMicroOp.VirtualThreadId != 0", descriptor,
            StringComparison.Ordinal);
        Assert.Contains("trapMicroOp.OwnerThreadId != 0", descriptor,
            StringComparison.Ordinal);
        Assert.Contains("int? ActiveVirtualThreadId", diagnostic,
            StringComparison.Ordinal);
    }

    [Fact]
    public void IndexMaskDictionaryReplayAndTelemetryConsumersRemainFrozen()
    {
        string root = FindRepositoryRoot();
        string state = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Architecture", "State", "Architectural", "CPU_Core.StateData.cs");
        string context = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "ExecutionFlow", "StageFlow",
            "CPU_Core.PipelineExecution.Context.cs");
        string fsp = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Scheduling", "Fsp", "CPU_Core.PipelineExecution.Fsp.cs");
        string atomic = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Execution", "Memory", "AtomicMemory", "AtomicMemoryUnit.cs");
        string lane7 = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Runtime", "Lanes", "Lane7", "Lane7StateBlock.cs");
        string lane6 = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Runtime", "Lanes", "Lane6", "Lane6QueueRuntime.cs");
        string replay = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Diagnostics", "ReplaySnapshot.cs");
        string telemetry = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Diagnostics", "TelemetryCounters.cs");

        Assert.Contains("ActiveVirtualThreadId = 0", state, StringComparison.Ordinal);
        Assert.Contains("ResolveLiveStateVtIdOrThrow", state, StringComparison.Ordinal);
        Assert.Contains("NormalizePipelineStateVtId", context, StringComparison.Ordinal);
        Assert.Contains("NormalizePipelineStateVtId(ownerVirtualThreadId)", fsp,
            StringComparison.Ordinal);
        Assert.Contains("CoreId, int VirtualThreadId", atomic, StringComparison.Ordinal);
        Assert.Contains("OwnerVirtualThreadId", lane7, StringComparison.Ordinal);
        Assert.Contains("ushort VtId", lane6, StringComparison.Ordinal);
        Assert.Contains("required byte VtId", replay, StringComparison.Ordinal);
        Assert.Contains("public const int VtCount = 4", telemetry,
            StringComparison.Ordinal);
        Assert.Contains("vtId < VtCount ? _instrCountPerVt[vtId]   : 0",
            telemetry, StringComparison.Ordinal);

        string production = JoinSources(Path.Combine(root, "HybridCPU_ISE"));
        Assert.Contains("1 << vt", production, StringComparison.Ordinal);
        Assert.Contains("[OwnerThreadId]", production, StringComparison.Ordinal);
        Assert.Contains("OriginalThreadId", production, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidAliasesClampsAndThrowingNormalizersRemainSeparate()
    {
        string root = FindRepositoryRoot();
        string production = JoinSources(Path.Combine(root, "HybridCPU_ISE"),
            path => !HasPathSegment(path, "Legacy"));
        Assert.Equal(3, Regex.Matches(production,
            @"Math\.Clamp\(OwnerThreadId,\s*0,\s*Processor\.CPU_Core\.SmtWays\s*-\s*1\)")
            .Count);
        Assert.Equal(1, Regex.Matches(production,
            @"Math\.Clamp\(seed\.OwnerThreadId,\s*0,\s*Processor\.CPU_Core\.SmtWays\s*-\s*1\)")
            .Count);

        string microOp = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Types", "MicroOp.cs");
        string context = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "ExecutionFlow", "StageFlow",
            "CPU_Core.PipelineExecution.Context.cs");
        string summary = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Decoder", "ClusterIssuePreparation.cs");

        Assert.Contains("NormalizeExecutionVtId", microOp, StringComparison.Ordinal);
        Assert.Contains("ArgumentOutOfRangeException", microOp,
            StringComparison.Ordinal);
        Assert.Contains("NormalizePipelineStateVtId", context,
            StringComparison.Ordinal);
        Assert.Contains("ArgumentOutOfRangeException", context,
            StringComparison.Ordinal);
        Assert.Contains("_ => 0", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void PaperDefinesVtFamilyAndNowOwnsTheFrozenRawContourDecision()
    {
        string root = FindRepositoryRoot();
        string paper = Read(root, "ResearchPaper", "section", "md base",
            "3_Architectural_Overview_and_Frontend_Contract.md");

        Assert.Contains("The existing `VtId` is the sole SMT identity", paper,
            StringComparison.Ordinal);
        Assert.Contains("Zero is valid VT0", paper, StringComparison.Ordinal);
        Assert.Contains("Absence requires an outer discriminator",
            paper, StringComparison.Ordinal);
        Assert.Contains("Clamp, modulo,\n   zero substitution",
            paper, StringComparison.Ordinal);
        Assert.Contains("trapMicroOp.VirtualThreadId == 0", paper,
            StringComparison.Ordinal);
        Assert.Contains("HybridCpuBundleFormer.VirtualThreadId == -1", paper,
            StringComparison.Ordinal);
        Assert.Contains("#### 3.7.16 SMT virtual-thread role contours and migration boundary",
            paper, StringComparison.Ordinal);
    }

    private static InventoryFingerprint CaptureRoot(string root, string sourceRoot)
    {
        var regex = new Regex(FamilyPattern, RegexOptions.CultureInvariant);
        var entries = new List<string>();
        string absoluteRoot = Path.Combine(root, sourceRoot);
        foreach (string path in EnumerateSources(absoluteRoot)
                     .Where(path => !path.EndsWith(ThisFile,
                         StringComparison.OrdinalIgnoreCase))
                     .Where(path => !path.EndsWith(Rf124aGuardFile,
                         StringComparison.OrdinalIgnoreCase))
                     .Where(path => !path.EndsWith(Rf124bGuardFile,
                         StringComparison.OrdinalIgnoreCase))
                     .Where(path => !path.EndsWith(Rf124cGuardFile,
                         StringComparison.OrdinalIgnoreCase))
                     .Where(path => !path.EndsWith(Rf124dGuardFile,
                         StringComparison.OrdinalIgnoreCase))
                     .Where(path => !path.EndsWith(Rf124eGuardFile,
                         StringComparison.OrdinalIgnoreCase))
                     .Where(path => !path.EndsWith(Rf124fGuardFile,
                         StringComparison.OrdinalIgnoreCase))
                     .Where(path => !path.EndsWith(Rf124gGuardFile,
                         StringComparison.OrdinalIgnoreCase))
                     .Where(path => !path.EndsWith(Rf124hGuardFile,
                         StringComparison.OrdinalIgnoreCase))
                     .Where(path => !path.EndsWith(Rf124iGuardFile,
                         StringComparison.OrdinalIgnoreCase))
                     .Where(path => !path.EndsWith(Rf124jGuardFile,
                         StringComparison.OrdinalIgnoreCase))
                     .Where(path => !path.EndsWith(Rf124kGuardFile,
                         StringComparison.OrdinalIgnoreCase))
                     .Where(path => !path.EndsWith(Rf124lGuardFile,
                         StringComparison.OrdinalIgnoreCase))
                     .Where(path => !path.EndsWith(Rf124mGuardFile,
                         StringComparison.OrdinalIgnoreCase))
                     .Where(path => !path.EndsWith(Rf124nGuardFile,
                         StringComparison.OrdinalIgnoreCase))
                     .Where(path => !path.EndsWith(Rf124oGuardFile,
                         StringComparison.OrdinalIgnoreCase))
                     .Where(path => !path.EndsWith(Rf124pGuardFile,
                         StringComparison.OrdinalIgnoreCase)))
        {
            string relative = Path.GetRelativePath(root, path)
                .Replace('\\', '/');
            foreach (string line in File.ReadLines(path))
            {
                int count = regex.Matches(line).Count;
                for (int occurrence = 0; occurrence < count; occurrence++)
                {
                    entries.Add($"{relative}:{line.Trim()}");
                }
            }
        }

        entries.Sort(StringComparer.Ordinal);
        string joined = string.Join("\n", entries);
        string sha256 = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(joined)))
            .ToLowerInvariant();
        return new InventoryFingerprint(entries.Count, sha256);
    }

    private static Dictionary<string, int> FindCallers(string root, string pattern)
    {
        var regex = new Regex(pattern, RegexOptions.CultureInvariant);
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (string sourceRoot in new[]
                 {
                     "HybridCPU_ISE", "HybridCPU_Compiler",
                     "TestAssemblerConsoleApps"
                 })
        {
            string absoluteRoot = Path.Combine(root, sourceRoot);
            foreach (string path in EnumerateSources(absoluteRoot)
                         .Where(path => !HasPathSegment(path, "Legacy")))
            {
                int count = regex.Matches(File.ReadAllText(path)).Count;
                if (count != 0)
                {
                    result[Path.GetRelativePath(root, path).Replace('\\', '/')] =
                        count;
                }
            }
        }

        return result;
    }

    private static string JoinSources(string root,
        Func<string, bool>? include = null) =>
        string.Join("\n", EnumerateSources(root)
            .Where(path => include?.Invoke(path) ?? true)
            .Select(File.ReadAllText));

    private static IEnumerable<string> EnumerateSources(string root) =>
        Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !HasPathSegment(path, "bin") &&
                           !HasPathSegment(path, "obj"))
            .OrderBy(path => path, StringComparer.Ordinal);

    private static bool HasPathSegment(string path, string segment) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Contains(segment, StringComparer.OrdinalIgnoreCase);

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(parts.Aggregate(root, Path.Combine));

    private static string FindRepositoryRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current, "HybridCPU v2.slnx")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }

    private readonly record struct InventoryFingerprint(int Count, string Sha256);
}
