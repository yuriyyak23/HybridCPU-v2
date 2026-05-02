using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HybridCPU.Compiler.Core.IR;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Backends;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class Ex1Phase12ConformanceMigrationTests
{
    private static readonly ForbiddenCurrentClaim[] ForbiddenCurrentClaims =
    {
        new(
            "executable lane6 DSC",
            new Regex(
                @"\b(?:lane6|DmaStreamComputeMicroOp|DSC)\b.*\b(?:executable|executes|execution enabled)\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)),
        new(
            "async DMA overlap",
            new Regex(
                @"\basync\b.*\b(?:DMA|DmaStreamCompute)\b.*\boverlap\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)),
        new(
            "IOMMU-translated DSC execution",
            new Regex(
                @"\b(?:DSC|DmaStreamCompute)\b.*\bIOMMU(?:-translated| translated)?\b.*\b(?:execution|runtime|memory|access|integrat(?:ed|ion))\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)),
        new(
            "executable L7 ACCEL",
            new Regex(
                @"\b(?:L7|ACCEL_\*)\b.*\b(?:executable|executes|execution enabled)\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)),
        new(
            "QUERY_CAPS/POLL architectural writeback",
            new Regex(
                @"\b(?:QUERY_CAPS|POLL)\b.*\b(?:writeback|writes?|rd|architectural register)\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)),
        new(
            "fake/test backend as production protocol",
            new Regex(
                @"\b(?:fake|test)\b.*\bbackend\b.*\bproduction\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)),
        new(
            "global conflict authority installed",
            new Regex(
                @"\bglobal\b.*\b(?:conflict|CPU load/store)\b.*\b(?:installed|mandatory|authority|hook)\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)),
        new(
            "coherent DMA/cache",
            new Regex(
                @"\b(?:coherent|coherency)\b.*\b(?:DMA|cache)\b|\bDMA/cache\b.*\bcoherent\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)),
        new(
            "production executable compiler lowering",
            new Regex(
                @"\bproduction\b.*\b(?:executable\s+)?lower(?:ing)?\b.*\b(?:allowed|available|enabled|may|can)\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)),
        new(
            "successful partial completion",
            new Regex(
                @"\bsuccessful\b.*\bpartial completion\b|\bpartial completion\b.*\bsuccessful\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
    };

    [Fact]
    public void Phase12_EveryEx1PhaseHasMappedConformanceCategory()
    {
        string phase12 = ReadRepoFile(
            "Documentation/Refactoring/Phases Ex1/12_Testing_Conformance_And_Documentation_Migration.md");
        string[] phaseDocs = Directory
            .EnumerateFiles(PhaseEx1Root(), "*.md", SearchOption.TopDirectoryOnly)
            .Where(static path =>
                Regex.IsMatch(Path.GetFileName(path), @"^(?:\d{2}|ADR_\d{2})_", RegexOptions.CultureInvariant))
            .Where(static path => Regex.IsMatch(
                Path.GetFileName(path),
                @"^(?:\d{2})_",
                RegexOptions.CultureInvariant))
            .OrderBy(static path => Path.GetFileName(path), StringComparer.Ordinal)
            .ToArray();

        Assert.Contains(phaseDocs, static path => Path.GetFileName(path).StartsWith("00_", StringComparison.Ordinal));
        Assert.Contains(phaseDocs, static path => Path.GetFileName(path).StartsWith("13_", StringComparison.Ordinal));

        var missingRows = new List<string>();
        foreach (string phaseDoc in phaseDocs)
        {
            string phaseId = Path.GetFileName(phaseDoc)[..2];
            Regex rowPattern = new(
                @"^\|\s*" + Regex.Escape(phaseId) + @"\s*\|(?<category>[^|]+)\|(?<evidence>[^|]+)\|(?<tests>[^|]+)\|",
                RegexOptions.Multiline | RegexOptions.CultureInvariant);
            Match match = rowPattern.Match(phase12);
            if (!match.Success ||
                string.IsNullOrWhiteSpace(match.Groups["category"].Value) ||
                string.IsNullOrWhiteSpace(match.Groups["evidence"].Value) ||
                string.IsNullOrWhiteSpace(match.Groups["tests"].Value))
            {
                missingRows.Add($"{phaseId}: {Path.GetFileName(phaseDoc)}");
            }
        }

        Assert.True(
            missingRows.Count == 0,
            "Phase12 traceability matrix is missing Ex1 phase rows:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, missingRows));
    }

    [Fact]
    public void Phase12_DocumentationClaimSafetyBlocksForbiddenCurrentClaims()
    {
        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string[] documentPaths = EnumeratePhase12ClaimSafetyDocuments(repoRoot).ToArray();
        Assert.NotEmpty(documentPaths);

        var violations = new List<string>();
        foreach (string documentPath in documentPaths)
        {
            string[] lines = File.ReadAllLines(documentPath);
            bool inCurrentContract = false;
            bool inGuardedCurrentList = false;

            for (int index = 0; index < lines.Length; index++)
            {
                string line = lines[index].Trim();
                if (StartsCurrentContractBlock(line))
                {
                    inCurrentContract = true;
                    continue;
                }

                if (inCurrentContract && EndsCurrentContractBlock(line))
                {
                    inCurrentContract = false;
                    inGuardedCurrentList = false;
                }

                if (inCurrentContract && StartsGuardedCurrentList(line))
                {
                    inGuardedCurrentList = true;
                    continue;
                }

                if (inGuardedCurrentList &&
                    line.Length > 0 &&
                    !line.StartsWith("-", StringComparison.Ordinal))
                {
                    inGuardedCurrentList = false;
                }

                if (!inCurrentContract ||
                    inGuardedCurrentList ||
                    line.Length == 0 ||
                    line.StartsWith("```", StringComparison.Ordinal) ||
                    IsNegatedOrGated(line))
                {
                    continue;
                }

                foreach (ForbiddenCurrentClaim claim in ForbiddenCurrentClaims)
                {
                    if (claim.Pattern.IsMatch(line))
                    {
                        string relativePath = NormalizeRelativePath(Path.GetRelativePath(repoRoot, documentPath));
                        violations.Add($"{relativePath}:{index + 1}: {claim.Name}: {line}");
                    }
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "Current-contract documentation contains forbidden Phase12 claims:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void Phase12_FailClosedCompatibilityEvidenceRemainsActiveWhileCurrentContractHolds()
    {
        DmaStreamComputeDescriptor descriptor =
            DmaStreamComputeTestDescriptorFactory.CreateDescriptor();
        var core = new Processor.CPU_Core(0);

        Assert.False(DmaStreamComputeDescriptorParser.ExecutionEnabled);
        Assert.False(new DmaStreamComputeMicroOp(descriptor).WritesRegister);
        Assert.Throws<InvalidOperationException>(
            () => new DmaStreamComputeMicroOp(descriptor).Execute(ref core));

        foreach (SystemDeviceCommandMicroOp carrier in CreateL7Carriers())
        {
            Assert.False(carrier.WritesRegister);
            Assert.Empty(carrier.WriteRegisters);
            Assert.Throws<InvalidOperationException>(() => carrier.Execute(ref core));
        }

        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        AssertTestFileHasRunnableFacts(repoRoot, "HybridCPU_ISE.Tests/tests/DmaStreamComputeTokenStorePhase03Tests.cs");
        AssertTestFileHasRunnableFacts(repoRoot, "HybridCPU_ISE.Tests/tests/DmaStreamComputeRetirePublicationPhase04Tests.cs");
        AssertTestFileHasRunnableFacts(repoRoot, "HybridCPU_ISE.Tests/tests/GlobalMemoryConflictServicePhase05Tests.cs");
        AssertTestFileHasRunnableFacts(repoRoot, "HybridCPU_ISE.Tests/tests/AddressingBackendResolverPhase06Tests.cs");
        AssertTestFileHasRunnableFacts(repoRoot, "HybridCPU_ISE.Tests/tests/DmaStreamComputeDsc2Phase07Tests.cs");
        AssertTestFileHasRunnableFacts(repoRoot, "HybridCPU_ISE.Tests/tests/DmaStreamComputeAllOrNonePhase08Tests.cs");
        AssertTestFileHasRunnableFacts(repoRoot, "HybridCPU_ISE.Tests/tests/CachePrefetchNonCoherentPhase09Tests.cs");
        AssertTestFileHasRunnableFacts(repoRoot, "HybridCPU_ISE.Tests/tests/L7SdcPhase10GateTests.cs");
        AssertTestFileHasRunnableFacts(repoRoot, "HybridCPU_ISE.Tests/CompilerTests/CompilerBackendLoweringPhase11Tests.cs");
    }

    [Fact]
    public void Phase12_FutureClaimMigrationRequiresApprovalCodeTestsCompilerAndDocsEvidence()
    {
        string phase12 = ReadRepoFile(
            "Documentation/Refactoring/Phases Ex1/12_Testing_Conformance_And_Documentation_Migration.md");
        string adr12 = ReadRepoFile(
            "Documentation/Refactoring/Phases Ex1/ADR_12_Testing_Conformance_And_Documentation_Migration.md");
        string combined = phase12 + Environment.NewLine + adr12;

        Assert.Contains("Architecture approval is recorded", combined, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Code implementation lands", combined, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Positive and negative tests pass", combined, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("compiler/backend conformance", combined, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("documentation claim-safety", combined, StringComparison.OrdinalIgnoreCase);

        AssertProductionRejectedWhenAnyGateIsMissing(
            CompilerBackendLoweringSurface.Lane6DmaStreamCompute,
            CompilerBackendLoweringContract.FutureDscRequiredRequirements,
            CompilerBackendLoweringContract.EvaluateProductionDscLowering);
        AssertProductionRejectedWhenAnyGateIsMissing(
            CompilerBackendLoweringSurface.Lane7SystemDeviceCommand,
            CompilerBackendLoweringContract.FutureL7RequiredRequirements,
            CompilerBackendLoweringContract.EvaluateProductionL7Lowering);
    }

    [Fact]
    public void Phase12_HelperParserModelAndFakeBackendEvidenceCannotSatisfyExecutableIsa()
    {
        var fakeBackend = new FakeMatMulExternalAcceleratorBackend();
        Assert.True(fakeBackend.IsTestOnly);

        CompilerBackendLoweringDecision parserOnlyDecision =
            CompilerBackendLoweringContract.EvaluateProductionDscLowering(
                new CompilerBackendLoweringRequest
                {
                    Surface = CompilerBackendLoweringSurface.Lane6DmaStreamCompute,
                    State = CompilerBackendCapabilityState.ParserOnly,
                    UsesParserValidationOnly = true
                });
        CompilerBackendLoweringDecision modelHelperDecision =
            CompilerBackendLoweringContract.EvaluateProductionL7Lowering(
                new CompilerBackendLoweringRequest
                {
                    Surface = CompilerBackendLoweringSurface.Lane7SystemDeviceCommand,
                    State = CompilerBackendCapabilityState.ProductionExecutable,
                    AvailableRequirements = CompilerBackendLoweringContract.FutureL7RequiredRequirements,
                    UsesModelOrTestHelper = true
                });

        Assert.False(parserOnlyDecision.IsAllowed);
        Assert.False(modelHelperDecision.IsAllowed);
        Assert.Contains("non-production", parserOnlyDecision.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("model or test helper", modelHelperDecision.Reason, StringComparison.OrdinalIgnoreCase);

        string compilerText = ReadAllSourceText(Path.Combine(CompatFreezeScanner.FindRepoRoot(), "HybridCPU_Compiler"));
        Assert.DoesNotContain("DmaStreamComputeRuntime", compilerText, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(FakeMatMulExternalAcceleratorBackend), compilerText, StringComparison.Ordinal);
    }

    private static void AssertProductionRejectedWhenAnyGateIsMissing(
        CompilerBackendLoweringSurface surface,
        CompilerBackendLoweringRequirement allRequirements,
        Func<CompilerBackendLoweringRequest, CompilerBackendLoweringDecision> evaluate)
    {
        foreach (CompilerBackendLoweringRequirement requirement in EnumerateFlags(allRequirements))
        {
            CompilerBackendLoweringDecision decision = evaluate(
                new CompilerBackendLoweringRequest
                {
                    Surface = surface,
                    State = CompilerBackendCapabilityState.ProductionExecutable,
                    AvailableRequirements = allRequirements & ~requirement
                });

            Assert.False(decision.IsAllowed);
            Assert.True((decision.MissingRequirements & requirement) != 0);
        }
    }

    private static IEnumerable<CompilerBackendLoweringRequirement> EnumerateFlags(
        CompilerBackendLoweringRequirement requirements)
    {
        foreach (CompilerBackendLoweringRequirement value in Enum.GetValues<CompilerBackendLoweringRequirement>())
        {
            if (value != CompilerBackendLoweringRequirement.None &&
                (requirements & value) == value)
            {
                yield return value;
            }
        }
    }

    private static void AssertTestFileHasRunnableFacts(string repoRoot, string relativePath)
    {
        string fullPath = Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(fullPath), $"Missing conformance test file: {relativePath}");

        string text = File.ReadAllText(fullPath);
        Assert.True(
            text.Contains("[Fact]", StringComparison.Ordinal) ||
            text.Contains("[Theory]", StringComparison.Ordinal),
            $"Conformance test file has no xUnit facts/theories: {relativePath}");
    }

    private static SystemDeviceCommandMicroOp[] CreateL7Carriers() =>
        new SystemDeviceCommandMicroOp[]
        {
            new AcceleratorQueryCapsMicroOp(),
            new AcceleratorSubmitMicroOp(),
            new AcceleratorPollMicroOp(),
            new AcceleratorWaitMicroOp(),
            new AcceleratorCancelMicroOp(),
            new AcceleratorFenceMicroOp()
        };

    private static IEnumerable<string> EnumeratePhase12ClaimSafetyDocuments(string repoRoot)
    {
        string[] roots =
        {
            Path.Combine(repoRoot, "Documentation", "Refactoring", "Phases Ex1"),
            Path.Combine(repoRoot, "Documentation", "Stream WhiteBook"),
            Path.Combine(repoRoot, "Documentation", "CustomExternalAccelerator")
        };

        foreach (string root in roots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (string path in Directory.EnumerateFiles(root, "*.md", SearchOption.AllDirectories))
            {
                yield return path;
            }
        }
    }

    private static bool StartsCurrentContractBlock(string line) =>
        line.Equals("Current contract:", StringComparison.OrdinalIgnoreCase) ||
        line.Equals("Current Implemented Contract", StringComparison.OrdinalIgnoreCase) ||
        line.Equals("## Current Contract", StringComparison.OrdinalIgnoreCase) ||
        line.Equals("## Current Implemented Contract", StringComparison.OrdinalIgnoreCase) ||
        line.Equals("### Current Contract", StringComparison.OrdinalIgnoreCase) ||
        line.Equals("### Current Implemented Contract", StringComparison.OrdinalIgnoreCase);

    private static bool EndsCurrentContractBlock(string line)
    {
        if (line.StartsWith("##", StringComparison.Ordinal) &&
            !line.Contains("Current Contract", StringComparison.OrdinalIgnoreCase) &&
            !line.Contains("Current Implemented Contract", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string[] labels =
        {
            "Future gated:",
            "Future design:",
            "Architecture decision:",
            "Non-goals:",
            "Required design gates:",
            "Implementation plan:",
            "Affected files/classes/methods:",
            "Testing requirements:",
            "Documentation updates:",
            "Compiler/backend impact:",
            "Compatibility risks:",
            "Exit criteria:",
            "Blocked by:",
            "Enables:"
        };
        return labels.Any(label => line.StartsWith(label, StringComparison.OrdinalIgnoreCase));
    }

    private static bool StartsGuardedCurrentList(string line) =>
        line.EndsWith("is forbidden for:", StringComparison.OrdinalIgnoreCase) ||
        line.EndsWith("production lowering is forbidden for:", StringComparison.OrdinalIgnoreCase) ||
        line.EndsWith("does not implement:", StringComparison.OrdinalIgnoreCase);

    private static bool IsNegatedOrGated(string line)
    {
        string normalized = line.ToLowerInvariant();
        string[] guards =
        {
            " not ",
            "not ",
            "no ",
            " never",
            "cannot",
            "can't",
            "must not",
            "may not",
            "does not",
            "do not",
            "is not",
            "are not",
            "without",
            "forbid",
            "forbidden",
            "reject",
            "rejected",
            "fail-closed",
            "future",
            "gated",
            "blocked",
            "requires",
            "only after",
            "non-executable",
            "model-only",
            "evidence only",
            "not authority"
        };

        return guards.Any(guard => normalized.Contains(guard, StringComparison.Ordinal));
    }

    private static string ReadRepoFile(string relativePath)
    {
        string fullPath = Path.Combine(
            CompatFreezeScanner.FindRepoRoot(),
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(fullPath), $"Missing repository file: {relativePath}");
        return File.ReadAllText(fullPath);
    }

    private static string ReadAllSourceText(string directory)
    {
        Assert.True(Directory.Exists(directory), $"Missing source directory: {directory}");
        return string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
                .Where(static path => !CompatFreezeScanner.IsGeneratedPath(path))
                .Select(File.ReadAllText));
    }

    private static string PhaseEx1Root() =>
        Path.Combine(
            CompatFreezeScanner.FindRepoRoot(),
            "Documentation",
            "Refactoring",
            "Phases Ex1");

    private static string NormalizeRelativePath(string relativePath) =>
        relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

    private sealed record ForbiddenCurrentClaim(string Name, Regex Pattern);
}
