using System.Buffers.Binary;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using HybridCPU.Compiler.Core.IR;
using HybridCPU.Compiler.Core.Target;
using HybridCPU.Compiler.Core.Target.Managed;
using HybridCPU.Compiler.Core.Target.Object;
using HybridCPU.Platform.Contracts;

namespace HybridCPU.Compiler.Cil;

public enum ManagedEhImportStatusV1 : byte
{
    Success = 0,
    Unsupported = 1,
    InvalidInput = 2,
    BudgetExhausted = 3
}

public enum ManagedEhOperationKindV1 : byte
{
    Throw = 0,
    Rethrow = 1,
    Leave = 2,
    EndFinally = 3
}

public sealed record ManagedEhClausePlanV1(
    HybridCpuManagedEhClauseKindV1 Kind,
    int TryOffset,
    int TryLength,
    int HandlerOffset,
    int HandlerLength,
    string? CatchTypeIdentity,
    ulong CatchTypeId,
    int Ordinal);

public sealed record ManagedEhOperationPlanV1(
    ManagedEhOperationKindV1 Kind,
    int IlOffset,
    int LeaveTarget,
    string RuntimeHelperSymbol);

public sealed record ManagedEhHandlerEntryV1(
    int ClauseOrdinal, int IlOffset, IReadOnlyList<RestrictedCilTypeV1> EvaluationStack,
    int? ExceptionReferenceRegister);

public enum ManagedEhLeaveActionKindV1 : byte
{
    InvokeFinally = 0,
    ReleaseCatchScope = 1
}

public sealed record ManagedEhLeaveActionV1(ManagedEhLeaveActionKindV1 Kind, int ClauseOrdinal, int HandlerOffset);
public sealed record ManagedEhLeaveTransferV1(int IlOffset, int TargetOffset,
    IReadOnlyList<ManagedEhLeaveActionV1> Actions)
{
    public bool ClearsEvaluationStack => true;
}

public sealed record ManagedEhFinallyContinuationStepV1(
    int ClauseOrdinal,
    int HandlerOffset,
    int ReleaseCatchScopesBeforeEntry,
    int? NextClauseOrdinal,
    int NextOffset);

public sealed record ManagedEhFinallyContinuationV1(
    int Token,
    int LeaveOffset,
    int TargetOffset,
    IReadOnlyList<ManagedEhFinallyContinuationStepV1> Steps,
    int ReleaseCatchScopesBeforeTarget);

public sealed record ManagedEhFinallyContinuationPlanV1(
    int ExceptionalToken,
    IReadOnlyList<ManagedEhFinallyContinuationV1> Continuations,
    string Digest)
{
    public const int ReservedExceptionalToken = 0;
}

public sealed record ManagedEhMethodPlanV1(
    string MethodIdentity,
    string InstructionIdentityPrefix,
    int MethodBodySize,
    IReadOnlyList<ManagedEhClausePlanV1> Clauses,
    IReadOnlyList<ManagedEhOperationPlanV1> Operations,
    string ContractDigest)
{
    public ManagedEhControlFlowV1? ControlFlow { get; init; }
    // Local/argument homes are preserved by the transfer, never zeroed at handler entry.
    // These are derived views of digest-bound clauses/operations, not caller-supplied authority.
    public IReadOnlyList<ManagedEhHandlerEntryV1> HandlerEntries => Clauses.Select(clause =>
        new ManagedEhHandlerEntryV1(clause.Ordinal, clause.HandlerOffset,
            clause.Kind == HybridCpuManagedEhClauseKindV1.Catch
                ? new[] { RestrictedCilTypeV1.ObjectReference } : Array.Empty<RestrictedCilTypeV1>(),
            clause.Kind == HybridCpuManagedEhClauseKindV1.Catch ? 10 : null)).ToArray();

    public IReadOnlyList<ManagedEhLeaveTransferV1> LeaveTransfers => Operations
        .Where(operation => operation.Kind == ManagedEhOperationKindV1.Leave)
        .Select(operation => new ManagedEhLeaveTransferV1(operation.IlOffset, operation.LeaveTarget,
            Clauses.Select(clause => (Clause: clause,
                    Start: clause.Kind == HybridCpuManagedEhClauseKindV1.Finally ? clause.TryOffset : clause.HandlerOffset,
                    Length: clause.Kind == HybridCpuManagedEhClauseKindV1.Finally ? clause.TryLength : clause.HandlerLength))
                .Where(region => Contains(region.Start, region.Length, operation.IlOffset) &&
                    !Contains(region.Start, region.Length, operation.LeaveTarget))
                .OrderBy(region => region.Length).ThenByDescending(region => region.Start)
                .ThenBy(region => region.Clause.Ordinal)
                .Select(region => new ManagedEhLeaveActionV1(
                    region.Clause.Kind == HybridCpuManagedEhClauseKindV1.Finally
                        ? ManagedEhLeaveActionKindV1.InvokeFinally : ManagedEhLeaveActionKindV1.ReleaseCatchScope,
                    region.Clause.Ordinal, region.Clause.HandlerOffset)).ToArray())).ToArray();

    public ManagedEhFinallyContinuationPlanV1 FinallyContinuations =>
        ManagedEhFinallyContinuationPlannerV1.Create(this);

    private static bool Contains(int start, int length, int offset) =>
        offset >= start && (long)offset < (long)start + length;
}

public static class ManagedEhFinallyContinuationPlannerV1
{
    public static ManagedEhFinallyContinuationPlanV1 Create(ManagedEhMethodPlanV1 plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var continuations = new List<ManagedEhFinallyContinuationV1>();
        int token = 1;
        foreach (ManagedEhLeaveTransferV1 leave in plan.LeaveTransfers.OrderBy(static row => row.IlOffset))
        {
            ManagedEhLeaveActionV1[] finallyActions = leave.Actions
                .Where(static action => action.Kind == ManagedEhLeaveActionKindV1.InvokeFinally).ToArray();
            if (finallyActions.Length == 0) continue;
            if (token > HybridCpuManagedEhSchemaV1.MaximumOperationsPerMethod)
                throw new InvalidOperationException("Managed finally continuation-token budget exceeded.");
            var steps = new ManagedEhFinallyContinuationStepV1[finallyActions.Length];
            int stepIndex = 0;
            int pendingCatchReleases = 0;
            foreach (ManagedEhLeaveActionV1 action in leave.Actions)
            {
                if (action.Kind == ManagedEhLeaveActionKindV1.ReleaseCatchScope)
                {
                    pendingCatchReleases++;
                    continue;
                }
                ManagedEhLeaveActionV1? next = stepIndex + 1 < finallyActions.Length ? finallyActions[stepIndex + 1] : null;
                if (!plan.Clauses.Any(clause => clause.Ordinal == action.ClauseOrdinal &&
                        clause.Kind == HybridCpuManagedEhClauseKindV1.Finally && clause.HandlerOffset == action.HandlerOffset))
                    throw new InvalidOperationException("Managed finally continuation references a non-finally clause.");
                steps[stepIndex++] = new(action.ClauseOrdinal, action.HandlerOffset, pendingCatchReleases,
                    next?.ClauseOrdinal, next?.HandlerOffset ?? leave.TargetOffset);
                pendingCatchReleases = 0;
            }
            continuations.Add(new(token++, leave.IlOffset, leave.TargetOffset, steps, pendingCatchReleases));
        }
        ManagedEhFinallyContinuationV1[] ordered = continuations.OrderBy(static row => row.Token).ToArray();
        string digest = HybridCpuPlatformContractV1.Hash(string.Join('|',
            "hybridcpu.managed-finally-continuation/v1", plan.ContractDigest,
            ManagedEhFinallyContinuationPlanV1.ReservedExceptionalToken,
            string.Join(';', ordered.Select(row =>
                $"{row.Token}:{row.LeaveOffset}:{row.TargetOffset}:" +
                string.Join(',', row.Steps.Select(step =>
                    $"release={step.ReleaseCatchScopesBeforeEntry}:{step.ClauseOrdinal}@{step.HandlerOffset}->{step.NextClauseOrdinal?.ToString() ?? "target"}@{step.NextOffset}")) +
                $":release-target={row.ReleaseCatchScopesBeforeTarget}"))));
        return new(ManagedEhFinallyContinuationPlanV1.ReservedExceptionalToken, ordered, digest);
    }
}

