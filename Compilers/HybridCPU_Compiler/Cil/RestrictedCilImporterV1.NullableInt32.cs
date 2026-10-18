using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace HybridCPU.Compiler.Cil;

public sealed partial class RestrictedCilImporterV1
{
    // Set only on a synchronous primary BodyWorld child which is not an entry root.
    private bool _nullableInt32ParameterIngress;
    private const ushort NullableInt32ParameterValue = 0xff19, NullableInt32ParameterPresence = 0xff1a;

    private bool IsExactNullableInt32ParameterGetter(MetadataReader metadata,
        System.Reflection.PortableExecutable.PEReader pe, MethodDefinitionHandle handle, MethodSignature signature,
        RestrictedCilProvenanceV1 provenance, CancellationToken cancellationToken, out RestrictedCilImportResultV1? qualificationFailure, Func<int, bool>? dependencyProof = null)
    {
        qualificationFailure = null;
        if (signature.Status != SignatureStatus.Success || signature.HasThis || signature.Parameters.Count is not (1 or 2) ||
            signature.Parameters[0] != RestrictedCilTypeV1.Int64 || signature.AggregateParameters?.ElementAtOrDefault(0) != NullableInt32Identity ||
            signature.AggregateReturn is not null || signature.ReturnType is not (RestrictedCilTypeV1.Int32 or RestrictedCilTypeV1.Int64)) return false;
        bool dynamicDefault = signature.Parameters.Count == 2;
        if (dynamicDefault && !IsNullableInt32FallbackParameter(signature)) return false;
        var method = metadata.GetMethodDefinition(handle);
        var owner = metadata.GetTypeDefinition(method.GetDeclaringType());
        if ((method.Attributes & System.Reflection.MethodAttributes.MemberAccessMask) != System.Reflection.MethodAttributes.Private ||
            !method.Attributes.HasFlag(System.Reflection.MethodAttributes.Static) || method.RelativeVirtualAddress == 0 ||
            method.GetGenericParameters().Count != 0 || owner.GetGenericParameters().Count != 0 ||
            owner.GetMethods().Any(h => metadata.GetString(metadata.GetMethodDefinition(h).Name) == ".cctor")) return false;
        var body = pe.GetMethodBody(method.RelativeVirtualAddress);
        byte[] il = body.GetILBytes() ?? [];
        if (il.Length > _budgets.MaximumMethodBodyBytes) return false;
        bool wide = signature.ReturnType == RestrictedCilTypeV1.Int64;
        if (body.ExceptionRegions.Length != 0 || !body.LocalSignature.IsNil || il.Length < 8 ||
            il[0] != 0x0f || il[1] != 0) return false;
        if (TryNullableInt32ParameterDefault(metadata, il, 2, signature, out int defaultCall))
            return il.AsSpan(defaultCall + 5).SequenceEqual(wide ? new byte[] { 0x6a, 0x2a } : new byte[] { 0x2a });
        if (dynamicDefault) {
            var decoded = Decode(il, provenance, metadata, pe, allowProjection: false, scalarSignature: signature, allowNullableParameterAddress: true);
            if (decoded.Failure is not null) {
                if (decoded.Failure.Status == RestrictedCilImportStatusV1.BudgetExhausted) qualificationFailure = decoded.Failure;
                return false;
            }
            bool valid = TryNullableInt32ParameterComputedDefault(metadata, pe, decoded.Instructions,
                signature, provenance, cancellationToken, out _, out var failure, dependencyProof: dependencyProof);
            if (failure?.Status == RestrictedCilImportStatusV1.BudgetExhausted) qualificationFailure = failure;
            return valid;
        }
        if (il[2] != 0x28) return false;
        ushort member = NullableInt32Member(metadata, System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(il.AsSpan(3, 4)));
        return member == NullableInt32Value && il.Length == (wide ? 9 : 8) && il[^1] == 0x2a && (!wide || il[^2] == 0x6a) ||
            member == NullableInt32HasValue && wide && il.AsSpan(7).SequenceEqual(new byte[] { 0x16, 0xfe, 0x03, 0x6a, 0x2a });
    }

    private static bool IsNullableInt32FallbackParameter(MethodSignature signature) =>
        signature.Parameters.Count == 2 && signature.Parameters[1] == RestrictedCilTypeV1.Int32 &&
        signature.AggregateParameters?.ElementAtOrDefault(1) is null;

    private static bool TryNullableInt32ParameterDefault(MetadataReader metadata, ReadOnlySpan<byte> il,
        int constantOffset, MethodSignature signature, out int callOffset)
    {
        callOffset = constantOffset;
        if (constantOffset < 0 || constantOffset >= il.Length) return false;
        int size = IsNullableInt32FallbackParameter(signature) ? il[constantOffset] == 0x03 ? 1 : 0 :
            signature.Parameters.Count == 1 ? il[constantOffset] switch { >= 0x15 and <= 0x1e => 1, 0x1f => 2, 0x20 => 5, _ => 0 } : 0;
        if (size == 0 || il.Length - constantOffset < size + 5) return false;
        callOffset += size;
        return il[callOffset] == 0x28 && NullableInt32Member(metadata,
            System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(il.Slice(callOffset + 1, 4))) == NullableInt32Default;
    }

