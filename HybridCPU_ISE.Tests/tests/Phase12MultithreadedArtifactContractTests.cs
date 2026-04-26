using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HybridCPU.Compiler.Core.Multithreaded;
using HybridCPU.Compiler.Core.IR;
using HybridCPU.Compiler.Core.Threading;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase12;

public sealed class Phase12MultithreadedArtifactContractTests
{
    [Fact]
    public void CompileCanonicalMultithreaded_PublishesCanonicalArtifactsAsPrimarySurface()
    {
        (HybridCpuMultithreadedCompiler compiler, _) = CreateCompilerWithTwoVirtualThreads();

        HybridCpuMultithreadedCompilationArtifacts result = compiler.CompileCanonicalMultithreaded(CreateConfig());

        Assert.Equal(new ulong[] { 0x10, 0x20, 0x0, 0x0 }, result.ThreadDomainTags);
        Assert.Equal(2, result.TotalInstructions);
        Assert.Equal(1, result.Stats.InstructionsPerThread);
        Assert.Equal(0, result.Stats.BarriersInserted);
        Assert.Equal(0, result.Stats.CrossThreadDependencies);
        Assert.True(result.Stats.BundleUtilization > 0d);

        Assert.NotNull(result.CanonicalThreadPrograms[0]);
        Assert.NotNull(result.CanonicalThreadPrograms[1]);
        Assert.Null(result.CanonicalThreadPrograms[2]);
        Assert.Null(result.CanonicalThreadPrograms[3]);

        byte[]?[] images = result.BuildVliwBundleImages();
        Assert.Equal(result.CanonicalThreadPrograms[0]!.ProgramImage, images[0]);
        Assert.Equal(result.CanonicalThreadPrograms[1]!.ProgramImage, images[1]);
    }

    [Fact]
    public void CompileMultithreaded_LegacyWrapper_RetainsCompatInterleavingWithoutForkingCanonicalTruth()
    {
        (HybridCpuMultithreadedCompiler canonicalCompiler, _) = CreateCompilerWithTwoVirtualThreads();
        HybridCpuMultithreadedCompilationArtifacts canonical = canonicalCompiler.CompileCanonicalMultithreaded(CreateConfig());

        (HybridCpuMultithreadedCompiler legacyCompiler, VLIW_Instruction[] expectedInterleavedInstructions) = CreateCompilerWithTwoVirtualThreads();
        HybridCpuMultithreadedCompiledProgram legacy = legacyCompiler.CompileMultithreaded(CreateConfig());

        Assert.Equal(canonical.ThreadDomainTags, legacy.ThreadDomainTags);
        Assert.Equal(canonical.TotalInstructions, legacy.TotalInstructions);
        Assert.Equal(canonical.TotalBundles, legacy.TotalBundles);
        Assert.Equal(canonical.Stats.InstructionsPerThread, legacy.Stats.InstructionsPerThread);
        Assert.Equal(canonical.Stats.BarriersInserted, legacy.Stats.BarriersInserted);
        Assert.Equal(canonical.Stats.CrossThreadDependencies, legacy.Stats.CrossThreadDependencies);
        Assert.Equal(canonical.Stats.BundleUtilization, legacy.Stats.BundleUtilization);

        Assert.Equal(canonical.CanonicalThreadPrograms[0]!.ProgramImage, legacy.CanonicalThreadPrograms[0]!.ProgramImage);
        Assert.Equal(canonical.CanonicalThreadPrograms[1]!.ProgramImage, legacy.CanonicalThreadPrograms[1]!.ProgramImage);

        Assert.Equal(new[] { 0, 32, -1, -1 }, legacy.ThreadEntryPoints);
        Assert.Equal(BuildExpectedCompatibilityBinary(expectedInterleavedInstructions), legacy.BinaryCode);
    }

    [Fact]
    public void CompileCanonicalMultithreaded_EmbedsPerThreadDomainTagsIntoCanonicalThreadPrograms()
    {
        (HybridCpuMultithreadedCompiler compiler, _) = CreateCompilerWithTwoVirtualThreads();

        HybridCpuMultithreadedCompilationArtifacts result = compiler.CompileCanonicalMultithreaded(CreateConfig());

        IrInstruction vt0Instruction = Assert.Single(result.CanonicalThreadPrograms[0]!.ProgramSchedule.Program.Instructions);
        IrInstruction vt1Instruction = Assert.Single(result.CanonicalThreadPrograms[1]!.ProgramSchedule.Program.Instructions);

        Assert.Equal(0x10UL, vt0Instruction.Annotation.DomainTag);
        Assert.Equal(0x20UL, vt1Instruction.Annotation.DomainTag);
    }

