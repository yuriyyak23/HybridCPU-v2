param(
    [Parameter(Mandatory = $true)]
    [string] $ProjectRoot,

    [string] $OutputRoot = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Resolve-ProjectPath {
    param([Parameter(Mandatory = $true)][string] $RelativePath)

    $nativeRelativePath = $RelativePath.Replace("/", [System.IO.Path]::DirectorySeparatorChar)
    return [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($ProjectRoot, $nativeRelativePath))
}

function Read-JsonArtifact {
    param([Parameter(Mandatory = $true)][string] $RelativePath)

    $path = Resolve-ProjectPath $RelativePath
    if (-not [System.IO.File]::Exists($path)) {
        throw "Missing projection schema artifact: $RelativePath"
    }

    return Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

function Format-HexLiteral {
    param([Parameter(Mandatory = $true)][string] $Value)

    $hex = $Value.Trim()
    if ($hex.StartsWith("0x", [System.StringComparison]::OrdinalIgnoreCase)) {
        $hex = $hex.Substring(2)
    }

    $hex = $hex.Replace("_", "").ToUpperInvariant()
    if ($hex.Length -eq 0) {
        throw "Empty hash literal in projection schema."
    }

    $groups = New-Object System.Collections.Generic.List[string]
    for ($index = 0; $index -lt $hex.Length; $index += 4) {
        $length = [System.Math]::Min(4, $hex.Length - $index)
        $groups.Add($hex.Substring($index, $length))
    }

    return "0x$([string]::Join("_", $groups))UL"
}

function Format-BoolLiteral {
    param([Parameter(Mandatory = $true)] $Value)

    if ([bool] $Value) {
        return "true"
    }

    return "false"
}

function Normalize-Text {
    param([Parameter(Mandatory = $true)][string] $Text)

    return ($Text -replace "`r`n", "`n").TrimEnd() + "`n"
}

function Write-Utf8NoBom {
    param(
        [Parameter(Mandatory = $true)][string] $Path,
        [Parameter(Mandatory = $true)][string] $Text)

    $directory = [System.IO.Path]::GetDirectoryName($Path)
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        [System.IO.Directory]::CreateDirectory($directory) | Out-Null
    }

    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Text, $encoding)
}

function Assert-RequiredSchemaSwitches {
    param(
        [Parameter(Mandatory = $true)] $Schema,
        [Parameter(Mandatory = $true)][string] $ExpectedName,
        [Parameter(Mandatory = $true)][string] $ExpectedGenerator)

    if ($Schema.schemaName -ne $ExpectedName) {
        throw "Unexpected schema name '$($Schema.schemaName)' for $ExpectedName."
    }

    if ([int] $Schema.schemaVersion -ne 1) {
        throw "Unsupported schema version '$($Schema.schemaVersion)' for $ExpectedName."
    }

    if ($Schema.generator -ne $ExpectedGenerator) {
        throw "Unexpected generator '$($Schema.generator)' for $ExpectedName."
    }

    if (-not [bool] $Schema.generatedAtBuild) {
        throw "$ExpectedName must declare generatedAtBuild=true."
    }

    if (-not [bool] $Schema.requiresConformanceParity) {
        throw "$ExpectedName must require conformance parity."
    }

    if (-not [bool] $Schema.requiresAbiFreeze) {
        throw "$ExpectedName must require ABI freeze."
    }

    if ([string]::IsNullOrWhiteSpace([string] $Schema.canonicalSourceHash) -or
        [string]::IsNullOrWhiteSpace([string] $Schema.generatedArtifactHash)) {
        throw "$ExpectedName must publish canonical source and generated artifact hashes."
    }
}