public sealed record ManagedEhImportResultV1(
    ManagedEhImportStatusV1 Status,
    ManagedEhMethodPlanV1? Plan,
    string Code,
    string Reason)
{
    public ManagedEhControlFlowV1? ControlFlow { get; init; }
}

public enum ManagedEhEdgeKindV1 : byte
{
    Normal = 0,
    LeaveContinuation = 1,
    ExceptionDispatchCandidate = 2
}

public sealed record ManagedEhEdgeV1(int SourceOffset, int TargetOffset, ManagedEhEdgeKindV1 Kind,
    int? ClauseOrdinal = null);

public sealed record ManagedEhBasicBlockV1(int Id, int StartOffset, int EndOffsetExclusive,
    IReadOnlyList<int> InstructionOffsets, IReadOnlyList<int> HandlerClauseOrdinals);

// SourceOffset remains explicit: an exceptional edge observes pre-instruction state,
// including when its source is in the middle of the basic block.
public sealed record ManagedEhBlockEdgeV1(int SourceBlock, int TargetBlock, int SourceOffset,
    ManagedEhEdgeKindV1 Kind, int? ClauseOrdinal);

public sealed record ManagedEhStateHomeV1(string Slot, RestrictedCilTypeV1 Type,
    string? AggregateIdentity, bool InitializedAtMethodEntry)
{
    public bool IsObjectRoot => Type == RestrictedCilTypeV1.ObjectReference;
}

public sealed record ManagedEhTypedValueV1(string Identity, RestrictedCilTypeV1 Type, bool Initialized,
    ulong? Constant, string? AggregateIdentity, string? ReceiverIdentity, ulong? FunctionPointerSignatureId);
public sealed record ManagedEhTypedStateV1(int IlOffset, IReadOnlyList<ManagedEhTypedValueV1> Stack,
    IReadOnlyList<ManagedEhTypedValueV1> Locals, IReadOnlyList<ManagedEhTypedValueV1> Arguments);
public sealed record ManagedEhTypedEdgeStateV1(ManagedEhEdgeV1 Edge, ManagedEhTypedStateV1 State);
public sealed record ManagedEhTypedDataflowV1(IReadOnlyList<ManagedEhTypedStateV1> Entries,
    IReadOnlyList<ManagedEhTypedEdgeStateV1> EdgeStates, string Digest);

public enum ManagedEhHomeAccessKindV1 : byte { Initialize = 0, StoreAfterDefinition = 1, ReloadAtHandlerEntry = 2 }
public enum ManagedEhHomeValueKindV1 : byte { ExistingSsaValue = 0, ZeroConstant = 1, NonZeroConstant = 2, ReloadDefinition = 3 }
public sealed record ManagedEhHomeAccessV1(ManagedEhHomeAccessKindV1 Kind, string Slot,
    int IlOffset, RestrictedCilTypeV1 Type, bool IsObjectRoot, string ValueIdentity,
    ManagedEhHomeValueKindV1 ValueKind, ulong? ConstantValue);
public sealed record ManagedEhHomeAccessPlanV1(IReadOnlyList<ManagedEhHomeAccessV1> Accesses, string Digest);
public enum ManagedEhInsertionPlacementV1 : byte { Before = 0, After = 1 }
public sealed record ManagedEhHomeInsertionV1(int AnchorIlOffset, ManagedEhInsertionPlacementV1 Placement,
    ManagedEhHomeAccessV1 Access, IrInstruction Instruction);

/// <summary>Instruction-level EH graph for subsequent typed dataflow. Dispatch candidates
/// are conservative exceptional edges, never ordinary branches or promises that a catch matches.
/// Leave continuation is traversable only after its ordered cleanup actions complete.</summary>
public sealed record ManagedEhControlFlowV1(IReadOnlyList<int> InstructionOffsets,
    IReadOnlyList<ManagedEhEdgeV1> Edges, string Digest)
{
    public IReadOnlyList<string> RequiredStateHomes { get; init; } = [];
    public IReadOnlyList<ManagedEhStateHomeV1> StateHomes { get; init; } = [];
    public IReadOnlyList<ManagedEhBasicBlockV1> Blocks { get; init; } = [];
    public IReadOnlyList<ManagedEhBlockEdgeV1> BlockEdges { get; init; } = [];
    public IReadOnlyDictionary<int, IReadOnlyList<string>> LiveStateAtEntry { get; init; } =
        new Dictionary<int, IReadOnlyList<string>>();
}

public static class ManagedEhPlanContractV1
{
    public static string ComputeDigest(string methodIdentity, string instructionIdentityPrefix, int methodBodySize,
        IReadOnlyList<ManagedEhClausePlanV1> clauses, IReadOnlyList<ManagedEhOperationPlanV1> operations) =>
        HybridCpuPlatformContractV1.Hash(string.Join('|', HybridCpuManagedEhSchemaV1.SchemaId, methodIdentity,
            instructionIdentityPrefix, methodBodySize,
            string.Join(';', clauses.Select(static row =>
                $"{row.Kind}:{row.TryOffset}:{row.TryLength}:{row.HandlerOffset}:{row.HandlerLength}:{row.CatchTypeIdentity}:{row.CatchTypeId}:{row.Ordinal}")),
            string.Join(';', operations.Select(static row =>
                $"{row.Kind}:{row.IlOffset}:{row.LeaveTarget}:{row.RuntimeHelperSymbol}"))));
}

public sealed record ManagedEhNativeOperationV1(
    ManagedEhOperationKindV1 Kind,
    int NativeOffsetBytes,
    int LeaveTargetOffsetBytes,
    string RuntimeHelperSymbol);

public sealed record ManagedEhNativeFinallyContinuationStepV1(
    int ClauseOrdinal,
    int HandlerOffsetBytes,
    int HandlerEndOffsetBytes,
    int ReleaseCatchScopesBeforeEntry,
    int? NextClauseOrdinal,
    int NextOffsetBytes);

public sealed record ManagedEhNativeFinallyContinuationV1(
    int Token,
    int LeaveOffsetBytes,
    int TargetOffsetBytes,
    IReadOnlyList<ManagedEhNativeFinallyContinuationStepV1> Steps,
    int ReleaseCatchScopesBeforeTarget);

public sealed record ManagedEhNativeFinallyContinuationPlanV1(
    int TokenSlotOffsetFromAdjustedStackPointerBytes,
    IReadOnlyList<ManagedEhNativeFinallyContinuationV1> Continuations,
    byte[] Encoding,
    string Digest);

public sealed record ManagedEhFinalizationArtifactV1(
    HybridCpuManagedMetadataStatusV1 Status,
    string Reason,
    IReadOnlyList<HybridCpuManagedEhClauseRegistrationV1> Clauses,
    IReadOnlyList<ManagedEhNativeOperationV1> Operations,
    byte[] EhInfo,
    byte[] UnwindInfo,
    HybridCpuManagedUnwindRecordV2? Unwind,
    IReadOnlyList<HybridCpuObjectSectionV1> ObjectSections,
    string ResultDigest,
    ManagedEhNativeFinallyContinuationPlanV1? FinallyContinuations = null);

public sealed partial class RestrictedCilImporterV1
{
    private static readonly IReadOnlyDictionary<ushort, OpCode> ManagedEhOpcodes = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(static field => field.FieldType == typeof(OpCode))
        .Select(static field => (OpCode)field.GetValue(null)!)
        .ToDictionary(static opcode => unchecked((ushort)opcode.Value));