    private bool TryNullableInt32ParameterComputedDefault(MetadataReader metadata, System.Reflection.PortableExecutable.PEReader pe,
        IReadOnlyList<DecodedInstruction> instructions, MethodSignature signature, RestrictedCilProvenanceV1 provenance,
        CancellationToken cancellationToken, out int getter, out RestrictedCilImportResultV1? failure,
        bool deferCalls = false, Func<int, bool>? dependencyProof = null)
    {
        getter = -1; failure = null;
        if (!IsNullableInt32FallbackParameter(signature) || instructions.Count < 4 || instructions[0].Encoding != 0x0f || instructions[0].Literal != 0) return false;
        int call = 1;
        var pureCalls = new Dictionary<int, bool>();
        bool hasCalls = false;
        for (; call < instructions.Count; call++) {
            cancellationToken.ThrowIfCancellationRequested();
            var row = instructions[call];
            if (row.Encoding == 0x28 && NullableInt32Member(metadata, row.Token) == NullableInt32Default) break;
            if (row.Encoding == 0x28 && MetadataTokens.EntityHandle(row.Token).Kind == HandleKind.MethodDefinition) {
                var method = metadata.GetMethodDefinition((MethodDefinitionHandle)MetadataTokens.EntityHandle(row.Token));
                var helperSignature = ParseMethodSignature(metadata, method.Signature, [], [], allowAggregates: true);
                if ((method.Attributes & System.Reflection.MethodAttributes.MemberAccessMask) != System.Reflection.MethodAttributes.Private ||
                    helperSignature.Status != SignatureStatus.Success || helperSignature.HasThis || helperSignature.ReturnType != RestrictedCilTypeV1.Int32 ||
                    helperSignature.AggregateReturn is not null || helperSignature.Parameters.Count != 1 ||
                    helperSignature.Parameters[0] != RestrictedCilTypeV1.Int32 || helperSignature.AggregateParameters?.Any(value => value is not null) == true) return false;
                hasCalls = true;
                if (!deferCalls && !pureCalls.ContainsKey(row.Token)) {
                    bool valid = dependencyProof?.Invoke(row.Token) ?? IsPureLocalConstructorArgumentCall(metadata, pe, row.Token, provenance, cancellationToken);
                    if (!valid) return false;
                    pureCalls.Add(row.Token, true);
                }
                continue;
            }
            // Borrowed receiver is not an expression value. No stores or memory;
            // typed CFG proof below owns stack/types, forward edges and fault freedom.
            if (!(row.Encoding == 0x03 || row.Encoding == 0x0e && row.Literal == 1 ||
                row.Encoding is 0x00 or >= 0x15 and <= 0x21 or 0x25 or 0x26 or >= 0x2b and <= 0x44 or
                    >= 0x58 and <= 0x6a or 0x6d or 0x6e or 0xd1 or 0xd2 or 0xd3 or >= 0xfe01 and <= 0xfe05)) return false;
        }
        bool wide = signature.ReturnType == RestrictedCilTypeV1.Int64;
        if (call >= instructions.Count || instructions.Count != call + (wide ? 3 : 2) ||
            instructions[^1].Encoding != 0x2a || wide && instructions[call + 1].Encoding != 0x6a) return false;
        // Discovery retains call edges; it cannot grant final body admission.
        if (deferCalls && hasCalls) { getter = call; return true; }
        return TryInt64ConstructorArgument(metadata, pe, instructions, 0, new(SignatureStatus.Success, [], ""), signature,
            NullableInt32Identity, pureCalls, provenance, cancellationToken, out getter, out failure, nullableDefault: true, requirePure: true);
    }