function Assert-GeneratedSourceMatches {
    param(
        [Parameter(Mandatory = $true)][string] $RelativeSourcePath,
        [Parameter(Mandatory = $true)][string] $ExpectedFileName,
        [Parameter(Mandatory = $true)][string] $ExpectedSource)

    if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
        $script:OutputRoot = Resolve-ProjectPath "obj/GeneratedProjectionLineage"
    }

    $actualPath = Resolve-ProjectPath $RelativeSourcePath
    if (-not [System.IO.File]::Exists($actualPath)) {
        throw "Missing generated projection source: $RelativeSourcePath"
    }

    $expectedPath = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($OutputRoot, $ExpectedFileName))
    Write-Utf8NoBom $expectedPath $ExpectedSource

    $actual = [System.IO.File]::ReadAllText($actualPath)
    if ((Normalize-Text $actual) -ne (Normalize-Text $ExpectedSource)) {
        throw "Generated projection drift detected for $RelativeSourcePath. Regenerated expectation: $expectedPath"
    }

    return $expectedPath
}

function New-VmcsFieldProjectionSchemaSource {
    param([Parameter(Mandatory = $true)] $Schema)

    $entries = @($Schema.entries)
    if ($entries.Count -le 0) {
        throw "vmcs-field-projection-schema requires concrete entries for build-time regeneration."
    }

    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add("using System;")
    $lines.Add("")
    $lines.Add("namespace YAKSys_Hybrid_CPU.Core;")
    $lines.Add("")
    $lines.Add("public enum VmcsFieldProjectionOwner : byte")
    $lines.Add("{")
    $lines.Add("    ExecutionDomainDescriptor = 0,")
    $lines.Add("    MemoryDomainDescriptor = 1,")
    $lines.Add("    CompletionRecord = 2,")
    $lines.Add("    CompatibilityControlDescriptor = 3,")
    $lines.Add("}")
    $lines.Add("")
    $lines.Add("public enum VmcsFieldProjectionAccessPolicy : byte")
    $lines.Add("{")
    $lines.Add("    Denied = 0,")
    $lines.Add("    ReadOnly = 1,")
    $lines.Add("    ReadWrite = 2,")
    $lines.Add("}")
    $lines.Add("")
    $lines.Add("public enum VmcsFieldProjectionMigrationPolicy : byte")
    $lines.Add("{")
    $lines.Add("    None = 0,")
    $lines.Add("    DescriptorOwned = 1,")
    $lines.Add("    RecomputedCompletion = 2,")
    $lines.Add("    ProjectionOnly = 3,")
    $lines.Add("}")
    $lines.Add("")
    $lines.Add("public readonly record struct VmcsFieldProjectionSchemaEntry(")
    $lines.Add("    VmcsField Field,")
    $lines.Add("    VmcsFieldProjectionOwner Owner,")
    $lines.Add("    EvidenceVisibilityClass EvidenceClass,")
    $lines.Add("    VmcsFieldProjectionAccessPolicy AccessPolicy,")
    $lines.Add("    VmcsFieldProjectionMigrationPolicy MigrationPolicy,")
    $lines.Add("    bool IsGeneratedAlias);")
    $lines.Add("")
    $lines.Add("public readonly record struct VmcsFieldProjectionSchemaArtifact(")
    $lines.Add("    string SchemaName,")
    $lines.Add("    int SchemaVersion,")
    $lines.Add("    ulong SourceHash,")
    $lines.Add("    ulong ArtifactHash,")
    $lines.Add("    int EntryCount,")
    $lines.Add("    bool IsCanonical);")
    $lines.Add("")
    $lines.Add("public static class VmcsFieldProjectionSchema")
    $lines.Add("{")
    $lines.Add("    public const int CurrentVersion = $($Schema.schemaVersion);")
    $lines.Add("")
    $lines.Add("    private static readonly VmcsFieldProjectionSchemaEntry[] EntryTable = new VmcsFieldProjectionSchemaEntry[]")
    $lines.Add("    {")
    foreach ($entry in $entries) {
        $generatedAlias = Format-BoolLiteral $entry.generatedAlias
        $lines.Add("        new(VmcsField.$($entry.field), VmcsFieldProjectionOwner.$($entry.owner), EvidenceVisibilityClass.$($entry.evidenceClass), VmcsFieldProjectionAccessPolicy.$($entry.accessPolicy), VmcsFieldProjectionMigrationPolicy.$($entry.migrationPolicy), $generatedAlias),")
    }
    $lines.Add("    };")
    $lines.Add("")
    $lines.Add("    public static ReadOnlySpan<VmcsFieldProjectionSchemaEntry> Entries => EntryTable;")
    $lines.Add("")
    $lines.Add("    public static VmcsFieldProjectionSchemaArtifact CanonicalArtifact { get; } = new(")
    $lines.Add("        ""$($Schema.schemaName)"",")
    $lines.Add("        CurrentVersion,")
    $lines.Add("        SourceHash: $(Format-HexLiteral $Schema.canonicalSourceHash),")
    $lines.Add("        ArtifactHash: $(Format-HexLiteral $Schema.generatedArtifactHash),")
    $lines.Add("        EntryTable.Length,")
    $lines.Add("        IsCanonical: true);")
    $lines.Add("")
    $lines.Add("    public static bool TryGet(VmcsField field, out VmcsFieldProjectionSchemaEntry entry)")
    $lines.Add("    {")
    $lines.Add("        foreach (var candidate in Entries)")
    $lines.Add("        {")
    $lines.Add("            if (candidate.Field == field)")
    $lines.Add("            {")
    $lines.Add("                entry = candidate;")
    $lines.Add("                return true;")
    $lines.Add("            }")
    $lines.Add("        }")
    $lines.Add("")
    $lines.Add("        entry = default;")
    $lines.Add("        return false;")
    $lines.Add("    }")
    $lines.Add("")
    $lines.Add("    public static bool CanRead(VmcsFieldProjectionSchemaEntry entry) =>")
    $lines.Add("        entry.AccessPolicy == VmcsFieldProjectionAccessPolicy.ReadOnly ||")
    $lines.Add("        entry.AccessPolicy == VmcsFieldProjectionAccessPolicy.ReadWrite;")
    $lines.Add("")
    $lines.Add("    public static bool CanWrite(VmcsFieldProjectionSchemaEntry entry) => false;")
    $lines.Add("}")

    return [string]::Join("`r`n", $lines) + "`r`n"
}

