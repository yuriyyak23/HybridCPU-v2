using System.Text;
using HybridCPU.Compiler.Core.IR;
using HybridCPU.Compiler.Llvm;
using LLVMSharp.Interop;

namespace HybridCPU_ISE.Tests.CompilerTests;

public sealed class CompilerRefPlan5Phase10LlvmSemanticMappingTests
{
    [Fact]
    public void MappingContract_IsCompleteDeterministicAndNonAuthoritative()
    {
        LlvmSemanticMappingContractV1 contract = LlvmSemanticMappingContractV1.Default;
        Assert.Equal(30, contract.Entries.Count);
        Assert.Equal(30, contract.Entries.Select(static row => row.SourceIdentity).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal("a0848cd2ac0a8452358aff4c77ac678e600eae545bcb64e6d9e78cc9af483fcf", contract.ContractDigest);
        Assert.False(contract.HasTargetLegalityAuthority);
        Assert.False(contract.HasRuntimeAuthority);
        Assert.Equal(LlvmToolchainContractV1.Default.ContractDigest, contract.ToolchainContractDigest);
        Assert.Equal(LlvmToolchainContractV1.Default.CoreTargetContractDigest, contract.CoreTargetContractDigest);
        Assert.Equal(LlvmToolchainContractV1.Default.PlatformContractDigest, contract.PlatformContractDigest);
        Assert.Contains(contract.Entries, static row => row.SourceIdentity == "value.poison" && row.Support == LlvmSemanticMappingSupportV1.Unsupported);
        Assert.Contains(contract.Entries, static row => row.SourceIdentity == "memory.volatile" && row.Support == LlvmSemanticMappingSupportV1.Unsupported);
        Assert.Contains(contract.Entries, static row => row.SourceIdentity == "analysis.profile.noalias" && row.FailureCode == "HCLM0028");
    }

    [Fact]
    public void RealLlvmIntegerPrimitive_MapsLosslesslyToCanonicalIrAndValueFlow()
    {
        if (!PinnedRuntimeAvailable()) return;
        LlvmSemanticMappingResultV1 result = MapText(SupportedModuleText);

        Assert.Equal(LlvmSemanticMappingStatusV1.Success, result.Status);
        IrProgram program = Assert.IsType<IrProgram>(result.Program);
        Assert.Equal(3, program.Instructions.Count);
        Assert.Equal(HybridCpuOpcode.ADD, program.Instructions[0].Opcode);
        Assert.Equal(HybridCpuOpcode.MUL, program.Instructions[1].Opcode);
        Assert.Equal(HybridCpuOpcode.JALR, program.Instructions[2].Opcode);
        Assert.Equal(IrIntegerOverflowSemantics.SourcePoison, program.Instructions[0].Semantics.IntegerOverflow);
        Assert.Equal(IrUndefinedValueSemantics.SourcePoison, program.Instructions[0].Semantics.UndefinedValue);
        Assert.Equal(IrShiftSemantics.NotApplicable, program.Instructions[0].Semantics.Shift);
        Assert.Equal(IrIntegerOverflowSemantics.NotApplicable, program.Instructions[2].Semantics.IntegerOverflow);
        Assert.Equal(IrArchitecturalEffectKind.Control | IrArchitecturalEffectKind.Return,
            program.Instructions[2].SideEffects.ArchitecturalEffects);
        Assert.Equal(2, program.ValueFlow.Values.Count);
        Assert.All(program.ValueFlow.Values, static value =>
        {
            Assert.Equal(32, value.ValueKind.BitWidth);
            Assert.True(value.Allocation.IsAllocatable);
        });
        Assert.Equal(2, program.ValueFlow.Accesses.Count(static access => access.Kind == IrValueAccessKind.Def));
        Assert.Single(program.ValueFlow.Accesses, static access => access.Kind == IrValueAccessKind.Use);
        Assert.Equal(IrValueAnalysisStatus.Complete,
            new HybridCpuValueLivenessPressureAnalyzerV1().Analyze(program).Status);
        IrBasicBlockDependencyGraph dependencies =
            new HybridCpuBasicBlockDependencyAnalyzer().AnalyzeBlock(program.BasicBlocks[0]);
        Assert.Contains(dependencies.Dependencies, static dependency =>
            dependency.Kind == IrInstructionDependencyKind.RegisterRaw &&
            dependency.ProducerInstructionIndex == 0 && dependency.ConsumerInstructionIndex == 1 &&
            dependency.RelatedOperandKind == IrOperandKind.VirtualValue);
        Assert.Equal(IrFrontendAdapterStatus.Success, CanonicalIrFrontendBoundaryV1.Validate(program).Status);
        Assert.All(program.Instructions, static instruction =>
        {
            Assert.Equal(IrSourceOriginKind.Llvm, Assert.Single(instruction.OriginChain.Links).Kind);
            Assert.DoesNotContain("LLVMValueRef", instruction.GetType().FullName, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void SemanticMapping_IsDeterministicAndTextBitcodeHaveCanonicalParity()
    {
        if (!PinnedRuntimeAvailable()) return;
        string textPath = CreateTempPath("ll");
        string bitcodePath = CreateTempPath("bc");
        try
        {
            File.WriteAllText(textPath, SupportedModuleText, new UTF8Encoding(false));
            AssembleBitcode(textPath, bitcodePath);
            var mapper = new LlvmSemanticMapperV1();
            LlvmSemanticMappingResultV1 first = mapper.MapFile(textPath, LlvmInputKind.TextIr);
            LlvmSemanticMappingResultV1 second = mapper.MapFile(textPath, LlvmInputKind.TextIr);
            LlvmSemanticMappingResultV1 bitcode = mapper.MapFile(bitcodePath, LlvmInputKind.Bitcode);
            IrProgram firstProgram = Assert.IsType<IrProgram>(first.Program);
            IrProgram secondProgram = Assert.IsType<IrProgram>(second.Program);
            IrProgram bitcodeProgram = Assert.IsType<IrProgram>(bitcode.Program);

            Assert.Equal(first.Status, second.Status);
            Assert.Equal(Project(firstProgram), Project(secondProgram));
            Assert.Equal(firstProgram.FrontendEvidence.Digest, secondProgram.FrontendEvidence.Digest);
            Assert.Equal(first.Diagnostics, second.Diagnostics);
            Assert.Equal(LlvmSemanticMappingStatusV1.Success, bitcode.Status);
            Assert.Equal(Project(firstProgram), Project(bitcodeProgram));
            Assert.NotEqual(first.ImportProvenance!.InputDigest, bitcode.ImportProvenance!.InputDigest);
        }
        finally
        {
            File.Delete(textPath);
            File.Delete(bitcodePath);
        }
    }

    [Fact]
    public void Phase09DefaultImport_RemainsStructuralAndRejectsSemanticOpcode()
    {
        string path = CreateTempPath("ll");
        try
        {
            File.WriteAllText(path, SupportedModuleText, new UTF8Encoding(false));
            LlvmImportResultV1 result = new LlvmModuleImporterV1().ImportFile(path, LlvmInputKind.TextIr);
            if (!PinnedRuntimeAvailable())
            {
                Assert.Equal(LlvmImportStatus.Unavailable, result.Status);
                Assert.Equal("HCLL0002", Assert.Single(result.Diagnostics).Code);
                return;
            }
            Assert.Equal(LlvmImportStatus.Unsupported, result.Status);
            Assert.Equal("HCLL0014", Assert.Single(result.Diagnostics).Code);
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData("add i32 1, 2", IrIntegerOverflowSemantics.Wrap)]
    [InlineData("add nsw i32 1, 2", IrIntegerOverflowSemantics.SourcePoison)]
    [InlineData("add nuw i32 1, 2", IrIntegerOverflowSemantics.SourcePoison)]
    public void IntegerOverflowFlags_AreMappedWithoutWeakening(
        string operation,
        IrIntegerOverflowSemantics expected)
    {
        if (!PinnedRuntimeAvailable()) return;
        string module = SupportedModuleText.Replace("add nsw i32 1, 2", operation, StringComparison.Ordinal);
        IrProgram program = Assert.IsType<IrProgram>(MapText(module).Program);
        Assert.Equal(expected, program.Instructions[0].Semantics.IntegerOverflow);
        Assert.Equal(expected == IrIntegerOverflowSemantics.SourcePoison
            ? IrUndefinedValueSemantics.SourcePoison
            : IrUndefinedValueSemantics.NotApplicable,
            program.Instructions[0].Semantics.UndefinedValue);
    }

    [Theory]
    [MemberData(nameof(RejectedSemanticModules))]
    public void UnsupportedOrWeakenedSemantics_FailClosedDeterministically(string module, string code)
    {
        if (!PinnedRuntimeAvailable())
        {
            LlvmSemanticMappingResultV1 unavailable = MapText(module);
            Assert.Equal(LlvmSemanticMappingStatusV1.Unavailable, unavailable.Status);
            Assert.Null(unavailable.Program);
            Assert.Equal("HCLL0002", Assert.Single(unavailable.Diagnostics).Code);
            return;
        }
        LlvmSemanticMappingResultV1 first = MapText(module);
        LlvmSemanticMappingResultV1 second = MapText(module);
        Assert.NotEqual(LlvmSemanticMappingStatusV1.Success, first.Status);
        Assert.Null(first.Program);
        Assert.Equal(code, Assert.Single(first.Diagnostics).Code);
        Assert.Equal(first.Status, second.Status);
        Assert.Equal(
            first.Diagnostics.Select(static diagnostic => (diagnostic.Code, diagnostic.Message)),
            second.Diagnostics.Select(static diagnostic => (diagnostic.Code, diagnostic.Message)));
    }

    [Fact]
    public void ProfileAndTbaaCannotStrengthenAliasFactsOrLegality()
    {
        if (!PinnedRuntimeAvailable()) return;
        IrProgram program = Assert.IsType<IrProgram>(MapText(SupportedModuleText).Program);
        IrFrontendAnalysisFactV1 profile = Fact(IrAnalysisEvidenceKind.Profile,
            IrAnalysisEvidenceTrust.ProfileOnly, IrAliasEvidencePrecision.NoAlias, legality: true);
        IrFrontendAdapterResultV1 profileResult = CanonicalIrFrontendBoundaryV1.Validate(
            program with { FrontendEvidence = IrFrontendAnalysisEvidenceSetV1.Create([profile]) });
        Assert.Equal("HCIR0011", Assert.Single(profileResult.Diagnostics).Code);

        IrFrontendAnalysisFactV1 tbaa = Fact(IrAnalysisEvidenceKind.Tbaa,
            IrAnalysisEvidenceTrust.FrontendStaticEvidence, IrAliasEvidencePrecision.NoAlias, legality: false);
        IrFrontendAdapterResultV1 tbaaResult = CanonicalIrFrontendBoundaryV1.Validate(
            program with { FrontendEvidence = IrFrontendAnalysisEvidenceSetV1.Create([tbaa]) });
        Assert.Equal("HCIR0012", Assert.Single(tbaaResult.Diagnostics).Code);

        IrFrontendAnalysisEvidenceSetV1 valid = IrFrontendAnalysisEvidenceSetV1.Create([
            Fact(IrAnalysisEvidenceKind.Profile, IrAnalysisEvidenceTrust.ProfileOnly,
                IrAliasEvidencePrecision.MayAlias, legality: false)
        ]);
        IrFrontendAdapterResultV1 tampered = CanonicalIrFrontendBoundaryV1.Validate(
            program with { FrontendEvidence = valid with { Digest = new string('0', 64) } });
        Assert.Equal("HCIR0009", Assert.Single(tampered.Diagnostics).Code);
    }

    [Fact]
    public void OptionalFrontendFacts_CanBeStrippedWithoutChangingLegalityOrCanonicalOperations()
    {
        if (!PinnedRuntimeAvailable()) return;
        IrProgram mapped = Assert.IsType<IrProgram>(MapText(SupportedModuleText).Program);
        IrProgram stripped = mapped with { FrontendEvidence = IrFrontendAnalysisEvidenceSetV1.Empty };
        Assert.Equal(IrFrontendAdapterStatus.Success, CanonicalIrFrontendBoundaryV1.Validate(stripped).Status);
        Assert.Equal(mapped.Instructions, stripped.Instructions);
        Assert.Equal(mapped.ValueFlow, stripped.ValueFlow);
    }

    public static TheoryData<string, string> RejectedSemanticModules => new()
    {
        { SupportedModuleText.Replace("add nsw i32 1, 2", "sdiv i32 4, 2", StringComparison.Ordinal), "HCLL0014" },
        { SupportedModuleText.Replace("add nsw i32 1, 2", "add i32 undef, 2", StringComparison.Ordinal), "HCLM0004" },
        { SupportedModuleText.Replace("%sum = add nsw i32 1, 2\n  %product = mul i32 %sum, 4", "%sum = add i24 1, 2\n  %product = mul i24 %sum, 4", StringComparison.Ordinal), "HCLL0026" },
        { SupportedModuleText.Replace("ret void", "br label %next\nnext:\n  ret void", StringComparison.Ordinal), "HCLM0026" },
        { SupportedModuleText.Replace("e-p:64:64-i8:8-i16:16-i32:32-i64:64-n8:16:32:64-S64", "e-p:32:32", StringComparison.Ordinal), "HCLL0012" },
        { SupportedModuleText.Replace("add nsw i32 1, 2", "load volatile i32, ptr null, align 4", StringComparison.Ordinal), "HCLL0014" },
        { SupportedModuleText + "\n!tbaa = !{!0}\n", "HCLL0018" }
    };

    private static IrFrontendAnalysisFactV1 Fact(
        IrAnalysisEvidenceKind kind,
        IrAnalysisEvidenceTrust trust,
        IrAliasEvidencePrecision precision,
        bool legality) =>
        new("fact:1", kind, trust, precision, "test", "1", new string('a', 64), legality, true);

    private static LlvmSemanticMappingResultV1 MapText(string text)
    {
        string path = CreateTempPath("ll");
        try
        {
            File.WriteAllText(path, text, new UTF8Encoding(false));
            return new LlvmSemanticMapperV1().MapFile(path, LlvmInputKind.TextIr);
        }
        finally { File.Delete(path); }
    }

    private static bool PinnedRuntimeAvailable() => new LlvmModuleImporterV1().ProbeRuntime().IsAvailable;

    private const string SupportedModuleText = """
        target datalayout = "e-p:64:64-i8:8-i16:16-i32:32-i64:64-n8:16:32:64-S64"
        target triple = "hybridcpuv2-unknown-none"

        define void @kernel() {
        entry:
          %sum = add nsw i32 1, 2
          %product = mul i32 %sum, 4
          ret void
        }

        !llvm.ident = !{!0}
        !0 = !{!"LLVM 20.1.2"}
        """;

    private static string CreateTempPath(string extension) =>
        Path.Combine(Path.GetTempPath(), $"hybridcpu-phase10-{Guid.NewGuid():N}.{extension}");

    private static string Project(IrProgram program) => string.Join('|',
        program.Instructions.Select(instruction => string.Join(':', instruction.Index, instruction.StableIdentity,
            instruction.Opcode, instruction.CanonicalType.Kind, instruction.CanonicalType.BitWidth,
            instruction.Semantics.IntegerOverflow, instruction.Semantics.UndefinedValue,
            instruction.SideEffects.Memory.Kind, instruction.SideEffects.ArchitecturalEffects,
            string.Join(',', instruction.Operands.Select(static operand => $"{operand.Kind}/{operand.Value}/{operand.Name}"))))
        .Concat(program.ValueFlow.Values.Select(value => $"v:{value.StableId}:{value.ValueKind.Kind}:{value.ValueKind.BitWidth}"))
        .Concat(program.ValueFlow.Accesses.Select(access => $"a:{access.ValueId}:{access.InstructionIndex}:{access.Kind}")));

    private static unsafe void AssembleBitcode(string textPath, string bitcodePath)
    {
        byte[] pathBytes = Encoding.UTF8.GetBytes(Path.GetFullPath(textPath) + '\0');
        LLVMContextRef context = default;
        LLVMMemoryBufferRef buffer = default;
        LLVMModuleRef module = default;
        sbyte* message = null;
        try
        {
            fixed (byte* pathPointer = pathBytes)
            {
                LLVMOpaqueMemoryBuffer* rawBuffer = null;
                Assert.Equal(0, LLVM.CreateMemoryBufferWithContentsOfFile((sbyte*)pathPointer, &rawBuffer, &message));
                buffer = rawBuffer;
            }
            context = LLVMContextRef.Create();
            bool parsed = context.TryParseIR(buffer, out module, out string parseMessage);
            buffer = default;
            Assert.True(parsed, parseMessage);
            Assert.Equal(0, module.WriteBitcodeToFile(bitcodePath));
        }
        finally
        {
            if (message != null) LLVM.DisposeMessage(message);
            if (module != default) module.Dispose();
            if (context != default) context.Dispose();
            if (buffer != default) LLVM.DisposeMemoryBuffer(buffer);
        }
    }
}
