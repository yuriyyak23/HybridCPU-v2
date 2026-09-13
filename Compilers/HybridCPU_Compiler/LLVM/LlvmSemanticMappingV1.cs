using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using HybridCPU.Compiler.Core;
using HybridCPU.Compiler.Core.IR;
using HybridCPU.Compiler.Core.Target;

namespace HybridCPU.Compiler.Llvm;

public enum LlvmSemanticMappingSupportV1 : byte
{
    Lossless = 0,
    VerifiedHelper = 1,
    Unsupported = 2,
    Unknown = 255
}

public enum LlvmSemanticMappingStatusV1 : byte
{
    Success = 0,
    Unavailable = 1,
    InvalidInput = 2,
    Unsupported = 3,
    BudgetExhausted = 4,
    UnknownSemantics = 5
}

public sealed record LlvmSemanticMappingEntryV1(
    string SourceIdentity,
    string Category,
    string CanonicalExpansion,
    string SideEffects,
    string FaultsOrTraps,
    string MemoryOrdering,
    string RequiredCapability,
    LlvmSemanticMappingSupportV1 Support,
    string FailureCode);

public sealed record LlvmSemanticMappingResultV1(
    LlvmSemanticMappingStatusV1 Status,
    IrProgram? Program,
    IReadOnlyList<IrFrontendDiagnosticV1> Diagnostics,
    string MappingContractDigest,
    LlvmImportProvenanceV1? ImportProvenance);

/// <summary>Normative Phase 10 semantic firewall. Unsupported rows reject; no row grants target legality.</summary>
public sealed class LlvmSemanticMappingContractV1
{
    private static readonly LlvmSemanticMappingEntryV1[] Table =
    [
        Lossless("type.integer.i1/i8/i16/i32/i64", "type", "Canonical integer/predicate type", "none", "none", "n/a", "target.scalar-layout"),
        Unsupported("type.float", "type", "No verified scalar floating lowering", "HCLM0010"),
        Unsupported("type.pointer.addrspace(0)", "type", "Pointer identity exists, but address provenance lowering is not qualified", "HCLM0011"),
        Unsupported("type.pointer.nonzero-address-space", "type", "No coercion to generic", "HCLM0011"),
        Lossless("opcode.add/sub/mul", "opcode", "HybridCPU scalar virtual-value operation", "none", "source poison on nuw/nsw overflow", "n/a", "isa.scalar-integer"),
        Unsupported("opcode.phi", "opcode", "Requires CFG-edge value mapping", "HCLM0012"),
        Unsupported("opcode.select", "opcode", "Requires predicate lowering", "HCLM0013"),
        Unsupported("opcode.call", "opcode", "Full function ABI is unsupported", "HCLM0014"),
        Unsupported("opcode.load/store", "memory", "Address provenance and lowering are unverified", "HCLM0015"),
        Unsupported("memory.volatile", "memory", "Target volatile semantics are unsupported", "HCLM0016"),
        Unsupported("memory.atomic", "memory", "Atomic address materialization is unverified", "HCLM0017"),
        Unsupported("opcode.fence", "memory", "Fence mapping awaits an explicit lowering row", "HCLM0018"),
        Unsupported("opcode.cast", "opcode", "Conversion loss/trap mapping is incomplete", "HCLM0019"),
        Unsupported("opcode.gep", "opcode", "Pointer provenance and inbounds poison are incomplete", "HCLM0020"),
        Unsupported("value.undef", "value", "Source undef cannot be weakened", "HCLM0021"),
        Unsupported("value.poison", "value", "Standalone poison materialization is unsupported", "HCLM0022"),
        Unsupported("opcode.freeze", "opcode", "Freeze choice materialization is unsupported", "HCLM0023"),
        Unsupported("attribute.function/call", "attribute", "Unknown semantic attributes reject", "HCLM0024"),
        Unsupported("intrinsic.unknown", "intrinsic", "No implicit helper selection", "HCLM0025"),
        Lossless("opcode.ret.void", "control", "HybridCPU explicit return control", "return", "none", "n/a", "target.primitive-control-transfer"),
        Lossless("cfg.single-basic-block", "cfg", "Canonical single-block CFG", "control", "none", "n/a", "canonical-ir.bb"),
        Unsupported("cfg.multi-basic-block", "cfg", "PHI/branch edge semantics not yet qualified", "HCLM0026"),
        Lossless("metadata.llvm.ident", "metadata", "Import provenance only", "none", "none", "n/a", "llvm.pinned-producer"),
        Unsupported("debug.source-location", "metadata", "Debug-location extraction is not yet qualified", "HCLM0029"),
        Unsupported("metadata.unknown-semantic", "metadata", "Cannot relax memory/control dependencies", "HCLM0027"),
        Unsupported("analysis.alias.may", "analysis", "No LLVM memory operation is admitted in this phase", "HCLM0030"),
        Unsupported("analysis.tbaa.noalias", "analysis", "TBAA cannot strengthen MayAlias", "HCLM0028"),
        Unsupported("analysis.profile.noalias", "analysis", "Profile cannot strengthen MayAlias", "HCLM0028"),
        Unsupported("analysis.profile", "analysis", "Profile metadata extraction is not yet qualified", "HCLM0031"),
        Unsupported("analysis.range/alignment/loop", "analysis", "Analysis metadata extraction is not yet qualified", "HCLM0032")
    ];