    private DecodeResult ProjectNullableInt32ParameterReads(MetadataReader metadata, System.Reflection.PortableExecutable.PEReader pe, IReadOnlyList<DecodedInstruction> instructions,
        MethodSignature signature, RestrictedCilProvenanceV1 provenance, bool deferCalls = false)
    {
        var targets = instructions.SelectMany(ControlFlowTargets).ToHashSet();
        var mutated = instructions.Where(row => row.Encoding == 0x10).Select(row => row.Literal).ToHashSet();
        var result = new List<DecodedInstruction>(instructions.Count);
        for (int index = 0; index < instructions.Count; index++) {
            var row = instructions[index];
            if (row.Encoding != 0x0f || signature.AggregateParameters?.ElementAtOrDefault((int)row.Literal) != NullableInt32Identity) { result.Add(row); continue; }
            if (index + 2 < instructions.Count && (signature.Parameters.Count == 1 && instructions[index + 1].Encoding is >= 0x15 and <= 0x20 ||
                IsNullableInt32FallbackParameter(signature) && instructions[index + 1].Encoding == 0x03) &&
                instructions[index + 2].Encoding == 0x28 && NullableInt32Member(metadata, instructions[index + 2].Token) == NullableInt32Default) {
                if (targets.Contains(instructions[index + 1].Offset) || targets.Contains(instructions[index + 2].Offset) || mutated.Contains(row.Literal) ||
                    instructions[index + 1].Encoding == 0x03 && mutated.Contains(1))
                    return new([], Reject(RestrictedCilImportStatusV1.Unsupported, "HCCIL1847", "Nullable default ingress must be immutable and untargeted.", OffsetIdentity(provenance, row.Offset), provenance));
                var input = instructions[index + 1]; var call = instructions[index + 2];
                result.Add(row with { Encoding = NullableInt32Default, Name = "nullable-int32.parameter-default",
                    NullableParameter = true, NullableDefault = input, Size = row.Size + input.Size + call.Size, ErasedReceiverDepth = 1 });
                index += 2;
                continue;
            }
            if (index == 0 && IsNullableInt32FallbackParameter(signature)) {
                bool computed = TryNullableInt32ParameterComputedDefault(metadata, pe, instructions, signature, provenance, default, out int getter, out var failure, deferCalls);
                if (failure is not null) return new([], failure);
                if (!computed) return new([], Reject(RestrictedCilImportStatusV1.Unsupported, "HCCIL1847", "Nullable computed default requires a fault-free immutable primitive argument region.", OffsetIdentity(provenance, row.Offset), provenance));
                result.Add(row with { Encoding = 0x00, Name = "nullable-parameter.erased-address", ErasedReceiverDepth = 1 });
                for (int part = 1; part < getter; part++) result.Add(instructions[part] with { ErasedReceiverDepth = 1 });
                result.Add(instructions[getter] with { Encoding = NullableDefaultStack, Name = "nullable-parameter.default-stack", Literal = 0, NullableParameter = true });
                for (int part = getter + 1; part < instructions.Count; part++) result.Add(instructions[part]);
                return new(result, null);
            }
            if (index + 1 >= instructions.Count || targets.Contains(instructions[index + 1].Offset) ||
                mutated.Contains(row.Literal))
                return new([], Reject(RestrictedCilImportStatusV1.Unsupported, "HCCIL1847", "Nullable getter ingress must be immutable and untargeted.", OffsetIdentity(provenance, row.Offset), provenance));
            bool presence = NullableInt32Member(metadata, instructions[index + 1].Token) == NullableInt32HasValue;
            result.Add(row with { Encoding = presence ? NullableInt32ParameterPresence : NullableInt32ParameterValue,
                Name = presence ? "nullable-int32.parameter-presence" : "nullable-int32.parameter-value", Size = row.Size + instructions[++index].Size });
        }
        return new(result, null);
    }
    // Private local representation only: ((uint)value << 1) | hasValue.
    // Exact primary private I4 getter ingress reuses this carrier; no general Nullable<T> ABI.
    private const string NullableInt32Identity = "compiler-local:corelib-nullable-int32/v1";
    private const string NullableInt64Identity = "compiler-local:corelib-nullable-int64/v1";
    private const string NullableUInt32Identity = "compiler-local:corelib-nullable-uint32/v1";
    private const string NullableUInt64Identity = "compiler-local:corelib-nullable-uint64/v1";
    private const string NullableUInt16Identity = "compiler-local:corelib-nullable-uint16/v1";
    private const string NullableUInt8Identity = "compiler-local:corelib-nullable-uint8/v1";
    private const string NullableInt8Identity = "compiler-local:corelib-nullable-int8/v1";
    private const string NullableBooleanIdentity = "compiler-local:corelib-nullable-boolean/v1";
    private const string NullableCharIdentity = "compiler-local:corelib-nullable-char/v1";
    private const string NullableInt16Identity = "compiler-local:corelib-nullable-int16/v1";
    private const string NullableEnumInt32Prefix = "compiler-local:corelib-nullable-enum-int32/v1:";
    private const string NullableEnumInt64Prefix = "compiler-local:corelib-nullable-enum-int64/v1:";
    private const string NullableEnumUInt64Prefix = "compiler-local:corelib-nullable-enum-uint64/v1:";
    private const string NullableEnumUInt32Prefix = "compiler-local:corelib-nullable-enum-uint32/v1:";
    private const string NullableEnumUInt16Prefix = "compiler-local:corelib-nullable-enum-uint16/v1:";
    private const string NullableEnumInt16Prefix = "compiler-local:corelib-nullable-enum-int16/v1:";
    private const string NullableEnumUInt8Prefix = "compiler-local:corelib-nullable-enum-uint8/v1:";
    private const string NullableEnumInt8Prefix = "compiler-local:corelib-nullable-enum-int8/v1:";
    private static bool IsNullableEnumInt8(string? identity) => identity?.StartsWith(NullableEnumInt8Prefix, StringComparison.Ordinal) == true;
    private static bool IsNullableEnumUInt8(string? identity) => identity?.StartsWith(NullableEnumUInt8Prefix, StringComparison.Ordinal) == true;
    private static bool IsNullableEnumInt16(string? identity) => identity?.StartsWith(NullableEnumInt16Prefix, StringComparison.Ordinal) == true;
    private static bool IsNullableEnumUInt16(string? identity) => identity?.StartsWith(NullableEnumUInt16Prefix, StringComparison.Ordinal) == true;
    private static bool IsNullableEnumUInt32(string? identity) => identity?.StartsWith(NullableEnumUInt32Prefix, StringComparison.Ordinal) == true;
    private static bool IsUnsignedNullableEnum(string? identity) => IsNullableEnumUInt8(identity) || IsNullableEnumUInt16(identity) || IsNullableEnumUInt32(identity) || IsNullableEnumUInt64(identity);
    private static bool IsNullableEnumUInt64(string? identity) => identity?.StartsWith(NullableEnumUInt64Prefix, StringComparison.Ordinal) == true;
    private static bool IsWideNullableEnum(string? identity) => IsNullableEnumInt64(identity) || IsNullableEnumUInt64(identity);
    private static bool IsNullableEnumInt64(string? identity) => identity?.StartsWith(NullableEnumInt64Prefix, StringComparison.Ordinal) == true;
    private static bool IsNullableEnum(string? identity) => IsNullableEnumInt8(identity) || IsNullableEnumUInt8(identity) || IsNullableEnumInt16(identity) || IsNullableEnumUInt16(identity) || IsNullableEnumInt32(identity) || IsNullableEnumUInt32(identity) || IsWideNullableEnum(identity);
    private static bool IsNullableEnumInt32(string? identity) => identity?.StartsWith(NullableEnumInt32Prefix, StringComparison.Ordinal) == true;
    private static bool IsNullableLocal(string? identity) => IsNullableEnum(identity) || identity is NullableInt32Identity or NullableInt64Identity or NullableUInt32Identity or NullableUInt64Identity or NullableUInt16Identity or NullableInt16Identity or NullableUInt8Identity or NullableInt8Identity or NullableCharIdentity or NullableBooleanIdentity;
    private static int NarrowNullableBits(string? identity) => IsNullableEnumInt8(identity) || IsNullableEnumUInt8(identity) ? 8 : IsNullableEnumInt16(identity) || IsNullableEnumUInt16(identity) ? 16 : identity is NullableUInt8Identity or NullableInt8Identity or NullableBooleanIdentity ? 8 : identity is NullableUInt16Identity or NullableInt16Identity or NullableCharIdentity ? 16 : 0;
    private static bool IsNarrowNullable(string? identity) => NarrowNullableBits(identity) != 0;
    private static bool IsWideNullable(string? identity) => IsWideNullableEnum(identity) || identity is NullableInt64Identity or NullableUInt64Identity;
    private static bool IsUnsignedNullable(string? identity) => IsUnsignedNullableEnum(identity) || identity is NullableUInt32Identity or NullableUInt64Identity or NullableUInt16Identity or NullableUInt8Identity or NullableCharIdentity or NullableBooleanIdentity;
    private static string NullableIdentity(DecodedInstruction row) => row.NullableEnumIdentity ?? (row.NullableBoolean ? NullableBooleanIdentity : row.NullableChar ? NullableCharIdentity : row.NullableNarrowBits == 8 ? row.NullableUnsigned ? NullableUInt8Identity : NullableInt8Identity : row.NullableNarrowBits == 16 ? row.NullableUnsigned ? NullableUInt16Identity : NullableInt16Identity : row.NullableWide ? row.NullableUnsigned ? NullableUInt64Identity : NullableInt64Identity : row.NullableUnsigned ? NullableUInt32Identity : NullableInt32Identity);
    private static bool ValidNullableInput(V2Value value, bool wide) => value.Initialized && value.AggregateIdentity is null &&
        (wide ? value.Type is RestrictedCilTypeV1.Int64 or RestrictedCilTypeV1.UInt64 : value.Type is RestrictedCilTypeV1.Int32 or RestrictedCilTypeV1.UInt32);
    private const ushort NullableInt32New = 0xff10, NullableInt32Zero = 0xff11,
        NullableInt32Value = 0xff12, NullableInt32HasValue = 0xff13, NullableInt32Default = 0xff14, NullableInt32Initialize = 0xff15,
        NullableInitializeStack = 0xff16, NullableCheckedValue = 0xff17, NullableDefaultStack = 0xff18;