    public ManagedEhImportResultV1 ImportManagedEhPlan(
        ReadOnlyMemory<byte> peImage,
        RestrictedCilMethodSelectorV1 selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        if (_mode != RestrictedCilImportModeV1.ScalarControlFlowV2)
            return EhFailure(ManagedEhImportStatusV1.Unsupported, "HCCIL1801", "Managed EH requires ScalarControlFlowV2.");
        if (peImage.Length == 0 || peImage.Length > _budgets.MaximumPeBytes)
            return EhFailure(ManagedEhImportStatusV1.BudgetExhausted, "HCCIL2801", "Managed PE input exceeds the deterministic budget.");
        try
        {
            using var stream = new MemoryStream(peImage.ToArray(), writable: false);
            using var pe = new PEReader(stream, PEStreamOptions.LeaveOpen);
            if (!pe.HasMetadata) return EhFailure(ManagedEhImportStatusV1.InvalidInput, "HCCIL0801", "Input has no managed metadata.");
            MetadataReader metadata = pe.GetMetadataReader();
            MethodSelection selection = SelectMethod(metadata, selector);
            if (selection.Failure is not null)
                return EhFailure(ManagedEhImportStatusV1.InvalidInput, selection.Failure.Diagnostics[0].Code,
                    selection.Failure.Diagnostics[0].Message);
            MethodDefinition method = metadata.GetMethodDefinition(selection.Method);
            MethodBodyBlock body = pe.GetMethodBody(method.RelativeVirtualAddress);
            if (body.ExceptionRegions.Length == 0)
                return EhFailure(ManagedEhImportStatusV1.Unsupported, "HCCIL1802", "The selected method has no EH clauses.");
            if (body.ExceptionRegions.Length > HybridCpuManagedEhSchemaV1.MaximumClausesPerMethod)
                return EhFailure(ManagedEhImportStatusV1.BudgetExhausted, "HCCIL2802", "The EH clause budget was exhausted.");
            if (body.ExceptionRegions.Any(static region =>
                    region.Kind is ExceptionRegionKind.Filter or ExceptionRegionKind.Fault))
                return EhFailure(ManagedEhImportStatusV1.Unsupported, "HCCIL1803",
                    "Filter and fault clauses are outside managed EH V1.");

            IReadOnlyList<RestrictedCilGenericArgumentV1> typeArguments = selector.GenericTypeArguments ?? [];
            IReadOnlyList<RestrictedCilGenericArgumentV1> methodArguments = selector.GenericMethodArguments ?? [];
            MethodSignature signature = ParseMethodSignature(metadata, method.Signature, typeArguments, methodArguments,
                allowAggregates: true);
            signature = ProjectScalarValueSignature(metadata, BindValueReceiver(metadata, selection.Type, signature));
            LocalSignature locals = ProjectScalarValueLocals(metadata,
                ParseLocalSignature(metadata, body.LocalSignature, typeArguments, methodArguments, allowAggregates: true));
            if (signature.Status != SignatureStatus.Success || locals.Status != SignatureStatus.Success)
                return EhFailure(ManagedEhImportStatusV1.Unsupported, "HCCIL1805",
                    "EH state requires exact supported method and local signatures.");
            ManagedMethodIdentityV1 identity = CreateManagedMethodIdentity(metadata, selection.Type, selection.Method,
                signature, typeArguments, methodArguments);
            byte[] il = body.GetILBytes() ?? [];
            var provenance = new RestrictedCilProvenanceV1("managed-eh-plan", HashBytes(peImage.Span),
                identity.StableIdentity, $"0x{identity.MetadataToken:x8}", _matrix.ContractDigest,
                RestrictedCilSupportMatrixV1.OptionsDigest(_budgets), $"{identity.DeclaringType}.{identity.MethodName}");
            DecodeResult decoded = DecodeManagedEhBody(il, provenance);
            if (decoded.Failure is not null)
                return EhFailure(decoded.Failure.Status == RestrictedCilImportStatusV1.InvalidInput
                    ? ManagedEhImportStatusV1.InvalidInput : ManagedEhImportStatusV1.Unsupported,
                    decoded.Failure.Diagnostics[0].Code, decoded.Failure.Diagnostics[0].Message);

            var clauses = new List<ManagedEhClausePlanV1>(body.ExceptionRegions.Length);
            var boundaries = decoded.Instructions.Select(static instruction => instruction.Offset).ToHashSet();
            boundaries.Add(il.Length);
            int ordinal = 0;
            foreach (ExceptionRegion region in body.ExceptionRegions.OrderBy(static row => row.TryOffset)
                         .ThenBy(static row => row.TryLength).ThenBy(static row => row.HandlerOffset))
            {
                if (region.Kind is not (ExceptionRegionKind.Catch or ExceptionRegionKind.Finally) ||
                    region.TryOffset < 0 || region.TryLength <= 0 || region.HandlerOffset < 0 || region.HandlerLength <= 0 ||
                    region.TryOffset > il.Length - region.TryLength || region.HandlerOffset > il.Length - region.HandlerLength ||
                    !boundaries.Contains(region.TryOffset) || !boundaries.Contains(region.TryOffset + region.TryLength) ||
                    !boundaries.Contains(region.HandlerOffset) || !boundaries.Contains(region.HandlerOffset + region.HandlerLength) ||
                    region.TryOffset < region.HandlerOffset + region.HandlerLength && region.HandlerOffset < region.TryOffset + region.TryLength)
                    return EhFailure(ManagedEhImportStatusV1.InvalidInput, "HCCIL0802", "EH clause ranges are malformed.");
                string? catchIdentity = null;
                ulong catchTypeId = 0;
                if (region.Kind == ExceptionRegionKind.Catch)
                {
                    catchIdentity = ExactTypeHandleName(metadata, region.CatchType);
                    if (string.IsNullOrWhiteSpace(catchIdentity))
                        return EhFailure(ManagedEhImportStatusV1.Unsupported, "HCCIL1804", "Catch type identity is not exact.");
                    catchTypeId = ManagedTypeId(catchIdentity);
                }
                clauses.Add(new(region.Kind == ExceptionRegionKind.Catch
                        ? HybridCpuManagedEhClauseKindV1.Catch : HybridCpuManagedEhClauseKindV1.Finally,
                    region.TryOffset, region.TryLength, region.HandlerOffset, region.HandlerLength,
                    catchIdentity, catchTypeId, ordinal++));
            }

            ManagedEhOperationPlanV1[] operations = decoded.Instructions.Where(static row =>
                    row.Encoding is 0x7a or 0xfe1a or 0xdd or 0xde or 0xdc)
                .Select(static row => new ManagedEhOperationPlanV1(row.Encoding switch
                    {
                        0x7a => ManagedEhOperationKindV1.Throw,
                        0xfe1a => ManagedEhOperationKindV1.Rethrow,
                        0xdc => ManagedEhOperationKindV1.EndFinally,
                        _ => ManagedEhOperationKindV1.Leave
                    }, row.Offset, row.BranchTarget,
                    row.Encoding == 0x7a ? "__hybridcpu_managed_throw" :
                    row.Encoding == 0xfe1a ? "__hybridcpu_managed_rethrow" :
                    row.Encoding == 0xdc ? "__hybridcpu_managed_endfinally" : string.Empty))
                .OrderBy(static row => row.IlOffset).ToArray();
            // Ordinary branches cannot perform an EH transfer. Keep protected-region
            // membership exact, including switch edges, before constructing handler SSA.
            int[] RegionsAt(int offset) => clauses.SelectMany(clause => new[]
                {
                    (Id: clause.Ordinal * 2, Start: clause.TryOffset, Length: clause.TryLength),
                    (Id: clause.Ordinal * 2 + 1, Start: clause.HandlerOffset, Length: clause.HandlerLength)
                }).Where(region => offset >= region.Start && offset < region.Start + region.Length)
                .Select(region => region.Id).ToArray();
            foreach (DecodedInstruction instruction in decoded.Instructions)
            {
                int[] sourceRegions = RegionsAt(instruction.Offset);
                if (instruction.Encoding == 0x2a && sourceRegions.Length != 0)
                    return EhFailure(ManagedEhImportStatusV1.InvalidInput, "HCCIL0815",
                        "ret cannot exit a protected region; leave must perform the EH transfer.");
                if (instruction.Encoding is 0xdd or 0xde) continue;
                foreach (int target in ControlFlowTargets(instruction))
                    if (!sourceRegions.SequenceEqual(RegionsAt(target)))
                        return EhFailure(ManagedEhImportStatusV1.InvalidInput, "HCCIL0816",
                            "An ordinary branch cannot cross a try or handler boundary.");
            }
            foreach (var operation in operations)
            {
                static bool Within(int offset, int start, int length) => offset >= start && offset < start + length;
                var handler = clauses.Where(clause => Within(operation.IlOffset, clause.HandlerOffset, clause.HandlerLength))
                    .OrderBy(static clause => clause.HandlerLength).FirstOrDefault();
                if (operation.Kind == ManagedEhOperationKindV1.Rethrow && handler?.Kind != HybridCpuManagedEhClauseKindV1.Catch)
                    return EhFailure(ManagedEhImportStatusV1.InvalidInput, "HCCIL0812", "rethrow requires an enclosing catch handler.");
                if (operation.Kind == ManagedEhOperationKindV1.EndFinally && handler?.Kind != HybridCpuManagedEhClauseKindV1.Finally)
                    return EhFailure(ManagedEhImportStatusV1.InvalidInput, "HCCIL0813", "endfinally requires an enclosing finally handler.");
                if (operation.Kind == ManagedEhOperationKindV1.Leave &&
                    (handler?.Kind == HybridCpuManagedEhClauseKindV1.Finally &&
                        !Within(operation.LeaveTarget, handler.HandlerOffset, handler.HandlerLength) || clauses.Any(clause =>
                        Within(operation.LeaveTarget, clause.TryOffset, clause.TryLength) && !Within(operation.IlOffset, clause.TryOffset, clause.TryLength) ||
                        Within(operation.LeaveTarget, clause.HandlerOffset, clause.HandlerLength) && !Within(operation.IlOffset, clause.HandlerOffset, clause.HandlerLength))))
                    return EhFailure(ManagedEhImportStatusV1.InvalidInput, "HCCIL0814", "leave cannot exit a finally or enter a new protected region.");
            }
            string instructionPrefix = $"cil:{provenance.PeSha256}:{provenance.CanonicalMethodLocalIdentity}:il_";
            string digest = ManagedEhPlanContractV1.ComputeDigest(identity.StableIdentity, instructionPrefix,
                il.Length, clauses, operations);
            var graph = BuildManagedEhControlFlow(decoded.Instructions, clauses, digest);
            if (graph is null)
                return EhFailure(ManagedEhImportStatusV1.BudgetExhausted, "HCCIL2803",
                    "The conservative exceptional control-flow edge budget was exhausted.");
            var stateFailure = BindManagedEhState(decoded.Instructions, signature, locals,
                body.LocalVariablesInitialized, ref graph);
            if (stateFailure is not null) return stateFailure;
            if (!BindManagedEhBlocks(decoded.Instructions, clauses, ref graph))
                return EhFailure(ManagedEhImportStatusV1.BudgetExhausted, "HCCIL2804",
                    "EH basic-block count exceeds the configured budget.");
            return new(ManagedEhImportStatusV1.Success,
                new(identity.StableIdentity,
                    instructionPrefix,
                    il.Length, clauses, operations, digest) { ControlFlow = graph }, string.Empty, string.Empty) { ControlFlow = graph };
        }
        catch (Exception exception) when (exception is BadImageFormatException or ArgumentException or OverflowException)
        {
            return EhFailure(ManagedEhImportStatusV1.InvalidInput, "HCCIL0803", exception.Message);
        }
    }

