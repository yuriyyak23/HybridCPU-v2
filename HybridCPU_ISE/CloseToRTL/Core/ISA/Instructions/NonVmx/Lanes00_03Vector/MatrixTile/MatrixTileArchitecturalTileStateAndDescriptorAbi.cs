namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile;

public enum MatrixTileStateOwnerKind : byte
{
    Unspecified = 0,
    GuestArchitecturalTileRegisterFile = 1,
}

public enum MatrixTileLifetimePolicyKind : byte
{
    Unspecified = 0,
    GuestArchitecturalTileRegisterFile = 1,
}

public enum MatrixTileDescriptorLayoutKind : byte
{
    Unspecified = 0,
    RowMajor = 1,
}

public readonly record struct MatrixTileCanonicalDescriptorAbi(
    ushort Rows,
    ushort Columns,
    ushort ElementSizeBytes,
    uint StrideBytes,
    MatrixTileDescriptorLayoutKind Layout)
{
    public static MatrixTileCanonicalDescriptorAbi Zero { get; } = default;

    public static MatrixTileCanonicalDescriptorAbi Create(
        ushort rows,
        ushort columns,
        ushort elementSizeBytes,
        uint strideBytes,
        MatrixTileDescriptorLayoutKind layout = MatrixTileDescriptorLayoutKind.RowMajor)
    {
        return new MatrixTileCanonicalDescriptorAbi(rows, columns, elementSizeBytes, strideBytes, layout);
    }

    public bool IsZeroEncoding =>
        Rows == 0 &&
        Columns == 0 &&
        ElementSizeBytes == 0 &&
        StrideBytes == 0 &&
        Layout == MatrixTileDescriptorLayoutKind.Unspecified;

    public bool HasValidShape =>
        Rows != 0 &&
        Columns != 0 &&
        StrideBytes != 0;

    public bool HasValidElementSize =>
        ElementSizeBytes != 0;

    public bool HasCanonicalLayout =>
        Layout == MatrixTileDescriptorLayoutKind.RowMajor;

    public bool IsCanonical =>
        HasValidShape &&
        HasValidElementSize &&
        HasCanonicalLayout;

    public bool IsReservedEncoding =>
        !IsZeroEncoding &&
        !IsCanonical;
}

public readonly record struct MatrixTileArchitecturalTileStateContract(
    MatrixTileStateOwnerKind OwnerKind,
    MatrixTileLifetimePolicyKind LifetimePolicy,
    MatrixTileCanonicalDescriptorAbi Descriptor,
    bool GuestVisible,
    bool HostOwnedEvidenceIsNonArchitectural)
{
    public bool HasSelectedOwner =>
        OwnerKind == MatrixTileStateOwnerKind.GuestArchitecturalTileRegisterFile;

    public bool HasSelectedLifetimePolicy =>
        LifetimePolicy == MatrixTileLifetimePolicyKind.GuestArchitecturalTileRegisterFile;

    public bool HasCanonicalDescriptor =>
        Descriptor.IsCanonical;
}

public static class MatrixTileArchitecturalTileStateAndDescriptorAbi
{
    public const string TileStateOwnerDecision = "GuestArchitecturalTileRegisterFileOwnerSelected";
    public const string TileStateLifetimeDecision = "GuestArchitecturalTileRegisterFileLifetimeSelected";
    public const string DescriptorCarrierDecision = "CanonicalMatrixTileDescriptorCarrier";
    public const string DescriptorValidationDecision = "RowsColumnsElementSizeStrideAndLayoutValidated";
    public const string ReservedDescriptorDecision = "ZeroAndReservedDescriptorsFailClosed";

    public const bool HasArchitecturalTileStateOwner = true;
    public const bool HasCanonicalTileDescriptorAbi = true;
    public const bool HasRuntimeOwnedTileStateContract = true;
    public const bool HasTileDescriptorValidationHelpers = true;
    public const bool HasReservedDescriptorFailFastTests = true;
    public const bool KeepsHostOwnedEvidenceNonArchitectural = true;
    public const bool KeepsCompilerHandoffBlocked = true;

    public static MatrixTileArchitecturalTileStateContract GuestArchitecturalTileStateContract { get; } =
        new(
            MatrixTileStateOwnerKind.GuestArchitecturalTileRegisterFile,
            MatrixTileLifetimePolicyKind.GuestArchitecturalTileRegisterFile,
            MatrixTileCanonicalDescriptorAbi.Create(1, 1, 1, 1),
            GuestVisible: true,
            HostOwnedEvidenceIsNonArchitectural: true);

    public static bool IsGuestArchitecturalOwner(MatrixTileStateOwnerKind ownerKind) =>
        ownerKind == MatrixTileStateOwnerKind.GuestArchitecturalTileRegisterFile;

    public static bool HasCanonicalLifetimePolicy(MatrixTileLifetimePolicyKind lifetimePolicy) =>
        lifetimePolicy == MatrixTileLifetimePolicyKind.GuestArchitecturalTileRegisterFile;

    public static bool IsCanonicalDescriptor(MatrixTileCanonicalDescriptorAbi descriptor) =>
        descriptor.IsCanonical;

    public static bool IsZeroDescriptor(MatrixTileCanonicalDescriptorAbi descriptor) =>
        descriptor.IsZeroEncoding;

    public static bool IsReservedDescriptor(MatrixTileCanonicalDescriptorAbi descriptor) =>
        descriptor.IsReservedEncoding;

    public static string ValidateDescriptor(MatrixTileCanonicalDescriptorAbi descriptor) =>
        descriptor.IsCanonical
            ? DescriptorValidationDecision
            : descriptor.IsZeroEncoding
                ? "ZeroMatrixTileDescriptorEncodingRejected"
                : "ReservedMatrixTileDescriptorEncodingRejected";
}