function New-CompatAliasMapSource {
    param([Parameter(Mandatory = $true)] $Schema)

    $entries = @($Schema.entries)
    if ($entries.Count -le 0) {
        throw "compat-alias-schema requires concrete entries for build-time regeneration."
    }

    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add("using System;")
    $lines.Add("")
    $lines.Add("namespace YAKSys_Hybrid_CPU.Core;")
    $lines.Add("")
    $lines.Add("public enum CompatAliasSourceKind : byte")
    $lines.Add("{")
    $lines.Add("    Opcode = 0,")
    $lines.Add("    Csr = 1,")
    $lines.Add("    VmcsField = 2,")
    $lines.Add("    Completion = 3,")
    $lines.Add("    FunctionLeaf = 4,")
    $lines.Add("}")
    $lines.Add("")
    $lines.Add("public enum CompatAliasTargetKind : byte")
    $lines.Add("{")
    $lines.Add("    DomainRuntimeOperation = 0,")
    $lines.Add("    DescriptorField = 1,")
    $lines.Add("    CapabilityGrant = 2,")
    $lines.Add("    CompletionRecord = 3,")
    $lines.Add("    CompatibilityProjection = 4,")
    $lines.Add("}")
    $lines.Add("")
    $lines.Add("public readonly record struct CompatAliasMapEntry(")
    $lines.Add("    CompatAliasSourceKind SourceKind,")
    $lines.Add("    string SourceName,")
    $lines.Add("    CompatAliasTargetKind TargetKind,")
    $lines.Add("    string TargetName,")
    $lines.Add("    bool IsFrozenAbi);")
    $lines.Add("")
    $lines.Add("public readonly record struct CompatAliasSchemaArtifact(")
    $lines.Add("    string SchemaName,")
    $lines.Add("    int SchemaVersion,")
    $lines.Add("    ulong SourceHash,")
    $lines.Add("    ulong ArtifactHash,")
    $lines.Add("    int EntryCount,")
    $lines.Add("    bool IsCanonical);")
    $lines.Add("")
    $lines.Add("public sealed partial class CompatAliasMap")
    $lines.Add("{")
    $lines.Add("    private const ulong CanonicalSchemaHash = $(Format-HexLiteral $Schema.canonicalSourceHash);")
    $lines.Add("    private const ulong GeneratedArtifactHash = $(Format-HexLiteral $Schema.generatedArtifactHash);")
    $lines.Add("")
    $lines.Add("    private static readonly CompatAliasMapEntry[] EntryTable = new CompatAliasMapEntry[]")
    $lines.Add("    {")
    foreach ($entry in $entries) {
        $frozenAbi = Format-BoolLiteral $entry.frozenAbi
        $lines.Add("        new(CompatAliasSourceKind.$($entry.sourceKind), ""$($entry.sourceName)"", CompatAliasTargetKind.$($entry.targetKind), ""$($entry.targetName)"", $frozenAbi),")
    }
    $lines.Add("    };")
    $lines.Add("")
    $lines.Add("    public static ReadOnlySpan<CompatAliasMapEntry> Entries => EntryTable;")
    $lines.Add("")
    $lines.Add("    public static CompatAliasSchemaArtifact CanonicalSchema { get; } = new(")
    $lines.Add("        ""$($Schema.schemaName)"",")
    $lines.Add("        SchemaVersion: $($Schema.schemaVersion),")
    $lines.Add("        CanonicalSchemaHash,")
    $lines.Add("        GeneratedArtifactHash,")
    $lines.Add("        EntryTable.Length,")
    $lines.Add("        IsCanonical: true);")
    $lines.Add("")
    $lines.Add("    public bool MatchesCanonicalSchema(CompatAliasSchemaArtifact artifact) =>")
    $lines.Add("        artifact.IsCanonical &&")
    $lines.Add("        artifact.SchemaVersion == CanonicalSchema.SchemaVersion &&")
    $lines.Add("        artifact.SourceHash == CanonicalSchema.SourceHash &&")
    $lines.Add("        artifact.ArtifactHash == CanonicalSchema.ArtifactHash &&")
    $lines.Add("        artifact.EntryCount == EntryTable.Length;")
    $lines.Add("")
    $lines.Add("    public bool ContainsSource(CompatAliasSourceKind sourceKind, string sourceName)")
    $lines.Add("    {")
    $lines.Add("        return TryGetEntry(sourceKind, sourceName, out _);")
    $lines.Add("    }")
    $lines.Add("")
    $lines.Add("    public bool TryGetEntry(")
    $lines.Add("        CompatAliasSourceKind sourceKind,")
    $lines.Add("        string sourceName,")
    $lines.Add("        out CompatAliasMapEntry entry)")
    $lines.Add("    {")
    $lines.Add("        foreach (var candidate in Entries)")
    $lines.Add("        {")
    $lines.Add("            if (candidate.SourceKind == sourceKind")
    $lines.Add("                && string.Equals(candidate.SourceName, sourceName, StringComparison.Ordinal))")
    $lines.Add("            {")
    $lines.Add("                entry = candidate;")
    $lines.Add("                return true;")
    $lines.Add("            }")
    $lines.Add("        }")
    $lines.Add("")
    $lines.Add("        entry = default;")
    $lines.Add("        return false;")
    $lines.Add("    }")
    $lines.Add("")
    $lines.Add("    public bool IsFrozenAbiSource(CompatAliasSourceKind sourceKind, string sourceName)")
    $lines.Add("    {")
    $lines.Add("        return TryGetEntry(sourceKind, sourceName, out var entry) && entry.IsFrozenAbi;")
    $lines.Add("    }")
    $lines.Add("}")

    return [string]::Join("`r`n", $lines) + "`r`n"
}