    public static LlvmSemanticMappingContractV1 Default { get; } = new();

    private LlvmSemanticMappingContractV1()
    {
        Entries = Array.AsReadOnly(Table);
        ToolchainContractDigest = LlvmToolchainContractV1.Default.ContractDigest;
        CoreTargetContractDigest = LlvmToolchainContractV1.Default.CoreTargetContractDigest;
        PlatformContractDigest = LlvmToolchainContractV1.Default.PlatformContractDigest;
        var text = new StringBuilder("hybridcpu.llvm-semantic-mapping/v1")
            .Append('|').Append(ToolchainContractDigest)
            .Append('|').Append(CoreTargetContractDigest)
            .Append('|').Append(PlatformContractDigest);
        foreach (LlvmSemanticMappingEntryV1 row in Table.OrderBy(static row => row.SourceIdentity, StringComparer.Ordinal))
            text.Append('|').Append(row.SourceIdentity).Append(':').Append(row.Category).Append(':')
                .Append(row.CanonicalExpansion).Append(':').Append(row.SideEffects).Append(':')
                .Append(row.FaultsOrTraps).Append(':').Append(row.MemoryOrdering).Append(':')
                .Append(row.RequiredCapability).Append(':').Append(row.Support).Append(':').Append(row.FailureCode);
        ContractDigest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString()))).ToLowerInvariant();
    }

    public IReadOnlyList<LlvmSemanticMappingEntryV1> Entries { get; }
    public string ToolchainContractDigest { get; }
    public string CoreTargetContractDigest { get; }
    public string PlatformContractDigest { get; }
    public string ContractDigest { get; }
    public bool HasTargetLegalityAuthority => false;
    public bool HasRuntimeAuthority => false;

    private static LlvmSemanticMappingEntryV1 Lossless(
        string source, string category, string expansion, string effects, string traps, string ordering, string capability) =>
        new(source, category, expansion, effects, traps, ordering, capability, LlvmSemanticMappingSupportV1.Lossless, string.Empty);

    private static LlvmSemanticMappingEntryV1 Unsupported(string source, string category, string expansion, string code) =>
        new(source, category, expansion, "unknown/conservative", "unknown/conservative", "unknown", "explicit verified lowering required",
            LlvmSemanticMappingSupportV1.Unsupported, code);
}