    [Fact]
    public void BinaryCode_ProductionMentionsStayInsideMultithreadedCompatibilityBoundary()
    {
        string repoRoot = FindRepoRoot();
        string[] allowedPaths =
        [
            Path.Combine(repoRoot, "HybridCPU_Compiler", "API", "Multithreaded", "HybridCpuMultithreadedCompiler.cs"),
        ];
        string[] productionRoots =
        [
            Path.Combine(repoRoot, "HybridCPU_Compiler"),
        ];

        string[] unexpectedCallSites = FindUnexpectedCallSites(
            repoRoot,
            "BinaryCode",
            allowedPaths,
            productionRoots);

        Assert.Empty(unexpectedCallSites);
    }

    private static (HybridCpuMultithreadedCompiler Compiler, VLIW_Instruction[] ExpectedInterleavedInstructions) CreateCompilerWithTwoVirtualThreads()
    {
        var compiler = new HybridCpuMultithreadedCompiler();
        HybridCpuThreadCompilerContext vt0 = compiler.GetThreadContext(0);
        HybridCpuThreadCompilerContext vt1 = compiler.GetThreadContext(1);

        vt0.CompileInstruction(
            opCode: (uint)InstructionsEnum.Addition,
            dataType: 0,
            predicate: 0xFF,
            immediate: 0x11,
            destSrc1: 0x1111,
            src2: 0x2222,
            streamLength: 4,
            stride: 8,
            stealabilityPolicy: StealabilityPolicy.Stealable);

        vt1.CompileInstruction(
            opCode: (uint)InstructionsEnum.Addition,
            dataType: 0,
            predicate: 0x7F,
            immediate: 0x33,
            destSrc1: 0x3333,
            src2: 0x4444,
            streamLength: 12,
            stride: 16,
            stealabilityPolicy: StealabilityPolicy.Stealable);

        VLIW_Instruction vt0Instruction = vt0.GetCompiledInstructions()[0];
        VLIW_Instruction vt1Instruction = vt1.GetCompiledInstructions()[0];

        return (compiler, new[] { vt0Instruction, vt1Instruction });
    }

    private static HybridCpuMultithreadedCompilerConfig CreateConfig()
    {
        return new HybridCpuMultithreadedCompilerConfig
        {
            ThreadDomainTags = new ulong[] { 0x10, 0x20, 0x0, 0x0 }
        };
    }

    private static byte[] BuildExpectedCompatibilityBinary(IReadOnlyList<VLIW_Instruction> instructions)
    {
        var binary = new byte[instructions.Count * 32];
        for (int index = 0; index < instructions.Count; index++)
        {
            bool serialized = instructions[index].TryWriteBytes(binary.AsSpan(index * 32, 32));
            Assert.True(serialized);
        }

        return binary;
    }

    private static string[] FindUnexpectedCallSites(
        string repoRoot,
        string pattern,
        IReadOnlyCollection<string> allowedPaths,
        IReadOnlyCollection<string> productionRoots)
    {
        var unexpectedCallSites = new List<string>();
        foreach (string productionRoot in productionRoots)
        {
            if (!Directory.Exists(productionRoot))
            {
                continue;
            }

            foreach (string filePath in Directory.EnumerateFiles(productionRoot, "*.cs", SearchOption.AllDirectories))
            {
                if (filePath.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                    filePath.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                {
                    continue;
                }

                if (allowedPaths.Contains(filePath, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                string[] lines = File.ReadAllLines(filePath);
                for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    if (lines[lineIndex].Contains(pattern, StringComparison.Ordinal))
                    {
                        unexpectedCallSites.Add($"{Path.GetRelativePath(repoRoot, filePath)}:{lineIndex + 1}");
                    }
                }
            }
        }

        return unexpectedCallSites.ToArray();
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            bool hasRepoLayout =
                Directory.Exists(Path.Combine(current.FullName, "Documentation")) &&
                Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE.Tests"));
            if (hasRepoLayout)
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate HybridCPU ISE repository root from test output directory.");
    }
}
