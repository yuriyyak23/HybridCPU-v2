using System.Text.Json;
using System.Text.RegularExpressions;
using HybridCPU.Compiler.Core.Target.Managed;
using HybridCPU.Compiler.Core.Target.Runtime;
using HybridCPU.ManagedRuntime;
using HybridCPU.Platform.Contracts;
using HybridCPU.RuntimeKernel;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.CloseToHSL.Core.Runtime.Managed;
using HybridCPU_ISE.Core;
using YAKSys_Hybrid_CPU;

// Passive report import must dispatch before any image inspection, loading or execution.
if (args.Length > 0 && args[0] == "--report")
    return HybridCpuIseImageRunner.ReportCommand.Run(args, Console.Out, Console.Error);

if (args.Length is < 1 or > 4 ||
    !args[0].EndsWith(".hcexe", StringComparison.OrdinalIgnoreCase) ||
    (args.Length >= 2 && (!int.TryParse(args[1], out int requestedMaximumPipelineCycles) ||
        requestedMaximumPipelineCycles <= 0)) ||
    (args.Length == 4 && !TryParseAddress(args[3], out _)))
{
    Console.Error.WriteLine("HCISE-RUN0001: expected a .hcexe path, optional positive max-pipeline-cycles, optional code-record identity filter, and optional diagnostic PC; or --report <runner.json> [--image <package.hcexe>] for passive diagnostics");
    return 2;
}

int maximumPipelineCycles = args.Length >= 2
    ? int.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture)
    : 350_000;
string? codeRecordIdentityFilter = args.Length >= 3 ? args[2] : null;
ulong? diagnosticPc = args.Length == 4 && TryParseAddress(args[3], out ulong parsedDiagnosticPc)
    ? parsedDiagnosticPc
    : null;

string path = Path.GetFullPath(args[0]);
if (!File.Exists(path))
{
    Console.Error.WriteLine("HCISE-RUN0002: HCEXE does not exist");
    return 3;
}

byte[] bytes = File.ReadAllBytes(path);
HybridCpuRestrictedImageV1 image = new HybridCpuRestrictedImageBuilderV1().Inspect(bytes);
if (image.Status != HybridCpuStartupStatusV1.Success || image.RuntimeBootstrap is null ||
    image.InitialRegisters is not HybridCpuStartupRegisterStateV1 registers)
{
    Console.Error.WriteLine($"HCISE-RUN1001: HCEXE inspection rejected: {string.Join("; ", image.Diagnostics.Select(static row => $"{row.Code}: {row.Message}"))}");
    return 4;
}

var memory = new HybridCpuIseSparseMainMemoryAreaV1();
var kernel = new DeterministicRuntimeKernelV1();
var helpers = image.RuntimeBootstrap.RuntimeHelpers.ToDictionary(
    static row => row.Symbol,
    static _ => (HybridCpuRuntimeHelperEntryV1)(_ => true),
    StringComparer.Ordinal);
var initializerBindings = image.RuntimeBootstrap.ModuleInitializers.Select(registration => new
{
    registration.Order,
    registration.ModuleIdentity,
    registration.InitializerSymbol,
    registration.TypeId,
    CodeRecords = image.RuntimeBootstrap.CodeManagerRecords
        .Where(record => string.Equals(record.MethodIdentity, registration.InitializerSymbol, StringComparison.Ordinal))
        .Select(record => new { record.MethodIdentity, record.CodeStartOffsetBytes, record.CodeSizeBytes })
        .ToArray()
}).ToArray();
if (initializerBindings.Any(static binding => binding.CodeRecords.Length != 1))
{
    Console.Error.WriteLine("HCISE-RUN1003: initializer symbols do not have one exact image-owned code-manager binding: " +
        JsonSerializer.Serialize(initializerBindings));
    return 7;
}
var duplicateInitializerTypes = initializerBindings.Where(static binding => binding.TypeId.HasValue)
    .GroupBy(static binding => binding.TypeId!.Value)
    .Where(static group => group.Count() > 1)
    .Select(group => new { TypeId = group.Key, Initializers = group.Select(static row => row.InitializerSymbol).ToArray() })
    .ToArray();
if (duplicateInitializerTypes.Length != 0)
{
    Console.Error.WriteLine("HCISE-RUN1004: multiple initializer registrations share a TypeId: " +
        JsonSerializer.Serialize(duplicateInitializerTypes));
    return 8;
}
HybridCpuIseManagedImageLoadResultV1 loaded = new HybridCpuIseManagedImageLoaderV1().Load(
    new(image.ImageBytes, image.ImageBase, image.EntryAddress, image.PackageSha256, image.RuntimeBootstrap,
        HybridCpuManagedHeapOptionsV1.Create(0x2800_0000, 0x0010_0000,
            HybridCpuManagedHeapOptionsV1.DefaultMaximumObjectSizeBytes, -3),
        HybridCpuManagedAbiFamilyV1.Default.TargetContractDigest,
        HybridCpuManagedAbiFamilyV1.Default.NativeAbiDigest,
        HybridCpuManagedAbiFamilyV1.RuntimePackRevision),
    memory, kernel, helpers);