function New-VmxCapsCapabilityBitSchemaSource {
    param([Parameter(Mandatory = $true)] $Schema)

    $entries = @($Schema.entries)
    if ($entries.Count -le 0) {
        throw "vmxcaps-capability-bit-schema requires concrete entries for build-time regeneration."
    }

    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add("using System;")
    $lines.Add("using System.Collections.Generic;")
    $lines.Add("")
    $lines.Add("namespace YAKSys_Hybrid_CPU.Core;")
    $lines.Add("")
    $lines.Add("public readonly record struct CapabilityBitSchemaEntry(")
    $lines.Add("    string Name,")
    $lines.Add("    ulong CapabilityMask,")
    $lines.Add("    CapabilityGrantScope ProjectionScope,")
    $lines.Add("    string TypedGrantSource,")
    $lines.Add("    CapabilityFrontendProjectionPolicy FrontendProjectionPolicy,")
    $lines.Add("    CapabilityEvidenceVisibility EvidenceVisibility);")
    $lines.Add("")
    $lines.Add("public readonly record struct CapabilityBitSchemaArtifact(")
    $lines.Add("    string SchemaName,")
    $lines.Add("    int SchemaVersion,")
    $lines.Add("    ulong SourceHash,")
    $lines.Add("    ulong ArtifactHash,")
    $lines.Add("    ulong CompatibilityPublicationMask,")
    $lines.Add("    int EntryCount,")
    $lines.Add("    bool IsCanonical);")
    $lines.Add("")
    $lines.Add("public sealed partial class CapabilityDescriptorSetSchema")
    $lines.Add("{")
    $lines.Add("    public CapabilityDescriptorSetSchema()")
    $lines.Add("        : this(compatibilityPublicationMask: 0)")
    $lines.Add("    {")
    $lines.Add("    }")
    $lines.Add("")
    $lines.Add("    public CapabilityDescriptorSetSchema(ulong compatibilityPublicationMask)")
    $lines.Add("    {")
    $lines.Add("        CompatibilityPublicationMask = compatibilityPublicationMask;")
    $lines.Add("    }")
    $lines.Add("")
    $lines.Add("    public const int CurrentVersion = $($Schema.schemaVersion);")
    $lines.Add("")
    $lines.Add("    public static ulong KnownVmxV2CompatibilityMask =>")
    for ($index = 0; $index -lt $entries.Count; $index++) {
        $suffix = if ($index -eq $entries.Count - 1) { ";" } else { " |" }
        $lines.Add("        $($entries[$index].capabilityMask)$suffix")
    }
    $lines.Add("")
    $lines.Add("    private static readonly CapabilityBitSchemaEntry[] VmxCompatibilityBitTable =")
    $lines.Add("    {")
    foreach ($entry in $entries) {
        $lines.Add("        new(""$($entry.name)"", $($entry.capabilityMask), CapabilityGrantScope.$($entry.projectionScope), ""$($entry.typedGrantSource)"", CapabilityFrontendProjectionPolicy.$($entry.frontendProjectionPolicy), CapabilityEvidenceVisibility.$($entry.evidenceVisibility)),")
    }
    $lines.Add("    };")
    $lines.Add("")
    $lines.Add("    public static CapabilityDescriptorSetSchema VmxCompatibility { get; } =")
    $lines.Add("        new(KnownVmxV2CompatibilityMask);")
    $lines.Add("")
    $lines.Add("    public static ReadOnlySpan<CapabilityBitSchemaEntry> VmxCompatibilityBits =>")
    $lines.Add("        VmxCompatibilityBitTable;")
    $lines.Add("")
    $lines.Add("    public static CapabilityBitSchemaArtifact VmxCompatibilityBitSchema { get; } = new(")
    $lines.Add("        ""$($Schema.schemaName)"",")
    $lines.Add("        CurrentVersion,")
    $lines.Add("        SourceHash: $(Format-HexLiteral $Schema.canonicalSourceHash),")
    $lines.Add("        ArtifactHash: $(Format-HexLiteral $Schema.generatedArtifactHash),")
    $lines.Add("        KnownVmxV2CompatibilityMask,")
    $lines.Add("        VmxCompatibilityBitTable.Length,")
    $lines.Add("        IsCanonical: true);")
    $lines.Add("")
    $lines.Add("    public ulong CompatibilityPublicationMask { get; }")
    $lines.Add("")
    $lines.Add("    public ulong FilterCompatibilityCaps(ulong capabilityMask) =>")
    $lines.Add("        capabilityMask & CompatibilityPublicationMask;")
    $lines.Add("")
    $lines.Add("    public ulong GetUnknownCompatibilityCaps(ulong capabilityMask) =>")
    $lines.Add("        capabilityMask & ~CompatibilityPublicationMask;")
    $lines.Add("")
    $lines.Add("    public bool ContainsOnlyKnownCompatibilityCaps(ulong capabilityMask) =>")
    $lines.Add("        GetUnknownCompatibilityCaps(capabilityMask) == 0;")
    $lines.Add("")
    $lines.Add("    public bool MatchesCanonicalVmxCompatibilityBitSchema(")
    $lines.Add("        CapabilityBitSchemaArtifact artifact)")
    $lines.Add("    {")
    $lines.Add("        if (!artifact.IsCanonical ||")
    $lines.Add("            artifact.SchemaVersion != CurrentVersion ||")
    $lines.Add("            artifact.SourceHash == 0 ||")
    $lines.Add("            artifact.ArtifactHash == 0 ||")
    $lines.Add("            artifact.CompatibilityPublicationMask != CompatibilityPublicationMask ||")
    $lines.Add("            artifact.EntryCount != VmxCompatibilityBitTable.Length)")
    $lines.Add("        {")
    $lines.Add("            return false;")
    $lines.Add("        }")
    $lines.Add("")
    $lines.Add("        ulong declaredMask = 0;")
    $lines.Add("        var seenMasks = new HashSet<ulong>();")
    $lines.Add("")
    $lines.Add("        foreach (var entry in VmxCompatibilityBits)")
    $lines.Add("        {")
    $lines.Add("            if (string.IsNullOrWhiteSpace(entry.Name) ||")
    $lines.Add("                string.IsNullOrWhiteSpace(entry.TypedGrantSource) ||")
    $lines.Add("                entry.CapabilityMask == 0 ||")
    $lines.Add("                entry.ProjectionScope != CapabilityGrantScope.CompatibilityProjection ||")
    $lines.Add("                entry.FrontendProjectionPolicy != CapabilityFrontendProjectionPolicy.ProjectIfCompatible ||")
    $lines.Add("                entry.EvidenceVisibility != CapabilityEvidenceVisibility.GuestVisibleProjection ||")
    $lines.Add("                !seenMasks.Add(entry.CapabilityMask))")
    $lines.Add("            {")
    $lines.Add("                return false;")
    $lines.Add("            }")
    $lines.Add("")
    $lines.Add("            declaredMask |= entry.CapabilityMask;")
    $lines.Add("        }")
    $lines.Add("")
    $lines.Add("        return declaredMask == artifact.CompatibilityPublicationMask;")
    $lines.Add("    }")
    $lines.Add("}")

    return [string]::Join("`r`n", $lines) + "`r`n"
}