    private static ManagedEhControlFlowV1? BuildManagedEhControlFlow(
        IReadOnlyList<DecodedInstruction> instructions, IReadOnlyList<ManagedEhClausePlanV1> clauses,
        string planDigest)
    {
        const int maximumEdges = 65536;
        var edges = new HashSet<ManagedEhEdgeV1>();
        for (int index = 0; index < instructions.Count; index++)
        {
            DecodedInstruction instruction = instructions[index];
            bool leave = instruction.Encoding is 0xdd or 0xde;
            foreach (int target in ControlFlowTargets(instruction))
                edges.Add(new(instruction.Offset, target,
                    leave ? ManagedEhEdgeKindV1.LeaveContinuation : ManagedEhEdgeKindV1.Normal));
            FlowControl flow = ManagedEhOpcodes[instruction.Encoding].FlowControl;
            if (!leave && flow is not (FlowControl.Branch or FlowControl.Return or FlowControl.Throw) &&
                index + 1 < instructions.Count)
                edges.Add(new(instruction.Offset, instructions[index + 1].Offset, ManagedEhEdgeKindV1.Normal));
            // Conservatively retain pre-instruction local/argument state at every point
            // in a protected try. Stack values are discarded, not merged into handlers.
            foreach (var clause in clauses)
                if (instruction.Offset >= clause.TryOffset && instruction.Offset < clause.TryOffset + clause.TryLength)
                    edges.Add(new(instruction.Offset, clause.HandlerOffset,
                        ManagedEhEdgeKindV1.ExceptionDispatchCandidate, clause.Ordinal));
            if (edges.Count > maximumEdges) return null;
        }
        var ordered = edges.OrderBy(edge => edge.SourceOffset).ThenBy(edge => edge.Kind)
            .ThenBy(edge => edge.TargetOffset).ThenBy(edge => edge.ClauseOrdinal).ToArray();
        int[] offsets = instructions.Select(instruction => instruction.Offset).ToArray();
        string digest = HybridCpuPlatformContractV1.Hash(string.Join('|', "hybridcpu.eh-cfg/v1", planDigest,
            string.Join(',', offsets), string.Join(';', ordered.Select(edge =>
                $"{edge.SourceOffset}:{edge.TargetOffset}:{edge.Kind}:{edge.ClauseOrdinal}"))));
        var uses = instructions.ToDictionary(instruction => instruction.Offset, StateUse);
        var definitions = instructions.ToDictionary(instruction => instruction.Offset, StateDefinition);
        var live = offsets.ToDictionary(offset => offset, _ => new HashSet<string>(StringComparer.Ordinal));
        var outgoing = ordered.ToLookup(edge => edge.SourceOffset);
        // Slots whose address escapes cannot be reasoned about using scalar stloc kills.
        var addressed = instructions.Where(instruction => instruction.Encoding is 0x0f or 0x12 or 0xfe0a or 0xfe0d)
            .Select(StateUse).OfType<string>().ToHashSet(StringComparer.Ordinal);
        bool changed;
        do
        {
            changed = false;
            foreach (var instruction in instructions.Reverse())
            {
                var next = new HashSet<string>(StringComparer.Ordinal);
                foreach (var edge in outgoing[instruction.Offset])
                    foreach (string slot in live[edge.TargetOffset])
                        if (edge.Kind == ManagedEhEdgeKindV1.ExceptionDispatchCandidate ||
                            slot != definitions[instruction.Offset] || addressed.Contains(slot))
                            next.Add(slot);
                if (uses[instruction.Offset] is string used) next.Add(used);
                // endfinally has a runtime continuation, not a statically unique successor.
                // Conservatively retain all method state for finally methods until that
                // continuation is represented in typed SSA.
                if (instruction.Encoding == 0xdc)
                    next.UnionWith(uses.Values.OfType<string>());
                if (!live[instruction.Offset].SetEquals(next))
                {
                    live[instruction.Offset] = next;
                    changed = true;
                }
            }
        } while (changed);
        var homes = ordered.Where(edge => edge.Kind == ManagedEhEdgeKindV1.ExceptionDispatchCandidate)
            .SelectMany(edge => live[edge.TargetOffset]).Concat(addressed).Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).ToArray();
        var liveEntries = live.ToDictionary(row => row.Key,
            row => (IReadOnlyList<string>)row.Value.Order(StringComparer.Ordinal).ToArray());
        digest = HybridCpuPlatformContractV1.Hash(string.Join('|', digest, "eh-state-liveness/v1",
            string.Join(',', homes), string.Join(';', liveEntries.OrderBy(row => row.Key)
                .Select(row => $"{row.Key}:{string.Join(',', row.Value)}"))));
        return new(offsets, ordered, digest) { RequiredStateHomes = homes, LiveStateAtEntry = liveEntries };

    }

    private static string? StateUse(DecodedInstruction instruction) => instruction.Encoding switch
        {
            >= 0x02 and <= 0x05 => $"arg:{instruction.Encoding - 0x02}",
            >= 0x06 and <= 0x09 => $"local:{instruction.Encoding - 0x06}",
            0x0e or 0x0f or 0xfe09 or 0xfe0a => $"arg:{instruction.Literal}",
            0x11 or 0x12 or 0xfe0c or 0xfe0d => $"local:{instruction.Literal}",
            _ => null
        };
    private static string? StateDefinition(DecodedInstruction instruction) => instruction.Encoding switch
        {
            >= 0x0a and <= 0x0d => $"local:{instruction.Encoding - 0x0a}",
            0x10 or 0xfe0b => $"arg:{instruction.Literal}",
            0x13 or 0xfe0e => $"local:{instruction.Literal}",
            _ => null
        };

    private bool BindManagedEhBlocks(IReadOnlyList<DecodedInstruction> instructions,
        IReadOnlyList<ManagedEhClausePlanV1> clauses, ref ManagedEhControlFlowV1 graph)
    {
        int end = instructions[^1].Offset + instructions[^1].Size;
        var leaders = new SortedSet<int> { instructions[0].Offset };
        foreach (var clause in clauses)
            foreach (int boundary in new[] { clause.TryOffset, clause.TryOffset + clause.TryLength,
                         clause.HandlerOffset, clause.HandlerOffset + clause.HandlerLength })
                if (boundary < end) leaders.Add(boundary);
        for (int index = 0; index < instructions.Count; index++)
        {
            var instruction = instructions[index];
            foreach (int target in ControlFlowTargets(instruction)) leaders.Add(target);
            if (ManagedEhOpcodes[instruction.Encoding].FlowControl is
                FlowControl.Branch or FlowControl.Cond_Branch or FlowControl.Return or FlowControl.Throw)
                if (index + 1 < instructions.Count) leaders.Add(instructions[index + 1].Offset);
        }
        if (leaders.Count > _budgets.MaximumBasicBlocks) return false;
        int[] starts = leaders.ToArray();
        var blocks = new List<ManagedEhBasicBlockV1>(starts.Length);
        var owner = new Dictionary<int, int>();
        int cursor = 0;
        for (int id = 0; id < starts.Length; id++)
        {
            int limit = id + 1 < starts.Length ? starts[id + 1] : end;
            var offsets = new List<int>();
            while (cursor < instructions.Count && instructions[cursor].Offset < limit)
            {
                int offset = instructions[cursor++].Offset;
                offsets.Add(offset);
                owner.Add(offset, id);
            }
            blocks.Add(new(id, starts[id], limit, offsets.ToArray(),
                clauses.Where(clause => clause.HandlerOffset == starts[id]).Select(clause => clause.Ordinal).ToArray()));
        }
        var edges = graph.Edges.Where(edge => owner[edge.SourceOffset] != owner[edge.TargetOffset] ||
                edge.Kind != ManagedEhEdgeKindV1.Normal ||
                blocks[owner[edge.SourceOffset]].InstructionOffsets[^1] == edge.SourceOffset)
            .Select(edge => new ManagedEhBlockEdgeV1(owner[edge.SourceOffset], owner[edge.TargetOffset],
                edge.SourceOffset, edge.Kind, edge.ClauseOrdinal)).ToArray();
        graph = graph with
        {
            Blocks = blocks.ToArray(), BlockEdges = edges,
            Digest = HybridCpuPlatformContractV1.Hash(string.Join('|', graph.Digest, "eh-basic-blocks/v1",
                string.Join(';', blocks.Select(block => $"{block.Id}:{block.StartOffset}:{block.EndOffsetExclusive}:{string.Join(',', block.HandlerClauseOrdinals)}")),
                string.Join(';', edges.Select(edge => $"{edge.SourceBlock}:{edge.TargetBlock}:{edge.SourceOffset}:{edge.Kind}:{edge.ClauseOrdinal}"))))
        };
        return true;
    }

    private static ManagedEhImportResultV1? BindManagedEhState(
        IReadOnlyList<DecodedInstruction> instructions, MethodSignature signature, LocalSignature locals,
        bool initLocals, ref ManagedEhControlFlowV1 graph)
    {
        var slots = new Dictionary<string, ManagedEhStateHomeV1>(StringComparer.Ordinal);
        for (int index = 0; index < signature.Parameters.Count; index++)
            slots.Add($"arg:{index}", new($"arg:{index}", signature.Parameters[index],
                signature.AggregateParameters?.ElementAtOrDefault(index), true));
        for (int index = 0; index < locals.Types.Count; index++)
            slots.Add($"local:{index}", new($"local:{index}", locals.Types[index],
                locals.Aggregates?.ElementAtOrDefault(index), initLocals));
        foreach (var instruction in instructions)
            foreach (string slot in new[] { StateUse(instruction), StateDefinition(instruction) }.OfType<string>())
                if (!slots.ContainsKey(slot))
                    return EhFailure(ManagedEhImportStatusV1.InvalidInput, "HCCIL0817",
                        $"EH state access at IL_{instruction.Offset:x4} references undeclared {slot}.");

        // Must-analysis: exceptional edges carry state BEFORE the throwing instruction;
        // normal/leave edges carry state after a successful assignment. Seed only the
        // real method entry; an EH handler is not a fresh zero-initialized invocation.
        var outgoing = graph.Edges.ToLookup(edge => edge.SourceOffset);
        var byOffset = instructions.ToDictionary(instruction => instruction.Offset);
        var assigned = new Dictionary<int, HashSet<string>>
        {
            [instructions[0].Offset] = slots.Values.Where(slot => slot.InitializedAtMethodEntry)
                .Select(slot => slot.Slot).ToHashSet(StringComparer.Ordinal)
        };
        var work = new SortedSet<int> { instructions[0].Offset };
        while (work.Count != 0)
        {
            int offset = work.Min;
            work.Remove(offset);
            foreach (var edge in outgoing[offset])
            {
                var candidate = new HashSet<string>(assigned[offset], StringComparer.Ordinal);
                if (edge.Kind != ManagedEhEdgeKindV1.ExceptionDispatchCandidate &&
                    StateDefinition(byOffset[offset]) is string defined) candidate.Add(defined);
                if (!assigned.TryGetValue(edge.TargetOffset, out var current))
                {
                    assigned.Add(edge.TargetOffset, candidate);
                    work.Add(edge.TargetOffset);
                }
                else
                {
                    int previousCount = current.Count;
                    current.IntersectWith(candidate);
                    if (current.Count != previousCount) work.Add(edge.TargetOffset);
                }
            }
        }
        foreach (var instruction in instructions)
            if (assigned.TryGetValue(instruction.Offset, out var state) && StateUse(instruction) is string used &&
                instruction.Encoding is not (0x0f or 0x12 or 0xfe0a or 0xfe0d) && !state.Contains(used))
                return EhFailure(ManagedEhImportStatusV1.Unsupported, "HCCIL1818",
                    $"Conservative EH analysis cannot prove initialization of {used} at IL_{instruction.Offset:x4}.");
        var homes = graph.RequiredStateHomes.Select(slot => slots[slot]).ToArray();
        graph = graph with
        {
            StateHomes = homes,
            Digest = HybridCpuPlatformContractV1.Hash(string.Join('|', graph.Digest, "typed-eh-homes/v1", initLocals,
                string.Join(';', homes.Select(home =>
                    $"{home.Slot}:{home.Type}:{home.AggregateIdentity}:{home.InitializedAtMethodEntry}"))))
        };
        return null;
    }

    private DecodeResult DecodeManagedEhBody(byte[] bytes, RestrictedCilProvenanceV1 provenance)
    {
        var instructions = new List<DecodedInstruction>();
        var targets = new List<int>();
        int offset = 0;
        while (offset < bytes.Length)
        {
            if (instructions.Count >= _budgets.MaximumDecodedInstructions)
                return new([], Reject(RestrictedCilImportStatusV1.BudgetExhausted, "HCCIL2004",
                    "The decoded instruction budget was exhausted.", OffsetIdentity(provenance, offset), provenance));
            int start = offset;
            ushort encoding = bytes[offset++] is byte first && first == 0xfe
                ? offset < bytes.Length ? (ushort)(0xfe00 | bytes[offset++]) : ushort.MaxValue
                : first;
            if (!ManagedEhOpcodes.TryGetValue(encoding, out OpCode opcode))
                return InvalidDecode($"Opcode encoding 0x{encoding:x4} is invalid or reserved.", provenance, start);

            int operandStart = offset;
            int operandBytes;
            try
            {
                operandBytes = opcode.OperandType switch
                {
                    OperandType.InlineNone => 0,
                    OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                    OperandType.InlineVar => 2,
                    OperandType.InlineBrTarget or OperandType.InlineField or OperandType.InlineI or
                    OperandType.InlineMethod or OperandType.InlineSig or OperandType.InlineString or
                    OperandType.InlineTok or OperandType.InlineType or OperandType.ShortInlineR => 4,
                    OperandType.InlineI8 or OperandType.InlineR => 8,
                    OperandType.InlineSwitch when bytes.Length - offset >= 4 => checked(4 +
                        4 * BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, 4))),
                    _ => -1
                };
            }
            catch (OverflowException)
            {
                return InvalidDecode($"Operand for {opcode.Name} overflowed.", provenance, start);
            }
            if (operandBytes < 0 || bytes.Length - offset < operandBytes)
                return InvalidDecode($"Operand for {opcode.Name} is truncated.", provenance, start);
            offset += operandBytes;

            int branchTarget = -1;
            int[]? switchTargets = null;
            if (opcode.OperandType == OperandType.ShortInlineBrTarget)
            {
                branchTarget = checked(offset + unchecked((sbyte)bytes[operandStart]));
                targets.Add(branchTarget);
            }
            else if (opcode.OperandType == OperandType.InlineBrTarget)
            {
                branchTarget = checked(offset + BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(operandStart, 4)));
                targets.Add(branchTarget);
            }
            else if (opcode.OperandType == OperandType.InlineSwitch)
            {
                int count = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(operandStart, 4));
                if (count < 0) return InvalidDecode("Switch target count is negative.", provenance, start);
                switchTargets = new int[count];
                for (int index = 0; index < count; index++)
                {
                    switchTargets[index] = checked(offset + BinaryPrimitives.ReadInt32LittleEndian(
                        bytes.AsSpan(operandStart + 4 + index * 4, 4)));
                    targets.Add(switchTargets[index]);
                }
            }
            int token = opcode.OperandType is OperandType.InlineField or OperandType.InlineMethod or
                OperandType.InlineSig or OperandType.InlineString or OperandType.InlineTok or OperandType.InlineType
                ? BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(operandStart, 4)) : 0;
            instructions.Add(new(start, offset - start, encoding, opcode.Name ?? $"0x{encoding:x4}",
                opcode.OperandType == OperandType.ShortInlineVar ? bytes[operandStart] :
                    opcode.OperandType == OperandType.InlineVar ? BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(operandStart, 2)) : 0,
                branchTarget, token, SwitchTargets: switchTargets));
        }
        if (instructions.Count == 0) return InvalidDecode("The selected method has an empty CIL body.", provenance, 0);
        HashSet<int> boundaries = instructions.Select(static instruction => instruction.Offset).ToHashSet();
        if (targets.Any(target => !boundaries.Contains(target)))
            return InvalidDecode("A branch target is not a CIL instruction boundary.", provenance, 0);
        return new(instructions, null);
    }

    private static ManagedEhImportResultV1 EhFailure(ManagedEhImportStatusV1 status, string code, string reason) =>
        new(status, null, code, reason);

    private static ulong ManagedTypeId(string identity)
    {
        ulong result = BinaryPrimitives.ReadUInt64LittleEndian(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"hybridcpu.managed-type-id/v1|{identity}")));
        return result == 0 ? 1 : result;
    }
}

