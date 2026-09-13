using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using HybridCPU.Compiler.Core;
using HybridCPU.Compiler.Core.IR;
using HybridCPU.Compiler.Core.IR.Contracts;
using HybridCPU.Compiler.Core.IR.Resources;
using HybridCPU.Compiler.Core.IR.Telemetry;
using HybridCPU.Compiler.Core.Target;

namespace HybridCPU.Compiler.Native;

public abstract class NativeCompilerFrontendBase : INativeCompilerFrontend
{
    public const int MaxInstructions = 4096;
    public const int MaxAssemblyCharacters = 1_048_576;
    private const string ModelIdentity = "HybridCPU-W8-existing-structural-model";

    public abstract string Id { get; }
    public abstract NativeCompilationResult Compile(NativeFrontendRequest request);

    protected NativeCompilationResult CompileWords(
        NativeFrontendRequest request,
        IReadOnlyList<HybridCpuInstructionWord> instructionWords,
        string? normalizedAssembly)
    {
        HybridCpuTargetCompatibility targetCompatibility = HybridCpuTargetMachineContractV1.Default.CheckCompatibility(
            request.BuildIdentity.TargetSchemaMajor,
            request.BuildIdentity.TargetTriple,
            request.BuildIdentity.DataLayoutVersion,
            request.BuildIdentity.DataLayoutIdentity);
        if (targetCompatibility != HybridCpuTargetCompatibility.Compatible)
        {
            return NativeCompilationFailure.Create(
                NativeCompilationStatus.Unsupported,
                "HCN0008",
                $"TargetMachine compatibility is {targetCompatibility}; host or frontend defaults cannot fill HybridCPU target facts.");
        }

        NativeCompilationResult? capabilityFailure = ValidateCapabilities(request, "final-lowering");
        if (capabilityFailure is not null) return capabilityFailure;

        if (instructionWords.Count > MaxInstructions)
        {
            return NativeCompilationFailure.Create(
                NativeCompilationStatus.BudgetExhausted,
                "HCN0002",
                $"Native instruction budget {MaxInstructions} was exceeded.");
        }

        HybridCpuInstructionWord[] words = instructionWords.ToArray();
        List<(string Phase, string Detail)> progress = [];
        HybridCpuCompiledProgram program;
        try
        {
            program = HybridCpuCanonicalCompiler.CompileProgram(
                request.VirtualThreadId,
                words,
                progressObserver: (phase, detail) => progress.Add((phase, detail)));
        }
        catch (ArgumentException exception)
        {
            return NativeCompilationFailure.Create(
                NativeCompilationStatus.InvalidInput,
                "HCN0003",
                exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return NativeCompilationFailure.Create(
                NativeCompilationStatus.InvalidInput,
                "HCN0004",
                exception.Message);
        }
        catch (NotSupportedException exception)
        {
            return NativeCompilationFailure.Create(
                NativeCompilationStatus.Unsupported,
                "HCN0007",
                exception.Message);
        }

        string inputDigest = CompilerScheduleFingerprintV1.HashInput(request.VirtualThreadId, words);
        var report = new NativeSchedulingReport(
            "hybridcpu.native-scheduling-report/v1",
            CompilerScheduleFingerprintV1.HashSchedule(program.ProgramSchedule),
            CompilerScheduleFingerprintV1.HashBundles(program.BundleLayout),
            program.BundleCount);
        string optionsDigest = HashIdentity(request.OptionsIdentity);
        CompilerBuildProvenanceV1 buildProvenance = new(
            CompilerCrossLayerSchemaCatalogV1.Provenance.SchemaId,
            CompilerCrossLayerSchemaCatalogV1.Provenance.Version,
            "HybridCPU.Compiler.Native",
            request.BuildIdentity.ProducerVersion,
            request.BuildIdentity.SourceCommit,
            request.BuildIdentity.SourceTree,
            request.BuildIdentity.Toolchains,
            Id,
            "1",
            optionsDigest,
            request.BuildIdentity.DataLayoutVersion,
            request.BuildIdentity.AbiVersion,
            request.ProfitabilityProfileHash);
        CompilerFeatureSet featureSet = request.AvailableCapabilities;
        CompilerCrossLayerEnvelopeV1 envelope = new(
            CompilerCrossLayerSchemaCatalogV1.Envelope.SchemaId,
            CompilerCrossLayerSchemaCatalogV1.Envelope.Version,
            request.BuildIdentity.ProducerVersion,
            HybridCpuTargetMachineContractV1.TargetArchitectureRevision,
            HybridCpuMachineDescriptionV1.Default.ContractDigest,
            HybridCpuMachineTopologyV1.Default.ContractDigest,
            request.BuildIdentity.AbiVersion,
            featureSet.Digest,
            request.RequiredCapabilities,
            buildProvenance.Digest);
        CompilerSemanticCacheKeyV1 cacheKey = new(
            HybridCpuTargetMachineContractV1.TargetArchitectureRevision,
            HybridCpuMachineDescriptionV1.Default.ContractDigest,
            HybridCpuMachineTopologyV1.Default.ContractDigest,
            request.BuildIdentity.AbiVersion,
            featureSet.Digest,
            optionsDigest,
            request.BuildIdentity.DataLayoutVersion,
            inputDigest);
        var provenance = new NativeCompilationProvenance(
            "hybridcpu.native-provenance/v1",
            Id,
            HybridCpuTargetMachineContractV1.Default.ContractDigest,
            HashIdentity(ModelIdentity),
            optionsDigest,
            inputDigest,
            program.ContractVersion,
            buildProvenance,
            envelope,
            cacheKey);
        var artifacts = new NativeCompilationArtifacts(normalizedAssembly, program, report, provenance);

        bool usedFallback = progress.Any(static item => item.Phase == "ScheduleFallback");
        return new NativeCompilationResult(
            usedFallback ? NativeCompilationStatus.ConservativeFallback : NativeCompilationStatus.Success,
            artifacts,
            usedFallback
                ? [new NativeCompilerDiagnostic(
                    NativeCompilationStatus.ConservativeFallback,
                    "HCN1001",
                    "The exact existing program-order fallback was selected by the bounded Core scheduler.")]
                : Array.Empty<NativeCompilerDiagnostic>());
    }

    private static string HashIdentity(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    protected static NativeCompilationResult? ValidateCapabilities(NativeFrontendRequest request, string boundary)
    {
        foreach ((CompilerSchemaDeclaration Producer, CompilerSchemaDeclaration Consumer) pair in new[]
        {
            (CompilerCrossLayerSchemaCatalogV1.Capability, request.ConsumerCapabilitySchema),
            (CompilerCrossLayerSchemaCatalogV1.Envelope, request.ConsumerEnvelopeSchema),
            (CompilerCrossLayerSchemaCatalogV1.Provenance, request.ConsumerProvenanceSchema)
        })
        {
            CompilerCompatibilityResult compatibility = CompilerSchemaCompatibility.Evaluate(pair.Producer, pair.Consumer);
            if (compatibility.Disposition != CompilerCompatibilityDisposition.Compatible)
            {
                return NativeCompilationFailure.Create(
                    NativeCompilationStatus.Unsupported,
                    "HCN0006",
                    $"Schema validation failed at {boundary}: {compatibility.Code} {compatibility.Reason}");
            }
        }

        CompilerCapabilityValidationResult validation = CompilerCapabilityValidator.ValidateRequired(
            request.RequiredCapabilities,
            request.AvailableCapabilities);
        return validation.IsSatisfied
            ? null
            : NativeCompilationFailure.Create(
                NativeCompilationStatus.Unsupported,
                "HCN0005",
                $"Capability validation failed at {boundary}: {validation.Reason}");
    }
}

public sealed class NativeCarrierFrontend : NativeCompilerFrontendBase
{
    public override string Id => "native-carrier-v1";

    public override NativeCompilationResult Compile(NativeFrontendRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        NativeCompilationResult? capabilityFailure = ValidateCapabilities(request, "frontend-core");
        if (capabilityFailure is not null) return capabilityFailure;
        if (request.InstructionWords is null || request.AssemblySource is not null)
        {
            return NativeCompilationFailure.Create(
                NativeCompilationStatus.InvalidInput,
                "HCN0101",
                "Carrier frontend requires InstructionWords and forbids AssemblySource.");
        }
        return CompileWords(request, request.InstructionWords, normalizedAssembly: null);
    }
}

/// <summary>
/// Dependency-free canonical scalar ASM ingress. It accepts one instruction per line in
/// <c>OPCODE rd, rs1, rs2</c> or <c>OPCODE rd, rs1, immediate</c> form.
/// </summary>
public sealed class NativeAssemblyFrontend : NativeCompilerFrontendBase
{
    public override string Id => "native-asm-v1";

    public override NativeCompilationResult Compile(NativeFrontendRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        NativeCompilationResult? capabilityFailure = ValidateCapabilities(request, "frontend-core");
        if (capabilityFailure is not null) return capabilityFailure;
        if (request.AssemblySource is null || request.InstructionWords is not null)
        {
            return NativeCompilationFailure.Create(
                NativeCompilationStatus.InvalidInput,
                "HCN0201",
                "ASM frontend requires AssemblySource and forbids InstructionWords.");
        }
        if (request.AssemblySource.Length > MaxAssemblyCharacters)
        {
            return NativeCompilationFailure.Create(
                NativeCompilationStatus.BudgetExhausted,
                "HCN0202",
                $"Native assembly character budget {MaxAssemblyCharacters} was exceeded.");
        }

        List<HybridCpuInstructionWord> words = [];
        List<string> normalizedLines = [];
        string[] lines = request.AssemblySource.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        for (int index = 0; index < lines.Length; index++)
        {
            string source = StripComment(lines[index]).Trim();
            if (source.Length == 0) continue;
            if (words.Count == MaxInstructions)
            {
                return NativeCompilationFailure.Create(
                    NativeCompilationStatus.BudgetExhausted,
                    "HCN0203",
                    $"Native instruction budget {MaxInstructions} was exceeded.",
                    index + 1);
            }

            NativeCompilationResult? failure = TryParseInstruction(
                source,
                request.VirtualThreadId,
                index + 1,
                out HybridCpuInstructionWord word,
                out string normalized);
            if (failure is not null) return failure;
            words.Add(word);
            normalizedLines.Add(normalized);
        }

        if (words.Count == 0)
        {
            return NativeCompilationFailure.Create(
                NativeCompilationStatus.InvalidInput,
                "HCN0204",
                "Native assembly contains no instructions.");
        }

        return CompileWords(request, words, string.Join('\n', normalizedLines));
    }

    private static NativeCompilationResult? TryParseInstruction(
        string source,
        byte virtualThreadId,
        int sourceLine,
        out HybridCpuInstructionWord word,
        out string normalized)
    {
        word = default;
        normalized = string.Empty;
        string[] tokens = source.Replace(",", " ", StringComparison.Ordinal)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length != 4)
        {
            return NativeCompilationFailure.Create(
                NativeCompilationStatus.InvalidInput,
                "HCN0205",
                "Expected 'OPCODE rd, rs1, rs2' or 'OPCODE rd, rs1, immediate'.",
                sourceLine);
        }
        if (!Enum.TryParse(tokens[0], ignoreCase: true, out HybridCpuOpcode opcode) ||
            HybridCpuOpcodeCatalog.GetInfo(opcode) is null)
        {
            return NativeCompilationFailure.Create(
                NativeCompilationStatus.Unsupported,
                "HCN0206",
                $"Opcode '{tokens[0]}' is not supported by the native ISA catalog.",
                sourceLine);
        }
        if (!TryParseRegister(tokens[1], out byte rd) || !TryParseRegister(tokens[2], out byte rs1))
        {
            return NativeCompilationFailure.Create(
                NativeCompilationStatus.InvalidInput,
                "HCN0207",
                "Destination and first source must be architectural registers r0..r31.",
                sourceLine);
        }

        bool thirdIsRegister = TryParseRegister(tokens[3], out byte rs2);
        long immediate = 0;
        if (!thirdIsRegister && !long.TryParse(tokens[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out immediate))
        {
            return NativeCompilationFailure.Create(
                NativeCompilationStatus.InvalidInput,
                "HCN0208",
                "Third operand must be an architectural register or a signed decimal immediate.",
                sourceLine);
        }

        word = new HybridCpuInstructionWord
        {
            OpCode = (uint)opcode,
            DataTypeValue = HybridCpuDataType.INT32,
            PredicateMask = byte.MaxValue,
            Word1 = HybridCpuInstructionWord.PackArchRegs(
                rd,
                rs1,
                thirdIsRegister ? rs2 : HybridCpuInstructionWord.NoArchReg),
            Src2Pointer = thirdIsRegister ? 0UL : unchecked((ulong)immediate),
            VirtualThreadId = virtualThreadId
        };
        normalized = thirdIsRegister
            ? $"{opcode} r{rd}, r{rs1}, r{rs2}"
            : $"{opcode} r{rd}, r{rs1}, {immediate.ToString(CultureInfo.InvariantCulture)}";
        return null;
    }

    private static bool TryParseRegister(string token, out byte register)
    {
        register = default;
        return token.Length >= 2 &&
            (token[0] is 'r' or 'R') &&
            byte.TryParse(token.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out register) &&
            register <= 31;
    }

    private static string StripComment(string line)
    {
        int semicolon = line.IndexOf(';');
        int hash = line.IndexOf('#');
        int comment = semicolon < 0 ? hash : hash < 0 ? semicolon : Math.Min(semicolon, hash);
        return comment < 0 ? line : line[..comment];
    }
}