public sealed class LlvmSemanticMapperV1
{
    public LlvmSemanticMappingResultV1 MapFile(string path, LlvmInputKind kind)
    {
        LlvmImportResultV1 import = new LlvmModuleImporterV1().ImportFileForSemanticMapping(path, kind);
        if (import.Status != LlvmImportStatus.Success || import.Module?.SemanticSnapshot is null)
            return ImportFailure(import);

        LlvmSemanticModuleSnapshotV1 snapshot = import.Module.SemanticSnapshot;
        if (snapshot.Functions.Count != 1 || snapshot.Functions[0].Blocks.Count != 1)
            return Reject("HCLM0026", "Only one no-argument void function with one basic block is qualified.", import.Module.Provenance);
        LlvmSemanticFunctionSnapshotV1 function = snapshot.Functions[0];
        LlvmSemanticBasicBlockSnapshotV1 block = function.Blocks[0];
        if (block.Instructions.Count == 0 || block.Instructions[^1].Opcode != "LLVMRet")
            return Reject("HCLM0002", "Qualified primitive must terminate with ret void.", import.Module.Provenance);

        var words = new List<HybridCpuInstructionWord>(block.Instructions.Count);
        foreach (LlvmSemanticInstructionSnapshotV1 instruction in block.Instructions)
        {
            if (!TryMapOpcode(instruction.Opcode, out HybridCpuOpcode opcode))
                return Reject("HCLM0003", $"Opcode {instruction.Opcode} has no active lossless mapping.", import.Module.Provenance, instruction.StableIdentity);
            if (opcode != HybridCpuOpcode.JALR && instruction.TypeBitWidth is not (8 or 16 or 32 or 64))
                return Reject("HCLM0010", "Integer width is outside the verified target scalar layouts.", import.Module.Provenance, instruction.StableIdentity);
            if (opcode != HybridCpuOpcode.JALR &&
                (instruction.Operands.Count != 2 || instruction.Operands.Any(static operand => operand.Kind == LlvmSnapshotOperandKindV1.Unknown)))
                return Reject("HCLM0004", "Arithmetic operands are incomplete or unsupported.", import.Module.Provenance, instruction.StableIdentity);
            words.Add(new HybridCpuInstructionWord
            {
                OpCode = (uint)opcode,
                DataTypeValue = ToDataType(instruction.TypeBitWidth),
                PredicateMask = byte.MaxValue,
                VirtualThreadId = 0
            });
        }

        IrProgram nativeShape;
        try { nativeShape = new HybridCpuIrBuilder().BuildProgram(0, words.ToArray()); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return Reject("HCLM0005", "Target Canonical IR builder rejected the mapped primitive shape.", import.Module.Provenance);
        }

        var values = new SortedDictionary<string, IrVirtualValueV1>(StringComparer.Ordinal);
        var accesses = new List<IrValueAccessV1>();
        var mappedInstructions = new List<IrInstruction>(block.Instructions.Count);
        for (int index = 0; index < block.Instructions.Count; index++)
        {
            LlvmSemanticInstructionSnapshotV1 source = block.Instructions[index];
            IrInstruction target = nativeShape.Instructions[index];
            IrOperand[] uses = source.Operands.Select(ToOperand).ToArray();
            IrOperand[] defs = string.IsNullOrWhiteSpace(source.ResultName)
                ? Array.Empty<IrOperand>()
                : [new(IrOperandKind.VirtualValue, StableValueNumber($"llvm:{function.Name}:value:{source.ResultName}"),
                    $"llvm:{function.Name}:value:{source.ResultName}")];
            foreach (IrOperand use in uses.Where(static operand => operand.Kind == IrOperandKind.VirtualValue))
            {
                if (!values.ContainsKey(use.Name))
                    return Reject("HCLM0006", "SSA use does not reference a previously mapped value.",
                        import.Module.Provenance, source.StableIdentity);
                accesses.Add(new(use.Name, index, IrValueAccessKind.Use));
            }
            foreach (IrOperand definition in defs)
            {
                values[definition.Name] = CreateValue(definition.Name, source.TypeBitWidth);
                accesses.Add(new(definition.Name, index, IrValueAccessKind.Def));
            }

            var origin = new IrSourceOriginChainV1(1,
            [
                new(source.StableIdentity, IrSourceOriginKind.Llvm, "LLVM", LlvmToolchainContractV1.LlvmRelease,
                    null, IrFrontendEvidenceTrust.ValidatedStructural)
            ]);
            bool poisonOnOverflow = source.NoSignedWrap || source.NoUnsignedWrap;
            IrInstruction mapped = target with
            {
                Operands = [.. uses, .. defs],
                Annotation = target.Annotation with { Defs = defs, Uses = uses },
                StableIdentity = source.StableIdentity,
                CanonicalType = target.Opcode == HybridCpuOpcode.JALR
                    ? new(IrCanonicalValueKind.Opaque, 0, false)
                    : new(IrCanonicalValueKind.Integer, source.TypeBitWidth, false),
                Semantics = new(
                    target.Opcode == HybridCpuOpcode.JALR
                        ? IrIntegerOverflowSemantics.NotApplicable
                        : poisonOnOverflow ? IrIntegerOverflowSemantics.SourcePoison : IrIntegerOverflowSemantics.Wrap,
                    IrShiftSemantics.NotApplicable,
                    IrPointerArithmeticSemantics.NotApplicable,
                    poisonOnOverflow ? IrUndefinedValueSemantics.SourcePoison : IrUndefinedValueSemantics.NotApplicable,
                    ConversionMayTrap: false,
                    OperationMayFault: false),
                OriginChain = origin,
                SideEffects = target.Opcode == HybridCpuOpcode.JALR
                    ? new(IrCanonicalMemoryEffectV1.None, IrArchitecturalEffectKind.Control | IrArchitecturalEffectKind.Return)
                    : new(IrCanonicalMemoryEffectV1.None, IrArchitecturalEffectKind.None)
            };
            mappedInstructions.Add(mapped);
        }

        IrBasicBlock nativeBlock = nativeShape.BasicBlocks[0];
        IrBasicBlock mappedBlock = nativeBlock with { Instructions = mappedInstructions, FunctionName = function.Name };
        IrProgram program = nativeShape with
        {
            Instructions = mappedInstructions,
            ControlFlowGraph = new([mappedBlock], Array.Empty<IrControlFlowEdge>()),
            ValueFlow = new("hybridcpu.value-flow/v1", 1, values.Values.ToArray(),
                accesses.OrderBy(static access => access.InstructionIndex).ThenBy(static access => access.Kind).ToArray()),
            FrontendEvidence = IrFrontendAnalysisEvidenceSetV1.Empty,
            Contract = nativeShape.Contract with
            {
                RequiredCapabilities = ["frontend.llvm-semantic-map/v1", "isa.hybridcpu-w8-native-v1"]
            }
        };
        IrFrontendAdapterResultV1 validation = CanonicalIrFrontendBoundaryV1.Validate(program);
        return validation.Status == IrFrontendAdapterStatus.Success
            ? new(LlvmSemanticMappingStatusV1.Success, program, Array.Empty<IrFrontendDiagnosticV1>(),
                LlvmSemanticMappingContractV1.Default.ContractDigest, import.Module.Provenance)
            : new(ToStatus(validation.Status), null, validation.Diagnostics,
                LlvmSemanticMappingContractV1.Default.ContractDigest, import.Module.Provenance);
    }

