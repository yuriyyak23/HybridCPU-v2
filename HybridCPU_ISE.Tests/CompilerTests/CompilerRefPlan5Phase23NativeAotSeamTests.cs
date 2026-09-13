using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using HybridCPU.Compiler.Cil;
using HybridCPU.Compiler.Core.IR;
using HybridCPU.Compiler.NativeAot;

namespace HybridCPU_ISE.Tests.CompilerTests;

public sealed class CompilerRefPlan5Phase23NativeAotSeamTests
{
    private static readonly string FixtureAssembly = typeof(RestrictedCilCSharpFixtures).Assembly.Location;
    private const string FixtureType = "HybridCPU_ISE.Tests.CompilerTests.RestrictedCilCSharpFixtures";

    [Fact]
    public void BaselineAndPatch_AreExactAndDriftChecked()
    {
        string root = FindRepositoryRoot();
        string patch = Path.Combine(root, "Compilers", "HybridCPU_Compiler", "NativeAot", "Patches", "0001-hybridcpu-phase23-single-method-seam.patch");
        string map = File.ReadAllText(Path.Combine(root, "Compilers", "HybridCPU_Compiler", "NativeAot", "PHASE23_TARGET_ENABLEMENT_MAP.md"));

        Assert.Equal("94ea82652cdd4e0f8046b5bd5becbd11461482ca", NativeAotSeamBaselineV1.SourceCommit);
        Assert.Equal("10.0.105", NativeAotSeamBaselineV1.SdkVersion);
        Assert.Equal(NativeAotSeamBaselineV1.PatchDigest,
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(patch))).ToLowerInvariant());
        for (int item = 1; item <= 10; item++) Assert.Contains($"## {item}.", map, StringComparison.Ordinal);
        Assert.Contains("ComputeMarkedNodes", map, StringComparison.Ordinal);
        Assert.Contains("ObjectWriter", map, StringComparison.Ordinal);
    }

    [Fact]
    public void AdapterProject_DependsOnlyOnCilAndCore_AndCoreHasNoIlCompilerLeak()
    {
        string root = FindRepositoryRoot();
        string project = File.ReadAllText(Path.Combine(root, "Compilers", "HybridCPU_Compiler", "NativeAot", "HybridCPU.Compiler.NativeAot.Adapter.csproj"));
        string core = File.ReadAllText(Path.Combine(root, "Compilers", "HybridCPU_Compiler", "Core", "HybridCPU.Compiler.Core.csproj"));
        string legacy = File.ReadAllText(Path.Combine(root, "Compilers", "HybridCPU_Compiler", "HybridCPU_Compiler.csproj"));

        Assert.Contains("..\\Cil\\HybridCPU.Compiler.Cil.csproj", project, StringComparison.Ordinal);
        Assert.Contains("..\\Core\\HybridCPU.Compiler.Core.csproj", project, StringComparison.Ordinal);
        Assert.DoesNotContain("PackageReference", project, StringComparison.Ordinal);
        Assert.DoesNotContain("ILCompiler", project, StringComparison.Ordinal);
        Assert.DoesNotContain("Internal.TypeSystem", project, StringComparison.Ordinal);
        Assert.DoesNotContain("ILCompiler", core, StringComparison.Ordinal);
        Assert.Contains("Compile Remove=\"NativeAot\\**\\*.cs\"", legacy, StringComparison.Ordinal);
        Assert.DoesNotContain(typeof(IrProgram).Assembly.GetReferencedAssemblies(),
            static reference => reference.Name?.Contains("ILCompiler", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public void QualifiedMethod_ProducesDeterministicTargetPureArtifactAndManifest()
    {
        using AdapterRun first = RunAdapter(nameof(RestrictedCilCSharpFixtures.Add));
        using AdapterRun second = RunAdapter(nameof(RestrictedCilCSharpFixtures.Add));

        Assert.Equal(0, first.ExitCode);
        Assert.Equal(0, second.ExitCode);
        Assert.Equal(first.Code, second.Code);
        Assert.NotEmpty(first.Code);
        Assert.Equal(0, first.Code.Length % 32);
        Assert.Equal(JsonSerializer.Serialize(first.Artifact), JsonSerializer.Serialize(second.Artifact));
        Assert.Equal(NativeAotSeamStatusV1.Success, first.Artifact!.Status);
        Assert.Empty(first.Artifact.Relocations);
        Assert.Empty(first.Artifact.FrameRecords);
        Assert.Null(first.Artifact.GcInfo);
        Assert.Null(first.Artifact.EhInfo);
        Assert.False(first.Artifact.HasRuntimeAuthority);
        Assert.False(first.Artifact.HasPublicationAuthority);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(first.Code)).ToLowerInvariant(), first.Artifact.CodeSha256);

        RestrictedCilImportResultV1 imported = new RestrictedCilImporterV1().ImportFile(FixtureAssembly,
            new(FixtureType, nameof(RestrictedCilCSharpFixtures.Add)));
        IrProgramSchedule schedule = new HybridCpuLocalListScheduler().ScheduleProgram(Assert.IsType<IrProgram>(imported.Program));
        byte[] direct = new HybridCpuBundleSerializer().SerializeProgram(
            new HybridCpuBundleLowerer().LowerProgram(new HybridCpuBundleFormer().BundleProgram(schedule)));
        Assert.Equal(direct, first.Code);
    }

    [Fact]
    public void QualifiedInlineHelper_UsesTheExistingRestrictedCilAndCorePipeline()
    {
        using AdapterRun result = RunAdapter(nameof(RestrictedCilCSharpFixtures.CallIdentity));

        Assert.Equal(0, result.ExitCode);
        Assert.NotEmpty(result.Code);
        Assert.Equal(NativeAotSeamStatusV1.Success, result.Artifact!.Status);
        Assert.Contains(nameof(RestrictedCilCSharpFixtures.CallIdentity), result.Artifact.MethodIdentity, StringComparison.Ordinal);
        Assert.Empty(result.Artifact.Relocations);
    }

    [Theory]
    [InlineData(nameof(RestrictedCilCSharpFixtures.GenericIdentity), "HCCIL1012")]
    [InlineData(nameof(RestrictedCilCSharpFixtures.TryCatch), "HCCIL1011")]
    [InlineData(nameof(RestrictedCilCSharpFixtures.StringLength), "HCCIL1001")]
    [InlineData(nameof(RestrictedCilCSharpFixtures.CallUnknown), "HCCIL1474")]
    public void UnsupportedNodeFamilies_FailClosedBeforeArtifactEmission(string method, string diagnostic)
    {
        using AdapterRun result = RunAdapter(method);

        Assert.Equal(4, result.ExitCode);
        Assert.Contains(diagnostic, result.StandardError, StringComparison.Ordinal);
        Assert.Empty(result.Code);
        Assert.Null(result.Artifact);
    }

    [Fact]
    public void VersionSkew_FailsDeterministicallyBeforeReadingMethod()
    {
        using AdapterRun first = RunAdapter(nameof(RestrictedCilCSharpFixtures.Add), sourceCommit: new string('0', 40));
        using AdapterRun second = RunAdapter(nameof(RestrictedCilCSharpFixtures.Add), sourceCommit: new string('0', 40));

        Assert.Equal(3, first.ExitCode);
        Assert.Equal(first.ExitCode, second.ExitCode);
        Assert.Equal(first.StandardError, second.StandardError);
        Assert.Contains("HCNAOT1001", first.StandardError, StringComparison.Ordinal);
        Assert.Empty(first.Code);
    }

    [Fact]
    public void PatchExplicitlyBypassesHostObjectWriterOnlyInClosedSeamMode()
    {
        string root = FindRepositoryRoot();
        string patch = File.ReadAllText(Path.Combine(root, "Compilers", "HybridCPU_Compiler", "NativeAot", "Patches", "0001-hybridcpu-phase23-single-method-seam.patch"));

        Assert.Contains("CompileHybridCpuMethod(methodCodeNodeNeedingCode)", patch, StringComparison.Ordinal);
        Assert.Contains("methodNode.SetCode", patch, StringComparison.Ordinal);
        Assert.Contains("File.WriteAllBytes(outputFile, _hybridCpuCode)", patch, StringComparison.Ordinal);
        Assert.Contains("if (_hybridCpuSeam != null)", patch, StringComparison.Ordinal);
        Assert.DoesNotContain("-            ObjectWriter.ObjectWriter.EmitObject", patch, StringComparison.Ordinal);
        Assert.DoesNotContain("DateTime", patch, StringComparison.Ordinal);
        Assert.DoesNotContain("Stopwatch", patch, StringComparison.Ordinal);
    }

    private static AdapterRun RunAdapter(string method, string? sourceCommit = null)
    {
        string output = Path.Combine(Path.GetTempPath(), $"hybridcpu-naot-test-{Guid.NewGuid():N}.bin");
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        start.ArgumentList.Add(typeof(NativeAotSeamArtifactV1).Assembly.Location);
        start.ArgumentList.Add("compile");
        AddPair(start, "--assembly", FixtureAssembly);
        AddPair(start, "--type", FixtureType);
        AddPair(start, "--method", method);
        AddPair(start, "--out", output);
        AddPair(start, "--source-commit", sourceCommit ?? NativeAotSeamBaselineV1.SourceCommit);
        AddPair(start, "--patch-digest", NativeAotSeamBaselineV1.PatchDigest);
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Adapter process did not start.");
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        byte[] code = File.Exists(output) ? File.ReadAllBytes(output) : Array.Empty<byte>();
        NativeAotSeamArtifactV1? artifact = File.Exists(output + ".json")
            ? JsonSerializer.Deserialize<NativeAotSeamArtifactV1>(File.ReadAllText(output + ".json"))
            : null;
        return new(process.ExitCode, stdout, stderr, output, code, artifact);
    }

    private static void AddPair(ProcessStartInfo start, string name, string value)
    {
        start.ArgumentList.Add(name);
        start.ArgumentList.Add(value);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !Directory.Exists(Path.Combine(current.FullName, "Compilers", "HybridCPU_Compiler"))) current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }

    private sealed record AdapterRun(
        int ExitCode,
        string StandardOutput,
        string StandardError,
        string OutputPath,
        byte[] Code,
        NativeAotSeamArtifactV1? Artifact) : IDisposable
    {
        public void Dispose()
        {
            if (File.Exists(OutputPath)) File.Delete(OutputPath);
            if (File.Exists(OutputPath + ".json")) File.Delete(OutputPath + ".json");
        }
    }
}
