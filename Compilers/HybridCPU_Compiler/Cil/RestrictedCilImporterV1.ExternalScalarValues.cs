using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using HybridCPU.Compiler.Core.IR.Contracts;

namespace HybridCPU.Compiler.Cil;

public sealed partial class RestrictedCilImporterV1
{
    private sealed record ScalarValueDeclaration(string DefinitionImageDigest, RestrictedCilTypeV1 Carrier,
        IReadOnlySet<string> GetterIdentities);
    // Synchronous BodyWorld-owned proofs, never readers or persistent cache entries.
    private IReadOnlyDictionary<string, ScalarValueDeclaration> _externalScalarValues =
        new Dictionary<string, ScalarValueDeclaration>(StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, ScalarValueDeclaration> CollectScalarValueDeclarations(
        IEnumerable<ManagedModuleContext> modules, ManagedMetadataBindingSet bindings, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, ScalarValueDeclaration>(StringComparer.Ordinal);
        foreach (var module in modules) {
            cancellationToken.ThrowIfCancellationRequested();
            var allocations = bindings.AllocationsByAssembly.GetValueOrDefault(module.AssemblyName) ?? [];
            foreach (var type in module.Metadata.TypeDefinitions) {
                cancellationToken.ThrowIfCancellationRequested();
                if (module.Metadata.GetTypeDefinition(type).BaseType.IsNil) continue;
                string? identity = AggregateIdentity(module.Metadata, type);
                if (identity is null) continue;
                var carriers = allocations.Where(binding => binding.IsScalarValueProjection &&
                    binding.ScalarValueType is RestrictedCilTypeV1.Int32 or RestrictedCilTypeV1.Int64 &&
                    binding.TypeDescriptor.StableIdentity == NestedTypeName(module.Metadata, type) && IsExactScalarCarrierLayout(binding))
                    .Select(binding => binding.ScalarValueType).Distinct().ToArray();
                if (carriers.Length != 1) continue;
                var carrier = carriers[0];
                var getters = new HashSet<string>(StringComparer.Ordinal);
                foreach (var method in module.Metadata.GetTypeDefinition(type).GetMethods()) {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!IsExactLocalScalarGetter(module.Metadata, module.Reader, MetadataTokens.GetToken(method), identity, carrier)) continue;
                    var signature = BindValueReceiver(module.Metadata, type,
                        ParseMethodSignature(module.Metadata, module.Metadata.GetMethodDefinition(method).Signature, [], [], allowAggregates: true));
                    getters.Add(CreateManagedMethodIdentity(module.Metadata, type, method, signature).StableIdentity);
                }
                if (getters.Count != 0 && !result.TryAdd(identity, new(module.ImageDigest, carrier, getters)))
                    throw new BadImageFormatException("Duplicate scalar value declaration identity.");
            }
        }
        return result;
    }

    private static IReadOnlyDictionary<string, ScalarValueDeclaration> ReferencedScalarValues(MetadataReader metadata,
        IReadOnlyDictionary<string, ScalarValueDeclaration> declarations, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, ScalarValueDeclaration>(StringComparer.Ordinal);
        foreach (var type in metadata.TypeReferences) {
            cancellationToken.ThrowIfCancellationRequested();
            string? identity = AggregateIdentity(metadata, type);
            if (identity is not null && declarations.TryGetValue(identity, out var declaration)) result[identity] = declaration;
        }
        return result;
    }

    private bool IsExactScalarGetter(MetadataReader metadata, System.Reflection.PortableExecutable.PEReader pe, int token,
        string identity, RestrictedCilTypeV1 carrier) =>
        IsExactLocalScalarGetter(metadata, pe, token, identity, carrier) ||
        _externalScalarValues.TryGetValue(identity, out var declaration) && declaration.Carrier == carrier &&
        _managedCallTargets.TryGetValue(token, out var target) && target.ReturnType == carrier &&
        target.Receiver?.ScopedTypeIdentity == identity && target.ParameterTypes.Count == 1 &&
        target.ParameterTypes[0] == RestrictedCilTypeV1.ManagedByRef && declaration.GetterIdentities.Contains(target.StableIdentity);

    private string WithExternalScalarOptions(string original) => _externalScalarValues.Count == 0 ? original :
        CompilerContractCanonicalJson.Hash(CompilerContractCanonicalJson.Serialize(new {
            Schema = "hybridcpu.cil-external-scalar-options/v2", Original = original,
            Definitions = _externalScalarValues.OrderBy(row => row.Key, StringComparer.Ordinal)
                .Select(row => new { Identity = row.Key, row.Value.DefinitionImageDigest, row.Value.Carrier,
                    Getters = row.Value.GetterIdentities.Order(StringComparer.Ordinal).ToArray() }).ToArray()
        }));
}