if (!loaded.IsSuccess)
{
    HybridCpuManagedTypeRegistrationV1? firstType = image.RuntimeBootstrap.ManagedTypes?.FirstOrDefault();
    Console.Error.WriteLine($"HCISE-RUN1002: loader rejected HCEXE: status={loaded.Status}, reason={loaded.Reason} " +
        $"image=[0x{image.ImageBase:x},0x{checked(image.ImageBase + (ulong)image.ImageBytes.Length):x}), " +
        $"sp=0x{registers.StackPointer:x}, fp=0x{registers.FramePointer:x}, heap=[0x28000000,0x28100000), " +
        $"first-type-offset={firstType?.MetadataOffsetBytes}, first-type-size={firstType?.MetadataSizeBytes}.");
    return 5;
}

string? fullTracePath = Environment.GetEnvironmentVariable("HYBRIDCPU_FULL_TRACE_PATH");
TraceSink? priorTraceSink = Processor.TraceSink;
TraceSink? fullTraceSink = null;
if (!string.IsNullOrWhiteSpace(fullTracePath))
{
    fullTraceSink = new TraceSink(TraceFormat.Binary, fullTracePath);
    fullTraceSink.SetEnabled(true);
    fullTraceSink.SetLevel(TraceLevel.Full);
    Processor.TraceSink = fullTraceSink;
}

HybridCpuIseManagedGuestExecutionResultV1 result;
try
{
    result = new HybridCpuIseManagedGuestExecutionRunnerV1().Execute(
    new(image.ImageBase, image.EntryAddress, HybridCpuRestrictedStartupOptionsV1.Production.ReturnSentinel,
        registers.StackPointerRegister, registers.FramePointerRegister, registers.ThreadPointerRegister,
        registers.ReturnAddressRegister, registers.ReturnValueRegister, registers.GlobalPointerRegister,
        registers.StackPointer, registers.FramePointer, registers.ThreadPointer, registers.ReturnAddress,
        registers.GlobalPointer, loaded, 0, maximumPipelineCycles, RequireGcSafepointEvidence: true),
    memory, kernel);
}
finally
{
    if (fullTraceSink is not null)
    {
        fullTraceSink.ExportBinaryTrace(fullTracePath!);
        Processor.TraceSink = priorTraceSink;
    }
}

ulong diagnosticWindowStart = result.LastRetiredBundlePc > 0x10_000
    ? result.LastRetiredBundlePc - 0x10_000
    : 0;
ulong diagnosticWindowEnd = result.LastRetiredBundlePc > ulong.MaxValue - 0x10_000
    ? ulong.MaxValue
    : result.LastRetiredBundlePc + 0x10_000;
var nearbyCodeRecords = image.RuntimeBootstrap.CodeManagerRecords
    .Where(record =>
    {
        ulong start = checked(image.ImageBase + (ulong)record.CodeStartOffsetBytes);
        ulong end = checked(start + (ulong)record.CodeSizeBytes);
        return start < diagnosticWindowEnd && end > diagnosticWindowStart;
    })
    .Select(record => new
    {
        record.MethodIdentity,
        codeStart = checked(image.ImageBase + (ulong)record.CodeStartOffsetBytes),
        codeEnd = checked(image.ImageBase + (ulong)record.CodeStartOffsetBytes + (ulong)record.CodeSizeBytes)
    })
    .ToArray();
var managedBridge = loaded.EcallBridge as HybridCpuIseManagedEcallBridgeV1;
var lastManagedReceiverDescriptor = managedBridge is null
    ? null
    : loaded.TypeSystem?.ResolveTypeHandle(managedBridge.LastManagedReceiver)?.StableIdentity;
Match returnAddressMatch = Regex.Match(result.Reason, @"(?:^|[,; ])x1=0x(?<value>[0-9a-fA-F]+)");
ulong? managedCallerPc = returnAddressMatch.Success &&
    ulong.TryParse(returnAddressMatch.Groups["value"].Value,
        System.Globalization.NumberStyles.HexNumber, null, out ulong returnAddress) && returnAddress >= 4
    ? returnAddress - 4
    : null;
