using HybridCPU_ISE.Arch;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Xunit;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09MaterializerCallerBoundaryTests
{
    [Fact]
    public void DecodedBundleTransportProjector_ProductionCallersStayInsideCpuCoreDecoder()
    {
        string repoRoot = FindRepoRoot();
        string productionRoot = Path.Combine(repoRoot, "HybridCPU_ISE");
        string allowedCallerPath = Path.Combine(
            productionRoot,
            "Core",
            "Decoder",
            "CPU_Core.Decoder.cs");
        string projectorDefinitionPath = Path.Combine(
            productionRoot,
            "Core",
            "Decoder",
            "DecodedBundleTransportProjector.cs");
        string legacyMaterializerPath = Path.Combine(
            productionRoot,
            "Core",
            "Decoder",
            "LegacyMicroOpSlotCarrierMaterializer.cs");

        var unexpectedCallSites = new List<string>();
        foreach (string filePath in Directory.EnumerateFiles(productionRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (filePath.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                filePath.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            if (filePath.Equals(allowedCallerPath, StringComparison.OrdinalIgnoreCase) ||
                filePath.Equals(projectorDefinitionPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string[] lines = File.ReadAllLines(filePath);
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex];
                if (line.Contains("DecodedBundleTransportProjector.BuildCanonicalTransportFacts(", StringComparison.Ordinal) ||
                    line.Contains("DecodedBundleTransportProjector.BuildFallbackTransportFacts(", StringComparison.Ordinal))
                {
                    unexpectedCallSites.Add($"{Path.GetRelativePath(repoRoot, filePath)}:{lineIndex + 1}");
                }
            }
        }

        Assert.False(File.Exists(legacyMaterializerPath));
        Assert.Contains(
            "DecodedBundleTransportProjector.BuildCanonicalTransportFacts(",
            File.ReadAllText(allowedCallerPath),
            StringComparison.Ordinal);
        Assert.Contains(
            "DecodedBundleTransportProjector.BuildFallbackTransportFacts(",
            File.ReadAllText(allowedCallerPath),
            StringComparison.Ordinal);
        Assert.Empty(unexpectedCallSites);
    }

    [Fact]
    public void DecodedBundleTransport_ProjectorOwnsPublishedTransportFactsWithoutLegacyMaterializerBridge()
    {
        string repoRoot = FindRepoRoot();
        string decoderPath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Decoder",
            "CPU_Core.Decoder.cs");
        string projectorPath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Decoder",
            "DecodedBundleTransportProjector.cs");
        string aluAndSmtPath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Pipeline",
            "Core",
            "CPU_Core.PipelineExecution.ALUAndSMT.cs");
        string executePath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Pipeline",
            "Core",
            "CPU_Core.PipelineExecution.Execute.cs");
        string forwardingPath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Pipeline",
            "Core",
            "CPU_Core.PipelineExecution.Forwarding.cs");
        string hazardsPath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Pipeline",
            "Core",
            "CPU_Core.PipelineExecution.Hazards.cs");
        string memoryWriteBackPath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Pipeline",
            "Core",
            "CPU_Core.PipelineExecution.MemoryWriteBack.cs");
        string fetchDecodePath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Pipeline",
            "Core",
            "CPU_Core.PipelineExecution.FetchDecode.cs");
        string stageFlowPath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Pipeline",
            "Core",
            "CPU_Core.PipelineExecution.StageFlow.cs");
        string pipelinePath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Pipeline",
            "Core",
            "CPU_Core.Pipeline.cs");
        string fetchStagePath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Pipeline",
            "Core",
            "CPU_Core.Pipeline.Stages.FetchStage.cs");
        string pipelineExecutionPath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Pipeline",
            "Core",
            "CPU_Core.PipelineExecution.cs");
        string pipelineExecutionFspPath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Pipeline",
            "Core",
            "CPU_Core.PipelineExecution.Fsp.cs");
        string pipelineExecutionCombinedSource =
            File.ReadAllText(pipelineExecutionPath) +
            Environment.NewLine +
            File.ReadAllText(pipelineExecutionFspPath);

        Assert.False(File.Exists(aluAndSmtPath));
        Assert.False(File.Exists(executePath));
        Assert.False(File.Exists(forwardingPath));
        Assert.False(File.Exists(hazardsPath));
        Assert.False(File.Exists(memoryWriteBackPath));
        Assert.False(File.Exists(fetchDecodePath));
        Assert.Contains(
            "DecodedBundleTransportProjector.BuildCanonicalTransportFacts(",
            File.ReadAllText(decoderPath),
            StringComparison.Ordinal);
        Assert.Contains(
            "DecodedBundleTransportProjector.BuildFallbackTransportFacts(",
            File.ReadAllText(decoderPath),
            StringComparison.Ordinal);
        Assert.Contains(
            "internal static DecodedBundleTransportFacts BuildCanonicalTransportFacts(",
            File.ReadAllText(projectorPath),
            StringComparison.Ordinal);
        Assert.Contains(
            "internal static DecodedBundleTransportFacts BuildFallbackTransportFacts(",
            File.ReadAllText(projectorPath),
            StringComparison.Ordinal);
        Assert.Contains(
            "_loopBuffer.StoreSlot(i, decodedBundleSlots[i].MicroOp)",
            File.ReadAllText(stageFlowPath),
            StringComparison.Ordinal);
        Assert.Contains(
            "private Core.MicroOp?[]? _replayFetchBuffer;",
            File.ReadAllText(pipelinePath),
            StringComparison.Ordinal);
        Assert.Contains(
            "_replayFetchBuffer ??= new Core.MicroOp?[Core.BundleMetadata.BundleSlotCount];",
            File.ReadAllText(pipelinePath),
            StringComparison.Ordinal);
        Assert.Contains(
            "private byte[]? _fetchVliwBuffer;",
            File.ReadAllText(pipelinePath),
            StringComparison.Ordinal);
        Assert.Contains(
            "_fetchVliwBuffer ??= new byte[256];",
            File.ReadAllText(pipelinePath),
            StringComparison.Ordinal);
        Assert.Contains(
            "Core.MicroOp?[] replayTarget = _replayFetchBuffer;",
            File.ReadAllText(stageFlowPath),
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Core.MicroOp?[] replayTarget = new Core.MicroOp?[8];",
            File.ReadAllText(stageFlowPath),
            StringComparison.Ordinal);
        Assert.Contains(
            "pipeIF.VLIWBundle = _fetchVliwBuffer;",
            File.ReadAllText(stageFlowPath),
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "pipeIF.VLIWBundle = new byte[256];",
            File.ReadAllText(stageFlowPath),
            StringComparison.Ordinal);
        Assert.Contains(
            "VLIWBundle = Array.Empty<byte>();",
            File.ReadAllText(fetchStagePath),
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "VLIWBundle = new byte[256];",
            File.ReadAllText(fetchStagePath),
            StringComparison.Ordinal);
        Assert.Contains(
            "System.Collections.Generic.IReadOnlyList<Core.MicroOp?> carrierBundle)",
            File.ReadAllText(stageFlowPath),
            StringComparison.Ordinal);
        Assert.Contains(
            "private System.Collections.Generic.IReadOnlyList<Core.MicroOp?> ApplyFSPPacking(",
            pipelineExecutionCombinedSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "MaterializeDecodedBundleCarrierBundle(",
            pipelineExecutionCombinedSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DecodeFallbackAndProjectorBoundary_UseTypedFaultContractsWithoutBroadCatch()
    {
        string repoRoot = FindRepoRoot();
        string decoderPath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Decoder",
            "CPU_Core.Decoder.cs");
        string projectorPath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Decoder",
            "DecodedBundleTransportProjector.cs");

        string decoderSource = File.ReadAllText(decoderPath);
        string projectorSource = File.ReadAllText(projectorPath);

        Assert.Contains(
            "catch (InvalidOpcodeException decodeException)",
            decoderSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "catch (Exception decodeException)",
            decoderSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "InvalidOpcodeException bundleDecodeException",
            projectorSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "catch (DecodeProjectionFaultException projectionException)",
            projectorSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "catch (Exception slotDecodeException)",
            projectorSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "catch (Exception ex)",
            projectorSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ReplaySafetyAndPackingRuntime_UseNullableTransportViewAtLiveAbiSeams()
    {
        string repoRoot = FindRepoRoot();
        string loopBufferPath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Pipeline",
            "Components",
            "LoopBuffer.cs");
        string bundleCertificatePath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Pipeline",
            "Certificates",
            "BundleCertificate.cs");
        string bundleResourceCertificatePath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Pipeline",
            "Certificates",
            "BundleResourceCertificate.cs");
        string replayPhaseSubstrateInterfacesPath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Pipeline",
            "Certificates",
            "ReplayPhaseSubstrate.Interfaces.cs");
        string safetyVerifierPath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Pipeline",
            "Safety",
            "SafetyVerifier.Verification.cs");
        string stageFlowPath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Pipeline",
            "Core",
            "CPU_Core.PipelineExecution.StageFlow.cs");
        string pipelineExecutionPath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Pipeline",
            "Core",
            "CPU_Core.PipelineExecution.cs");
        string pipelineExecutionFspPath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Pipeline",
            "Core",
            "CPU_Core.PipelineExecution.Fsp.cs");
        string pipelineExecutionCombinedSource =
            File.ReadAllText(pipelineExecutionPath) +
            Environment.NewLine +
            File.ReadAllText(pipelineExecutionFspPath);
        string packBundlePath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Pipeline",
            "Scheduling",
            "MicroOpScheduler.PackBundle.cs");
        string schedulerSmtPath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Pipeline",
            "Scheduling",
            "MicroOpScheduler.SMT.cs");
        string podControllerPath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Processor",
            "Core",
            "PodController.cs");

        Assert.Contains(
            "public void StoreSlot(int slotIndex, MicroOp? op)",
            File.ReadAllText(loopBufferPath),
            StringComparison.Ordinal);
        Assert.Contains(
            "public bool TryReplay(ulong requestedPC, MicroOp?[] target)",
            File.ReadAllText(loopBufferPath),
            StringComparison.Ordinal);
        Assert.Contains(
            "public static uint CalculateBundleHash(System.Collections.Generic.IReadOnlyList<MicroOp?> bundle)",
            File.ReadAllText(bundleCertificatePath),
            StringComparison.Ordinal);
        Assert.Contains(
            "System.Collections.Generic.IReadOnlyList<MicroOp?> bundle,",
            File.ReadAllText(bundleResourceCertificatePath),
            StringComparison.Ordinal);
        Assert.Contains(
            "IReadOnlyList<MicroOp?> bundle,",
            File.ReadAllText(replayPhaseSubstrateInterfacesPath),
            StringComparison.Ordinal);
        Assert.Contains(
            "public bool VerifyBundle(IReadOnlyList<MicroOp?> bundle)",
            File.ReadAllText(safetyVerifierPath),
            StringComparison.Ordinal);
        Assert.Contains(
            "private static Core.MicroOp?[] CloneDecodedBundleCarrierBundle(",
            File.ReadAllText(stageFlowPath),
            StringComparison.Ordinal);
        Assert.Contains(
            "private System.Collections.Generic.IReadOnlyList<Core.MicroOp?> ApplyFSPPacking(",
            pipelineExecutionCombinedSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "MaterializeDecodedBundleCarrierBundle(",
            pipelineExecutionCombinedSource,
            StringComparison.Ordinal);
        Assert.Contains("public MicroOp[] PackBundle(", File.ReadAllText(packBundlePath), StringComparison.Ordinal);
        Assert.Contains(
            "public MicroOp[] PackBundleIntraCoreSmt(",
            File.ReadAllText(schedulerSmtPath),
            StringComparison.Ordinal);
        Assert.Contains(
            "System.Collections.Generic.IReadOnlyList<MicroOp?> bundle,",
            File.ReadAllText(schedulerSmtPath),
            StringComparison.Ordinal);
        Assert.Contains(
            "System.Collections.Generic.IReadOnlyList<MicroOp?> originalBundle,",
            File.ReadAllText(podControllerPath),
            StringComparison.Ordinal);
    }

    [Fact]
    public void LiveFetchedDecode_UsesExplicitAnnotationCarrierThroughMemoryCacheAndFetchContours()
    {
        string repoRoot = FindRepoRoot();
        string decoderPath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Decoder",
            "CPU_Core.Decoder.cs");
        string fetchStagePath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Pipeline",
            "Core",
            "CPU_Core.Pipeline.Stages.FetchStage.cs");
        string stageFlowPath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Pipeline",
            "Core",
            "CPU_Core.PipelineExecution.StageFlow.cs");
        string cachePath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Cache",
            "CPU_Core.Cache.cs");
        string memoryPath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Processor",
            "Memory",
            "Processor.Memory.cs");

        string decoderSource = File.ReadAllText(decoderPath);
        string fetchStageSource = File.ReadAllText(fetchStagePath);
        string stageFlowSource = File.ReadAllText(stageFlowPath);
        string cacheSource = File.ReadAllText(cachePath);
        string memorySource = File.ReadAllText(memoryPath);

        Assert.DoesNotContain(
            "BuildNeutralFetchedBundleAnnotationsIfNeeded(",
            decoderSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "still have no persisted annotation carrier",
            decoderSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "if (!pipeIF.HasBundleAnnotations || pipeIF.BundleAnnotations == null)",
            decoderSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "VliwBundleAnnotations fetchedBundleAnnotations = pipeIF.BundleAnnotations;",
            decoderSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "public VliwBundleAnnotations? BundleAnnotations;",
            fetchStageSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "public bool HasBundleAnnotations;",
            fetchStageSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "pipeIF.BundleAnnotations = null;",
            stageFlowSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "pipeIF.HasBundleAnnotations = false;",
            stageFlowSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "pipeIF.BundleAnnotations = fetchedBundle.VLIWCache_BundleAnnotations;",
            stageFlowSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "pipeIF.HasBundleAnnotations = fetchedBundle.VLIWCache_HasAnnotationCarrier;",
            stageFlowSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "public VliwBundleAnnotations? VLIWCache_BundleAnnotations;",
            cacheSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "public bool VLIWCache_HasAnnotationCarrier;",
            cacheSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "MainMemory.TryReadVliwBundleAnnotations(",
            cacheSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "public virtual void PublishVliwBundleAnnotations(",
            memorySource,
            StringComparison.Ordinal);
        Assert.Contains(
            "public virtual bool TryReadVliwBundleAnnotations(",
            memorySource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void LegacyMaterializerFile_IsGone_And_ProjectorTestingEntryPointsRemainLocal()
    {
        string repoRoot = FindRepoRoot();
        string materializerPath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Decoder",
            "LegacyMicroOpSlotCarrierMaterializer.cs");
        string projectorPath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Decoder",
            "DecodedBundleTransportProjector.cs");
        string projectorSource = File.ReadAllText(projectorPath);

        Assert.False(File.Exists(materializerPath));
        Assert.Contains(
            "internal static MicroOp?[] BuildCanonicalCarrierBundleForTesting(",
            projectorSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "internal static MicroOp?[] BuildFallbackCarrierBundleForTesting(",
            projectorSource,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(InstructionsEnum.Load)]
    [InlineData(InstructionsEnum.Store)]
    public void ProjectCanonicalMaterializationInstruction_DoesNotRewriteRetainedAbsoluteLoadStoreRawInstruction(
        InstructionsEnum opcode)
    {
        MethodInfo projectMethod = typeof(DecodedBundleTransportProjector).GetMethod(
                "ProjectCanonicalMaterializationInstruction",
                BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                "DecodedBundleTransportProjector.ProjectCanonicalMaterializationInstruction was not found.");

        var rawInstruction = new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            DestSrc1Pointer = 0x1122UL,
            Src2Pointer = 0x3344UL,
            Immediate = 0x55,
            DataType = (byte)DataTypeEnum.INT64,
        };
        var instruction = new InstructionIR
        {
            CanonicalOpcode = opcode,
            Class = InstructionClass.Memory,
            SerializationClass = opcode == InstructionsEnum.Store
                ? SerializationClass.MemoryOrdered
                : SerializationClass.Free,
            Rd = 7,
            Rs1 = 8,
            Rs2 = 9,
            Imm = 0x9000,
            HasAbsoluteAddressing = true,
        };

        object? result = projectMethod.Invoke(null, new object?[] { rawInstruction, instruction, 0UL });
        var projectedInstruction = Assert.IsType<VLIW_Instruction>(result);

        Assert.Equal(rawInstruction.OpCode, projectedInstruction.OpCode);
        Assert.Equal(rawInstruction.DestSrc1Pointer, projectedInstruction.DestSrc1Pointer);
        Assert.Equal(rawInstruction.Src2Pointer, projectedInstruction.Src2Pointer);
        Assert.Equal(rawInstruction.Immediate, projectedInstruction.Immediate);
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

