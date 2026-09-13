using System.IO;
using System.Text.Json;
using HybridCPU.Compiler.Core.Target.Managed;
using HybridCPU.Compiler.Core.Target.Runtime;
using HybridCPU.ManagedRuntime;
using HybridCPU.RuntimeKernel;
using HybridCPU_ISE.CloseToHSL.Core.Runtime.Managed;
using HybridCPU_ISE.NonRTL.Runtime;
using HybridCPU.Platform.Contracts;
using System.Security.Cryptography;

namespace DoomSharp.HybridCpu.Windows;

public sealed record DoomGuestExecutionReport(
    HybridCpuIseManagedImageLoadResultV1 Load,
    HybridCpuIseManagedGuestExecutionResultV1? Execution,
    int ExitCode,
    string Detail,
    IReadOnlyList<HybridCpuIseConsoleOutputV1>? ConsoleOutput = null)
{
    public bool StartedCpu => Execution is not null;
}

/// <summary>
/// Windows-host wiring for the production loader and ISE CPU runner. This service has
/// no CoreCLR guest fallback; WAD bytes are copied into a rooted runtime-owned guest array.
/// </summary>
public sealed class DoomGuestExecutionService
{
    public Func<OwnedCoreObservationBinding, IOwnedCoreObservationConsumer>? ObservationAttach { get; init; }
    public Action<DoomFramebufferFrame>? FramePresented { get; init; }
    /// <summary>Explicit test-only diagnostic request; absent in ordinary host execution.</summary>
    public TestOnlyInitializerTrace? TestOnlyInitializerTrace { get; init; }
    public DoomGuestExecutionReport Execute(string imagePath, string wadPath, int maximumPipelineCycles)
        => Execute(imagePath, wadPath, maximumPipelineCycles, requireGcSafepointEvidence: false);