public sealed class ManagedEhMetadataFinalizerV1
{
    public ManagedEhFinalizationArtifactV1 Finalize(ManagedEhMethodPlanV1 plan, IrRegisterAllocationResultV1 allocation)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(allocation);
        if (!ValidPlan(plan))
            return Failure("The managed EH plan is malformed, stale or not bound to its exact deterministic digest.");
        if (allocation.Status != IrRegisterAllocationStatusV1.Allocated || allocation.Witness is null ||
            allocation.FinalBundles is null || allocation.OriginalSchedule is null ||
            !allocation.Witness.Rebuild.DependenciesCurrent || !allocation.Witness.Rebuild.LivenessCurrent ||
            !allocation.Witness.Rebuild.PressureCurrent || !allocation.Witness.Rebuild.ResourceFactsCurrent ||
            !allocation.Witness.Rebuild.ExactW8PlacementRecomputed)
            return Failure("Final post-scheduling, post-RA and post-frame-lowering evidence is required.");

        SortedDictionary<int, int> placement = FinalIlPlacement(allocation.FinalBundles, plan.InstructionIdentityPrefix);
        int codeSize = checked(allocation.FinalBundles.BlockResults.Sum(static row => row.Bundles.Count) *
            HybridCpuBundleSerializer.BundleSizeBytes);
        if (placement.Count == 0 || string.IsNullOrWhiteSpace(plan.MethodIdentity) ||
            string.IsNullOrWhiteSpace(plan.InstructionIdentityPrefix) ||
            !allocation.OriginalSchedule.Program.Instructions.Any(instruction =>
                instruction.StableIdentity.StartsWith(plan.InstructionIdentityPrefix, StringComparison.Ordinal)))
            return Failure("Final placement does not contain exact CIL instruction identities.");
        int Map(int ilOffset) => placement.Where(row => row.Key >= ilOffset).Select(static row => row.Value)
            .DefaultIfEmpty(codeSize).Min();

