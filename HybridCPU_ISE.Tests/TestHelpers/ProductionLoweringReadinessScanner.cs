using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace HybridCPU_ISE.Tests.TestHelpers;

internal sealed record ProductionLoweringReadinessViolation(
    string Rule,
    string RelativePath,
    int LineNumber,
    string SourceLine)
{
    public override string ToString() =>
        $"{Rule}: {RelativePath}:{LineNumber}: {SourceLine.Trim()}";
}

/// <summary>
/// PR-1 source guard for compiler-to-ISE authority boundaries.
///
/// This is deliberately a small lexical scanner, not a compiler or a Roslyn
/// analyzer. It catches the source shapes that would silently turn an existing
/// compatibility/helper/parser surface into production authority. Existing
/// compatibility adapters are accepted only when their typed evidence-only
/// boundary is visible in the same source context.
/// </summary>
internal static class ProductionLoweringReadinessScanner
{
    internal const string LegacyAuthorityPromotionRule = "legacy-authority-promotion";
    internal const string ParserHelperPromotionRule = "parser-helper-promotion";
    internal const string CarrierLifecyclePromotionRule = "carrier-lifecycle-promotion";
    internal const string RuntimeGuardAuthorityRule = "runtime-guard-authority";
    internal const string VmxEmissionRule = "vmx-emission-or-vmcs-ownership";
    internal const string SecureComputeEmissionRule = "securecompute-backend-emission";
    internal const string HiddenFallbackRule = "hidden-cross-contour-fallback";
    internal const string DescriptorlessL7Rule = "descriptorless-l7-submit";
    internal const string ContourMixRule = "dsc-l7-contour-mixing";
    internal const string RawPublicBoolRule = "raw-public-bool-boundary";
    internal const string ForbiddenAuthorityTokenRule = "forbidden-authority-token";

    private static readonly (string Token, string Rule)[] ForbiddenAuthorityTokens =
    [
        ("ProductionAllowedByExplicitCompilerGate", ForbiddenAuthorityTokenRule),
        ("RuntimeLegalityDecision", ForbiddenAuthorityTokenRule),
        ("ExecutionReady", ForbiddenAuthorityTokenRule),
        ("RuntimeLegalDecision", ForbiddenAuthorityTokenRule),
        ("CompilerOwnedVmcs", VmxEmissionRule),
        ("VmcsOwner", VmxEmissionRule),
        ("SecureComputeBackend", SecureComputeEmissionRule),
        ("SecureBackendExecution", SecureComputeEmissionRule),
        ("ExecutableDescriptor", ParserHelperPromotionRule),
        ("CanExecuteDescriptor", ParserHelperPromotionRule),
        ("DescriptorAuthority", ParserHelperPromotionRule),
        ("HelperSuccess", ParserHelperPromotionRule),
        ("ParserSuccess", ParserHelperPromotionRule),
        ("PublishedArchitecturalState", CarrierLifecyclePromotionRule)
    ];

    private static readonly string[] LegacySuccessTokens =
    [
        "IsAllowed",
        "IsLegal",
        "LegalSlots",
        "TryRecoverFromInstruction",
        "HelperAbiRecovered",
        "Success",
        "Valid",
        "Accepted"
    ];

    private static readonly string[] ProductionAuthorityTokens =
    [
        "ProductionAllowed",
        "ProductionExecutable",
        "ProductionLowering",
        "ExecutionReady",
        "RuntimeLegal",
        "CanExecute",
        "CommitReady",
        "RetireReady",
        "PublishedArchitecturalState"
    ];

    private static readonly string[] LifecyclePromotionTokens =
    [
        "CarrierExecution",
        "CarrierPublication",
        "CarrierCommit",
        "CarrierRetire",
        "ExecuteCarrier",
        "PublishCarrier",
        "CommitCarrier",
        "RetireCarrier",
        "CarrierAsExecution",
        "CarrierAsPublication"
    ];

