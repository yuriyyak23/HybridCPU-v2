using System;

namespace YAKSys_Hybrid_CPU.Core;

public enum CoreVmxAuthorityBoundaryViolation : byte
{
    None = 0,
    DirectShadowVmcsAccess = 1,
    DirectVmxIommuHook = 2,
    DirectVmcsFieldAccess = 3,
    DirectVmcsTranslationSource = 4,
    DirectVmxCapsCsrSource = 5,
}

public readonly record struct CoreVmxAuthorityMarker(
    string Marker,
    CoreVmxAuthorityBoundaryViolation Violation,
    string AllowedRelativePaths);

public sealed class CoreVmxAuthorityBoundaryContract
{
    private const string DeniedIommuAliasProjectionPath =
        "Core/VMX/Compatibility/Adapters/IO/IommuVmxCompatibilityAliases.cs";

    private static readonly CoreVmxAuthorityMarker[] Markers =
    {
        new(
            ".ShadowVmcs.",
            CoreVmxAuthorityBoundaryViolation.DirectShadowVmcsAccess,
            string.Empty),
        new(
            "IOMMU.",
            CoreVmxAuthorityBoundaryViolation.DirectVmxIommuHook,
            string.Empty),
        new(
            "BindVmx",
            CoreVmxAuthorityBoundaryViolation.DirectVmxIommuHook,
            DeniedIommuAliasProjectionPath),
        new(
            "InvalidateVmx",
            CoreVmxAuthorityBoundaryViolation.DirectVmxIommuHook,
            DeniedIommuAliasProjectionPath),
        new(
            "ApplyVmx",
            CoreVmxAuthorityBoundaryViolation.DirectVmxIommuHook,
            DeniedIommuAliasProjectionPath),
        new(
            "UnbindVmx",
            CoreVmxAuthorityBoundaryViolation.DirectVmxIommuHook,
            DeniedIommuAliasProjectionPath),
        new(
            "ReadFieldValue(",
            CoreVmxAuthorityBoundaryViolation.DirectVmcsFieldAccess,
            string.Empty),
        new(
            "WriteFieldValue(",
            CoreVmxAuthorityBoundaryViolation.DirectVmcsFieldAccess,
            string.Empty),
        new(
            "MemoryTranslationControl.FromVmcs",
            CoreVmxAuthorityBoundaryViolation.DirectVmcsTranslationSource,
            string.Empty),
        new(
            "CsrAddresses.VmxCaps",
            CoreVmxAuthorityBoundaryViolation.DirectVmxCapsCsrSource,
            "Core/VMX/Compatibility/Generated/CsrProjection/VmxCapsProjection.cs;Core/VMX/Compatibility/FrozenAbi/CsrAliases/VmxCsrAliasSet.cs"),
    };

    public ReadOnlySpan<CoreVmxAuthorityMarker> ForbiddenMarkers => Markers;

    public CoreVmxAuthorityBoundaryViolation EvaluateLine(
        string relativePath,
        string line)
    {
        string normalizedPath = NormalizePath(relativePath);

        foreach (var marker in ForbiddenMarkers)
        {
            if (!line.Contains(marker.Marker, StringComparison.Ordinal))
            {
                continue;
            }

            if (IsAllowedPath(normalizedPath, marker.AllowedRelativePaths))
            {
                continue;
            }

            return marker.Violation;
        }

        return CoreVmxAuthorityBoundaryViolation.None;
    }

    public bool IsAllowedLine(string relativePath, string line) =>
        EvaluateLine(relativePath, line) == CoreVmxAuthorityBoundaryViolation.None;

    private static string NormalizePath(string path) =>
        path.Replace('\\', '/');

    private static bool IsAllowedPath(string normalizedPath, string allowedRelativePaths)
    {
        if (string.IsNullOrEmpty(allowedRelativePaths))
        {
            return false;
        }

        foreach (string allowedPath in allowedRelativePaths.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            if (string.Equals(normalizedPath, allowedPath, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