        var clauses = new List<HybridCpuManagedEhClauseRegistrationV1>(plan.Clauses.Count);
        foreach (ManagedEhClausePlanV1 clause in plan.Clauses.OrderBy(static row => row.Ordinal))
        {
            int tryStart = Map(clause.TryOffset);
            int tryEnd = Map(checked(clause.TryOffset + clause.TryLength));
            int handlerStart = Map(clause.HandlerOffset);
            int handlerEnd = Map(checked(clause.HandlerOffset + clause.HandlerLength));
            if (tryStart >= tryEnd || handlerStart >= handlerEnd || tryEnd > codeSize || handlerEnd > codeSize)
                return Failure("An EH source range has no exact non-empty final native-PC range.");
            clauses.Add(new(clause.Kind, tryStart, tryEnd - tryStart, handlerStart, handlerEnd - handlerStart,
                clause.CatchTypeId, clause.Ordinal));
        }
        ManagedEhNativeOperationV1[] operations = plan.Operations.Select(operation => new ManagedEhNativeOperationV1(
                operation.Kind, Map(operation.IlOffset), operation.LeaveTarget < 0 ? -1 : Map(operation.LeaveTarget),
                operation.RuntimeHelperSymbol)).OrderBy(static row => row.NativeOffsetBytes).ToArray();
        byte[] ehInfo = EncodeEh(clauses);