    private static bool ReadNullableInt32(MetadataReader metadata, ref BlobReader reader, bool wide = false, bool unsigned = false, int narrowBits = 0, bool character = false, bool logical = false)
    {
        if (narrowBits is not (0 or 8 or 16) || character && (wide || !unsigned || narrowBits != 16) ||
            logical && (character || wide || !unsigned || narrowBits != 8)) return false;
        BlobReader probe = reader;
        if (probe.RemainingBytes < 4 || probe.ReadByte() != 0x15 || probe.ReadByte() != 0x11) return false;
        EntityHandle definition = ReadTypeDefOrRefEncoded(ref probe);
        if (definition.Kind != HandleKind.TypeReference) return false;
        TypeReference type = metadata.GetTypeReference((TypeReferenceHandle)definition);
        if (metadata.GetString(type.Namespace) != "System" || metadata.GetString(type.Name) != "Nullable`1" ||
            type.ResolutionScope.Kind != HandleKind.AssemblyReference ||
            ReferencedAssemblyName(metadata, (TypeReferenceHandle)definition) is not ("System.Private.CoreLib" or "System.Runtime" or "mscorlib") ||
            probe.ReadCompressedInteger() != 1 || probe.RemainingBytes < 1 || probe.ReadByte() != (logical ? 2 : character ? 3 : narrowBits == 8 ? unsigned ? 5 : 4 : narrowBits == 16 ? unsigned ? 7 : 6 : wide ? unsigned ? 0x0b : 0x0a : unsigned ? 9 : 8)) return false;
        reader = probe;
        return true;
    }