function New-CompatSpecArtifactSetSource {
    param([Parameter(Mandatory = $true)] $Schema)

    $entries = @($Schema.entries)
    if ($entries.Count -le 0) {
        throw "compat-spec-artifact-schema requires concrete entries for build-time regeneration."
    }

    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add("using System;")
    $lines.Add("")
    $lines.Add("namespace YAKSys_Hybrid_CPU.Core;")
    $lines.Add("")
    $lines.Add("public enum CompatSpecArtifactKind : byte")
    $lines.Add("{")
    $lines.Add("    FrozenOpcodeAliases = 0,")
    $lines.Add("    FrozenCsrAliases = 1,")
    $lines.Add("    VmcsFieldAliases = 2,")
    $lines.Add("    CapabilityProjection = 3,")
    $lines.Add("    CompletionProjection = 4,")
    $lines.Add("    CompatAliasMap = 5,")
    $lines.Add("}")
    $lines.Add("")
    $lines.Add("public enum CompatSpecArtifactLineageKind : byte")
    $lines.Add("{")
    $lines.Add("    FrozenAbiVocabulary = 0,")
    $lines.Add("    ExecutableGeneratedSource = 1,")
    $lines.Add("    ProjectionContractOnly = 2,")
    $lines.Add("}")
    $lines.Add("")
    $lines.Add("public readonly record struct CompatSpecArtifact(")
    $lines.Add("    CompatSpecArtifactKind Kind,")
    $lines.Add("    string Name,")
    $lines.Add("    string LineageSource,")
    $lines.Add("    bool IsGenerated,")
    $lines.Add("    bool IsFrozenAbi,")
    $lines.Add("    CompatSpecArtifactLineageKind LineageKind);")
    $lines.Add("")
    $lines.Add("public sealed partial class CompatSpecArtifactSet")
    $lines.Add("{")
    $lines.Add("    private static readonly CompatSpecArtifact[] ArtifactTable = new CompatSpecArtifact[]")
    $lines.Add("    {")
    foreach ($entry in $entries) {
        $generated = Format-BoolLiteral $entry.generated
        $frozenAbi = Format-BoolLiteral $entry.frozenAbi
        $lines.Add("        new(CompatSpecArtifactKind.$($entry.kind), ""$($entry.name)"", ""$($entry.lineageSource)"", $generated, $frozenAbi, CompatSpecArtifactLineageKind.$($entry.lineageKind)),")
    }
    $lines.Add("    };")
    $lines.Add("")
    $lines.Add("    public static ReadOnlySpan<CompatSpecArtifact> Artifacts => ArtifactTable;")
    $lines.Add("")
    $lines.Add("    public bool Contains(CompatSpecArtifactKind kind)")
    $lines.Add("    {")
    $lines.Add("        foreach (var artifact in Artifacts)")
    $lines.Add("        {")
    $lines.Add("            if (artifact.Kind == kind)")
    $lines.Add("            {")
    $lines.Add("                return true;")
    $lines.Add("            }")
    $lines.Add("        }")
    $lines.Add("")
    $lines.Add("        return false;")
    $lines.Add("    }")
    $lines.Add("")
    $lines.Add("    public bool TryGetArtifact(CompatSpecArtifactKind kind, out CompatSpecArtifact artifact)")
    $lines.Add("    {")
    $lines.Add("        foreach (var candidate in Artifacts)")
    $lines.Add("        {")
    $lines.Add("            if (candidate.Kind == kind)")
    $lines.Add("            {")
    $lines.Add("                artifact = candidate;")
    $lines.Add("                return true;")
    $lines.Add("            }")
    $lines.Add("        }")
    $lines.Add("")
    $lines.Add("        artifact = default;")
    $lines.Add("        return false;")
    $lines.Add("    }")
    $lines.Add("")
    $lines.Add("    public bool HasExecutableLineage(CompatSpecArtifactKind kind)")
    $lines.Add("    {")
    $lines.Add("        return TryGetArtifact(kind, out var artifact) &&")
    $lines.Add("            artifact.LineageKind == CompatSpecArtifactLineageKind.ExecutableGeneratedSource;")
    $lines.Add("    }")
    $lines.Add("")
    $lines.Add("    public bool RequiresGeneratedParity(CompatSpecArtifactKind kind)")
    $lines.Add("    {")
    $lines.Add("        return TryGetArtifact(kind, out var artifact) &&")
    $lines.Add("            artifact.IsGenerated &&")
    $lines.Add("            artifact.LineageKind == CompatSpecArtifactLineageKind.ExecutableGeneratedSource;")
    $lines.Add("    }")
    $lines.Add("")
    $lines.Add("    public bool RequiresAbiFreeze(CompatSpecArtifactKind kind)")
    $lines.Add("    {")
    $lines.Add("        return TryGetArtifact(kind, out var artifact) && artifact.IsFrozenAbi;")
    $lines.Add("    }")
    $lines.Add("}")

    return [string]::Join("`r`n", $lines) + "`r`n"
}

