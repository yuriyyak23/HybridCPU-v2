using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxPhase53NeutralCpuTranslationFaultProducerE0Tests
{
    [Fact]
    public void MachineCurrent_PreservesBlockedHistoryAndRecordsAuthorizedPhase54WithoutOpeningVmRead()
    {
        string root = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        string plan = Path.Combine(root, "HybridCPU_ISE", "docs", "ref2", "VirtualizationActivationPlan");
        using JsonDocument status = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            plan, "VirtualizationActivationStatusV1.json")));
        JsonElement phase53 = status.RootElement.GetProperty(
            "Phase53NeutralCpuTranslationFaultProducerE0");

        Assert.Equal(
            "BlockedNoCanonicalCpuInstructionTranslationSourceOrTypedFaultCaller",
            phase53.GetProperty("State").GetString());
        Assert.False(phase53.GetProperty("ProducerImplemented").GetBoolean());
        Assert.Equal("Absent", phase53.GetProperty("ProducerRegistration").GetString());
        Assert.Equal("NotOpened", phase53.GetProperty("VmReadD2").GetString());
        JsonElement phase54 = status.RootElement.GetProperty(
            "Phase54CanonicalNeutralCpuInstructionTranslationContourAndProducerReaudit");
        Assert.Equal("ClosedGreenSubjectAndLaterNonSelfReferentialEvidence",
            phase54.GetProperty("State").GetString());
        Assert.Equal("IdentityAddressing",
            phase54.GetProperty("ActivationDefault").GetString());
        Assert.Equal("IncompleteNoGuestPhysicalAddressOrEptViolationQualification",
            phase54.GetProperty("ExactFourFieldCoverage").GetString());
        Assert.Equal("NotOpened", phase54.GetProperty("VmReadD2").GetString());
        Assert.Equal(
            "NoneUntilSeparateOwnerAuthorizationForNextExactFieldOrOperation",
            status.RootElement.GetProperty("NextCandidatePool").GetString());
    }

    [Fact]
    public void Evidence_IsLaterNonSelfReferentialAndHashesBlockedE0SubjectBytes()
    {
        const string subject = "43a3125c3a1a37c2793ee71a37c361addc180919";
        const string tree = "41d8a5dc234985ef9144bd5390bfb2acab38b311";
        string repositoryRoot =
            VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        string evidencePath = Path.Combine(
            repositoryRoot,
            "HybridCPU_ISE", "docs", "ref2", "VirtualizationActivationPlan", "evidence",
            "2026-08-13-phase53-neutral-cpu-translation-fault-producer-e0-blocked-clean-evidence.json");
        using JsonDocument evidence = JsonDocument.Parse(File.ReadAllText(evidencePath));
        JsonElement root = evidence.RootElement;
        Assert.True(root.GetProperty("non_self_referential").GetBoolean());
        Assert.False(root.GetProperty("runtime_authority").GetBoolean());
        Assert.Equal(subject,
            root.GetProperty("implementation_subject").GetProperty("commit_sha").GetString());
        Assert.Equal(tree, GitText(repositoryRoot, "rev-parse", $"{subject}^{{tree}}"));

        foreach (JsonProperty source in root
            .GetProperty("source_hashes_sha256_clean_subject_bytes")
            .EnumerateObject())
        {
            byte[] bytes = GitBytes(
                repositoryRoot,
                "cat-file",
                "blob",
                $"{subject}:{source.Name}");
            Assert.Equal(
                source.Value.GetString(),
                Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        }
        Assert.False(root.GetProperty("completion_backed_vmread_opened").GetBoolean());
    }

    [Fact]
    public void Phase53Subject_CpuFetchAndScalarMemoryCallersHadNoCpuAddressTranslation()
    {
        const string subject = "43a3125c3a1a37c2793ee71a37c361addc180919";
        string root = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        string fetch = GitText(root, "show",
            $"{subject}:HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/StageFlow/CPU_Core.PipelineExecution.StageFlow.cs");
        string loadStore = GitText(root, "show",
            $"{subject}:HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Memory/MicroOp.LoadStore.cs");
        string controller = GitText(root, "show",
            $"{subject}:HybridCPU_ISE/CloseToHSL/Memory/Timing/MemoryCycleController.cs");

        Assert.Contains("GetVLIWBundleByPointer(fetchPC)", fetch);
        Assert.Contains("if (fetchPC >= GetBoundMainMemoryLength())", fetch);
        Assert.DoesNotContain("Translate", ExtractMethod(fetch, "private void PipelineStage_Fetch()"));

        Assert.Contains("TryAcceptSingleLaneScalarLoad", loadStore);
        Assert.Contains("TryAcceptSingleLaneScalarStore", loadStore);
        Assert.Contains("HasExactMainMemoryRange", loadStore);
        Assert.DoesNotContain("IOMMU", loadStore);
        Assert.DoesNotContain("TranslateGuestAccess", loadStore);

        Assert.Contains("CapturePublishedPhysicalMemoryBankBindingUnderControllerGate", controller);
        Assert.DoesNotContain("TranslateGuestAccess", controller);
        Assert.DoesNotContain("TranslateAndValidateAccess", controller);
    }

    [Fact]
    public void GenericPageFaultCarrier_RemainsNonAuthoritativeWhileTypedWinnerIsSeparate()
    {
        string exception = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToHSL/Core/Pipeline/MicroOps/Types/MicroOp.Exceptions.cs");
        string loadStore = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToHSL/Core/Pipeline/MicroOps/Memory/MicroOp.LoadStore.cs");
        string arbitration = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToHSL/Core/Pipeline/ExecutionFlow/StageFlow/CPU_Core.Pipeline.Helpers.cs");

        string pageFault = ExtractType(exception, "public class PageFaultException");
        Assert.Contains("public ulong FaultAddress", pageFault);
        Assert.Contains("public bool IsWrite", pageFault);
        Assert.DoesNotContain("Translation", pageFault);
        Assert.DoesNotContain("FaultKind", pageFault);
        Assert.Contains("Convert other memory exceptions to PageFaultException", loadStore);

        Assert.Contains("TryResolveStageAwareExceptionWinnerMetadata", arbitration);
        Assert.Contains("PipelineStage.WriteBack", arbitration);
        Assert.Contains("CompareMemoryLaneOrder", arbitration);
        string metadata = ExtractType(arbitration, "public readonly struct StageAwareExceptionWinnerMetadata");
        Assert.DoesNotContain("AttemptId", metadata);
        Assert.DoesNotContain("EventId", metadata);
        Assert.Contains("CpuInstructionTranslationFault", metadata);
    }

    [Fact]
    public void ProductionGraph_RegistersOneExactCpuProducerWithoutBorrowingIommuAuthority()
    {
        string canonicalRoot = Path.Combine(
            VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot(),
            "HybridCPU_ISE", "CloseToHSL");
        string[] files = Directory.GetFiles(canonicalRoot, "*.cs", SearchOption.AllDirectories);
        string production = string.Join("\n", files.Select(File.ReadAllText));

        Assert.Equal(1, CountOccurrences(
            production,
            "\"CanonicalCpuInstructionTranslationFaultProducer\""));
        Assert.Contains("CommitAtCanonicalPreciseFaultBoundary", production);
        Assert.Contains("CpuInstructionTranslationOwner", production);
        Assert.DoesNotContain("VmExitReason", ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToHSL/Core/Runtime/Memory/Translation/CpuInstructionTranslationContour.cs"));

        string nonIommu = string.Join("\n", files
            .Where(path => !path.Contains(
                Path.Combine("Memory", "MMU"),
                StringComparison.OrdinalIgnoreCase))
            .Select(File.ReadAllText));
        Assert.DoesNotContain("IOMMU.TranslateGuestAccess(", nonIommu);
        Assert.DoesNotContain("IOMMU.TranslateAndValidateAccess(", nonIommu);
    }

    private static string ExtractMethod(string source, string signature)
    {
        int start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, signature);
        int next = source.IndexOf("\n            [MethodImpl", start + signature.Length, StringComparison.Ordinal);
        return next >= 0 ? source[start..next] : source[start..];
    }

    private static string GitText(string workingDirectory, params string[] arguments) =>
        System.Text.Encoding.UTF8.GetString(GitBytes(workingDirectory, arguments)).TrimEnd();

    private static byte[] GitBytes(string workingDirectory, params string[] arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
        };
        foreach (string argument in arguments)
            process.StartInfo.ArgumentList.Add(argument);
        Assert.True(process.Start());
        using var output = new MemoryStream();
        process.StandardOutput.BaseStream.CopyTo(output);
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, error);
        return output.ToArray();
    }

    private static string ExtractType(string source, string signature)
    {
        int start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, signature);
        int next = source.IndexOf("\n    /// <summary>", start + signature.Length, StringComparison.Ordinal);
        return next >= 0 ? source[start..next] : source[start..];
    }

    private static int CountOccurrences(string source, string value)
    {
        int count = 0;
        int offset = 0;
        while ((offset = source.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }
        return count;
    }
}
