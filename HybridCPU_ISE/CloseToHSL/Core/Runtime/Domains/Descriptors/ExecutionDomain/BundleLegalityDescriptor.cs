namespace YAKSys_Hybrid_CPU.Core;

public enum BundleLegalityAuthority : byte
{
    Runtime = 0,
    CompilerEvidence = 1,
    CompatibilityProjection = 2,
}

public sealed partial class BundleLegalityDescriptor
{
    public BundleLegalityDescriptor()
        : this(
            authority: BundleLegalityAuthority.Runtime,
            requiresRuntimeValidation: true,
            acceptsCompilerEvidence: true,
            allowsCompatibilityProjection: true)
    {
    }

    public BundleLegalityDescriptor(
        BundleLegalityAuthority authority,
        bool requiresRuntimeValidation,
        bool acceptsCompilerEvidence,
        bool allowsCompatibilityProjection)
    {
        Authority = authority;
        RequiresRuntimeValidation = requiresRuntimeValidation;
        AcceptsCompilerEvidence = acceptsCompilerEvidence;
        AllowsCompatibilityProjection = allowsCompatibilityProjection;
    }

    public BundleLegalityAuthority Authority { get; }

    public bool RequiresRuntimeValidation { get; }

    public bool AcceptsCompilerEvidence { get; }

    public bool AllowsCompatibilityProjection { get; }

    public bool IsRuntimeAuthoritative =>
        Authority == BundleLegalityAuthority.Runtime;

    public bool CanUseCompilerEvidenceAsAuthority => false;

    public bool CanProjectToCompatibilityFrontend =>
        AllowsCompatibilityProjection &&
        IsRuntimeAuthoritative;

    public BundleLegalityDescriptor WithCompatibilityProjection(bool enabled) =>
        new(Authority, RequiresRuntimeValidation, AcceptsCompilerEvidence, enabled);
}