    private static bool IsNullableInt32Type(MetadataReader metadata, EntityHandle handle, bool wide = false, bool unsigned = false, int narrowBits = 0, bool character = false, bool logical = false, string? enumIdentity = null, IReadOnlyDictionary<string, ExternalEnumCarrier>? externalEnums = null)
    {
        if (handle.Kind != HandleKind.TypeSpecification) return false;
        BlobReader reader = metadata.GetBlobReader(metadata.GetTypeSpecification((TypeSpecificationHandle)handle).Signature);
        if (enumIdentity is not null) return wide == IsWideNullableEnum(enumIdentity) && unsigned == IsUnsignedNullableEnum(enumIdentity) && narrowBits == NarrowNullableBits(enumIdentity) && !character && !logical &&
            ReadNullableEnum(metadata, ref reader, out var actual, externalEnums) && actual == enumIdentity && reader.RemainingBytes == 0;
        return ReadNullableInt32(metadata, ref reader, wide, unsigned, narrowBits, character, logical) && reader.RemainingBytes == 0;
    }

    private static ushort NullableInt32Member(MetadataReader metadata, int token, bool wide = false, bool unsigned = false, int narrowBits = 0, bool character = false, bool logical = false, string? enumIdentity = null, IReadOnlyDictionary<string, ExternalEnumCarrier>? externalEnums = null)
    {
        EntityHandle handle = MetadataTokens.EntityHandle(token);
        if (handle.Kind != HandleKind.MemberReference) return 0;
        MemberReference member = metadata.GetMemberReference((MemberReferenceHandle)handle);
        if (!IsNullableInt32Type(metadata, member.Parent, wide, unsigned, narrowBits, character, logical, enumIdentity, externalEnums)) return 0;
        byte[] signature = metadata.GetBlobBytes(member.Signature);
        return metadata.GetString(member.Name) switch {
            ".ctor" when signature.AsSpan().SequenceEqual(new byte[] { 0x20, 1, 1, 0x13, 0 }) => NullableInt32New,
            "GetValueOrDefault" when signature.AsSpan().SequenceEqual(new byte[] { 0x20, 0, 0x13, 0 }) => NullableInt32Value,
            "GetValueOrDefault" when signature.AsSpan().SequenceEqual(new byte[] { 0x20, 1, 0x13, 0, 0x13, 0 }) => NullableInt32Default,
            "get_HasValue" when signature.AsSpan().SequenceEqual(new byte[] { 0x20, 0, 2 }) => NullableInt32HasValue,
            "get_Value" when signature.AsSpan().SequenceEqual(new byte[] { 0x20, 0, 0x13, 0 }) => NullableCheckedValue,
            _ => 0 };
    }

    private static bool ReadNullableEnum(MetadataReader metadata, ref BlobReader reader, out string? identity,
        IReadOnlyDictionary<string, ExternalEnumCarrier>? externalEnums = null)
    {
        identity = null;
        BlobReader probe = reader;
        if (probe.RemainingBytes < 6 || probe.ReadByte() != 0x15 || probe.ReadByte() != 0x11) return false;
        EntityHandle nullable = ReadTypeDefOrRefEncoded(ref probe);
        if (nullable.Kind != HandleKind.TypeReference) return false;
        var wrapper = metadata.GetTypeReference((TypeReferenceHandle)nullable);
        if (metadata.GetString(wrapper.Namespace) != "System" || metadata.GetString(wrapper.Name) != "Nullable`1" ||
            wrapper.ResolutionScope.Kind != HandleKind.AssemblyReference ||
            ReferencedAssemblyName(metadata, (TypeReferenceHandle)nullable) is not ("System.Private.CoreLib" or "System.Runtime" or "mscorlib") ||
            probe.ReadCompressedInteger() != 1 || probe.RemainingBytes < 2 || probe.ReadByte() != 0x11) return false;
        EntityHandle handle = ReadTypeDefOrRefEncoded(ref probe);
        if (handle.Kind == HandleKind.TypeReference) {
            string? key = AggregateIdentity(metadata, handle);
            if (key is null || externalEnums is null || !externalEnums.TryGetValue(key, out var declaration) ||
                declaration.Carrier is not (RestrictedCilTypeV1.Int32 or RestrictedCilTypeV1.Int64 or RestrictedCilTypeV1.UInt64 or RestrictedCilTypeV1.UInt32 or RestrictedCilTypeV1.UInt16 or RestrictedCilTypeV1.Int16 or RestrictedCilTypeV1.UInt8 or RestrictedCilTypeV1.Int8) || declaration.NullableLocalIdentity is null) return false;
            identity = declaration.NullableLocalIdentity;
        } else identity = NullableEnumDefinitionIdentity(metadata, handle);
        if (identity is null) return false;
        reader = probe;
        return true;
    }