        ManagedEhNativeFinallyContinuationPlanV1? finallyContinuations = null;
        if (plan.Clauses.Any(static clause => clause.Kind == HybridCpuManagedEhClauseKindV1.Finally))
        {
            HybridCpuFrameSlotV2? tokenSlot = allocation.Witness.Frame.Slots.SingleOrDefault(static slot =>
                slot.Identity == ManagedEhFrameHomesV1.FinallyContinuationTokenSlot);
            if (tokenSlot is null || tokenSlot.SizeBytes != 8 || tokenSlot.AlignmentBytes < 8)
                return Failure("Managed finally requires one exact aligned frame-owned continuation-token slot.");
            ManagedEhNativeFinallyContinuationV1[] nativeContinuations = plan.FinallyContinuations.Continuations
                .Select(continuation => new ManagedEhNativeFinallyContinuationV1(
                    continuation.Token, Map(continuation.LeaveOffset), Map(continuation.TargetOffset),
                    continuation.Steps.Select(step =>
                    {
                        ManagedEhClausePlanV1 clause = plan.Clauses.Single(row => row.Ordinal == step.ClauseOrdinal);
                        return new ManagedEhNativeFinallyContinuationStepV1(
                            step.ClauseOrdinal, Map(step.HandlerOffset), Map(clause.HandlerOffset + clause.HandlerLength),
                            step.ReleaseCatchScopesBeforeEntry, step.NextClauseOrdinal, Map(step.NextOffset));
                    }).ToArray(),
                    continuation.ReleaseCatchScopesBeforeTarget)).ToArray();
            byte[] encoding = EncodeFinallyContinuations(tokenSlot.OffsetFromAdjustedStackPointerBytes, nativeContinuations);
            finallyContinuations = new(tokenSlot.OffsetFromAdjustedStackPointerBytes, nativeContinuations, encoding,
                Hash(string.Join('|', "hybridcpu.managed-finally-native/v1", plan.FinallyContinuations.Digest,
                    allocation.Witness.WitnessDigest, Convert.ToHexString(encoding))));
        }

