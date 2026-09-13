using System.Buffers.Binary;
using HybridCPU.ManagedRuntime;
using HybridCPU.Platform.Contracts;
using HybridCPU.RuntimeKernel;
using HybridCPU_ISE.NonRTL.Runtime;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.CloseToHSL.Core.Runtime.Managed;

public enum HybridCpuIseManagedImageLoadStatusV1 : byte
{
    Success = 0,
    InvalidImage = 1,
    MemoryRejected = 2,
    KernelRejected = 3,
    TypeMetadataRejected = 4,
    HeapRejected = 5,
    BootstrapRejected = 6,
    GcMetadataRejected = 7
}

/// <summary>
/// Compiler-independent input accepted by the production ISE loader. The compiler owns
/// package inspection; the ISE consumes only an already inspected image plus its exact
/// platform bootstrap contract.
/// </summary>
public sealed record HybridCpuIseManagedImageLoadRequestV1(
    ReadOnlyMemory<byte> ImageBytes,
    ulong ImageBase,
    ulong EntryAddress,
    string ImageContractDigest,
    HybridCpuImageRuntimeBootstrapDescriptorV1 RuntimeBootstrap,
    HybridCpuManagedHeapOptionsV1 HeapOptions,
    string TargetPlatformDigest,
    string NativeAbiDigest,
    string RuntimePackRevision,
    int InitialVirtualThreadId = 0,
    ulong VirtualClockTicksPerSecond = 1_000);

public sealed record HybridCpuIseManagedImageLoadResultV1(
    HybridCpuIseManagedImageLoadStatusV1 Status,
    string Reason,
    HybridCpuExecutionContextDescriptorV1? Context,
    HybridCpuManagedTypeSystemV1? TypeSystem,
    HybridCpuManagedHeapAllocatorV1? Heap,
    HybridCpuManagedStringRuntimeV1? Strings,
    HybridCpuManagedNonMovingGcV1? Gc,
    HybridCpuManagedBootstrapResultV1? Bootstrap,
    IHybridCpuIseEcallBridgeV1? EcallBridge = null,
    IReadOnlyList<HybridCpuManagedStackMapRegistrationV1>? StackMaps = null,
    IReadOnlyList<HybridCpuIseManagedInitializerBindingV1>? PendingInitializers = null,
    IReadOnlyList<HybridCpuManagedGcRootV1>? ProcessRoots = null)
{
    public bool IsSuccess => Status == HybridCpuIseManagedImageLoadStatusV1.Success;
    public bool HasImageMaterializationAuthority => IsSuccess;
    public bool HasRuntimeBootstrapAuthority => IsSuccess;
    public bool HasExecutionAuthority => false;
    public bool HasEcallAuthority => false;
}

public sealed record HybridCpuIseManagedInitializerBindingV1(
    int Order, string ModuleIdentity, string InitializerSymbol, ulong EntryAddress, ulong? TypeId,
    ulong? TypeHandle);