    private static IrVirtualValueV1 CreateValue(string identity, int width)
    {
        HybridCpuTargetMachineContractV1 target = HybridCpuTargetMachineContractV1.Default;
        int[] registers = target.ArchitecturalRegisters.Where(static register => register.IsAllocatable)
            .Select(static register => register.Id).ToArray();
        int[] groups = registers.Select(id => target.ArchitecturalRegisters[id].RegisterGroup).Distinct().Order().ToArray();
        return new(identity, new(IrCanonicalValueKind.Integer, width, false), IrVirtualValueClass.ScalarInteger,
            new(width, 1, false, true, HybridCpuArchitecturalRegisterClass.ScalarInteger64, null, null, 0,
                ["native-scalar"], null, registers, groups, target.ContractDigest, null));
    }

    private static IrOperand ToOperand(LlvmSemanticOperandSnapshotV1 operand) => operand.Kind switch
    {
        LlvmSnapshotOperandKindV1.ConstantInteger => new(IrOperandKind.Constant,
            unchecked((ulong)operand.SignedIntegerValue.GetValueOrDefault()), operand.StableIdentity),
        LlvmSnapshotOperandKindV1.Value => new(IrOperandKind.VirtualValue,
            StableValueNumber(operand.StableIdentity), operand.StableIdentity),
        _ => new(IrOperandKind.None, 0, operand.StableIdentity)
    };

    private static bool TryMapOpcode(string opcode, out HybridCpuOpcode mapped)
    {
        mapped = opcode switch
        {
            "LLVMAdd" => HybridCpuOpcode.ADD,
            "LLVMSub" => HybridCpuOpcode.SUB,
            "LLVMMul" => HybridCpuOpcode.MUL,
            "LLVMRet" => HybridCpuOpcode.JALR,
            _ => HybridCpuOpcode.Nope
        };
        return mapped != HybridCpuOpcode.Nope;
    }

    private static HybridCpuDataType ToDataType(int width) => width switch
    {
        8 => HybridCpuDataType.UINT8,
        16 => HybridCpuDataType.UINT16,
        32 => HybridCpuDataType.UINT32,
        64 => HybridCpuDataType.UINT64,
        _ => HybridCpuDataType.UINT8
    };

    private static ulong StableValueNumber(string identity) =>
        BinaryPrimitives.ReadUInt64LittleEndian(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));

    private static LlvmSemanticMappingResultV1 ImportFailure(LlvmImportResultV1 import) => new(
        import.Status switch
        {
            LlvmImportStatus.Unavailable => LlvmSemanticMappingStatusV1.Unavailable,
            LlvmImportStatus.InvalidInput => LlvmSemanticMappingStatusV1.InvalidInput,
            LlvmImportStatus.Unsupported => LlvmSemanticMappingStatusV1.Unsupported,
            LlvmImportStatus.BudgetExhausted => LlvmSemanticMappingStatusV1.BudgetExhausted,
            _ => LlvmSemanticMappingStatusV1.UnknownSemantics
        },
        null,
        import.Diagnostics.Select(static diagnostic => new IrFrontendDiagnosticV1(
            diagnostic.Code, diagnostic.Message, diagnostic.StableSourceIdentity)).ToArray(),
        LlvmSemanticMappingContractV1.Default.ContractDigest,
        import.Module?.Provenance);

    private static LlvmSemanticMappingResultV1 Reject(
        string code,
        string message,
        LlvmImportProvenanceV1 provenance,
        string? identity = null) =>
        new(LlvmSemanticMappingStatusV1.Unsupported, null, [new(code, message, identity)],
            LlvmSemanticMappingContractV1.Default.ContractDigest, provenance);

    private static LlvmSemanticMappingStatusV1 ToStatus(IrFrontendAdapterStatus status) => status switch
    {
        IrFrontendAdapterStatus.Unsupported => LlvmSemanticMappingStatusV1.Unsupported,
        IrFrontendAdapterStatus.InvalidInput => LlvmSemanticMappingStatusV1.InvalidInput,
        _ => LlvmSemanticMappingStatusV1.UnknownSemantics
    };
}
