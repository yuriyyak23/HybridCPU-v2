using System.Diagnostics;
using System.Security.Cryptography;
using HybridCPU.Compiler.Core.IR.Contracts;
using HybridCPU.Compiler.Core.Target;
using HybridCPU.Compiler.Native;
using HybridCPU.Compiler.Release;

if (args.Length > 0 && args[0] == "write-refplan7-final")
    return WriteRefPlan7Final(args);

if (args.Length != 6 || args[0] != "--commit" || args[2] != "--phase26-evidence" || args[4] != "--out")
{
    Console.Error.WriteLine("HCRL1101: expected --commit <sha> --phase26-evidence <sha> --out <path>.");
    return 2;
}

string commit = args[1];
string phase26Evidence = args[3];
string output = Path.GetFullPath(args[5]);
var identity = new NativeBuildIdentity(
    "RefPlan5-Phase27",
    commit,
    "qualified-tree",
    [new CompilerToolchainIdentity("dotnet-sdk", "10.0.204", "local-pinned")],
    HybridCpuTargetMachineContractV1.DataLayoutVersion,
    "hybridcpu.native-abi/v2");
var request = new NativeFrontendRequest(0, "ADDI r10, r0, 7\nADD r11, r10, r10") { BuildIdentity = identity };
var frontend = new NativeAssemblyFrontend();

Process process = Process.GetCurrentProcess();
long startPeak = process.PeakWorkingSet64;
var stopwatch = Stopwatch.StartNew();
NativeCompilationResult first = frontend.Compile(request);
NativeCompilationResult second = frontend.Compile(request);
stopwatch.Stop();
if (!first.Succeeded || !second.Succeeded || first.Artifacts is null || second.Artifacts is null)
{
    Console.Error.WriteLine("HCRL1102: native qualification compilation failed.");
    return 3;
}
if (!first.Artifacts.BinaryImage.SequenceEqual(second.Artifacts.BinaryImage) ||
    first.Artifacts.SchedulingReport != second.Artifacts.SchedulingReport ||
    first.Artifacts.Provenance.CacheKey.Digest != second.Artifacts.Provenance.CacheKey.Digest)
{
    Console.Error.WriteLine("HCRL1103: native qualification runs are not deterministic.");
    return 4;
}

var benchmark = new HybridCpuReleaseBenchmarkSummaryV1(
    "phase27-native-smoke-v1",
    1,
    2,
    first.Artifacts.SchedulingReport.ScheduleFingerprint,
    first.Artifacts.SchedulingReport.BundleFingerprint,
    Convert.ToHexString(SHA256.HashData(first.Artifacts.BinaryImage)).ToLowerInvariant(),
    first.Artifacts.SchedulingReport.BundleCount,
    NativeCompilerFrontendBase.MaxInstructions,
    1,
    0,
    "One bounded comparable case is carried from exact Phase15 evidence; the oracle remains CI-only and cannot select production output.",
    HybridCPU.Compiler.Oracle.HybridCpuExactSchedulingOracleContractV1.Default.ContractDigest,
    "7a4f760717ffdc6654a104fa41223b744461547a3d029c9f95bd8093df9c702b");
HybridCpuReleaseManifestV1 manifest = HybridCpuReleaseQualificationV1.Create(commit, phase26Evidence, benchmark);
HybridCpuReleaseValidationV1 validation = HybridCpuReleaseQualificationV1.Validate(manifest);
if (!validation.IsValid)
{
    Console.Error.WriteLine($"{validation.Code}: {validation.Reason}");
    return 5;
}
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
File.WriteAllText(output, HybridCpuReleaseQualificationV1.Serialize(manifest));
HybridCpuReleaseFeatureMatrixV1 featureMatrix = HybridCpuReleaseQualificationV1.CreateFeatureMatrix(manifest);
File.WriteAllText(output + ".features.json", System.Text.Json.JsonSerializer.Serialize(featureMatrix,
    new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
var telemetry = new HybridCpuReleaseTelemetryV1(
    "hybridcpu.release-telemetry/v1", commit, benchmark.Corpus, 2, stopwatch.Elapsed.TotalMilliseconds,
    Math.Max(startPeak, process.PeakWorkingSet64), SelectsProductionOutput: false);
File.WriteAllText(output + ".telemetry.json", System.Text.Json.JsonSerializer.Serialize(telemetry,
    new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(output);
Console.WriteLine(manifest.DeterministicDigest);
return 0;

static int WriteRefPlan7Final(string[] args)
{
    if (args.Length != 17)
    {
        Console.Error.WriteLine("HCR7L1101: expected write-refplan7-final --commit C --evidence-index E --source-digest S --toolchain-digest T --options-digest O --image-a A --image-b B --out M.");
        return 10;
    }
    var values = new Dictionary<string, string>(StringComparer.Ordinal);
    for (int index = 1; index < args.Length; index += 2)
        if (!args[index].StartsWith("--", StringComparison.Ordinal) || !values.TryAdd(args[index], args[index + 1]))
        {
            Console.Error.WriteLine("HCR7L1102: arguments must be unique named pairs.");
            return 11;
        }
    string[] required = ["--commit", "--evidence-index", "--source-digest", "--toolchain-digest", "--options-digest", "--image-a", "--image-b", "--out"];
    if (required.Any(key => !values.TryGetValue(key, out string? value) || string.IsNullOrWhiteSpace(value)))
    {
        Console.Error.WriteLine("HCR7L1103: one or more required arguments are missing.");
        return 12;
    }
    try
    {
        HybridCpuRefPlan7EvidenceArtifactV1[]? evidence = System.Text.Json.JsonSerializer.Deserialize<HybridCpuRefPlan7EvidenceArtifactV1[]>(
            File.ReadAllText(values["--evidence-index"]));
        if (evidence is null) throw new InvalidDataException("Evidence index is empty.");
        string imageA = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(values["--image-a"]))).ToLowerInvariant();
        string imageB = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(values["--image-b"]))).ToLowerInvariant();
        var image = new HybridCpuRefPlan7ImageQualificationV1(values["--source-digest"], values["--toolchain-digest"],
            values["--options-digest"], true, imageA, true, imageB);
        HybridCpuRefPlan7FinalReleaseManifestV1 final = HybridCpuRefPlan7FinalReleaseV1.Create(values["--commit"], evidence, image);
        HybridCpuRefPlan7FinalReleaseValidationV1 validation = HybridCpuRefPlan7FinalReleaseV1.Validate(final);
        if (!validation.IsValid)
        {
            Console.Error.WriteLine($"{validation.Code}: {validation.Reason}");
            return 13;
        }
        string output = Path.GetFullPath(values["--out"]);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.WriteAllText(output, HybridCpuRefPlan7FinalReleaseV1.Serialize(final));
        Console.WriteLine(output);
        Console.WriteLine(final.DeterministicDigest);
        return 0;
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException or ArgumentException)
    {
        Console.Error.WriteLine($"HCR7L1104: {exception.Message}");
        return 14;
    }
}