/// <summary>
/// Production image materialization and managed-runtime bootstrap for one explicitly
/// supplied ISE memory instance. This class does not start a CPU core and does not claim
/// an ECALL, retire, process-exit or publication capability.
/// </summary>
public sealed class HybridCpuIseManagedImageLoaderV1
{
    public HybridCpuIseManagedImageLoadResultV1 Load(
        HybridCpuIseManagedImageLoadRequestV1 request,
        Processor.MainMemoryArea memory,
        IHybridCpuRuntimeKernelV1 kernel,
        IReadOnlyDictionary<string, HybridCpuRuntimeHelperEntryV1> helpers,
        IReadOnlyDictionary<string, Func<bool>>? initializers = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(kernel);
        ArgumentNullException.ThrowIfNull(helpers);

        if (request.ImageBytes.IsEmpty || request.ImageBase == 0 ||
            request.ImageBytes.Length > int.MaxValue ||
            request.ImageBase > ulong.MaxValue - (ulong)request.ImageBytes.Length ||
            request.EntryAddress < request.ImageBase ||
            request.EntryAddress - request.ImageBase >= (ulong)request.ImageBytes.Length ||
            request.RuntimeBootstrap is null ||
            request.RuntimeBootstrap.ManagedTypes is not { Count: > 0 } ||
            request.ImageContractDigest is not { Length: 64 } ||
            request.ImageContractDigest.Any(static value => !char.IsAsciiHexDigit(value)) ||
            request.VirtualClockTicksPerSecond == 0)
            return Failure(HybridCpuIseManagedImageLoadStatusV1.InvalidImage,
                "Managed image identity, entry, type metadata or virtual clock is invalid.");

        ulong imageSize;
        try { imageSize = AlignUp((ulong)request.ImageBytes.Length, 4096); }
        catch (OverflowException) { return Failure(HybridCpuIseManagedImageLoadStatusV1.InvalidImage, "Managed image size overflows its aligned mapping."); }
        HybridCpuRestrictedMemoryLayoutV1 layout = new(request.ImageBase, imageSize,
            HybridCpuRestrictedStackV1.Base, HybridCpuRestrictedStackV1.Size,
            request.HeapOptions.BaseAddress, request.HeapOptions.SizeBytes);
        if (!layout.IsValid || layout.End > checked((ulong)memory.Length))
            return Failure(HybridCpuIseManagedImageLoadStatusV1.MemoryRejected,
                "Image, stack and managed heap must be disjoint and fully materializable in the explicitly bound ISE memory.");
        if (!memory.TryWritePhysicalRange(request.ImageBase, request.ImageBytes.Span))
            return Failure(HybridCpuIseManagedImageLoadStatusV1.MemoryRejected,
                "ISE memory rejected exact managed image materialization.");
        if (!TryPublishExecutableBundleAnnotations(request, memory, out string annotationFailure))
            return Failure(HybridCpuIseManagedImageLoadStatusV1.MemoryRejected, annotationFailure);

        HybridCpuKernelBootResultV1 boot = kernel.Boot(new(HybridCpuPlatformContractV1.ContractDigest,
            request.ImageContractDigest, request.ImageBase, imageSize, request.EntryAddress,
            HybridCpuRestrictedStackV1.Base, HybridCpuRestrictedStackV1.Size,
            request.InitialVirtualThreadId, request.VirtualClockTicksPerSecond));
        if (!boot.IsSuccess)
            return Failure(HybridCpuIseManagedImageLoadStatusV1.KernelRejected, boot.Reason);

        HybridCpuManagedTypeRegistrationV1[] registrations = request.RuntimeBootstrap.ManagedTypes
            .OrderBy(static row => row.TypeId).ToArray();
        if (registrations.Any(static row => row.TypeHandle == 0) ||
            registrations.Select(static row => row.TypeHandle).Distinct().Count() != registrations.Length)
            return Failure(HybridCpuIseManagedImageLoadStatusV1.TypeMetadataRejected,
                "Managed type registrations require unique non-zero compiler-owned TypeHandle identities.");
        var handles = registrations.ToDictionary(static row => row.TypeId, static row => row.TypeHandle);
        HybridCpuManagedTypeSystemBuildV1 typeBuild = HybridCpuManagedImageTypeLoaderV1.Load(
            request.ImageBytes, registrations, handles);
        if (!typeBuild.IsSuccess || typeBuild.TypeSystem is null)
            return Failure(HybridCpuIseManagedImageLoadStatusV1.TypeMetadataRejected, typeBuild.Reason);

        var heapMemory = new HybridCpuIseHeapMemoryV1(memory,
            request.HeapOptions.BaseAddress, request.HeapOptions.SizeBytes);
        var heap = new HybridCpuManagedHeapAllocatorV1(kernel, typeBuild.TypeSystem,
            request.HeapOptions, heapMemory);
        HybridCpuManagedHeapResultV1 heapResult = heap.Initialize();
        if (!heapResult.IsSuccess)
            return Failure(HybridCpuIseManagedImageLoadStatusV1.HeapRejected, heapResult.Reason);
        bool needsStringConcat2 = request.RuntimeBootstrap.RuntimeHelpers.Any(static helper =>
            helper.Symbol == "__hybridcpu_managed_string_concat2" && helper.Required);
        bool needsStringConcat3 = request.RuntimeBootstrap.RuntimeHelpers.Any(static helper =>
            helper.Symbol == "__hybridcpu_managed_string_concat3" && helper.Required);
        var stringTypes = typeBuild.TypeSystem.Descriptors.Where(static type =>
            type.StableIdentity == "System.String").ToArray();
        ulong? defaultStringHandle = stringTypes.Length == 1 &&
            stringTypes[0] is { Kind: HybridCpuManagedTypeKindV1.String,
                StringShape: { CharacterSizeBytes: 2, IsImmutable: true } }
            ? typeBuild.TypeSystem.TypeHandle(stringTypes[0].TypeId) : null;
        if ((needsStringConcat2 || needsStringConcat3) && defaultStringHandle is null)
            return Failure(HybridCpuIseManagedImageLoadStatusV1.TypeMetadataRejected,
                "String.Concat requires one exact image-owned immutable UTF-16 System.String descriptor.");
        var strings = new HybridCpuManagedStringRuntimeV1(typeBuild.TypeSystem, heap, defaultStringHandle);
        if (!HybridCpuIseStringEmptyBindingV1.TryInitialize(typeBuild.TypeSystem, strings,
                request.RuntimeBootstrap.ModuleInitializers, out string emptyFailure))
            return Failure(HybridCpuIseManagedImageLoadStatusV1.BootstrapRejected, emptyFailure);
        var gc = new HybridCpuManagedNonMovingGcV1(typeBuild.TypeSystem, heap,
            request.RuntimeBootstrap.ManagedAbiDigest, request.TargetPlatformDigest,
            request.NativeAbiDigest, request.RuntimePackRevision);
        HybridCpuManagedImageStackMapLoadResultV1 stackMaps = HybridCpuManagedImageStackMapLoaderV1.Load(
            request.ImageBytes, request.RuntimeBootstrap.CodeManagerRecords,
            request.RuntimeBootstrap.ManagedAbiDigest, request.TargetPlatformDigest,
            request.NativeAbiDigest, request.RuntimePackRevision);
        if (!stackMaps.IsSuccess)
            return new(HybridCpuIseManagedImageLoadStatusV1.GcMetadataRejected, stackMaps.Reason,
                boot.Context, typeBuild.TypeSystem, heap, strings, gc, null, null, null);
        IHybridCpuIseEcallBridgeV1? ecallBridge = null;
        bool needsArgumentMessage = request.RuntimeBootstrap.RuntimeHelpers.Any(static helper =>
            helper.Symbol == "__hybridcpu_managed_argument_exception_get_message" && helper.Required);
        bool needsTypeInitialization = request.RuntimeBootstrap.RuntimeHelpers.Any(static helper =>
            helper.Symbol == "__hybridcpu_managed_ensure_type_initialized" && helper.Required);
        bool needsStaticStoreInt32 = request.RuntimeBootstrap.RuntimeHelpers.Any(static helper =>
            helper.Symbol == "__hybridcpu_managed_static_store_i4" && helper.Required);
        bool needsStaticStoreReference = request.RuntimeBootstrap.RuntimeHelpers.Any(static helper =>
            helper.Symbol == "__hybridcpu_managed_static_store_ref" && helper.Required);
        bool needsStaticLoadInt32 = request.RuntimeBootstrap.RuntimeHelpers.Any(static helper =>
            helper.Symbol == "__hybridcpu_managed_static_load_i4" && helper.Required);
        bool needsStaticLoadReference = request.RuntimeBootstrap.RuntimeHelpers.Any(static helper =>
            helper.Symbol == "__hybridcpu_managed_static_load_ref" && helper.Required);
        bool needsNullCheck = request.RuntimeBootstrap.RuntimeHelpers.Any(static helper =>
            helper.Symbol == "__hybridcpu_managed_null_check" && helper.Required);
        bool needsArrayStoreInt32 = request.RuntimeBootstrap.RuntimeHelpers.Any(static helper =>
            helper.Symbol == "__hybridcpu_managed_array_store_i4" && helper.Required);
        bool needsArrayStoreInt8 = request.RuntimeBootstrap.RuntimeHelpers.Any(static helper =>
            helper.Symbol == "__hybridcpu_managed_array_store_i1" && helper.Required);
        bool needsArrayStoreInt16 = request.RuntimeBootstrap.RuntimeHelpers.Any(static helper =>
            helper.Symbol == "__hybridcpu_managed_array_store_i2" && helper.Required);
        bool needsArrayStoreReference = request.RuntimeBootstrap.RuntimeHelpers.Any(static helper =>
            helper.Symbol == "__hybridcpu_managed_array_store_ref" && helper.Required);
        bool needsArrayLoadReference = request.RuntimeBootstrap.RuntimeHelpers.Any(static helper =>
            helper.Symbol == "__hybridcpu_managed_array_load_ref" && helper.Required);
        bool needsArrayLoadInt32 = request.RuntimeBootstrap.RuntimeHelpers.Any(static helper =>
            helper.Symbol == "__hybridcpu_managed_array_load_i4" && helper.Required);
        bool needsArrayLoadUInt8 = request.RuntimeBootstrap.RuntimeHelpers.Any(static helper =>
            helper.Symbol == "__hybridcpu_managed_array_load_u1" && helper.Required);
        bool needsArrayLoadInt16 = request.RuntimeBootstrap.RuntimeHelpers.Any(static helper =>
            helper.Symbol == "__hybridcpu_managed_array_load_i2" && helper.Required);
        bool needsArrayLoadUInt16 = request.RuntimeBootstrap.RuntimeHelpers.Any(static helper =>
            helper.Symbol == "__hybridcpu_managed_array_load_u2" && helper.Required);
        bool needsArrayCopyAll = request.RuntimeBootstrap.RuntimeHelpers.Any(static helper =>
            helper.Symbol == "__hybridcpu_managed_array_copy_all" && helper.Required);
        bool needsArrayCopy = request.RuntimeBootstrap.RuntimeHelpers.Any(static helper =>
            helper.Symbol == "__hybridcpu_managed_array_copy" && helper.Required);
        bool needsIsInstance = request.RuntimeBootstrap.RuntimeHelpers.Any(static helper =>
            helper.Symbol == "__hybridcpu_managed_isinst" && helper.Required);
        bool needsInitializeArray = request.RuntimeBootstrap.RuntimeHelpers.Any(static helper =>
            helper.Symbol == "__hybridcpu_managed_initialize_array" && helper.Required);
        bool needsStringFromUtf16Array = request.RuntimeBootstrap.RuntimeHelpers.Any(static helper =>
            helper.Symbol == "__hybridcpu_managed_string_from_utf16_array" && helper.Required);
        bool needsNewArray = request.RuntimeBootstrap.RuntimeHelpers.Any(static helper =>
            helper.Symbol == "__hybridcpu_managed_newarr" && helper.Required);
        bool needsAllocateObject = request.RuntimeBootstrap.RuntimeHelpers.Any(static helper =>
            helper.Symbol == "__hybridcpu_managed_alloc" && helper.Required);
        bool needsDivideUInt32 = request.RuntimeBootstrap.RuntimeHelpers.Any(static helper =>
            helper.Symbol == "__hybridcpu_managed_divide_u4_checked" && helper.Required);
        bool needsDivideInt32 = request.RuntimeBootstrap.RuntimeHelpers.Any(static helper =>
            helper.Symbol == "__hybridcpu_managed_divide_i4_checked" && helper.Required);
        bool needsRemainderInt32 = request.RuntimeBootstrap.RuntimeHelpers.Any(static helper =>
            helper.Symbol == "__hybridcpu_managed_remainder_i4_checked" && helper.Required);
        bool needsDivideInt64 = request.RuntimeBootstrap.RuntimeHelpers.Any(static helper =>
            helper.Symbol == "__hybridcpu_managed_divide_i8_checked" && helper.Required);
        bool needsMathAbsInt64 = request.RuntimeBootstrap.RuntimeHelpers.Any(static helper =>
            helper.Symbol == "__hybridcpu_managed_math_abs_i8" && helper.Required);
        bool needsArrayEmpty = request.RuntimeBootstrap.RuntimeHelpers.Any(static helper =>
            helper.Symbol == "__hybridcpu_managed_array_empty" && helper.Required);
        bool needsArrayLength = request.RuntimeBootstrap.RuntimeHelpers.Any(static helper =>
            helper.Symbol == "__hybridcpu_managed_array_length" && helper.Required);
        bool needsStringCharacter = request.RuntimeBootstrap.RuntimeHelpers.Any(static helper =>
            helper.Symbol == "__hybridcpu_managed_string_char" && helper.Required);
        bool needsStringLength = request.RuntimeBootstrap.RuntimeHelpers.Any(static helper =>
            helper.Symbol == "__hybridcpu_managed_string_length" && helper.Required);
        bool needsArgumentNullCtor = request.RuntimeBootstrap.RuntimeHelpers.Any(static helper =>
            helper.Symbol == "__hybridcpu_managed_argument_null_ctor_param_name" && helper.Required);
        bool needsArgumentOutOfRangeCtor = request.RuntimeBootstrap.RuntimeHelpers.Any(static helper =>
            helper.Symbol == "__hybridcpu_managed_argument_out_of_range_ctor_param_name" && helper.Required);
        bool needsStringNotEquals = request.RuntimeBootstrap.RuntimeHelpers.Any(static helper =>
            helper.Symbol == "__hybridcpu_managed_string_not_equals" && helper.Required);
        bool needsArrayClear = request.RuntimeBootstrap.RuntimeHelpers.Any(static helper =>
            helper.Symbol == "__hybridcpu_managed_array_clear" && helper.Required);
        Func<ulong, HybridCpuManagedShapeResultV1>? argumentMessage = null;
        Func<ulong, ulong, HybridCpuManagedArrayStoreEcallResultV1>? argumentNullCtor = null;
        Func<ulong, ulong, HybridCpuManagedArrayStoreEcallResultV1>? argumentOutOfRangeCtor = null;
        HybridCpuManagedArgumentNullExceptionRuntimeV1? argumentNullRuntime = null;
        ulong? argumentNullTypeHandle = null, argumentRuntimeOomHandle = null, argumentRuntimeStringHandle = null;
        if (needsArgumentMessage || needsArgumentNullCtor || needsArgumentOutOfRangeCtor || needsArrayClear)
        {
            HybridCpuManagedTypeDescriptorV1? stringType = typeBuild.TypeSystem.Descriptors.SingleOrDefault(
                static type => type.StableIdentity == "System.String");
            HybridCpuManagedTypeDescriptorV1? nullType = typeBuild.TypeSystem.Descriptors.SingleOrDefault(
                static type => type.StableIdentity == "System.ArgumentNullException");
            HybridCpuManagedTypeDescriptorV1? rangeType = needsArgumentMessage || needsArgumentOutOfRangeCtor ? typeBuild.TypeSystem.Descriptors.SingleOrDefault(
                static type => type.StableIdentity == "System.ArgumentOutOfRangeException")
                : null;
            HybridCpuManagedTypeDescriptorV1? outOfMemoryType = needsArgumentNullCtor || needsArgumentOutOfRangeCtor || needsArrayClear ? typeBuild.TypeSystem.Descriptors.SingleOrDefault(
                static type => type.StableIdentity == "System.OutOfMemoryException") : null;
            ulong? stringHandle = stringType is null ? null : typeBuild.TypeSystem.TypeHandle(stringType.TypeId);
            ulong? outOfMemoryHandle = outOfMemoryType is null ? null : typeBuild.TypeSystem.TypeHandle(outOfMemoryType.TypeId);
            if (stringHandle is not ulong exactStringHandle || nullType is null ||
                (needsArgumentMessage || needsArgumentOutOfRangeCtor) && rangeType is null ||
                (needsArgumentNullCtor || needsArgumentOutOfRangeCtor || needsArrayClear) && outOfMemoryHandle is null)
                return Failure(HybridCpuIseManagedImageLoadStatusV1.TypeMetadataRejected,
                    "ArgumentNullException runtime helpers require exact String, ArgumentNullException, requested ArgumentOutOfRangeException and allocating OutOfMemoryException image types.");
            argumentNullRuntime = new HybridCpuManagedArgumentNullExceptionRuntimeV1(
                typeBuild.TypeSystem, heap, nullType.TypeId, exactStringHandle);
            argumentNullTypeHandle = typeBuild.TypeSystem.TypeHandle(nullType.TypeId);
            argumentRuntimeOomHandle = outOfMemoryHandle;
            argumentRuntimeStringHandle = exactStringHandle;
            HybridCpuManagedArgumentNullExceptionRuntimeV1? rangeRuntime = rangeType is null ? null :
                HybridCpuManagedArgumentNullExceptionRuntimeV1.ForArgumentOutOfRange(
                    typeBuild.TypeSystem, heap, rangeType.TypeId, exactStringHandle);
            if (needsArgumentMessage) argumentMessage = receiver =>
            {
                byte[]? bytes = heap.ReadObjectBytes(receiver);
                HybridCpuManagedTypeDescriptorV1? actual = bytes is { Length: >= 8 }
                    ? typeBuild.TypeSystem.ResolveTypeHandle(BitConverter.ToUInt64(bytes, 0)) : null;
                return actual?.StableIdentity switch
                {
                    "System.ArgumentNullException" => argumentNullRuntime.Message(receiver),
                    "System.ArgumentOutOfRangeException" => rangeRuntime!.Message(receiver),
                    _ => new(HybridCpuManagedShapeStatusV1.InvalidType,
                        "ArgumentException.Message ECALL receiver has no qualified exact runtime type.")
                };
            };
            if (needsArgumentNullCtor) argumentNullCtor = (receiver, parameter) =>
                Construct(argumentNullRuntime, receiver, parameter, "ArgumentNullException");
            if (needsArgumentOutOfRangeCtor) argumentOutOfRangeCtor = (receiver, parameter) =>
                Construct(rangeRuntime!, receiver, parameter, "ArgumentOutOfRangeException");

            HybridCpuManagedArrayStoreEcallResultV1 Construct(HybridCpuManagedArgumentNullExceptionRuntimeV1 runtime,
                ulong receiver, ulong parameter, string operation)
            {
                HybridCpuManagedShapeResultV1 result = runtime.Construct(receiver, parameter);
                if (result.IsSuccess) return new(true, 0, string.Empty);
                if (result.Status is not (HybridCpuManagedShapeStatusV1.HeapFailure or HybridCpuManagedShapeStatusV1.SizeOverflow))
                    return new(false, 0, result.Reason);
                HybridCpuManagedHeapResultV1 exception = heap.TryAllocate(outOfMemoryHandle!.Value);
                return exception.IsSuccess && exception.ObjectReference != 0
                    ? new(true, exception.ObjectReference, string.Empty, IsManagedThrow: true)
                    : new(false, 0, string.IsNullOrEmpty(exception.Reason)
                        ? operation + " constructor OOM allocation returned no object." : exception.Reason);
            }
        }
        if (needsArgumentMessage || needsArgumentNullCtor || needsArgumentOutOfRangeCtor || needsStringNotEquals || needsArrayClear || needsTypeInitialization || needsStaticStoreInt32 || needsStaticStoreReference ||
            needsStaticLoadInt32 || needsStaticLoadReference || needsAllocateObject || needsDivideUInt32 || needsDivideInt32 || needsRemainderInt32 || needsDivideInt64 || needsMathAbsInt64 || needsArrayEmpty || needsArrayLength || needsStringCharacter || needsStringLength || needsStringConcat2 || needsStringConcat3 ||
            needsNullCheck || needsArrayStoreInt32 || needsArrayStoreInt8 || needsArrayStoreInt16 || needsArrayStoreReference || needsArrayLoadReference || needsArrayLoadInt32 || needsArrayLoadUInt8 || needsArrayLoadInt16 || needsArrayLoadUInt16 || needsArrayCopyAll || needsArrayCopy || needsIsInstance ||
            needsInitializeArray || needsStringFromUtf16Array || needsNewArray)
        {
            var staticFields = new HybridCpuManagedStaticFieldRuntimeV1(typeBuild.TypeSystem);
            HybridCpuManagedTypeDescriptorV1? nullReferenceType = needsNullCheck || needsArrayStoreInt32 || needsArrayStoreInt8 || needsArrayStoreInt16 || needsStringFromUtf16Array ||
                needsArrayStoreReference || needsArrayLoadReference || needsArrayLoadInt32 || needsArrayLoadUInt8 || needsArrayLoadInt16 || needsArrayLoadUInt16 || needsArrayLength || needsStringCharacter || needsStringLength || needsInitializeArray
                ? typeBuild.TypeSystem.Descriptors.SingleOrDefault(static type =>
                    type.StableIdentity == HybridCpuManagedTrapMapperV1.NullReferenceExceptionIdentity)
                : null;
            ulong? nullReferenceHandle = nullReferenceType is null
                ? null : typeBuild.TypeSystem.TypeHandle(nullReferenceType.TypeId);
            if ((needsNullCheck || needsInitializeArray || needsStringFromUtf16Array || needsArrayLength || needsStringCharacter || needsStringLength) && nullReferenceHandle is null)
                return Failure(HybridCpuIseManagedImageLoadStatusV1.TypeMetadataRejected,
                    "Managed null-check requires the exact System.NullReferenceException image type.");
            HybridCpuManagedTypeDescriptorV1? indexExceptionType = needsArrayStoreInt32 || needsArrayStoreInt8 || needsArrayStoreInt16 || needsArrayStoreReference || needsArrayLoadReference || needsArrayLoadInt32 || needsArrayLoadUInt8 || needsArrayLoadInt16 || needsArrayLoadUInt16 || needsStringCharacter
                ? typeBuild.TypeSystem.Descriptors.SingleOrDefault(static type =>
                    type.StableIdentity == "System.IndexOutOfRangeException") : null;
            ulong? indexExceptionHandle = indexExceptionType is null
                ? null : typeBuild.TypeSystem.TypeHandle(indexExceptionType.TypeId);
            if ((needsArrayStoreInt32 || needsArrayStoreInt8 || needsArrayStoreInt16 || needsArrayStoreReference || needsArrayLoadReference || needsArrayLoadInt32 || needsArrayLoadUInt8 || needsArrayLoadInt16 || needsArrayLoadUInt16 || needsStringCharacter) &&
                (nullReferenceHandle is null || indexExceptionHandle is null))
                return Failure(HybridCpuIseManagedImageLoadStatusV1.TypeMetadataRejected,
                    "Managed i4 array store requires exact NullReferenceException and IndexOutOfRangeException image types.");
            HybridCpuManagedTypeDescriptorV1? mismatchExceptionType = needsArrayStoreReference
                ? typeBuild.TypeSystem.Descriptors.SingleOrDefault(static type =>
                    type.StableIdentity == "System.ArrayTypeMismatchException") : null;
            ulong? mismatchExceptionHandle = mismatchExceptionType is null
                ? null : typeBuild.TypeSystem.TypeHandle(mismatchExceptionType.TypeId);
            if (needsArrayStoreReference && mismatchExceptionHandle is null)
                return Failure(HybridCpuIseManagedImageLoadStatusV1.TypeMetadataRejected,
                    "Managed reference array store requires exact ArrayTypeMismatchException image type.");
            HybridCpuManagedTypeDescriptorV1? overflowExceptionType = needsNewArray || needsDivideInt32 || needsRemainderInt32 || needsDivideInt64 || needsMathAbsInt64
                ? typeBuild.TypeSystem.Descriptors.SingleOrDefault(static type =>
                    type.StableIdentity == "System.OverflowException") : null;
            HybridCpuManagedTypeDescriptorV1? outOfMemoryExceptionType = needsNewArray || needsAllocateObject || needsArrayEmpty || needsStringConcat2 || needsStringConcat3 || needsStringFromUtf16Array
                ? typeBuild.TypeSystem.Descriptors.SingleOrDefault(static type =>
                    type.StableIdentity == "System.OutOfMemoryException") : null;
            ulong? overflowExceptionHandle = overflowExceptionType is null ? null :
                typeBuild.TypeSystem.TypeHandle(overflowExceptionType.TypeId);
            ulong? outOfMemoryExceptionHandle = outOfMemoryExceptionType is null ? null :
                typeBuild.TypeSystem.TypeHandle(outOfMemoryExceptionType.TypeId);
            if (needsNewArray && (overflowExceptionHandle is null || outOfMemoryExceptionHandle is null))
                return Failure(HybridCpuIseManagedImageLoadStatusV1.TypeMetadataRejected,
                    "Managed newarr requires exact OverflowException and OutOfMemoryException image types.");
            if (needsAllocateObject && outOfMemoryExceptionHandle is null)
                return Failure(HybridCpuIseManagedImageLoadStatusV1.TypeMetadataRejected,
                    "Managed object allocation requires the exact OutOfMemoryException image type.");
            if (needsArrayEmpty && outOfMemoryExceptionHandle is null)
                return Failure(HybridCpuIseManagedImageLoadStatusV1.TypeMetadataRejected,
                    "Managed Array.Empty requires the exact OutOfMemoryException image type.");
            if (needsStringConcat2 && outOfMemoryExceptionHandle is null)
                return Failure(HybridCpuIseManagedImageLoadStatusV1.TypeMetadataRejected,
                    "Managed String.Concat(string,string) requires the exact OutOfMemoryException image type.");
            if (needsStringConcat3 && outOfMemoryExceptionHandle is null)
                return Failure(HybridCpuIseManagedImageLoadStatusV1.TypeMetadataRejected,
                    "Managed String.Concat(string,string,string) requires the exact OutOfMemoryException image type.");
            if (needsStringFromUtf16Array && outOfMemoryExceptionHandle is null)
                return Failure(HybridCpuIseManagedImageLoadStatusV1.TypeMetadataRejected,
                    "Managed String(char[]) requires the exact OutOfMemoryException image type.");
            HybridCpuManagedTypeDescriptorV1? divideByZeroExceptionType = needsDivideUInt32 || needsDivideInt32 || needsRemainderInt32 || needsDivideInt64
                ? typeBuild.TypeSystem.Descriptors.SingleOrDefault(static type =>
                    type.StableIdentity == "System.DivideByZeroException") : null;
            ulong? divideByZeroExceptionHandle = divideByZeroExceptionType is null ? null :
                typeBuild.TypeSystem.TypeHandle(divideByZeroExceptionType.TypeId);
            if (needsDivideUInt32 && divideByZeroExceptionHandle is null)
                return Failure(HybridCpuIseManagedImageLoadStatusV1.TypeMetadataRejected,
                    "Checked UInt32 division requires the exact DivideByZeroException image type.");
            if (needsDivideInt32 && (divideByZeroExceptionHandle is null || overflowExceptionHandle is null))
                return Failure(HybridCpuIseManagedImageLoadStatusV1.TypeMetadataRejected,
                    "Checked Int32 division requires exact DivideByZeroException and OverflowException image types.");
            if (needsRemainderInt32 && (divideByZeroExceptionHandle is null || overflowExceptionHandle is null))
                return Failure(HybridCpuIseManagedImageLoadStatusV1.TypeMetadataRejected,
                    "Checked Int32 remainder requires exact DivideByZeroException and OverflowException image types.");
            if (needsDivideInt64 && (divideByZeroExceptionHandle is null || overflowExceptionHandle is null))
                return Failure(HybridCpuIseManagedImageLoadStatusV1.TypeMetadataRejected,
                    "Checked Int64 division requires exact DivideByZeroException and OverflowException image types.");
            if (needsMathAbsInt64 && overflowExceptionHandle is null)
                return Failure(HybridCpuIseManagedImageLoadStatusV1.TypeMetadataRejected,
                    "Checked Math.Abs(Int64) requires the exact OverflowException image type.");
            ulong? CopyExceptionHandle(string identity)
            {
                HybridCpuManagedTypeDescriptorV1? descriptor = needsArrayCopyAll || needsArrayCopy
                    ? typeBuild.TypeSystem.Descriptors.SingleOrDefault(type => type.StableIdentity == identity)
                    : null;
                return descriptor is null ? null : typeBuild.TypeSystem.TypeHandle(descriptor.TypeId);
            }
            ulong? copyArgumentNullHandle = CopyExceptionHandle("System.ArgumentNullException");
            ulong? copyArgumentOutOfRangeHandle = CopyExceptionHandle("System.ArgumentOutOfRangeException");
            ulong? copyArgumentHandle = CopyExceptionHandle("System.ArgumentException");
            ulong? copyArrayTypeMismatchHandle = CopyExceptionHandle("System.ArrayTypeMismatchException");
            if ((needsArrayCopyAll || needsArrayCopy) && (copyArgumentNullHandle is null || copyArgumentOutOfRangeHandle is null ||
                copyArgumentHandle is null || copyArrayTypeMismatchHandle is null))
                return Failure(HybridCpuIseManagedImageLoadStatusV1.TypeMetadataRejected,
                    "Array.Copy(source,destination,length) requires exact ArgumentNullException, ArgumentOutOfRangeException, ArgumentException and ArrayTypeMismatchException image types.");
            var arrays = new HybridCpuManagedArrayRuntimeV1(typeBuild.TypeSystem, heap);
            var typeTests = new HybridCpuManagedTypeTestRuntimeV1(typeBuild.TypeSystem, heap);
            foreach (HybridCpuManagedFieldDataRegistrationV1 fieldData in request.RuntimeBootstrap.FieldData ?? [])
                if (!arrays.RegisterFieldData(fieldData.DataHandle, fieldData.Data))
                    return Failure(HybridCpuIseManagedImageLoadStatusV1.TypeMetadataRejected,
                        "Managed FieldRVA registration is duplicate, empty or outside deterministic bounds.");
            HybridCpuManagedArrayStoreEcallResultV1 ArrayClear(ulong array)
            {
                HybridCpuManagedShapeResultV1 cleared = arrays.Clear(array);
                if (cleared.IsSuccess) return new(true, 0, string.Empty);
                if (cleared.Status != HybridCpuManagedShapeStatusV1.NullReference)
                    return new(false, 0, cleared.Reason);
                HybridCpuManagedHeapResultV1 receiver = heap.TryAllocate(argumentNullTypeHandle!.Value);
                if (!receiver.IsSuccess || receiver.ObjectReference == 0) return Oom(receiver.Reason);
                HybridCpuManagedShapeResultV1 parameter = strings.MaterializeLiteral(argumentRuntimeStringHandle!.Value, "array");
                if (!parameter.IsSuccess || parameter.ObjectReference == 0) return Oom(parameter.Reason);
                HybridCpuManagedShapeResultV1 constructed = argumentNullRuntime!.Construct(receiver.ObjectReference, parameter.ObjectReference);
                if (constructed.IsSuccess)
                    return new(true, receiver.ObjectReference, string.Empty, IsManagedThrow: true);
                return constructed.Status is HybridCpuManagedShapeStatusV1.HeapFailure or HybridCpuManagedShapeStatusV1.SizeOverflow
                    ? Oom(constructed.Reason) : new(false, 0, constructed.Reason);

                HybridCpuManagedArrayStoreEcallResultV1 Oom(string reason)
                {
                    HybridCpuManagedHeapResultV1 exception = heap.TryAllocate(argumentRuntimeOomHandle!.Value);
                    return exception.IsSuccess && exception.ObjectReference != 0
                        ? new(true, exception.ObjectReference, string.Empty, IsManagedThrow: true)
                        : new(false, 0, string.IsNullOrEmpty(reason) ? "Array.Clear exception allocation failed." : reason);
                }
            }
            HybridCpuManagedArrayStoreEcallResultV1 ArrayStoreInt32(ulong array, int index, int value)
            {
                HybridCpuManagedShapeResultV1 store = arrays.StoreInt32(array, index, value);
                if (store.IsSuccess) return new(true, 0, string.Empty);
                ulong? exceptionHandle = store.Status switch
                {
                    HybridCpuManagedShapeStatusV1.NullReference => nullReferenceHandle,
                    HybridCpuManagedShapeStatusV1.BoundsViolation => indexExceptionHandle,
                    _ => null
                };
                if (exceptionHandle is not ulong exactHandle)
                    return new(false, 0, store.Reason);
                HybridCpuManagedHeapResultV1 allocation = heap.TryAllocate(exactHandle);
                return allocation.IsSuccess && allocation.ObjectReference != 0
                    ? new(true, allocation.ObjectReference, string.Empty)
                    : new(false, 0, string.IsNullOrEmpty(allocation.Reason)
                        ? "Managed array exception allocation returned no object." : allocation.Reason);
            }
            HybridCpuManagedArrayStoreEcallResultV1 ArrayStoreInt8(ulong array, int index, int value)
            {
                HybridCpuManagedShapeResultV1 store = arrays.StoreInt8(array, index, value);
                if (store.IsSuccess) return new(true, 0, string.Empty);
                ulong? exceptionHandle = store.Status switch { HybridCpuManagedShapeStatusV1.NullReference => nullReferenceHandle, HybridCpuManagedShapeStatusV1.BoundsViolation => indexExceptionHandle, _ => null };
                if (exceptionHandle is not ulong exactHandle) return new(false, 0, store.Reason);
                HybridCpuManagedHeapResultV1 allocation = heap.TryAllocate(exactHandle);
                return allocation.IsSuccess && allocation.ObjectReference != 0 ? new(true, allocation.ObjectReference, string.Empty) : new(false, 0, allocation.Reason);
            }
            HybridCpuManagedArrayStoreEcallResultV1 ArrayStoreInt16(ulong array, int index, int value)
            {
                HybridCpuManagedShapeResultV1 store = arrays.StoreInt16(array, index, value);
                if (store.IsSuccess) return new(true, 0, string.Empty);
                ulong? exceptionHandle = store.Status switch
                {
                    HybridCpuManagedShapeStatusV1.NullReference => nullReferenceHandle,
                    HybridCpuManagedShapeStatusV1.BoundsViolation => indexExceptionHandle,
                    _ => null
                };
                if (exceptionHandle is not ulong exactHandle) return new(false, 0, store.Reason);
                HybridCpuManagedHeapResultV1 allocation = heap.TryAllocate(exactHandle);
                return allocation.IsSuccess && allocation.ObjectReference != 0
                    ? new(true, allocation.ObjectReference, string.Empty, IsManagedThrow: true)
                    : new(false, 0, string.IsNullOrEmpty(allocation.Reason)
                        ? "Managed i2 array-store exception allocation returned no object." : allocation.Reason);
            }
            HybridCpuManagedArrayStoreEcallResultV1 ArrayStoreReference(ulong array, int index, ulong value)
            {
                HybridCpuManagedShapeResultV1 store = arrays.StoreReference(array, index, value);
                if (store.IsSuccess) return new(true, 0, string.Empty);
                ulong? exceptionHandle = store.Status switch
                {
                    HybridCpuManagedShapeStatusV1.NullReference => nullReferenceHandle,
                    HybridCpuManagedShapeStatusV1.BoundsViolation => indexExceptionHandle,
                    HybridCpuManagedShapeStatusV1.ArrayTypeMismatch => mismatchExceptionHandle,
                    _ => null
                };
                if (exceptionHandle is not ulong exactHandle) return new(false, 0, store.Reason);
                HybridCpuManagedHeapResultV1 allocation = heap.TryAllocate(exactHandle);
                return allocation.IsSuccess && allocation.ObjectReference != 0
                    ? new(true, allocation.ObjectReference, string.Empty)
                    : new(false, 0, string.IsNullOrEmpty(allocation.Reason)
                        ? "Managed reference-array exception allocation returned no object." : allocation.Reason);
            }
            HybridCpuManagedArrayStoreEcallResultV1 ArrayLoadReference(ulong array, int index)
            {
                HybridCpuManagedShapeResultV1 load = arrays.LoadReference(array, index);
                if (load.IsSuccess)
                    return new(true, unchecked((ulong)load.ScalarValue), string.Empty);
                ulong? exceptionHandle = load.Status switch
                {
                    HybridCpuManagedShapeStatusV1.NullReference => nullReferenceHandle,
                    HybridCpuManagedShapeStatusV1.BoundsViolation => indexExceptionHandle,
                    _ => null
                };
                if (exceptionHandle is not ulong exactHandle) return new(false, 0, load.Reason);
                HybridCpuManagedHeapResultV1 allocation = heap.TryAllocate(exactHandle);
                return allocation.IsSuccess && allocation.ObjectReference != 0
                    ? new(true, allocation.ObjectReference, string.Empty, IsManagedThrow: true)
                    : new(false, 0, string.IsNullOrEmpty(allocation.Reason)
                        ? "Managed reference-array load exception allocation returned no object." : allocation.Reason);
            }
            HybridCpuManagedArrayStoreEcallResultV1 ArrayLoadInt32(ulong array, int index)
            {
                HybridCpuManagedShapeResultV1 load = arrays.LoadInt32(array, index);
                if (load.IsSuccess)
                    return new(true, unchecked((ulong)load.ScalarValue), string.Empty);
                ulong? exceptionHandle = load.Status switch
                {
                    HybridCpuManagedShapeStatusV1.NullReference => nullReferenceHandle,
                    HybridCpuManagedShapeStatusV1.BoundsViolation => indexExceptionHandle,
                    _ => null
                };
                if (exceptionHandle is not ulong exactHandle) return new(false, 0, load.Reason);
                HybridCpuManagedHeapResultV1 allocation = heap.TryAllocate(exactHandle);
                return allocation.IsSuccess && allocation.ObjectReference != 0
                    ? new(true, allocation.ObjectReference, string.Empty, IsManagedThrow: true)
                    : new(false, 0, string.IsNullOrEmpty(allocation.Reason)
                        ? "Managed i4 array-load exception allocation returned no object." : allocation.Reason);
            }
            HybridCpuManagedArrayStoreEcallResultV1 ArrayLoadUInt8(ulong array, int index)
            {
                HybridCpuManagedShapeResultV1 load = arrays.LoadUInt8(array, index);
                if (load.IsSuccess)
                    return new(true, unchecked((ulong)load.ScalarValue), string.Empty);
                ulong? exceptionHandle = load.Status switch
                {
                    HybridCpuManagedShapeStatusV1.NullReference => nullReferenceHandle,
                    HybridCpuManagedShapeStatusV1.BoundsViolation => indexExceptionHandle,
                    _ => null
                };
                if (exceptionHandle is not ulong exactHandle) return new(false, 0, load.Reason);
                HybridCpuManagedHeapResultV1 allocation = heap.TryAllocate(exactHandle);
                return allocation.IsSuccess && allocation.ObjectReference != 0
                    ? new(true, allocation.ObjectReference, string.Empty, IsManagedThrow: true)
                    : new(false, 0, string.IsNullOrEmpty(allocation.Reason)
                        ? "Managed u1 array-load exception allocation returned no object." : allocation.Reason);
            }
            HybridCpuManagedArrayStoreEcallResultV1 ArrayLoadInt16(ulong array, int index)
            {
                HybridCpuManagedShapeResultV1 load = arrays.LoadInt16(array, index);
                if (load.IsSuccess)
                    return new(true, unchecked((ulong)(long)load.ScalarValue), string.Empty);
                ulong? exceptionHandle = load.Status switch
                {
                    HybridCpuManagedShapeStatusV1.NullReference => nullReferenceHandle,
                    HybridCpuManagedShapeStatusV1.BoundsViolation => indexExceptionHandle,
                    _ => null
                };
                if (exceptionHandle is not ulong exactHandle) return new(false, 0, load.Reason);
                HybridCpuManagedHeapResultV1 allocation = heap.TryAllocate(exactHandle);
                return allocation.IsSuccess && allocation.ObjectReference != 0
                    ? new(true, allocation.ObjectReference, string.Empty, IsManagedThrow: true)
                    : new(false, 0, string.IsNullOrEmpty(allocation.Reason)
                        ? "Managed i2 array-load exception allocation returned no object." : allocation.Reason);
            }
            HybridCpuManagedArrayStoreEcallResultV1 ArrayLoadUInt16(ulong array, int index)
            {
                HybridCpuManagedShapeResultV1 load = arrays.LoadUInt16(array, index);
                if (load.IsSuccess)
                    return new(true, unchecked((ulong)(uint)load.ScalarValue), string.Empty);
                ulong? exceptionHandle = load.Status switch
                {
                    HybridCpuManagedShapeStatusV1.NullReference => nullReferenceHandle,
                    HybridCpuManagedShapeStatusV1.BoundsViolation => indexExceptionHandle,
                    _ => null
                };
                if (exceptionHandle is not ulong exactHandle) return new(false, 0, load.Reason);
                HybridCpuManagedHeapResultV1 allocation = heap.TryAllocate(exactHandle);
                return allocation.IsSuccess && allocation.ObjectReference != 0
                    ? new(true, allocation.ObjectReference, string.Empty, IsManagedThrow: true)
                    : new(false, 0, string.IsNullOrEmpty(allocation.Reason)
                        ? "Managed u2 array-load exception allocation returned no object." : allocation.Reason);
            }
            HybridCpuManagedArrayStoreEcallResultV1 ArrayCopyAll(ulong source, ulong destination, int length)
                => ArrayCopy(source, 0, destination, 0, length);
            HybridCpuManagedArrayStoreEcallResultV1 ArrayCopy(ulong source, int sourceIndex, ulong destination, int destinationIndex, int length)
            {
                HybridCpuManagedShapeResultV1 copy = arrays.Copy(source, sourceIndex, destination, destinationIndex, length);
                if (copy.IsSuccess) return new(true, 0, string.Empty);
                ulong? exceptionHandle = copy.Status switch
                {
                    HybridCpuManagedShapeStatusV1.NullReference => copyArgumentNullHandle,
                    HybridCpuManagedShapeStatusV1.NegativeLength => copyArgumentOutOfRangeHandle,
                    HybridCpuManagedShapeStatusV1.BoundsViolation or HybridCpuManagedShapeStatusV1.SizeOverflow => copyArgumentHandle,
                    HybridCpuManagedShapeStatusV1.ArrayTypeMismatch => copyArrayTypeMismatchHandle,
                    _ => null
                };
                if (exceptionHandle is not ulong exactHandle) return new(false, 0, copy.Reason);
                HybridCpuManagedHeapResultV1 allocation = heap.TryAllocate(exactHandle);
                return allocation.IsSuccess && allocation.ObjectReference != 0
                    ? new(true, allocation.ObjectReference, string.Empty, IsManagedThrow: true)
                    : new(false, 0, string.IsNullOrEmpty(allocation.Reason)
                        ? "Managed Array.Copy exception allocation returned no object." : allocation.Reason);
            }
            HybridCpuManagedArrayStoreEcallResultV1 IsInstance(ulong receiver, ulong targetTypeHandle)
            {
                HybridCpuManagedShapeResultV1 result = typeTests.IsInstance(receiver, targetTypeHandle);
                return result.IsSuccess
                    ? new(true, result.ObjectReference, string.Empty)
                    : new(false, 0, result.Reason);
            }
            HybridCpuManagedArrayStoreEcallResultV1 InitializeArray(ulong array, ulong dataHandle)
            {
                HybridCpuManagedShapeResultV1 initialized = arrays.InitializeArray(array, dataHandle);
                if (initialized.IsSuccess) return new(true, 0, string.Empty);
                if (initialized.Status != HybridCpuManagedShapeStatusV1.NullReference)
                    return new(false, 0, initialized.Reason);
                if (nullReferenceHandle is not ulong exactHandle) return new(false, 0,
                    "InitializeArray null mapping lacks NullReferenceException type handle.");
                HybridCpuManagedHeapResultV1 allocation = heap.TryAllocate(exactHandle);
                return allocation.IsSuccess && allocation.ObjectReference != 0
                    ? new(true, allocation.ObjectReference, string.Empty)
                    : new(false, 0, allocation.Reason);
            }
            HybridCpuManagedArrayStoreEcallResultV1 StringFromUtf16Array(ulong typeHandle, ulong array)
            {
                HybridCpuManagedShapeResultV1 result = strings.FromCharArray(typeHandle, array);
                if (result.IsSuccess && result.ObjectReference != 0)
                    return new(true, result.ObjectReference, string.Empty);
                ulong? exceptionHandle = result.Status switch
                {
                    HybridCpuManagedShapeStatusV1.NullReference => nullReferenceHandle,
                    HybridCpuManagedShapeStatusV1.SizeOverflow or HybridCpuManagedShapeStatusV1.HeapFailure => outOfMemoryExceptionHandle,
                    _ => null
                };
                if (exceptionHandle is not ulong exactHandle) return new(false, 0, result.Reason);
                HybridCpuManagedHeapResultV1 exception = heap.TryAllocate(exactHandle);
                return exception.IsSuccess && exception.ObjectReference != 0
                    ? new(true, exception.ObjectReference, string.Empty, IsManagedThrow: true)
                    : new(false, 0, string.IsNullOrEmpty(exception.Reason)
                        ? "Managed String(char[]) exception allocation returned no object." : exception.Reason);
            }
            HybridCpuManagedArrayStoreEcallResultV1 NewArray(ulong typeHandle, int length)
            {
                HybridCpuManagedShapeResultV1 allocation = arrays.NewArray(typeHandle, length);
                if (allocation.IsSuccess && allocation.ObjectReference != 0)
                    return new(true, allocation.ObjectReference, string.Empty);
                ulong? exceptionHandle = allocation.Status switch
                {
                    HybridCpuManagedShapeStatusV1.NegativeLength or HybridCpuManagedShapeStatusV1.SizeOverflow =>
                        overflowExceptionHandle,
                    HybridCpuManagedShapeStatusV1.HeapFailure => outOfMemoryExceptionHandle,
                    _ => null
                };
                if (exceptionHandle is not ulong exactHandle) return new(false, 0, allocation.Reason);
                HybridCpuManagedHeapResultV1 exception = heap.TryAllocate(exactHandle);
                return exception.IsSuccess && exception.ObjectReference != 0
                    ? new(true, exception.ObjectReference, string.Empty, IsManagedThrow: true)
                    : new(false, 0, string.IsNullOrEmpty(exception.Reason)
                        ? "Managed newarr exception allocation returned no object." : exception.Reason);
            }
            HybridCpuManagedArrayStoreEcallResultV1 AllocateObject(ulong typeHandle)
            {
                HybridCpuManagedHeapResultV1 allocation = heap.TryAllocate(typeHandle);
                if (allocation.IsSuccess && allocation.ObjectReference != 0)
                    return new(true, allocation.ObjectReference, string.Empty);
                if (allocation.Status == HybridCpuManagedHeapStatusV1.InvalidTypeDescriptor)
                    return new(false, 0, allocation.Reason);
                HybridCpuManagedHeapResultV1 exception = heap.TryAllocate(outOfMemoryExceptionHandle!.Value);
                return exception.IsSuccess && exception.ObjectReference != 0
                    ? new(true, exception.ObjectReference, string.Empty, IsManagedThrow: true)
                    : new(false, 0, string.IsNullOrEmpty(exception.Reason)
                        ? "Managed allocation OOM exception returned no object." : exception.Reason);
            }
            HybridCpuManagedArrayStoreEcallResultV1 ArrayEmpty(ulong typeHandle)
            {
                HybridCpuManagedShapeResultV1 allocation = arrays.Empty(typeHandle);
                if (allocation.IsSuccess && allocation.ObjectReference != 0)
                    return new(true, allocation.ObjectReference, string.Empty);
                if (allocation.Status == HybridCpuManagedShapeStatusV1.InvalidType)
                    return new(false, 0, allocation.Reason);
                HybridCpuManagedHeapResultV1 exception = heap.TryAllocate(outOfMemoryExceptionHandle!.Value);
                return exception.IsSuccess && exception.ObjectReference != 0
                    ? new(true, exception.ObjectReference, string.Empty, IsManagedThrow: true)
                    : new(false, 0, string.IsNullOrEmpty(exception.Reason)
                        ? "Managed Array.Empty OOM exception returned no object." : exception.Reason);
            }
            HybridCpuManagedArrayStoreEcallResultV1 ArrayLength(ulong array)
            {
                HybridCpuManagedShapeResultV1 length = arrays.Length(array);
                if (length.IsSuccess) return new(true, unchecked((ulong)length.ScalarValue), string.Empty);
                if (length.Status != HybridCpuManagedShapeStatusV1.NullReference)
                    return new(false, 0, length.Reason);
                HybridCpuManagedHeapResultV1 exception = heap.TryAllocate(nullReferenceHandle!.Value);
                return exception.IsSuccess && exception.ObjectReference != 0
                    ? new(true, exception.ObjectReference, string.Empty, IsManagedThrow: true)
                    : new(false, 0, string.IsNullOrEmpty(exception.Reason)
                        ? "Managed array-length NRE allocation returned no object." : exception.Reason);
            }
            HybridCpuManagedArrayStoreEcallResultV1 StringCharacter(ulong reference, int index)
            {
                HybridCpuManagedShapeResultV1 character = strings.Character(reference, index);
                if (character.IsSuccess) return new(true, unchecked((ulong)character.ScalarValue), string.Empty);
                ulong? exceptionHandle = character.Status switch { HybridCpuManagedShapeStatusV1.NullReference => nullReferenceHandle, HybridCpuManagedShapeStatusV1.BoundsViolation => indexExceptionHandle, _ => null };
                if (exceptionHandle is not ulong exactHandle) return new(false, 0, character.Reason);
                HybridCpuManagedHeapResultV1 exception = heap.TryAllocate(exactHandle);
                return exception.IsSuccess && exception.ObjectReference != 0 ? new(true, exception.ObjectReference, string.Empty, IsManagedThrow: true) : new(false, 0, exception.Reason);
            }
            HybridCpuManagedArrayStoreEcallResultV1 StringLength(ulong reference)
            {
                HybridCpuManagedShapeResultV1 length = strings.Length(reference);
                if (length.IsSuccess) return new(true, unchecked((ulong)length.ScalarValue), string.Empty);
                if (length.Status != HybridCpuManagedShapeStatusV1.NullReference)
                    return new(false, 0, length.Reason);
                HybridCpuManagedHeapResultV1 exception = heap.TryAllocate(nullReferenceHandle!.Value);
                return exception.IsSuccess && exception.ObjectReference != 0
                    ? new(true, exception.ObjectReference, string.Empty, IsManagedThrow: true)
                    : new(false, 0, string.IsNullOrEmpty(exception.Reason)
                        ? "Managed string-length NRE allocation returned no object." : exception.Reason);
            }
            HybridCpuManagedArrayStoreEcallResultV1 StringConcat2(ulong first, ulong second)
            {
                HybridCpuManagedShapeResultV1 concatenated = strings.Concat2(first, second);
                if (concatenated.IsSuccess && concatenated.ObjectReference != 0)
                    return new(true, concatenated.ObjectReference, string.Empty);
                if (concatenated.Status is not (HybridCpuManagedShapeStatusV1.SizeOverflow or
                    HybridCpuManagedShapeStatusV1.HeapFailure))
                    return new(false, 0, concatenated.Reason);
                HybridCpuManagedHeapResultV1 exception = heap.TryAllocate(outOfMemoryExceptionHandle!.Value);
                return exception.IsSuccess && exception.ObjectReference != 0
                    ? new(true, exception.ObjectReference, string.Empty, IsManagedThrow: true)
                    : new(false, 0, string.IsNullOrEmpty(exception.Reason)
                        ? "Managed string-concat2 OOM exception returned no object." : exception.Reason);
            }
            HybridCpuManagedArrayStoreEcallResultV1 StringConcat3(ulong first, ulong second, ulong third)
            {
                HybridCpuManagedShapeResultV1 concatenated = strings.Concat3(first, second, third);
                if (concatenated.IsSuccess && concatenated.ObjectReference != 0)
                    return new(true, concatenated.ObjectReference, string.Empty);
                if (concatenated.Status is not (HybridCpuManagedShapeStatusV1.SizeOverflow or
                    HybridCpuManagedShapeStatusV1.HeapFailure))
                    return new(false, 0, concatenated.Reason);
                HybridCpuManagedHeapResultV1 exception = heap.TryAllocate(outOfMemoryExceptionHandle!.Value);
                return exception.IsSuccess && exception.ObjectReference != 0
                    ? new(true, exception.ObjectReference, string.Empty, IsManagedThrow: true)
                    : new(false, 0, string.IsNullOrEmpty(exception.Reason)
                        ? "Managed string-concat3 OOM exception returned no object." : exception.Reason);
            }
            HybridCpuManagedArrayStoreEcallResultV1 DivideUInt32(uint numerator, uint denominator)
            {
                if (denominator != 0)
                    return new(true, numerator / denominator, string.Empty);
                HybridCpuManagedHeapResultV1 exception = heap.TryAllocate(divideByZeroExceptionHandle!.Value);
                return exception.IsSuccess && exception.ObjectReference != 0
                    ? new(true, exception.ObjectReference, string.Empty, IsManagedThrow: true)
                    : new(false, 0, string.IsNullOrEmpty(exception.Reason)
                        ? "DivideByZeroException allocation returned no object." : exception.Reason);
            }
            HybridCpuManagedArrayStoreEcallResultV1 DivideInt32(int numerator, int denominator)
            {
                ulong? exceptionHandle = denominator == 0 ? divideByZeroExceptionHandle :
                    numerator == int.MinValue && denominator == -1 ? overflowExceptionHandle : null;
                if (exceptionHandle is null)
                    return new(true, unchecked((ulong)(long)(numerator / denominator)), string.Empty);
                HybridCpuManagedHeapResultV1 exception = heap.TryAllocate(exceptionHandle.Value);
                return exception.IsSuccess && exception.ObjectReference != 0
                    ? new(true, exception.ObjectReference, string.Empty, IsManagedThrow: true)
                    : new(false, 0, string.IsNullOrEmpty(exception.Reason)
                        ? "Managed Int32 division exception allocation returned no object." : exception.Reason);
            }
            HybridCpuManagedArrayStoreEcallResultV1 RemainderInt32(int numerator, int denominator)
            {
                ulong? exceptionHandle = denominator == 0 ? divideByZeroExceptionHandle :
                    numerator == int.MinValue && denominator == -1 ? overflowExceptionHandle : null;
                if (exceptionHandle is null)
                    return new(true, unchecked((ulong)(long)(numerator % denominator)), string.Empty);
                HybridCpuManagedHeapResultV1 exception = heap.TryAllocate(exceptionHandle.Value);
                return exception.IsSuccess && exception.ObjectReference != 0
                    ? new(true, exception.ObjectReference, string.Empty, IsManagedThrow: true)
                    : new(false, 0, string.IsNullOrEmpty(exception.Reason)
                        ? "Managed Int32 remainder exception allocation returned no object." : exception.Reason);
            }
            HybridCpuManagedArrayStoreEcallResultV1 DivideInt64(long numerator, long denominator)
            {
                ulong? exceptionHandle = denominator == 0 ? divideByZeroExceptionHandle :
                    numerator == long.MinValue && denominator == -1 ? overflowExceptionHandle : null;
                if (exceptionHandle is null)
                    return new(true, unchecked((ulong)(numerator / denominator)), string.Empty);
                HybridCpuManagedHeapResultV1 exception = heap.TryAllocate(exceptionHandle.Value);
                return exception.IsSuccess && exception.ObjectReference != 0
                    ? new(true, exception.ObjectReference, string.Empty, IsManagedThrow: true)
                    : new(false, 0, string.IsNullOrEmpty(exception.Reason)
                        ? "Managed Int64 division exception allocation returned no object." : exception.Reason);
            }
            HybridCpuManagedArrayStoreEcallResultV1 MathAbsInt64(long value)
            {
                if (value != long.MinValue)
                    return new(true, unchecked((ulong)(value < 0 ? -value : value)), string.Empty);
                HybridCpuManagedHeapResultV1 exception = heap.TryAllocate(overflowExceptionHandle!.Value);
                return exception.IsSuccess && exception.ObjectReference != 0
                    ? new(true, exception.ObjectReference, string.Empty, IsManagedThrow: true)
                    : new(false, 0, string.IsNullOrEmpty(exception.Reason)
                        ? "Managed Int64 Abs overflow exception allocation returned no object." : exception.Reason);
            }
            ecallBridge = new HybridCpuIseManagedEcallBridgeV1(kernel, argumentMessage,
                needsTypeInitialization ? handle =>
                {
                    HybridCpuManagedTypeDescriptorV1? type = typeBuild.TypeSystem.ResolveTypeHandle(handle);
                    if (type is null)
                        return new(HybridCpuManagedShapeStatusV1.InvalidType,
                            "Type-initialization ECALL requires an exact registered type handle.");
                    return typeBuild.TypeSystem.InitializationState(type.TypeId) ==
                        HybridCpuManagedTypeInitializationStateV1.Initialized
                        ? new(HybridCpuManagedShapeStatusV1.Success, string.Empty)
                        : new(HybridCpuManagedShapeStatusV1.InitializationFailed,
                            "Type initializer was not completed successfully before guest execution.");
                } : null,
                needsStaticStoreInt32 ? staticFields.StoreInt32 : null,
                needsStaticStoreReference ? staticFields.StoreReference : null,
                needsStaticLoadInt32 ? staticFields.LoadInt32 : null,
                needsStaticLoadReference ? staticFields.LoadReference : null,
                needsNullCheck ? () =>
                {
                    HybridCpuManagedHeapResultV1 allocation = heap.TryAllocate(nullReferenceHandle!.Value);
                    return allocation.IsSuccess && allocation.ObjectReference != 0
                        ? new(HybridCpuManagedShapeStatusV1.Success, string.Empty, allocation.ObjectReference)
                        : new(HybridCpuManagedShapeStatusV1.HeapFailure,
                            string.IsNullOrEmpty(allocation.Reason)
                                ? "NullReferenceException allocation returned no object." : allocation.Reason);
                } : null,
                needsArrayStoreInt32 ? ArrayStoreInt32 : null,
                needsArrayStoreInt8 ? ArrayStoreInt8 : null,
                needsArrayStoreInt16 ? ArrayStoreInt16 : null,
                needsArrayStoreReference ? ArrayStoreReference : null,
                needsArrayLoadReference ? ArrayLoadReference : null,
                needsArrayLoadInt32 ? ArrayLoadInt32 : null,
                needsArrayLoadUInt8 ? ArrayLoadUInt8 : null,
                needsArrayLoadInt16 ? ArrayLoadInt16 : null,
                needsArrayLoadUInt16 ? ArrayLoadUInt16 : null,
                needsArrayCopyAll ? ArrayCopyAll : null,
                needsIsInstance ? IsInstance : null,
                needsStringCharacter ? StringCharacter : null,
                needsStringConcat2 ? StringConcat2 : null,
                needsStringConcat3 ? StringConcat3 : null,
                needsStringLength ? StringLength : null,
                needsInitializeArray ? InitializeArray : null,
                needsStringFromUtf16Array ? StringFromUtf16Array : null,
                needsNewArray ? NewArray : null,
                needsAllocateObject ? AllocateObject : null,
                needsArrayEmpty ? ArrayEmpty : null,
                needsArrayLength ? ArrayLength : null,
                needsDivideUInt32 ? DivideUInt32 : null,
                needsDivideInt32 ? DivideInt32 : null,
                needsRemainderInt32 ? RemainderInt32 : null,
                needsDivideInt64 ? DivideInt64 : null,
                needsMathAbsInt64 ? MathAbsInt64 : null,
                arrayCopy: needsArrayCopy ? ArrayCopy : null,
                readGuestMemory: needsArrayCopy ? (address, count) =>
                {
                    if (count != HybridCpuManagedRuntimeEcallContractV1.ArrayCopyArgumentBlockBytes ||
                        address > ulong.MaxValue - (ulong)count) return null;
                    byte[] bytes = new byte[count];
                    return memory.TryReadPhysicalRange(address, bytes) ? bytes : null;
                } : null,
                argumentNullCtorParamName: argumentNullCtor,
                argumentOutOfRangeCtorParamName: argumentOutOfRangeCtor,
                stringNotEquals: needsStringNotEquals ? (first, second) =>
                {
                    HybridCpuManagedShapeResultV1 result = strings.AreNotEqual(first, second);
                    return result.IsSuccess
                        ? new(true, unchecked((ulong)(uint)result.ScalarValue), string.Empty)
                        : new(false, 0, result.Reason);
                } : null,
                arrayClear: needsArrayClear ? ArrayClear : null);
        }
        ecallBridge ??= new HybridCpuIseManagedEcallBridgeV1(kernel);
        var pendingInitializers = new List<HybridCpuIseManagedInitializerBindingV1>();
        var imageInitializerBindings = new Dictionary<string, Func<bool>>(StringComparer.Ordinal);
        foreach (HybridCpuModuleInitializerRegistrationV1 registration in request.RuntimeBootstrap.ModuleInitializers)
        {
            HybridCpuCodeManagerRegistrationV1[] matches = request.RuntimeBootstrap.CodeManagerRecords
                .Where(record => string.Equals(record.MethodIdentity, registration.InitializerSymbol, StringComparison.Ordinal))
                .ToArray();
            if (matches.Length != 1 || matches[0].CodeStartOffsetBytes < 0 ||
                (ulong)matches[0].CodeStartOffsetBytes > ulong.MaxValue - request.ImageBase)
                return new(HybridCpuIseManagedImageLoadStatusV1.BootstrapRejected,
                    $"Initializer '{registration.InitializerSymbol}' does not resolve to one exact image-owned code-manager range.",
                    boot.Context, typeBuild.TypeSystem, heap, strings, gc, null, ecallBridge, stackMaps.Registrations);
            ulong entryAddress = request.ImageBase + (ulong)matches[0].CodeStartOffsetBytes;
            ulong? initializerTypeHandle = registration.TypeId is ulong initializerTypeId
                ? registrations.SingleOrDefault(row => row.TypeId == initializerTypeId)?.TypeHandle
                : null;
            if (registration.TypeId.HasValue && initializerTypeHandle is not > 0)
                return new(HybridCpuIseManagedImageLoadStatusV1.BootstrapRejected,
                    $"Initializer '{registration.InitializerSymbol}' has no exact non-zero image TypeHandle.",
                    boot.Context, typeBuild.TypeSystem, heap, strings, gc, null, ecallBridge, stackMaps.Registrations);
            pendingInitializers.Add(new(registration.Order, registration.ModuleIdentity,
                registration.InitializerSymbol, entryAddress, registration.TypeId, initializerTypeHandle));
            imageInitializerBindings.Add(registration.InitializerSymbol,
                static () => throw new InvalidOperationException("Deferred image initializer must execute on the ISE CPU."));
        }
        HybridCpuManagedBootstrapResultV1 bootstrap = new HybridCpuManagedBootstrapRuntimeV1(
             request.RuntimeBootstrap.ManagedAbiDigest).Bootstrap(request.RuntimeBootstrap, kernel,
             helpers, typeBuild.TypeSystem, initializers ?? imageInitializerBindings, strings,
             executeInitializers: initializers is not null);
        if (!bootstrap.IsSuccess)
            return new(HybridCpuIseManagedImageLoadStatusV1.BootstrapRejected, bootstrap.Reason,
                boot.Context, typeBuild.TypeSystem, heap, strings, gc, bootstrap, ecallBridge, stackMaps.Registrations);
        foreach (HybridCpuManagedStringLiteralRegistrationV1 literal in request.RuntimeBootstrap.StringLiterals ?? [])
        {
            string expectedLiteralIdentity = "__hybridcpu_managed_string_literal_" + literal.LiteralHandle.ToString("x16");
            string expectedRootIdentity = "__hybridcpu_managed_string_literal_root_" + literal.LiteralHandle.ToString("x16");
            HybridCpuStaticRootRegistrationV1? root = request.RuntimeBootstrap.StaticRoots.SingleOrDefault(
                candidate => string.Equals(candidate.Identity, expectedRootIdentity, StringComparison.Ordinal));
            ulong? reference = strings.ResolveLiteral(literal.LiteralHandle);
            if (!string.Equals(literal.Identity, expectedLiteralIdentity, StringComparison.Ordinal) ||
                root is null || root.Size != sizeof(ulong) || reference is not > 0)
                return new(HybridCpuIseManagedImageLoadStatusV1.BootstrapRejected,
                    $"String literal '{literal.Identity}' requires one exact pointer-sized static root and a runtime-owned materialization.",
                    boot.Context, typeBuild.TypeSystem, heap, strings, gc, bootstrap, ecallBridge, stackMaps.Registrations);
            byte[] referenceBytes = new byte[sizeof(ulong)];
            BinaryPrimitives.WriteUInt64LittleEndian(referenceBytes, reference.Value);
            if (!memory.TryWritePhysicalRange(root.Address, referenceBytes))
                return new(HybridCpuIseManagedImageLoadStatusV1.BootstrapRejected,
                    $"String literal '{literal.Identity}' static root rejected its runtime-owned reference binding.",
                    boot.Context, typeBuild.TypeSystem, heap, strings, gc, bootstrap, ecallBridge, stackMaps.Registrations);
        }
        return new(HybridCpuIseManagedImageLoadStatusV1.Success, string.Empty,
            boot.Context, typeBuild.TypeSystem, heap, strings, gc, bootstrap, ecallBridge,
            stackMaps.Registrations, initializers is null ? pendingInitializers : []);
    }