    public DoomGuestExecutionReport Execute(string imagePath, string wadPath, int maximumPipelineCycles,
        bool requireGcSafepointEvidence)
    {
        if (string.IsNullOrWhiteSpace(wadPath) || !File.Exists(wadPath))
            return Rejected("HCDOOMGUI1301: IWAD/PWAD path does not exist.");
        WadInspection wad;
        try { wad = ImageAdmission.InspectWad(wadPath); }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        { return Rejected($"HCDOOMGUI1302: WAD inspection rejected the input: {exception.Message}"); }

        if (wad.Bytes > HybridCpuBootBlobServiceContractV1.MaximumBlobBytes)
            return Rejected("HCDOOMGUI1303: WAD exceeds the boot-blob contract size limit.");
        byte[] bytes;
        try
        {
            using var stream = new FileStream(wadPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length != wad.Bytes) return Rejected("HCDOOMGUI1304: WAD changed after inspection.");
            bytes = new byte[checked((int)wad.Bytes)];
            stream.ReadExactly(bytes);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { return Rejected($"HCDOOMGUI1302: WAD read rejected: {exception.Message}"); }
        if (!string.Equals(Convert.ToHexString(SHA256.HashData(bytes)), wad.Sha256, StringComparison.OrdinalIgnoreCase))
            return Rejected("HCDOOMGUI1304: WAD changed after inspection.");
        return ExecuteCore(imagePath, maximumPipelineCycles, bytes, requireGcSafepointEvidence);
    }

    public DoomGuestExecutionReport Execute(string imagePath, int maximumPipelineCycles)
        => Execute(imagePath, maximumPipelineCycles, requireGcSafepointEvidence: false);

    public DoomGuestExecutionReport Execute(string imagePath, int maximumPipelineCycles,
        bool requireGcSafepointEvidence)
        => ExecuteCore(imagePath, maximumPipelineCycles, null, requireGcSafepointEvidence);

    private DoomGuestExecutionReport ExecuteCore(string imagePath, int maximumPipelineCycles, byte[]? wad,
        bool requireGcSafepointEvidence)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            return Rejected("HCDOOMGUI1201: .hcexe path does not exist.");
        if (maximumPipelineCycles <= 0)
            return Rejected("HCDOOMGUI1202: execution budget must be positive.");

        byte[] package;
        try { package = File.ReadAllBytes(imagePath); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { return Rejected($"HCDOOMGUI1203: image read failed: {exception.Message}"); }

        HybridCpuRestrictedImageV1 inspected = new HybridCpuRestrictedImageBuilderV1().Inspect(package);
        if (inspected.Status != HybridCpuStartupStatusV1.Success || inspected.RuntimeBootstrap is null ||
            inspected.InitialRegisters is not HybridCpuStartupRegisterStateV1 registers)
            return Rejected("HCDOOMGUI1204: HCEXE inspection rejected the image or startup registers.");

        var memory = new HybridCpuIseSparseMainMemoryAreaV1();
        var bootBlob = new HybridCpuIseBootBlobBindingV1();
        var console = new HybridCpuIseConsoleProviderV1((address, count) =>
        {
            byte[] snapshot = new byte[count];
            return memory.TryReadPhysicalRange(address, snapshot) ? snapshot : null;
        });
        var graphics = new DoomFramebufferProvider((address, count) =>
        {
            byte[] snapshot = new byte[count];
            return memory.TryReadPhysicalRange(address, snapshot) ? snapshot : null;
        });
        if (FramePresented is not null) graphics.FramePresented += FramePresented;
        var hostProvider = new DoomCompositeHostProvider(console, graphics);
        var kernel = new DeterministicRuntimeKernelV1(hostServiceProvider: hostProvider, managedBootBlobProvider: bootBlob);
        IReadOnlyDictionary<string, HybridCpuRuntimeHelperEntryV1> helpers = inspected.RuntimeBootstrap.RuntimeHelpers
            .ToDictionary(static row => row.Symbol, static _ => (HybridCpuRuntimeHelperEntryV1)(_ => true), StringComparer.Ordinal);
        HybridCpuIseManagedImageLoadResultV1 loaded = new HybridCpuIseManagedImageLoaderV1().Load(
            new(inspected.ImageBytes, inspected.ImageBase, inspected.EntryAddress, inspected.PackageSha256,
                inspected.RuntimeBootstrap,
                HybridCpuManagedHeapOptionsV1.Create(0x2800_0000,
                    wad is null ? 0x0010_0000UL : 128UL * 1024 * 1024,
                    wad is null ? HybridCpuManagedHeapOptionsV1.DefaultMaximumObjectSizeBytes :
                        HybridCpuBootBlobServiceContractV1.MaximumBlobBytes + 4096, -3),
                HybridCpuManagedAbiFamilyV1.Default.TargetContractDigest,
                HybridCpuManagedAbiFamilyV1.Default.NativeAbiDigest,
                HybridCpuManagedAbiFamilyV1.RuntimePackRevision),
            memory, kernel, helpers);
        if (!loaded.IsSuccess)
            return new(loaded, null, 5, $"HCDOOMGUI1205: production loader rejected image: {loaded.Reason}");
        if (wad is not null && !bootBlob.TryBind(loaded, 1, wad, out loaded, out string bindingReason))
            return new(loaded, null, 5, $"HCDOOMGUI1101: {bindingReason}");

        var observationRun = ObservationAttach is null ? null :
            new OwnedCoreObservationRunIdentity(TestOnlyInitializerTrace?.RunId ?? Guid.NewGuid(), inspected.PackageSha256, loaded.Status.ToString());
        HybridCpuIseManagedGuestExecutionResultV1 execution =
            new HybridCpuIseManagedGuestExecutionRunnerV1().Execute(
                new(inspected.ImageBase, inspected.EntryAddress,
                    HybridCpuRestrictedStartupOptionsV1.Production.ReturnSentinel,
                    registers.StackPointerRegister, registers.FramePointerRegister,
                    registers.ThreadPointerRegister, registers.ReturnAddressRegister,
                    registers.ReturnValueRegister, registers.GlobalPointerRegister,
                    registers.StackPointer, registers.FramePointer, registers.ThreadPointer,
                    registers.ReturnAddress, registers.GlobalPointer, loaded, 0,
                    maximumPipelineCycles, RequireGcSafepointEvidence: requireGcSafepointEvidence,
                    ObservationAttach: ObservationAttach is null ? null : binding =>
                        ObservationAttach(binding with { RunIdentity = observationRun }),
                    TestOnlyInitializerTrace: TestOnlyInitializerTrace), memory, kernel);
        string detail = JsonSerializer.Serialize(new
        {
            RequireGcSafepointEvidence = requireGcSafepointEvidence,
            execution.Status,
            execution.ProcessExitCode,
            execution.RetiredPipelineCycles,
            execution.FinalProgramCounter,
            execution.GcSafepointsObserved,
            execution.ProcessExitEcallsObserved,
            execution.Reason
        });
        return new(loaded, execution, execution.IsSuccess ? 0 : 6,
            execution.IsSuccess ? "ISE guest execution completed." : $"HCDOOMGUI1206: ISE guest execution stopped: {detail}",
            console.Output);
    }

    private static DoomGuestExecutionReport Rejected(string reason) => new(
        new(HybridCpuIseManagedImageLoadStatusV1.InvalidImage, reason, null, null, null, null, null, null),
        null, 4, reason);
}