var managedCallerCodeRecord = managedCallerPc is ulong callerPc
    ? image.RuntimeBootstrap.CodeManagerRecords
        .Where(record =>
        {
            ulong start = checked(image.ImageBase + (ulong)record.CodeStartOffsetBytes);
            ulong end = checked(start + (ulong)record.CodeSizeBytes);
            return callerPc >= start && callerPc < end;
        })
        .Select(record => new
        {
            record.MethodIdentity,
            codeStart = checked(image.ImageBase + (ulong)record.CodeStartOffsetBytes),
            codeEnd = checked(image.ImageBase + (ulong)record.CodeStartOffsetBytes + (ulong)record.CodeSizeBytes),
            nativeOffset = checked(callerPc - image.ImageBase - (ulong)record.CodeStartOffsetBytes)
        })
        .SingleOrDefault()
    : null;
var managedCallerNearbyCodeRecords = managedCallerPc is ulong nearbyCallerPc
    ? image.RuntimeBootstrap.CodeManagerRecords
        .Select(record => new
        {
            record.MethodIdentity,
            codeStart = checked(image.ImageBase + (ulong)record.CodeStartOffsetBytes),
            codeEnd = checked(image.ImageBase + (ulong)record.CodeStartOffsetBytes + (ulong)record.CodeSizeBytes)
        })
        .OrderBy(record => nearbyCallerPc < record.codeStart
            ? record.codeStart - nearbyCallerPc
            : nearbyCallerPc >= record.codeEnd ? nearbyCallerPc - record.codeEnd + 1 : 0)
        .Take(6)
        .ToArray()
    : [];
var managedTypes = loaded.TypeSystem?.Descriptors.Select(descriptor => new
{
    descriptor.TypeId,
    descriptor.StableIdentity,
    TypeHandle = loaded.TypeSystem.TypeHandle(descriptor.TypeId),
    descriptor.Kind,
    descriptor.InstanceSizeBytes,
    InstanceFields = descriptor.InstanceFields.Select(field => new
    {
        field.Identity,
        field.OffsetBytes,
        field.SizeBytes,
        field.StorageKind
    }).ToArray()
}).ToArray();
var filteredCodeRecords = string.IsNullOrWhiteSpace(codeRecordIdentityFilter)
    ? []
    : image.RuntimeBootstrap.CodeManagerRecords
        .Where(record => record.MethodIdentity.Contains(codeRecordIdentityFilter, StringComparison.Ordinal))
        .Select(record => new
        {
            record.MethodIdentity,
            codeStart = checked(image.ImageBase + (ulong)record.CodeStartOffsetBytes),
            codeEnd = checked(image.ImageBase + (ulong)record.CodeStartOffsetBytes + (ulong)record.CodeSizeBytes)
        })
        .ToArray();
var diagnosticPcInstructionSummary = diagnosticPc is not ulong requestedPc
    ? []
    : Enumerable.Range(-8, 17)
        .Select(delta => checked((ulong)(checked((long)requestedPc + delta * 256L))))
        .Where(address => address >= image.ImageBase &&
            address - image.ImageBase <= checked((ulong)image.ImageBytes.Length - 256))
        .SelectMany(address =>
        {
            var bundle = new VLIW_Bundle();
            int offset = checked((int)(address - image.ImageBase));
            if (!bundle.TryReadBytes(image.ImageBytes, offset)) return [];
            return Enumerable.Range(0, 8)
                .Select(slot => (slot, instruction: bundle.GetInstruction(slot)))
                .Where(static row => row.instruction.OpCode != 0)
                .Select(row => new
                {
                    address,
                    row.slot,
                    row.instruction.OpCode,
                    row.instruction.Immediate,
                    row.instruction.Reg1ID,
                    row.instruction.Reg2ID,
                    row.instruction.Reg3ID
                });
        })
        .ToArray();
var diagnosticPcCodeRecord = diagnosticPc is not ulong recordPc
    ? null
    : image.RuntimeBootstrap.CodeManagerRecords
        .Where(record =>
        {
            ulong start = checked(image.ImageBase + (ulong)record.CodeStartOffsetBytes);
            ulong end = checked(start + (ulong)record.CodeSizeBytes);
            return recordPc >= start && recordPc < end;
        })
        .Select(record => new
        {
            record.MethodIdentity,
            codeStart = checked(image.ImageBase + (ulong)record.CodeStartOffsetBytes),
            codeEnd = checked(image.ImageBase + (ulong)record.CodeStartOffsetBytes + (ulong)record.CodeSizeBytes),
            nativeOffset = checked(recordPc - image.ImageBase - (ulong)record.CodeStartOffsetBytes)
        })
        .SingleOrDefault();