    private static string? NullableEnumDefinitionIdentity(MetadataReader metadata, EntityHandle handle)
    {
        if (handle.Kind != HandleKind.TypeDefinition) return null;
        var carrier = EnumCarrier(metadata, handle);
        if (carrier is not (RestrictedCilTypeV1.Int32 or RestrictedCilTypeV1.Int64 or RestrictedCilTypeV1.UInt64 or RestrictedCilTypeV1.UInt32 or RestrictedCilTypeV1.UInt16 or RestrictedCilTypeV1.Int16 or RestrictedCilTypeV1.UInt8 or RestrictedCilTypeV1.Int8)) return null;
        var definition = metadata.GetTypeDefinition((TypeDefinitionHandle)handle);
        if (definition.BaseType.Kind != HandleKind.TypeReference ||
            ReferencedAssemblyName(metadata, (TypeReferenceHandle)definition.BaseType) is not ("System.Private.CoreLib" or "System.Runtime" or "mscorlib") ||
            (definition.Attributes & System.Reflection.TypeAttributes.Sealed) == 0 ||
            definition.GetGenericParameters().Count != 0 || definition.GetMethods().Count != 0) return null;
        int instanceFields = 0;
        foreach (var fieldHandle in definition.GetFields()) {
            var field = metadata.GetFieldDefinition(fieldHandle);
            if ((field.Attributes & System.Reflection.FieldAttributes.Static) != 0) {
                if ((field.Attributes & System.Reflection.FieldAttributes.Literal) == 0) return null;
                continue;
            }
            instanceFields++;
            const System.Reflection.FieldAttributes required = System.Reflection.FieldAttributes.SpecialName | System.Reflection.FieldAttributes.RTSpecialName;
            if (metadata.GetString(field.Name) != "value__" || (field.Attributes & required) != required) return null;
        }
        if (instanceFields != 1) return null;
        return (carrier == RestrictedCilTypeV1.Int8 ? NullableEnumInt8Prefix : carrier == RestrictedCilTypeV1.UInt8 ? NullableEnumUInt8Prefix : carrier == RestrictedCilTypeV1.Int16 ? NullableEnumInt16Prefix : carrier == RestrictedCilTypeV1.UInt16 ? NullableEnumUInt16Prefix : carrier == RestrictedCilTypeV1.UInt32 ? NullableEnumUInt32Prefix : carrier == RestrictedCilTypeV1.UInt64 ? NullableEnumUInt64Prefix : carrier == RestrictedCilTypeV1.Int64 ? NullableEnumInt64Prefix : NullableEnumInt32Prefix) + metadata.GetGuid(metadata.GetModuleDefinition().Mvid).ToString("N") + ":" +
            MetadataTokens.GetToken(handle).ToString("x8", System.Globalization.CultureInfo.InvariantCulture) + ":" + AggregateIdentity(metadata, handle);
    }

    private static string? NullableEnumMemberIdentity(MetadataReader metadata, int token, IReadOnlyDictionary<string, ExternalEnumCarrier>? externalEnums)
    {
        EntityHandle handle = MetadataTokens.EntityHandle(token);
        if (handle.Kind != HandleKind.MemberReference) return null;
        var member = metadata.GetMemberReference((MemberReferenceHandle)handle);
        if (member.Parent.Kind != HandleKind.TypeSpecification) return null;
        BlobReader reader = metadata.GetBlobReader(metadata.GetTypeSpecification((TypeSpecificationHandle)member.Parent).Signature);
        return ReadNullableEnum(metadata, ref reader, out var identity, externalEnums) && reader.RemainingBytes == 0 ? identity : null;
    }

