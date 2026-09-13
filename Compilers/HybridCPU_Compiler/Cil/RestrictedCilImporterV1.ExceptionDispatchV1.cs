using System.Buffers.Binary;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;
using System.Text;

namespace HybridCPU.Compiler.Cil;

public sealed partial class RestrictedCilImporterV1
{
    // These are declarations, not implementations or permission to substitute base-field reads.
    // The final image must bind every runtime implementation named by the plan.
    private static readonly IReadOnlyDictionary<string, string> ExceptionMessageRuntimeParents =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["System.Exception"] = "",
            ["System.SystemException"] = "System.Exception",
            ["System.ArgumentException"] = "System.SystemException",
            ["System.ArgumentNullException"] = "System.ArgumentException",
            ["System.ArgumentOutOfRangeException"] = "System.ArgumentException"
        };

    private static bool IsExceptionMessageReference(MetadataReader metadata, int token)
    {
        EntityHandle handle = MetadataTokens.EntityHandle(token);
        if (handle.Kind != HandleKind.MemberReference) return false;
        MemberReference member = metadata.GetMemberReference((MemberReferenceHandle)handle);
        return member.GetKind() == MemberReferenceKind.Method && member.Parent.Kind == HandleKind.TypeReference &&
            TypeReferenceName(metadata, (TypeReferenceHandle)member.Parent) == "System.Exception" &&
            ReferencedAssemblyName(metadata, (TypeReferenceHandle)member.Parent) is
                "System.Runtime" or "System.Private.CoreLib" or "mscorlib" &&
            metadata.GetString(member.Name) == "get_Message" &&
            metadata.GetBlobBytes(member.Signature).AsSpan().SequenceEqual(new byte[] { 0x20, 0, 0x0e });
    }

    private static ManagedVirtualSlotPlanV1 BuildExceptionMessageSlotPlan(IReadOnlyList<ManagedModuleContext> modules)
    {
        const string owner = "System.Exception", declaration = "System.Exception.get_Message", signature = "instance():System.String";
        ulong slot = BinaryPrimitives.ReadUInt64LittleEndian(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"hybridcpu.managed-slot-id/v1|{MetadataTypeId(owner)}|{declaration}|{signature}")));
        if (slot == 0) slot = 1;
        var rows = new Dictionary<string, ManagedVirtualSlotTargetV1>(StringComparer.Ordinal);
        foreach (string type in ExceptionMessageRuntimeParents.Keys)
            rows.Add(type, new(type, type is "System.ArgumentException" or "System.ArgumentNullException" or "System.ArgumentOutOfRangeException"
                ? "System.ArgumentException.get_Message" : declaration, null, null));
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var hiddenMessageSlots = new HashSet<string>(StringComparer.Ordinal);
        var runtimeTypeSources = new Dictionary<string, string>(StringComparer.Ordinal);
        ManagedVirtualSlotTargetV1? Resolve(ManagedModuleContext module, TypeDefinitionHandle handle)
        {
            MetadataReader metadata = module.Metadata;
            string typeName = FullTypeName(metadata, handle);
            if (rows.TryGetValue(typeName, out var known))
            {
                if (ExceptionMessageRuntimeParents.ContainsKey(typeName) && module.AssemblyName != "System.Private.CoreLib")
                    throw new NotSupportedException($"Runtime-owned exception type '{typeName}' is shadowed by '{module.AssemblyName}'.");
                if (runtimeTypeSources.TryGetValue(typeName, out var source) && source != module.AssemblyName)
                    throw new NotSupportedException($"Exception type identity '{typeName}' is ambiguous across modules.");
                return known;
            }
            if (!visiting.Add(typeName)) throw new BadImageFormatException("Cyclic exception inheritance.");
            try
            {
                TypeDefinition type = metadata.GetTypeDefinition(handle);
                if (type.BaseType.IsNil) return null;
                string baseName = ExactTypeHandleName(metadata, type.BaseType);
                ManagedVirtualSlotTargetV1? inherited = null;
                if (ExceptionMessageRuntimeParents.ContainsKey(baseName)) inherited = rows[baseName];
                else if (TryResolveType(modules, module, type.BaseType, out var parentModule, out var parent))
                    inherited = Resolve(parentModule, parent);
                if (inherited is null) return null;
                bool hiddenSlot = hiddenMessageSlots.Contains(baseName);
                if (hiddenSlot || type.GetMethods().Any(methodHandle =>
                    {
                        var method = metadata.GetMethodDefinition(methodHandle);
                        return metadata.GetString(method.Name) == "get_Message" &&
                            method.Attributes.HasFlag(MethodAttributes.Virtual) && method.Attributes.HasFlag(MethodAttributes.NewSlot) &&
                            metadata.GetBlobBytes(method.Signature).AsSpan().SequenceEqual(new byte[] { 0x20, 0, 0x0e });
                    })) hiddenMessageSlots.Add(typeName);
                foreach (var implementationHandle in type.GetMethodImplementations())
                {
                    var implementation = metadata.GetMethodImplementation(implementationHandle);
                    if (DescribeMethodReference(metadata, MetadataTokens.GetToken(implementation.MethodDeclaration))
                        .EndsWith(".get_Message", StringComparison.Ordinal))
                        throw new NotSupportedException($"Explicit MethodImpl on '{typeName}.get_Message' requires exact slot mapping.");
                }
                MethodDefinitionHandle[] overrides = type.GetMethods().Where(methodHandle =>
                {
                    var method = metadata.GetMethodDefinition(methodHandle);
                    return metadata.GetString(method.Name) == "get_Message" &&
                        method.Attributes.HasFlag(MethodAttributes.Virtual) && !method.Attributes.HasFlag(MethodAttributes.NewSlot);
                }).ToArray();
                if (overrides.Length > 1) throw new BadImageFormatException("Ambiguous Message overrides.");
                var target = inherited with { RuntimeTypeIdentity = typeName };
                if (overrides.Length == 1 && !hiddenSlot)
                {
                    var method = metadata.GetMethodDefinition(overrides[0]);
                    if (type.GetGenericParameters().Count != 0 || method.GetGenericParameters().Count != 0 ||
                        method.Attributes.HasFlag(MethodAttributes.Abstract) ||
                        !metadata.GetBlobBytes(method.Signature).AsSpan().SequenceEqual(new byte[] { 0x20, 0, 0x0e }))
                        throw new NotSupportedException($"Override '{typeName}.get_Message' requires an exact concrete closed signature.");
                    target = new(typeName, typeName + ".get_Message", module.AssemblyName, MetadataTokens.GetToken(overrides[0]));
                }
                rows.Add(typeName, target);
                runtimeTypeSources.Add(typeName, module.AssemblyName);
                return target;
            }
            finally { visiting.Remove(typeName); }
        }
        foreach (var module in modules.OrderBy(static row => row.AssemblyName, StringComparer.Ordinal))
            foreach (var type in module.Metadata.TypeDefinitions) Resolve(module, type);
        var targets = rows.Values.OrderBy(static row => row.RuntimeTypeIdentity, StringComparer.Ordinal).ToArray();
        if (targets.Length > HybridCPU.Platform.Contracts.HybridCpuPlatformContractV1.MaximumManagedTypes)
            throw new NotSupportedException("Exception dispatch receiver set exceeds the managed type budget.");
        string digest = Hash(string.Join('|', "hybridcpu.virtual-slot-plan/v1", slot, owner, declaration, signature,
            string.Join(';', targets.Select(static row => $"{row.RuntimeTypeIdentity}:{row.ImplementationIdentity}:{row.AssemblyName}:{row.MethodMetadataToken}"))));
        return new(slot, owner, declaration, signature, targets, digest);
    }

    private static RestrictedCilDispatchBindingV1 ExceptionMessageBinding(int token, ManagedVirtualSlotPlanV1 plan) =>
        new(token, plan.DeclarationIdentity, [RestrictedCilTypeV1.ObjectReference], RestrictedCilTypeV1.ObjectReference,
            RestrictedCilDispatchKindV1.Virtual, plan.SlotId,
            ExactGenericCandidates: plan.Targets.Where(static row => row.MethodMetadataToken.HasValue)
                .DistinctBy(static row => (row.AssemblyName, row.MethodMetadataToken))
                .Select(static row => new RestrictedCilDispatchCandidateV1(row.MethodMetadataToken!.Value, [], [],
                    AssemblyName: row.AssemblyName)).ToArray(), RuntimeExternal: true);
}