    private static bool TryPublishExecutableBundleAnnotations(
        HybridCpuIseManagedImageLoadRequestV1 request,
        Processor.MainMemoryArea memory,
        out string failure)
    {
        const int bundleBytes = 256;
        IReadOnlyList<HybridCpuCodeManagerRegistrationV1> records =
            request.RuntimeBootstrap!.CodeManagerRecords;
        foreach (HybridCpuCodeManagerRegistrationV1 record in records
                     .OrderBy(static row => row.CodeStartOffsetBytes)
                     .ThenBy(static row => row.MethodIdentity, StringComparer.Ordinal))
        {
            if (record.CodeStartOffsetBytes < 0 || record.CodeSizeBytes <= 0 ||
                record.CodeStartOffsetBytes % bundleBytes != 0 ||
                record.CodeSizeBytes % bundleBytes != 0 ||
                record.CodeStartOffsetBytes > request.ImageBytes.Length - record.CodeSizeBytes)
            {
                failure = $"Managed method '{record.MethodIdentity}' has a malformed or non-bundle-aligned executable range.";
                return false;
            }

        }

        // HCEXE V1 is one flattened RX image and does not yet carry a separate static-helper
        // executable-range manifest. Empty annotations publish only the mandatory canonical
        // transport sideband; they do not bypass decode, runtime legality, or memory authority.
        // Publishing every complete transport bundle keeps linked helper code visible while a
        // branch into data still reaches the ordinary fail-closed decoder.
        int completeBundleBytes = request.ImageBytes.Length / bundleBytes * bundleBytes;
        for (int offset = 0; offset < completeBundleBytes; offset += bundleBytes)
        {
            ulong address = checked(request.ImageBase + (ulong)offset);
            try
            {
                memory.PublishVliwBundleAnnotations(
                    address,
                    global::HybridCPU_ISE.Arch.VliwBundleAnnotations.Empty);
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
            {
                failure = $"ISE memory rejected canonical HCEXE bundle annotations at " +
                    $"0x{address:x16}: {exception.Message}";
                return false;
            }
        }

        failure = string.Empty;
        return true;
    }

    private static HybridCpuIseManagedImageLoadResultV1 Failure(
        HybridCpuIseManagedImageLoadStatusV1 status, string reason) =>
        new(status, reason, null, null, null, null, null, null, null, null);

    private static ulong AlignUp(ulong value, ulong alignment) =>
        checked((value + alignment - 1) / alignment * alignment);

    private readonly record struct HybridCpuRestrictedMemoryLayoutV1(
        ulong ImageBase, ulong ImageSize, ulong StackBase, ulong StackSize,
        ulong HeapBase, ulong HeapSize)
    {
        public bool IsValid => Valid(ImageBase, ImageSize) && Valid(StackBase, StackSize) && Valid(HeapBase, HeapSize) &&
            !Overlap(ImageBase, ImageSize, StackBase, StackSize) &&
            !Overlap(ImageBase, ImageSize, HeapBase, HeapSize) &&
            !Overlap(StackBase, StackSize, HeapBase, HeapSize);
        public ulong End => Math.Max(checked(ImageBase + ImageSize),
            Math.Max(checked(StackBase + StackSize), checked(HeapBase + HeapSize)));
        private static bool Valid(ulong address, ulong size) => address != 0 && size != 0 && address <= ulong.MaxValue - size;
        private static bool Overlap(ulong a, ulong aSize, ulong b, ulong bSize) => a < b + bSize && b < a + aSize;
    }

    private static class HybridCpuRestrictedStackV1
    {
        public const ulong Base = 0x2000_0000;
        public const ulong Size = 1024 * 1024;
    }
}
