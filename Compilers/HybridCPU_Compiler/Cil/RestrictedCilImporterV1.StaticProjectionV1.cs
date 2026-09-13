using System.Buffers.Binary;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace HybridCPU.Compiler.Cil;

public sealed partial class RestrictedCilImporterV1
{
    private sealed record StaticScalarProjection(string OwnerType, int GetterToken, int InnerFieldToken, string BodyDigest);

    // Eliminate only an immediately consumed, nonescaping static byref whose complete
    // getter is a single I4 field read. This does not provide a general byref ABI.
    private static IReadOnlyList<DecodedInstruction> ProjectStaticScalarReads(MetadataReader metadata, PEReader pe,
        IReadOnlyList<DecodedInstruction> instructions)
    {
        var targets = instructions.SelectMany(ControlFlowTargets).ToHashSet();
        var result = new List<DecodedInstruction>();
        for (int index = 0; index < instructions.Count; index++)
        {
            var address = instructions[index];
            if (address.Encoding == 0x7f && index + 1 < instructions.Count && instructions[index + 1] is { Encoding: 0x28 } call &&
                !targets.Contains(call.Offset) && TryStaticScalarProjection(metadata, pe, address.Token, call.Token) is { } proof)
            {
                result.Add(address with { Encoding = 0x7e, Name = "ldsfld.projected-i4", Size = address.Size + call.Size, Projection = proof });
                index++;
            }
            else result.Add(address);
        }
        return result;
    }

    private static StaticScalarProjection? TryStaticScalarProjection(MetadataReader metadata, PEReader pe, int fieldToken, int methodToken)
    {
        if (MetadataTokens.EntityHandle(fieldToken).Kind != HandleKind.FieldDefinition ||
            MetadataTokens.EntityHandle(methodToken).Kind != HandleKind.MethodDefinition) return null;
        var fieldHandle = (FieldDefinitionHandle)MetadataTokens.EntityHandle(fieldToken);
        var methodHandle = (MethodDefinitionHandle)MetadataTokens.EntityHandle(methodToken);
        var field = metadata.GetFieldDefinition(fieldHandle);
        if ((field.Attributes & (FieldAttributes.Static | FieldAttributes.InitOnly)) != (FieldAttributes.Static | FieldAttributes.InitOnly)) return null;
        var fieldSig = metadata.GetBlobReader(field.Signature);
        if (fieldSig.RemainingBytes < 3 || fieldSig.ReadByte() != 0x06 || fieldSig.ReadByte() != 0x11) return null;
        var valueHandle = ReadTypeDefOrRefEncoded(ref fieldSig);
        if (fieldSig.RemainingBytes != 0 || valueHandle.Kind != HandleKind.TypeDefinition || valueHandle.IsNil) return null;
        var typeHandle = (TypeDefinitionHandle)valueHandle;
        var type = metadata.GetTypeDefinition(typeHandle);
        if (!type.GetFields().Contains(fieldHandle) || !type.GetMethods().Contains(methodHandle) || type.GetGenericParameters().Count != 0 ||
            ExactTypeHandleName(metadata, type.BaseType) != "System.ValueType" || new InlineStorageResolver([]).Value(metadata, typeHandle) is not { Size: 4, Alignment: 4 }) return null;
        var getter = metadata.GetMethodDefinition(methodHandle);
        byte[] signature = metadata.GetBlobBytes(getter.Signature);
        if ((getter.Attributes & (MethodAttributes.Static | MethodAttributes.Virtual | MethodAttributes.PinvokeImpl | MethodAttributes.Abstract)) != 0 ||
            (getter.ImplAttributes & MethodImplAttributes.Synchronized) != 0 || getter.GetGenericParameters().Count != 0 ||
            getter.RelativeVirtualAddress == 0 || signature.Length != 3 || signature[0] != 0x20 || signature[1] != 0 || signature[2] is not (0x08 or 0x09)) return null;
        var body = pe.GetMethodBody(getter.RelativeVirtualAddress);
        if (body.Size > 64) return null;
        byte[] il = body.GetILBytes() ?? [];
        if (body.ExceptionRegions.Length != 0 || !body.LocalSignature.IsNil || il.Length != 7 || il[0] != 0x02 || il[1] != 0x7b || il[6] != 0x2a) return null;
        int innerToken = BinaryPrimitives.ReadInt32LittleEndian(il.AsSpan(2, 4));
        if (MetadataTokens.EntityHandle(innerToken).Kind != HandleKind.FieldDefinition) return null;
        var instanceFields = type.GetFields().Where(handle => !metadata.GetFieldDefinition(handle).Attributes.HasFlag(FieldAttributes.Static)).ToArray();
        if (instanceFields.Length != 1 || MetadataTokens.GetToken(instanceFields[0]) != innerToken) return null;
        byte[] innerSignature = metadata.GetBlobBytes(metadata.GetFieldDefinition(instanceFields[0]).Signature);
        if (innerSignature.Length != 2 || innerSignature[0] != 0x06 || innerSignature[1] != signature[2]) return null;
        return new(FullTypeName(metadata, typeHandle), methodToken, innerToken, HashBytes(il));
    }
}