    private static readonly Regex PublicBoolDeclaration = new(
        @"\bpublic\s+(?:(?:static|readonly)\s+)*bool\s+(?<name>[A-Za-z_]\w*)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    internal static IReadOnlyList<ProductionLoweringReadinessViolation> ScanCompilerSources()
    {
        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        var violations = new List<ProductionLoweringReadinessViolation>();

        foreach (string filePath in CompilerSourceScanner.EnumerateCompilerSourceFiles())
        {
            string relativePath = NormalizePath(Path.GetRelativePath(repoRoot, filePath));
            violations.AddRange(ScanText(relativePath, File.ReadAllText(filePath)));
        }

        return violations
            .OrderBy(static violation => violation.RelativePath, StringComparer.Ordinal)
            .ThenBy(static violation => violation.LineNumber)
            .ThenBy(static violation => violation.Rule, StringComparer.Ordinal)
            .ToArray();
    }

    internal static IReadOnlyList<ProductionLoweringReadinessViolation> ScanText(
        string relativePath,
        string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentNullException.ThrowIfNull(source);

        string normalizedPath = NormalizePath(relativePath);
        string[] rawLines = source.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        string[] codeLines = MaskCommentsAndStringLiterals(source)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n');
        var violations = new List<ProductionLoweringReadinessViolation>();
        bool isLegacyVmxSource = normalizedPath.Contains(
            "HybridCPU_Compiler/Legacy/VMX-2/",
            StringComparison.OrdinalIgnoreCase);
        bool isProductionBoundarySource = IsProductionBoundarySource(normalizedPath, codeLines);

        for (int lineIndex = 0; lineIndex < codeLines.Length; lineIndex++)
        {
            string codeLine = codeLines[lineIndex];
            if (string.IsNullOrWhiteSpace(codeLine))
            {
                continue;
            }

            foreach ((string token, string rule) in ForbiddenAuthorityTokens)
            {
                if (codeLine.Contains(token, StringComparison.Ordinal))
                {
                    AddViolation(violations, rule, normalizedPath, lineIndex, rawLines[lineIndex]);
                }
            }

            if (codeLine.Contains("InstructionsEnum.VMX", StringComparison.Ordinal) &&
                !isLegacyVmxSource)
            {
                AddViolation(violations, VmxEmissionRule, normalizedPath, lineIndex, rawLines[lineIndex]);
            }

            if (codeLine.Contains("CanAttachToExecutableCompilerInstruction: true", StringComparison.Ordinal) ||
                codeLine.Contains("CompilerEmittable: true", StringComparison.Ordinal))
            {
                AddViolation(violations, VmxEmissionRule, normalizedPath, lineIndex, rawLines[lineIndex]);
            }

            if (IsSecureComputeEmissionShape(codeLine))
            {
                AddViolation(violations, SecureComputeEmissionRule, normalizedPath, lineIndex, rawLines[lineIndex]);
            }

            if (IsHiddenFallbackShape(codeLine, normalizedPath))
            {
                AddViolation(violations, HiddenFallbackRule, normalizedPath, lineIndex, rawLines[lineIndex]);
            }

            if (IsDescriptorlessL7EmissionShape(codeLine) &&
                !HasNearbyDescriptorBackedL7Guard(codeLines, normalizedPath, lineIndex))
            {
                AddViolation(violations, DescriptorlessL7Rule, normalizedPath, lineIndex, rawLines[lineIndex]);
            }

            if (ContainsBothContourDescriptors(codeLine))
            {
                AddViolation(violations, ContourMixRule, normalizedPath, lineIndex, rawLines[lineIndex]);
            }

            bool hasLegacySuccess = ContainsAny(codeLine, LegacySuccessTokens);
            bool hasProductionAuthority = ContainsAny(codeLine, ProductionAuthorityTokens);
            if (hasLegacySuccess && hasProductionAuthority &&
                !IsExplicitEvidenceOnlyAdapter(codeLines, normalizedPath, lineIndex))
            {
                AddViolation(violations, LegacyAuthorityPromotionRule, normalizedPath, lineIndex, rawLines[lineIndex]);
            }

            if (hasLegacySuccess &&
                IsAssignmentOrReturn(codeLine) &&
                isProductionBoundarySource &&
                !IsExplicitEvidenceOnlyAdapter(codeLines, normalizedPath, lineIndex))
            {
                AddViolation(violations, LegacyAuthorityPromotionRule, normalizedPath, lineIndex, rawLines[lineIndex]);
            }

            bool hasParserOrHelperSuccess =
                codeLine.Contains("IsDescriptorAbiAccepted", StringComparison.Ordinal) ||
                codeLine.Contains("IsDescriptorValid", StringComparison.Ordinal) ||
                codeLine.Contains("HelperAbiRecovered", StringComparison.Ordinal) ||
                codeLine.Contains("CompilerPositiveEmissionResult", StringComparison.Ordinal);
            if (hasParserOrHelperSuccess &&
                (hasProductionAuthority ||
                 (isProductionBoundarySource && IsAssignmentOrReturn(codeLine))) &&
                !IsExplicitEvidenceOnlyAdapter(codeLines, normalizedPath, lineIndex))
            {
                AddViolation(violations, ParserHelperPromotionRule, normalizedPath, lineIndex, rawLines[lineIndex]);
            }

            if (ContainsAny(codeLine, LifecyclePromotionTokens) ||
                IsCarrierAssignedToLifecycle(codeLine))
            {
                AddViolation(violations, CarrierLifecyclePromotionRule, normalizedPath, lineIndex, rawLines[lineIndex]);
            }

            if ((codeLine.Contains("guardObservation", StringComparison.OrdinalIgnoreCase) ||
                 codeLine.Contains("ObservedGuardAllowsProgress", StringComparison.Ordinal)) &&
                ContainsAny(codeLine, ["Production", "Execution", "Publication", "Commit", "Retire", "Authority"]) &&
                !codeLine.Contains("RuntimeAuthorityDependency", StringComparison.Ordinal))
            {
                AddViolation(violations, RuntimeGuardAuthorityRule, normalizedPath, lineIndex, rawLines[lineIndex]);
            }

            Match publicBool = PublicBoolDeclaration.Match(codeLine);
            if (publicBool.Success &&
                IsRawPublicProductionBoundary(normalizedPath, publicBool.Groups["name"].Value, codeLines) &&
                !IsExplicitLegacyBoolException(normalizedPath, publicBool.Groups["name"].Value, codeLines, lineIndex) &&
                !IsExplicitEvidenceOnlyAdapter(codeLines, normalizedPath, lineIndex))
            {
                AddViolation(violations, RawPublicBoolRule, normalizedPath, lineIndex, rawLines[lineIndex]);
            }
        }

        ScanContourDescriptorWindows(normalizedPath, rawLines, codeLines, violations);
        return violations
            .Distinct()
            .OrderBy(static violation => violation.LineNumber)
            .ThenBy(static violation => violation.Rule, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsProductionBoundarySource(string normalizedPath, IReadOnlyList<string> codeLines)
    {
        if (normalizedPath.Contains("/Core/IR/Lowering/", StringComparison.OrdinalIgnoreCase) &&
            (normalizedPath.Contains("Production", StringComparison.OrdinalIgnoreCase) ||
             normalizedPath.Contains("Provider", StringComparison.OrdinalIgnoreCase) ||
             normalizedPath.Contains("Backend", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        string source = string.Join('\n', codeLines);
        return source.Contains("class Production", StringComparison.Ordinal) ||
               source.Contains("record Production", StringComparison.Ordinal) ||
               source.Contains("IProduction", StringComparison.Ordinal);
    }

    private static bool IsRawPublicProductionBoundary(
        string normalizedPath,
        string memberName,
        IReadOnlyList<string> codeLines)
    {
        bool isAuthorityNamedMember =
            memberName.Contains("Production", StringComparison.OrdinalIgnoreCase) ||
            memberName is "IsAllowed" or "IsLegal" or "CanExecute" or "ExecutionReady" or
            "RuntimeLegal" or "ProductionAllowed" or "ProductionExecutable" or
            "CanSelectForProductionLowering";
        if (isAuthorityNamedMember &&
            (IsProductionBoundarySource(normalizedPath, codeLines) ||
             memberName.Contains("Production", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (normalizedPath.EndsWith(
                "/Core/IR/Model/CompilerBackendLoweringContract.cs",
                StringComparison.OrdinalIgnoreCase))
        {
            return memberName is "IsAllowed" or "CanSelectForProductionLowering";
        }

        return IsProductionBoundarySource(normalizedPath, codeLines);
    }

    private static bool IsExplicitLegacyBoolException(
        string normalizedPath,
        string memberName,
        IReadOnlyList<string> codeLines,
        int lineIndex)
    {
        bool isObsolete = codeLines
            .Skip(Math.Max(0, lineIndex - 8))
            .Take(9)
            .Any(static line => line.Contains("[Obsolete", StringComparison.Ordinal));

        if (!isObsolete)
        {
            return false;
        }

        if (normalizedPath.EndsWith(
                "/Core/IR/Model/CompilerBackendLoweringContract.cs",
                StringComparison.OrdinalIgnoreCase) &&
            memberName is "IsAllowed" or "CanSelectForProductionLowering")
        {
            return string.Join('\n', codeLines).Contains("IsAllowedObservation", StringComparison.Ordinal);
        }

        if (normalizedPath.EndsWith(
                "/API/Frontend/Directives/HybridCpuCompilerDirectives.cs",
                StringComparison.OrdinalIgnoreCase) &&
            memberName == "Success")
        {
            return string.Join('\n', codeLines).Contains("IsDirectiveParsed", StringComparison.Ordinal) &&
                   string.Join('\n', codeLines).Contains("parse-only", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static bool IsExplicitEvidenceOnlyAdapter(
        IReadOnlyList<string> codeLines,
        string normalizedPath,
        int lineIndex)
    {
        string context = string.Join(
            '\n',
            codeLines
                .Skip(Math.Max(0, lineIndex - 24))
                .Take(49));

        bool hasAdapterShape =
            context.Contains("FromLegacy", StringComparison.Ordinal) ||
            context.Contains("LegacyApiTranslation", StringComparison.Ordinal) ||
            context.Contains("StructuralOnly", StringComparison.Ordinal);
        bool hasEvidenceOnlyMetadata =
            context.Contains("EvidenceOnly", StringComparison.Ordinal) ||
            context.Contains("HelperAbiOnly", StringComparison.Ordinal) ||
            context.Contains("ParserOnly", StringComparison.Ordinal) ||
            context.Contains("RuntimeLegalityARequired", StringComparison.Ordinal) ||
            context.Contains("RuntimeLegalityBRequired", StringComparison.Ordinal) ||
            context.Contains("StrengthensAuthority", StringComparison.Ordinal) ||
            context.Contains("RequireRuntimeHandoffAuthority", StringComparison.Ordinal);

        if (hasAdapterShape && hasEvidenceOnlyMetadata)
        {
            return true;
        }

        return (normalizedPath.EndsWith(
                    "/Core/IR/Construction/CompilerMatrixTileEmissionLowerer.cs",
                    StringComparison.OrdinalIgnoreCase) ||
                normalizedPath.EndsWith(
                    "/Core/IR/Construction/CompilerVectorTransferEmissionLowerer.cs",
                    StringComparison.OrdinalIgnoreCase)) &&
               context.Contains("CompilerLoweringDecision", StringComparison.Ordinal) &&
               context.Contains("CarrierIsNotPublication", StringComparison.Ordinal);
    }

    private static bool IsSecureComputeEmissionShape(string codeLine)
    {
        if (!codeLine.Contains("SecureCompute", StringComparison.Ordinal) ||
            codeLine.Contains("Forbidden", StringComparison.Ordinal) ||
            codeLine.Contains("NoEmission", StringComparison.Ordinal) ||
            codeLine.Contains("PolicyAdmission", StringComparison.Ordinal))
        {
            return false;
        }

        return codeLine.Contains("CompilerPositiveEmissionResult", StringComparison.Ordinal) ||
               codeLine.Contains("Carrier", StringComparison.Ordinal) ||
               codeLine.Contains("new VLIW_Instruction", StringComparison.Ordinal) ||
               codeLine.Contains("CompileInstruction", StringComparison.Ordinal) ||
               codeLine.Contains("Backend", StringComparison.Ordinal) ||
               codeLine.Contains("Emit", StringComparison.Ordinal);
    }

    private static bool IsHiddenFallbackShape(string codeLine, string normalizedPath)
    {
        if (normalizedPath.Contains("PositiveEmissionAbiContract", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!codeLine.Contains("Fallback", StringComparison.OrdinalIgnoreCase) ||
            !ContainsAny(codeLine, ["Scalar", "Vector", "Stream", "Dsc", "DSC", "L7", "MatrixTile"]))
        {
            return false;
        }

        return !codeLine.Contains("NoFallback", StringComparison.OrdinalIgnoreCase) &&
               !codeLine.Contains("Forbidden", StringComparison.OrdinalIgnoreCase) &&
               !codeLine.Contains("Disallow", StringComparison.OrdinalIgnoreCase) &&
               !codeLine.Contains("Reject", StringComparison.OrdinalIgnoreCase) &&
               !codeLine.Contains("false", StringComparison.OrdinalIgnoreCase) &&
               !codeLine.Contains("NoLane", StringComparison.OrdinalIgnoreCase) &&
               !codeLine.Contains("NoDescriptor", StringComparison.OrdinalIgnoreCase) &&
               !codeLine.Contains("NoDma", StringComparison.OrdinalIgnoreCase) &&
               !codeLine.Contains("NoDsc", StringComparison.OrdinalIgnoreCase) &&
               !codeLine.Contains("NoScalar", StringComparison.OrdinalIgnoreCase) &&
               !codeLine.Contains("NoVector", StringComparison.OrdinalIgnoreCase) &&
               !codeLine.Contains("NoGeneric", StringComparison.OrdinalIgnoreCase) &&
               !codeLine.Contains("NoExternal", StringComparison.OrdinalIgnoreCase) &&
               !codeLine.Contains("NoBase", StringComparison.OrdinalIgnoreCase) &&
               !codeLine.Contains("NoMatrix", StringComparison.OrdinalIgnoreCase) &&
               !codeLine.Contains("PositiveEmissionAbiContract", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDescriptorlessL7EmissionShape(string codeLine) =>
        codeLine.Contains("InstructionsEnum.ACCEL_SUBMIT", StringComparison.Ordinal) &&
        !codeLine.Contains("!=", StringComparison.Ordinal) &&
        !codeLine.Contains("==", StringComparison.Ordinal) &&
        (codeLine.Contains("OpCode", StringComparison.Ordinal) ||
         codeLine.Contains("opCode", StringComparison.Ordinal) ||
         codeLine.Contains("opcode", StringComparison.Ordinal));

    private static bool HasNearbyDescriptorBackedL7Guard(
        IReadOnlyList<string> codeLines,
        string normalizedPath,
        int lineIndex)
    {
        if (!normalizedPath.EndsWith(
                "/API/Threading/HybridCpuThreadCompilerContext.cs",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        int start = Math.Max(0, lineIndex - 60);
        int count = Math.Min(codeLines.Count - start, 121);
        string context = string.Join('\n', codeLines.Skip(start).Take(count));
        return context.Contains("EnsureAcceleratorCommandDescriptorAdmissible", StringComparison.Ordinal) &&
               context.Contains("AcceleratorCommandDescriptor", StringComparison.Ordinal) &&
               context.Contains("DescriptorSideband", StringComparison.Ordinal);
    }

    private static bool ContainsBothContourDescriptors(string codeLine) =>
        codeLine.Contains("DmaStreamComputeDescriptor", StringComparison.Ordinal) &&
        codeLine.Contains("AcceleratorCommandDescriptor", StringComparison.Ordinal);

    private static void ScanContourDescriptorWindows(
        string normalizedPath,
        IReadOnlyList<string> rawLines,
        IReadOnlyList<string> codeLines,
        ICollection<ProductionLoweringReadinessViolation> violations)
    {
        for (int lineIndex = 0; lineIndex < codeLines.Count; lineIndex++)
        {
            if (!codeLines[lineIndex].Contains("DmaStreamComputeDescriptor", StringComparison.Ordinal))
            {
                continue;
            }

            int end = Math.Min(codeLines.Count, lineIndex + 13);
            bool hasL7Descriptor = codeLines
                .Skip(lineIndex)
                .Take(end - lineIndex)
                .Any(static line => line.Contains("AcceleratorCommandDescriptor", StringComparison.Ordinal));
            bool isSingleSlotOrContourContext = codeLines
                .Skip(lineIndex)
                .Take(end - lineIndex)
                .Any(static line =>
                    line.Contains("new InstructionSlotMetadata", StringComparison.Ordinal) ||
                    line.Contains("new VliwBundleAnnotations", StringComparison.Ordinal));

            if (hasL7Descriptor && isSingleSlotOrContourContext)
            {
                AddViolation(
                    violations,
                    ContourMixRule,
                    normalizedPath,
                    lineIndex,
                    rawLines[lineIndex]);
            }
        }
    }

    private static bool IsCarrierAssignedToLifecycle(string codeLine)
    {
        if (!codeLine.Contains("carrier", StringComparison.OrdinalIgnoreCase) ||
            !ContainsAny(codeLine, ["execution", "publication", "commit", "retire"]))
        {
            return false;
        }

        return !codeLine.Contains("CarrierBytesOnly", StringComparison.Ordinal) &&
               !codeLine.Contains("CarrierIsNotPublication", StringComparison.Ordinal) &&
               !codeLine.Contains("CarrierCandidate", StringComparison.Ordinal);
    }

    private static bool IsAssignmentOrReturn(string codeLine) =>
        codeLine.Contains("return", StringComparison.OrdinalIgnoreCase) ||
        codeLine.Contains("=", StringComparison.Ordinal) ||
        codeLine.Contains("=>", StringComparison.Ordinal);

    private static bool ContainsAny(string value, IReadOnlyList<string> tokens) =>
        tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));

    private static void AddViolation(
        ICollection<ProductionLoweringReadinessViolation> violations,
        string rule,
        string normalizedPath,
        int lineIndex,
        string rawLine) =>
        violations.Add(new ProductionLoweringReadinessViolation(
            rule,
            normalizedPath,
            lineIndex + 1,
            rawLine));

    private static string NormalizePath(string path) =>
        path.Replace('\\', '/');

    private static string MaskCommentsAndStringLiterals(string source)
    {
        var result = new StringBuilder(source.Length);
        bool inLineComment = false;
        bool inBlockComment = false;
        bool inQuotedLiteral = false;
        bool verbatimLiteral = false;
        char quote = '\0';

        for (int index = 0; index < source.Length; index++)
        {
            char current = source[index];
            char next = index + 1 < source.Length ? source[index + 1] : '\0';

            if (inLineComment)
            {
                if (current == '\n')
                {
                    inLineComment = false;
                    result.Append(current);
                }
                else
                {
                    result.Append(' ');
                }

                continue;
            }

            if (inBlockComment)
            {
                if (current == '*' && next == '/')
                {
                    result.Append("  ");
                    index++;
                    inBlockComment = false;
                }
                else
                {
                    result.Append(current == '\n' ? '\n' : ' ');
                }

                continue;
            }

            if (inQuotedLiteral)
            {
                if (verbatimLiteral && current == '"' && next == '"')
                {
                    result.Append("  ");
                    index++;
                    continue;
                }

                if (!verbatimLiteral && current == '\\' && next != '\0')
                {
                    result.Append("  ");
                    index++;
                    continue;
                }

                if (current == quote)
                {
                    inQuotedLiteral = false;
                    verbatimLiteral = false;
                }

                result.Append(current == '\n' ? '\n' : ' ');
                continue;
            }

            if (current == '/' && next == '/')
            {
                result.Append("  ");
                index++;
                inLineComment = true;
                continue;
            }

            if (current == '/' && next == '*')
            {
                result.Append("  ");
                index++;
                inBlockComment = true;
                continue;
            }

            if (current is '"' or '\'')
            {
                quote = current;
                verbatimLiteral = current == '"' && index > 0 && source[index - 1] == '@';
                inQuotedLiteral = true;
                result.Append(' ');
                continue;
            }

            result.Append(current);
        }

        return result.ToString();
    }
}