$ProjectRoot = [System.IO.Path]::GetFullPath($ProjectRoot)
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Resolve-ProjectPath "obj/GeneratedProjectionLineage"
} else {
    $OutputRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($ProjectRoot, $OutputRoot))
}

$vmcsFieldSchema = Read-JsonArtifact "docs/VMXRefactoring/schemas/vmcs-field-projection-schema.v1.json"
Assert-RequiredSchemaSwitches $vmcsFieldSchema "vmcs-field-projection-schema" "VmcsFieldProjectionGenerator"
$vmcsGeneratedPath = Assert-GeneratedSourceMatches `
    "CloseToRTL/Core/Virtualization/Compatibility/Generated/VmcsProjection/VmcsFieldProjectionSchema.cs" `
    "VmcsFieldProjectionSchema.expected.cs" `
    (New-VmcsFieldProjectionSchemaSource $vmcsFieldSchema)

$compatAliasSchema = Read-JsonArtifact "docs/VMXRefactoring/schemas/compat-alias-schema.v1.json"
Assert-RequiredSchemaSwitches $compatAliasSchema "compat-alias-schema" "CompatAliasMapGenerator"
$compatGeneratedPath = Assert-GeneratedSourceMatches `
    "CloseToRTL/Core/Virtualization/Compatibility/Generated/AliasMaps/CompatAliasMap.cs" `
    "CompatAliasMap.expected.cs" `
    (New-CompatAliasMapSource $compatAliasSchema)