        HybridCpuManagedUnwindRecordV2 unwind = CreateFrameUnwind(allocation.Witness.Frame);
        byte[] unwindInfo = HybridCpuManagedUnwindCodecV2.Encode(unwind);
        var sections = new List<HybridCpuObjectSectionV1>
        {
            new(".hceh", HybridCpuObjectSectionKind.ExceptionHandling, 8, ehInfo, (ulong)ehInfo.Length),
            new(".hcunwind", HybridCpuObjectSectionKind.Unwind, 8, unwindInfo, (ulong)unwindInfo.Length)
        };
        if (finallyContinuations is not null)
            sections.Add(new(".hcfinally", HybridCpuObjectSectionKind.ReadOnlyData, 8,
                finallyContinuations.Encoding, (ulong)finallyContinuations.Encoding.Length));
        string digest = Hash(string.Join('|', HybridCpuManagedEhSchemaV1.SchemaId, plan.ContractDigest,
            allocation.Witness.WitnessDigest, Convert.ToHexString(ehInfo), Convert.ToHexString(unwindInfo),
            finallyContinuations?.Digest ?? "no-finally-continuation"));
        return new(HybridCpuManagedMetadataStatusV1.Finalized,
            "EH clauses and unwind data were derived from final native placement and frame layout.",
            clauses, operations, ehInfo, unwindInfo, unwind, sections, digest, finallyContinuations);
    }

    // Shared by EH and handler-free managed frames. Offsets are relative to the
    // caller CFA, not the adjusted stack pointer used by allocation frame slots.
    internal static HybridCpuManagedUnwindRecordV2 CreateFrameUnwind(HybridCpuFrameLayoutV2 frame)
    {
        HybridCpuFrameSlotV2? returnSlot = frame.Slots.SingleOrDefault(static row => row.Identity == "saved:x1");
        HybridCpuManagedSavedRegisterV1[] saved = frame.Slots.Where(static row => row.Identity.StartsWith("saved:x", StringComparison.Ordinal))
            .Select(row => new HybridCpuManagedSavedRegisterV1(
                int.Parse(row.Identity.AsSpan("saved:x".Length), CultureInfo.InvariantCulture),
                checked(row.OffsetFromAdjustedStackPointerBytes - frame.FrameSizeBytes)))
            .OrderBy(static row => row.RegisterId).ToArray();
        return new HybridCpuManagedUnwindRecordV2(HybridCpuManagedFrameKindV1.Managed,
            HybridCpuManagedCfaBaseV1.StackPointer, frame.FrameSizeBytes,
            returnSlot is null ? HybridCpuNativeAbiContractV2.ReturnAddressRegister : null,
            returnSlot is null ? null : checked(returnSlot.OffsetFromAdjustedStackPointerBytes - frame.FrameSizeBytes), saved);
    }

    private static byte[] EncodeEh(IReadOnlyList<HybridCpuManagedEhClauseRegistrationV1> clauses)
    {
        if (clauses.Count > HybridCpuManagedEhSchemaV1.MaximumClausesPerMethod)
            throw new ArgumentException("EH clause budget exceeded.", nameof(clauses));
        byte[] bytes = new byte[HybridCpuManagedEhClauseEncodingV1.HeaderSizeBytes +
            clauses.Count * HybridCpuManagedEhClauseEncodingV1.ClauseSizeBytes];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, HybridCpuManagedEhSchemaV1.EhMagic);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), HybridCpuManagedEhSchemaV1.SchemaVersion);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(HybridCpuManagedEhClauseEncodingV1.CountOffset), clauses.Count);
        for (int index = 0, offset = HybridCpuManagedEhClauseEncodingV1.HeaderSizeBytes; index < clauses.Count;
             index++, offset += HybridCpuManagedEhClauseEncodingV1.ClauseSizeBytes)
        {
            HybridCpuManagedEhClauseRegistrationV1 row = clauses[index];
            bytes[offset + HybridCpuManagedEhClauseEncodingV1.KindOffset] = (byte)row.Kind;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset + HybridCpuManagedEhClauseEncodingV1.TryStartOffset), row.TryStartOffsetBytes);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset + HybridCpuManagedEhClauseEncodingV1.TrySizeOffset), row.TrySizeBytes);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset + HybridCpuManagedEhClauseEncodingV1.HandlerStartOffset), row.HandlerStartOffsetBytes);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset + HybridCpuManagedEhClauseEncodingV1.HandlerSizeOffset), row.HandlerSizeBytes);
            BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(offset + HybridCpuManagedEhClauseEncodingV1.CatchTypeIdOffset), row.CatchTypeId);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset + HybridCpuManagedEhClauseEncodingV1.OrdinalOffset), row.Ordinal);
        }
        return bytes;
    }

    private static byte[] EncodeFinallyContinuations(int tokenSlotOffset,
        IReadOnlyList<ManagedEhNativeFinallyContinuationV1> continuations)
    {
        int stepCount = continuations.Sum(static continuation => continuation.Steps.Count);
        if (continuations.Count > HybridCpuManagedEhSchemaV1.MaximumOperationsPerMethod ||
            stepCount > HybridCpuManagedEhSchemaV1.MaximumOperationsPerMethod * HybridCpuManagedEhSchemaV1.MaximumClausesPerMethod)
            throw new ArgumentException("Managed finally continuation metadata exceeds its deterministic budget.", nameof(continuations));
        int continuationBytes = checked(continuations.Count * HybridCpuManagedFinallyContinuationEncodingV1.ContinuationRowSizeBytes);
        byte[] bytes = new byte[checked(HybridCpuManagedFinallyContinuationEncodingV1.HeaderSizeBytes + continuationBytes +
            stepCount * HybridCpuManagedFinallyContinuationEncodingV1.StepRowSizeBytes)];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, HybridCpuManagedFinallyContinuationEncodingV1.Magic);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), HybridCpuManagedFinallyContinuationEncodingV1.Version);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(HybridCpuManagedFinallyContinuationEncodingV1.ContinuationCountOffset), continuations.Count);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(HybridCpuManagedFinallyContinuationEncodingV1.StepCountOffset), stepCount);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(HybridCpuManagedFinallyContinuationEncodingV1.TokenSlotOffset), tokenSlotOffset);
        int stepIndex = 0;
        for (int index = 0; index < continuations.Count; index++)
        {
            ManagedEhNativeFinallyContinuationV1 continuation = continuations[index];
            int offset = HybridCpuManagedFinallyContinuationEncodingV1.HeaderSizeBytes +
                index * HybridCpuManagedFinallyContinuationEncodingV1.ContinuationRowSizeBytes;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset + HybridCpuManagedFinallyContinuationEncodingV1.TokenOffset), continuation.Token);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset + HybridCpuManagedFinallyContinuationEncodingV1.LeaveOffset), continuation.LeaveOffsetBytes);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset + HybridCpuManagedFinallyContinuationEncodingV1.TargetOffset), continuation.TargetOffsetBytes);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset + HybridCpuManagedFinallyContinuationEncodingV1.FirstStepOffset), stepIndex);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset + HybridCpuManagedFinallyContinuationEncodingV1.ContinuationStepCountOffset), continuation.Steps.Count);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset + HybridCpuManagedFinallyContinuationEncodingV1.ReleaseBeforeTargetOffset), continuation.ReleaseCatchScopesBeforeTarget);
            foreach (ManagedEhNativeFinallyContinuationStepV1 step in continuation.Steps)
            {
                int stepOffset = HybridCpuManagedFinallyContinuationEncodingV1.HeaderSizeBytes + continuationBytes +
                    stepIndex++ * HybridCpuManagedFinallyContinuationEncodingV1.StepRowSizeBytes;
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(stepOffset + HybridCpuManagedFinallyContinuationEncodingV1.StepClauseOrdinalOffset), step.ClauseOrdinal);
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(stepOffset + HybridCpuManagedFinallyContinuationEncodingV1.StepHandlerOffset), step.HandlerOffsetBytes);
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(stepOffset + HybridCpuManagedFinallyContinuationEncodingV1.StepReleaseBeforeEntryOffset), step.ReleaseCatchScopesBeforeEntry);
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(stepOffset + HybridCpuManagedFinallyContinuationEncodingV1.StepNextClauseOrdinalOffset), step.NextClauseOrdinal ?? -1);
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(stepOffset + HybridCpuManagedFinallyContinuationEncodingV1.StepNextOffset), step.NextOffsetBytes);
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(stepOffset + HybridCpuManagedFinallyContinuationEncodingV1.StepHandlerEndOffset), step.HandlerEndOffsetBytes);
            }
        }
        return bytes;
    }

    private static bool ValidPlan(ManagedEhMethodPlanV1 plan)
    {
        if (string.IsNullOrWhiteSpace(plan.MethodIdentity) || string.IsNullOrWhiteSpace(plan.InstructionIdentityPrefix) ||
            plan.MethodBodySize <= 0 || plan.Clauses is null || plan.Operations is null ||
            plan.Clauses.Count is 0 or > HybridCpuManagedEhSchemaV1.MaximumClausesPerMethod ||
            plan.Operations.Count > HybridCpuManagedEhSchemaV1.MaximumOperationsPerMethod ||
            !string.Equals(plan.ContractDigest, ManagedEhPlanContractV1.ComputeDigest(plan.MethodIdentity,
                plan.InstructionIdentityPrefix, plan.MethodBodySize, plan.Clauses, plan.Operations),
                StringComparison.Ordinal) ||
            !plan.Clauses.Select(static row => row.Ordinal).SequenceEqual(Enumerable.Range(0, plan.Clauses.Count)))
            return false;
        foreach (ManagedEhClausePlanV1 row in plan.Clauses)
            if (!Enum.IsDefined(row.Kind) || row.TryOffset < 0 || row.TryLength <= 0 || row.HandlerOffset < 0 ||
                row.HandlerLength <= 0 || row.TryOffset > plan.MethodBodySize - row.TryLength ||
                row.HandlerOffset > plan.MethodBodySize - row.HandlerLength ||
                row.Kind == HybridCpuManagedEhClauseKindV1.Catch &&
                    (row.CatchTypeId == 0 || string.IsNullOrWhiteSpace(row.CatchTypeIdentity)) ||
                row.Kind == HybridCpuManagedEhClauseKindV1.Finally &&
                    (row.CatchTypeId != 0 || row.CatchTypeIdentity is not null))
                return false;
        foreach (ManagedEhOperationPlanV1 row in plan.Operations)
        {
            if (!Enum.IsDefined(row.Kind) || row.IlOffset < 0 || row.IlOffset >= plan.MethodBodySize)
                return false;
            if (row.Kind == ManagedEhOperationKindV1.Leave)
            {
                if (row.LeaveTarget < 0 || row.LeaveTarget > plan.MethodBodySize || row.RuntimeHelperSymbol.Length != 0)
                    return false;
            }
            else
            {
                if (row.LeaveTarget != -1) return false;
                bool helperValid = row.Kind switch
                {
                    ManagedEhOperationKindV1.Throw => row.RuntimeHelperSymbol == "__hybridcpu_managed_throw",
                    ManagedEhOperationKindV1.Rethrow => row.RuntimeHelperSymbol == "__hybridcpu_managed_rethrow",
                    ManagedEhOperationKindV1.EndFinally => row.RuntimeHelperSymbol == "__hybridcpu_managed_endfinally",
                    _ => row.RuntimeHelperSymbol.Length == 0
                };
                if (!helperValid) return false;
            }
        }
        return true;
    }

    private static SortedDictionary<int, int> FinalIlPlacement(IrProgramBundlingResult bundles, string prefix)
    {
        var result = new SortedDictionary<int, int>();
        int bundleIndex = 0;
        foreach (IrBasicBlockBundlingResult block in bundles.BlockResults.OrderBy(static row => row.Block.StartInstructionIndex))
        {
            foreach (IrMaterializedBundle bundle in block.Bundles.OrderBy(static row => row.Cycle))
            {
                foreach (IrMaterializedBundleSlot slot in bundle.Slots.Where(static row => row.Instruction is not null)
                             .OrderBy(static row => row.SlotIndex))
                {
                    string identity = slot.Instruction!.StableIdentity;
                    if (!identity.StartsWith(prefix, StringComparison.Ordinal)) continue;
                    int marker = identity.LastIndexOf(":il_", StringComparison.Ordinal);
                    if (marker >= 0 && marker + 8 <= identity.Length &&
                        int.TryParse(identity.AsSpan(marker + 4, 4), NumberStyles.HexNumber,
                            CultureInfo.InvariantCulture, out int ilOffset))
                        result.TryAdd(ilOffset, checked(bundleIndex * HybridCpuBundleSerializer.BundleSizeBytes));
                }
                bundleIndex++;
            }
        }
        return result;
    }

    private static ManagedEhFinalizationArtifactV1 Failure(string reason) =>
        new(HybridCpuManagedMetadataStatusV1.Unknown, reason, [], [], [], [], null, [],
            Hash($"{HybridCpuManagedEhSchemaV1.SchemaId}|failure|{reason}"));

    private static string Hash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
}