    private IReadOnlyList<DecodedInstruction> ProjectNullableInt32(MetadataReader metadata, System.Reflection.PortableExecutable.PEReader pe,
        IReadOnlyList<DecodedInstruction> instructions, LocalSignature? locals, MethodSignature? signature, RestrictedCilProvenanceV1 provenance,
        out RestrictedCilImportResultV1? projectionFailure)
    {
        projectionFailure = null;
        if (locals is null) return instructions;
        if (locals.Aggregates is null) locals = locals with { Aggregates = new string?[locals.Types.Count] };
        if (locals.Aggregates is not { } declarations) return instructions;
        if (!declarations.Any(IsNullableLocal) &&
            !instructions.Any(row => row.Encoding == 0x73 && NullableInt32Member(metadata, row.Token) == NullableInt32New)) return instructions;
        ushort Member(int token, bool wide = false, bool unsigned = false, int narrowBits = 0, bool character = false, bool logical = false, string? enumIdentity = null) =>
            NullableInt32Member(metadata, token, wide, unsigned, narrowBits, character, logical, enumIdentity, locals.EnumDeclarations);
        var targets = instructions.SelectMany(ControlFlowTargets).ToHashSet();
        // Project leaf accessors before proving an enclosing default expression.
        // This is one bounded pass over the same decoder rows, not recursive
        // helper discovery. No branch may enter the consumed call instruction.
        var leaves = new List<DecodedInstruction>();
        for (int leaf = 0; leaf < instructions.Count; leaf++) {
            var address = instructions[leaf];
            if (address.Encoding == 0x12 && address.Literal >= 0 && address.Literal < locals.Types.Count &&
                IsNullableLocal(declarations[(int)address.Literal]) && leaf + 1 < instructions.Count &&
                !targets.Contains(instructions[leaf + 1].Offset)) {
                bool leafWide = IsWideNullable(declarations[(int)address.Literal]);
                bool leafUnsigned = IsUnsignedNullable(declarations[(int)address.Literal]);
                string? leafEnum = IsNullableEnum(declarations[(int)address.Literal]) ? declarations[(int)address.Literal] : null;
                bool leafBoolean = declarations[(int)address.Literal] == NullableBooleanIdentity;
                bool leafChar = declarations[(int)address.Literal] == NullableCharIdentity;
                int leafNarrow = NarrowNullableBits(declarations[(int)address.Literal]);
                var call = instructions[leaf + 1];
                ushort accessor = call.Encoding == 0x28 ? Member(call.Token, leafWide, leafUnsigned, leafNarrow, leafChar, leafBoolean, leafEnum) : (ushort)0;
                if (accessor is NullableInt32Value or NullableInt32HasValue or NullableCheckedValue) {
                    leaves.Add(address with { Encoding = accessor, Name = "nullable.local-accessor", Size = address.Size + call.Size, NullableWide = leafWide, NullableUnsigned = leafUnsigned, NullableNarrowBits = leafNarrow, NullableChar = leafChar, NullableBoolean = leafBoolean, NullableEnumIdentity = leafEnum });
                    leaf++;
                    continue;
                }
            }
            leaves.Add(address);
        }
        var normalized = leaves;
        instructions = normalized;
        void Replace(int first, int last, IReadOnlyList<DecodedInstruction> rows) {
            normalized.RemoveRange(first, last - first + 1);
            normalized.InsertRange(first, rows);
        }
        // Normalize inner expressions before their enclosing local loans. Each
        // original address is visited once; there is no recursive/fixpoint decoder.
        for (int index = instructions.Count - 1; index >= 0; index--) {
            var row = instructions[index];
            string? enumIdentity = NullableEnumMemberIdentity(metadata, row.Token, locals.EnumDeclarations);
            bool logical = Member(row.Token, unsigned: true, narrowBits: 8, logical: true) == NullableInt32New;
            bool character = Member(row.Token, unsigned: true, narrowBits: 16, character: true) == NullableInt32New;
            bool unsignedNarrow = Member(row.Token, unsigned: true, narrowBits: 16) == NullableInt32New;
            bool unsignedByte = Member(row.Token, unsigned: true, narrowBits: 8) == NullableInt32New;
            bool enumNarrow = (IsNullableEnumInt16(enumIdentity) || IsNullableEnumUInt16(enumIdentity)) && Member(row.Token, unsigned: IsUnsignedNullableEnum(enumIdentity), narrowBits: 16, enumIdentity: enumIdentity) == NullableInt32New;
            bool enumByte = (IsNullableEnumInt8(enumIdentity) || IsNullableEnumUInt8(enumIdentity)) && Member(row.Token, unsigned: IsUnsignedNullableEnum(enumIdentity), narrowBits: 8, enumIdentity: enumIdentity) == NullableInt32New;
            int narrowBits = enumByte || logical || unsignedByte || Member(row.Token, narrowBits: 8) == NullableInt32New ? 8 :
                enumNarrow || character || unsignedNarrow || Member(row.Token, narrowBits: 16) == NullableInt32New ? 16 : 0;
            bool unsignedWide = Member(row.Token, wide: true, unsigned: true) == NullableInt32New;
            bool wide = IsWideNullableEnum(enumIdentity) && Member(row.Token, wide: true, unsigned: IsUnsignedNullableEnum(enumIdentity), enumIdentity: enumIdentity) == NullableInt32New ||
                unsignedWide || Member(row.Token, wide: true) == NullableInt32New;
            bool unsigned = IsUnsignedNullableEnum(enumIdentity) && Member(row.Token, wide: IsWideNullableEnum(enumIdentity), unsigned: true, narrowBits: NarrowNullableBits(enumIdentity), enumIdentity: enumIdentity) == NullableInt32New || logical || character || unsignedByte || unsignedNarrow || unsignedWide || Member(row.Token, unsigned: true) == NullableInt32New;
            if (row.Encoding == 0x73 && (wide || unsigned || narrowBits != 0 || enumIdentity is not null && Member(row.Token, wide: wide, unsigned: IsUnsignedNullableEnum(enumIdentity), narrowBits: narrowBits, enumIdentity: enumIdentity) == NullableInt32New || Member(row.Token) == NullableInt32New)) {
                Replace(index, index, [row with { Encoding = NullableInt32New, Name = "nullable-scalar.new", NullableWide = wide, NullableUnsigned = unsigned, NullableNarrowBits = narrowBits, NullableChar = character, NullableBoolean = logical, NullableEnumIdentity = enumIdentity }]);
                continue;
            }
            if (row.Encoding == 0x12 && row.Literal >= 0 && row.Literal < locals.Types.Count &&
                IsNullableLocal(declarations[(int)row.Literal]) && index + 1 < instructions.Count &&
                !targets.Contains(instructions[index + 1].Offset)) {
                var next = instructions[index + 1];
                wide = IsWideNullable(declarations[(int)row.Literal]);
                unsigned = IsUnsignedNullable(declarations[(int)row.Literal]);
                enumIdentity = IsNullableEnum(declarations[(int)row.Literal]) ? declarations[(int)row.Literal] : null;
                logical = declarations[(int)row.Literal] == NullableBooleanIdentity;
                character = declarations[(int)row.Literal] == NullableCharIdentity;
                narrowBits = NarrowNullableBits(declarations[(int)row.Literal]);
                int callIndex = index + 2;
                if (wide && next.Encoding is >= 0x15 and <= 0x20 && callIndex < instructions.Count &&
                    instructions[callIndex].Encoding == 0x6a && !targets.Contains(instructions[callIndex].Offset)) {
                    next = next with { Encoding = 0x21, Size = next.Size + instructions[callIndex].Size };
                    callIndex++;
                }
                if (callIndex < instructions.Count && !targets.Contains(instructions[callIndex].Offset) &&
                    instructions[callIndex].Encoding == 0x28 && Member(instructions[callIndex].Token, wide, unsigned, narrowBits, character, logical, enumIdentity) is NullableInt32Default or NullableInt32New &&
                    (next.Encoding is >= 0x15 and <= 0x21 ||
                     next.Encoding is >= 0x02 and <= 0x05 or 0x0e ||
                     next.Encoding is >= 0x06 and <= 0x09 or 0x11)) {
                    bool initialize = Member(instructions[callIndex].Token, wide, unsigned, narrowBits, character, logical, enumIdentity) == NullableInt32New;
                    Replace(index, callIndex, [row with { Encoding = initialize ? NullableInt32Initialize : NullableInt32Default, Name = "nullable-int32.local-input", Size = row.Size + next.Size + instructions[callIndex].Size,
                        NullableDefault = next, ErasedReceiverDepth = row.ErasedReceiverDepth + (initialize ? 2 : 1), NullableWide = wide, NullableUnsigned = unsigned, NullableNarrowBits = narrowBits, NullableChar = character, NullableBoolean = logical, NullableEnumIdentity = enumIdentity }]);
                    continue;
                }
                ushort operation = next.Encoding == 0xfe15 && IsNullableInt32Type(metadata, MetadataTokens.EntityHandle(next.Token), wide, unsigned, narrowBits, character, logical, enumIdentity, locals.EnumDeclarations)
                    ? NullableInt32Zero : next.Encoding == 0x28 ? Member(next.Token, wide, unsigned, narrowBits, character, logical, enumIdentity) : (ushort)0;
                if (operation is NullableInt32Zero or NullableInt32Value or NullableInt32HasValue or NullableCheckedValue) {
                    Replace(index, index + 1, [row with { Encoding = operation, Name = "nullable-int32.local", Size = row.Size + next.Size,
                        ErasedReceiverDepth = row.ErasedReceiverDepth + (operation == NullableInt32Zero ? 1 : 0), NullableWide = wide, NullableUnsigned = unsigned, NullableNarrowBits = narrowBits, NullableChar = character, NullableBoolean = logical, NullableEnumIdentity = enumIdentity }]);
                    continue;
                }
                if (signature is not null && TryInt64ConstructorArgument(metadata, pe, instructions, index, locals, signature,
                    declarations[(int)row.Literal]!, new Dictionary<int, bool>(), provenance, default,
                    out int defaultEnd, out projectionFailure, nullableDefault: true)) {
                    // Retain every argument instruction in order. The receiver is a local
                    // loan, not a first-class byref; the proof excludes stores to that local.
                    var result = new List<DecodedInstruction>();
                    result.Add(row with { Encoding = 0, Name = "nullable-default.erased-address", ErasedReceiverDepth = row.ErasedReceiverDepth + 1 });
                    for (int part = index + 1; part < defaultEnd; part++) result.Add(instructions[part] with { ErasedReceiverDepth = instructions[part].ErasedReceiverDepth + 1 });
                    result.Add(instructions[defaultEnd] with { Encoding = NullableDefaultStack, Name = "nullable.default-stack", Literal = row.Literal, NullableWide = wide, NullableUnsigned = unsigned, NullableNarrowBits = narrowBits, NullableChar = character, NullableBoolean = logical, NullableEnumIdentity = enumIdentity });
                    Replace(index, defaultEnd, result);
                    continue;
                }
                if (projectionFailure is not null) return instructions;
                if (signature is not null && TryInt64ConstructorArgument(metadata, pe, instructions, index, locals, signature,
                    declarations[(int)row.Literal]!, new Dictionary<int, bool>(), provenance, default, out int end, out projectionFailure)) {
                    var result = new List<DecodedInstruction>();
                    result.Add(row with { Encoding = 0, Name = "nullable-i8.erased-address", ErasedReceiverDepth = row.ErasedReceiverDepth + 1 });
                    for (int part = index + 1; part < end; part++) result.Add(instructions[part] with { ErasedReceiverDepth = instructions[part].ErasedReceiverDepth + 1 });
                    result.Add(instructions[end] with { Encoding = NullableInitializeStack, Name = "nullable-scalar.initialize", Literal = row.Literal, NullableWide = wide, NullableUnsigned = unsigned, NullableNarrowBits = narrowBits, NullableChar = character, NullableBoolean = logical, NullableEnumIdentity = enumIdentity });
                    Replace(index, end, result);
                    continue;
                }
                if (projectionFailure is not null) return instructions;
            }
        }
        return normalized;
    }

    private static V2Value NullableDefaultValue(DecodedInstruction row, V2State state) => row.Encoding switch {
        >= 0x15 and <= 0x20 => V2Value.Constant(row.Literal, RestrictedCilTypeV1.Int32, "nullable-default"),
        0x21 => V2Value.Constant(row.Literal, RestrictedCilTypeV1.Int64, "nullable-default"),
        >= 0x02 and <= 0x05 => state.Arguments.ElementAtOrDefault(row.Encoding - 0x02) ?? V2Value.Uninitialized,
        0x0e => state.Arguments.ElementAtOrDefault((int)row.Literal) ?? V2Value.Uninitialized,
        >= 0x06 and <= 0x09 => state.Locals.ElementAtOrDefault(row.Encoding - 0x06) ?? V2Value.Uninitialized,
        0x11 => state.Locals.ElementAtOrDefault((int)row.Literal) ?? V2Value.Uninitialized,
        _ => V2Value.Uninitialized };
}