$vmxCapsSchema = Read-JsonArtifact "docs/VMXRefactoring/schemas/vmxcaps-capability-bit-schema.v1.json"
Assert-RequiredSchemaSwitches $vmxCapsSchema "vmxcaps-capability-bit-schema" "VmxCapsCapabilityBitGenerator"
$vmxCapsGeneratedPath = Assert-GeneratedSourceMatches `
    "CloseToRTL/Core/Virtualization/Compatibility/Generated/CapabilityProjection/CapabilityDescriptorSetSchema.cs" `
    "CapabilityDescriptorSetSchema.expected.cs" `
    (New-VmxCapsCapabilityBitSchemaSource $vmxCapsSchema)

$compatSpecSchema = Read-JsonArtifact "docs/VMXRefactoring/schemas/compat-spec-artifact-schema.v1.json"
Assert-RequiredSchemaSwitches $compatSpecSchema "compat-spec-artifact-schema" "CompatSpecArtifactSetGenerator"
$compatSpecGeneratedPath = Assert-GeneratedSourceMatches `
    "CloseToRTL/Core/Virtualization/Compatibility/Generated/SpecArtifacts/CompatSpecArtifactSet.cs" `
    "CompatSpecArtifactSet.expected.cs" `
    (New-CompatSpecArtifactSetSource $compatSpecSchema)

$manifest = @(
    "VMX projection lineage verified.",
    "ProjectRoot=$ProjectRoot",
    "VmcsFieldProjectionSchema=$vmcsGeneratedPath",
    "CompatAliasMap=$compatGeneratedPath",
    "CapabilityDescriptorSetSchema=$vmxCapsGeneratedPath",
    "CompatSpecArtifactSet=$compatSpecGeneratedPath"
) -join "`r`n"
Write-Utf8NoBom ([System.IO.Path]::Combine($OutputRoot, "projection-lineage.manifest.txt")) ($manifest + "`r`n")
Write-Host "VMX projection lineage verified: VMCS field schema, compat alias map, VmxCaps bit schema, and compat spec artifact manifest match regenerated output."
