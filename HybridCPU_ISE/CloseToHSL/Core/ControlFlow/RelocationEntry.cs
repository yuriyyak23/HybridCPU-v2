namespace YAKSys_Hybrid_CPU.Core.ControlFlow
{
    public enum RelocationKind
    {
        AbsoluteJump,
        AbsoluteCall,
        AbsoluteInterrupt
    }

    public enum RelocationEncodingKind
    {
        LegacyAbsolute64
    }

    public readonly record struct RelocationEntry(
        RelocationKind Kind,
        ulong PatchAddress,
        string TargetSymbol,
        byte PatchWidth,
        ulong BundlePc,
        RelocationEncodingKind EncodingKind,
        ulong ResolvedTargetAddress)
    {
        public const byte Absolute64PatchWidth = 8;

        public static RelocationEntry CreateLegacyAbsolute64(
            RelocationKind kind,
            ulong emissionCursor,
            string targetSymbol,
            ulong resolvedTargetAddress)
        {
            ulong patchAddress =
                EntryPointRelocationEncodingRules.ResolveLegacyAbsoluteTargetPatchAddress(emissionCursor);

            return new RelocationEntry(
                kind,
                patchAddress,
                targetSymbol ?? string.Empty,
                Absolute64PatchWidth,
                patchAddress - (patchAddress % EntryPointRelocationEncodingRules.VliwBundleBytes),
                RelocationEncodingKind.LegacyAbsolute64,
                resolvedTargetAddress);
        }
    }

    public static class EntryPointRelocationEncodingRules
    {
        public const ulong VliwBundleBytes = 256;

        public const ulong LegacyAbsoluteTargetPatchBackOffsetBytes = 16;

        public static ulong ResolveLegacyAbsoluteTargetPatchAddress(ulong emissionCursor)
        {
            if (emissionCursor < LegacyAbsoluteTargetPatchBackOffsetBytes)
            {
                throw new RelocationEncodingException(
                    emissionCursor,
                    LegacyAbsoluteTargetPatchBackOffsetBytes);
            }

            return emissionCursor - LegacyAbsoluteTargetPatchBackOffsetBytes;
        }
    }
}
