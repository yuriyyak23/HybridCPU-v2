using System.Collections.Immutable;
using YAKSys_Hybrid_CPU.Arch.Generated;

namespace YAKSys_Hybrid_CPU.Core.Decoder;

/// <summary>Decoded security meaning, distinct from legacy descriptor policy classes.</summary>
public enum CanonicalSecurityOperationKind : byte
{
    Unknown = 0,
    Ordinary = 1,
}

public static class CanonicalSecurityOperationIdentity
{
    // Explicit conversion of the reviewed ordinary-only generated catalog.
    // A changed catalog must be reviewed here; new encodings never inherit Ordinary.
    private const string OrdinaryCatalogSha256 =
        "416607735e354770e0ec2707fe9b81ac3c3a7fccd344435e6dd8ccae6ebebdb0";

    private static readonly ImmutableDictionary<uint, GeneratedStaticBinding> Bindings =
        GeneratedIsaCatalog.Descriptors.ToImmutableDictionary(
            descriptor => descriptor.Opcode,
            descriptor => GeneratedStaticBinding.FromDescriptor(descriptor));

    public static CanonicalSecurityOperationKind FromGeneratedBinding(GeneratedStaticBinding? binding)
    {
        if (binding is null || binding.Opcode == 0 ||
            binding.CatalogSha256 != OrdinaryCatalogSha256 ||
            !Bindings.TryGetValue(binding.Opcode, out GeneratedStaticBinding? expected) ||
            binding != expected)
            return CanonicalSecurityOperationKind.Unknown;

        return CanonicalSecurityOperationKind.Ordinary;
    }

    /// <summary>Compatibility configuration only; never derives secure machine authority.</summary>
    public static CanonicalSecurityOperationKind FromLegacyPolicyClass(SecureDomainOperationClass value) =>
        value switch
        {
            SecureDomainOperationClass.Ordinary => CanonicalSecurityOperationKind.Ordinary,
            _ => CanonicalSecurityOperationKind.Unknown,
        };
}