var filteredCodeBoundaryBundles = string.IsNullOrWhiteSpace(codeRecordIdentityFilter)
    ? []
    : image.RuntimeBootstrap.CodeManagerRecords
        .Where(record => record.MethodIdentity.Contains(codeRecordIdentityFilter, StringComparison.Ordinal))
        .SelectMany(record => new[]
        {
            (record.MethodIdentity, Label: "start", Offset: record.CodeStartOffsetBytes),
            (record.MethodIdentity, Label: "last", Offset: checked(record.CodeStartOffsetBytes + record.CodeSizeBytes - 256)),
            (record.MethodIdentity, Label: "after", Offset: checked(record.CodeStartOffsetBytes + record.CodeSizeBytes))
        })
        .Where(row => row.Offset >= 0 && row.Offset <= image.ImageBytes.Length - 256)
        .Select(row =>
        {
            var bundle = new VLIW_Bundle();
            bool decoded = bundle.TryReadBytes(image.ImageBytes, row.Offset);
            return new
            {
                row.MethodIdentity,
                row.Label,
                address = checked(image.ImageBase + (ulong)row.Offset),
                decoded,
                slots = decoded
                    ? Enumerable.Range(0, 8).Select(slot => bundle.GetInstruction(slot))
                        .Where(static instruction => instruction.OpCode != 0)
                        .Select(static instruction => new
                        {
                            instruction.OpCode,
                            instruction.Immediate,
                            instruction.Reg1ID,
                            instruction.Reg2ID,
                            instruction.Reg3ID
                        }).ToArray()
                    : []
            };
        })
        .ToArray();
var filteredCodeInstructionSummary = string.IsNullOrWhiteSpace(codeRecordIdentityFilter)
    ? []
    : image.RuntimeBootstrap.CodeManagerRecords
        .Where(record => record.MethodIdentity.Contains(codeRecordIdentityFilter, StringComparison.Ordinal))
        .SelectMany(record => Enumerable.Range(0, record.CodeSizeBytes / 256).SelectMany(bundleIndex =>
        {
            int offset = checked(record.CodeStartOffsetBytes + bundleIndex * 256);
            var bundle = new VLIW_Bundle();
            if (!bundle.TryReadBytes(image.ImageBytes, offset)) return [];
            return Enumerable.Range(0, 8)
                .Select(slot => (slot, instruction: bundle.GetInstruction(slot)))
                .Where(static row => row.instruction.OpCode != 0 &&
                    (row.instruction.OpCode is 152 or 164 or 171 or 175 or 177 ||
                     row.instruction.Reg1ID == 20 || row.instruction.Reg2ID == 20 || row.instruction.Reg3ID == 20))
                .Select(row => new
                {
                    record.MethodIdentity,
                    address = checked(image.ImageBase + (ulong)offset),
                    row.slot,
                    row.instruction.OpCode,
                    row.instruction.Immediate,
                    row.instruction.Reg1ID,
                    row.instruction.Reg2ID,
                    row.instruction.Reg3ID
                }).ToArray();
        }))
        .ToArray();

Console.WriteLine(JsonSerializer.Serialize(new
{
    schema = "hybridcpu.ise-cpu-backed-image-run/v1",
    image = path,
    imageSha256 = image.PackageSha256,
    status = result.Status.ToString(),
    result.Reason,
    result.ProcessExitCode,
    result.RetiredPipelineCycles,
    result.FinalProgramCounter,
    result.GcSafepointsObserved,
    gcResultDigests = result.GcResultDigests,
    result.ProcessExitEcallsObserved,
    result.LastRetiredBundlePc,
    result.LastRetireSequence,
    nearbyCodeRecords,
    managedCallerPc,
    managedCallerCodeRecord,
    managedCallerNearbyCodeRecords,
    lastManagedReceiverDescriptor,
    managedTypes,
    filteredCodeRecords,
    diagnosticPcInstructionSummary,
    diagnosticPcCodeRecord,
    filteredCodeBoundaryBundles,
    filteredCodeInstructionSummary,
    managedObservations = managedBridge?.ManagedObservations,
    runtimeContextReleased = kernel.CurrentContext() is null
}, new JsonSerializerOptions { WriteIndented = true }));

return result.IsSuccess && result.ProcessExitCode.HasValue &&
    result.GcSafepointsObserved > 0 && result.ProcessExitEcallsObserved > 0 &&
    result.LastRetireSequence > 0 && kernel.CurrentContext() is null ? 0 : 6;

static bool TryParseAddress(string text, out ulong value)
{
    string digits = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? text[2..] : text;
    return ulong.TryParse(digits, System.Globalization.NumberStyles.HexNumber,
        System.Globalization.CultureInfo.InvariantCulture, out value);
}
