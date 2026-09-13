using System.Buffers.Binary;
using HybridCPU.ManagedRuntime;
using HybridCPU.Platform.Contracts;
using HybridCPU.RuntimeKernel;
using HybridCPU_ISE.NonRTL.Runtime;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Registers;

namespace HybridCPU_ISE.CloseToHSL.Core.Runtime.Managed;

public sealed record HybridCpuManagedArrayStoreEcallResultV1(
    bool IsSuccess, ulong ExceptionReference, string Reason, bool IsManagedThrow = false);
public sealed record HybridCpuManagedEcallObservationV1(ulong Operation, ulong Receiver,
    ulong Argument1, ulong Argument2, HybridCpuExternalServiceStatusV1 Status, int Error, ulong Value);

/// <summary>
/// Production retirement-time bridge for the bounded managed-runtime ECALL surface.
/// It owns no ambient/global CPU state and handles only an exact user-mode envelope.
/// </summary>
public sealed class HybridCpuIseManagedEcallBridgeV1 : IHybridCpuIseEcallBridgeV1
{
    private const ulong VliwBundleBytes = 256;
    private readonly IHybridCpuRuntimeKernelV1 _kernel;
    private readonly Func<ulong, HybridCpuManagedShapeResultV1>? _argumentMessage;
    private readonly Func<ulong, HybridCpuManagedShapeResultV1>? _ensureTypeInitialized;
    private readonly HashSet<ulong> _activeCpuInitializerHandles = [];
    private readonly Func<ulong, int, int, HybridCpuManagedShapeResultV1>? _staticStoreInt32;
    private readonly Func<ulong, int, ulong, HybridCpuManagedShapeResultV1>? _staticStoreReference;
    private readonly Func<ulong, int, HybridCpuManagedShapeResultV1>? _staticLoadInt32;
    private readonly Func<ulong, int, HybridCpuManagedShapeResultV1>? _staticLoadReference;
    private readonly Func<HybridCpuManagedShapeResultV1>? _allocateNullReferenceException;
    private readonly Func<ulong, int, int, HybridCpuManagedArrayStoreEcallResultV1>? _arrayStoreInt32;
    private readonly Func<ulong, int, int, HybridCpuManagedArrayStoreEcallResultV1>? _arrayStoreInt8;
    private readonly Func<ulong, int, int, HybridCpuManagedArrayStoreEcallResultV1>? _arrayStoreInt16;
    private readonly Func<ulong, int, ulong, HybridCpuManagedArrayStoreEcallResultV1>? _arrayStoreReference;
    private readonly Func<ulong, int, HybridCpuManagedArrayStoreEcallResultV1>? _arrayLoadReference;
    private readonly Func<ulong, int, HybridCpuManagedArrayStoreEcallResultV1>? _arrayLoadInt32;
    private readonly Func<ulong, int, HybridCpuManagedArrayStoreEcallResultV1>? _arrayLoadUInt8;
    private readonly Func<ulong, int, HybridCpuManagedArrayStoreEcallResultV1>? _arrayLoadInt16;
    private readonly Func<ulong, int, HybridCpuManagedArrayStoreEcallResultV1>? _arrayLoadUInt16;
    private readonly Func<ulong, ulong, int, HybridCpuManagedArrayStoreEcallResultV1>? _arrayCopyAll;
    private readonly Func<ulong, int, ulong, int, int, HybridCpuManagedArrayStoreEcallResultV1>? _arrayCopy;
    private readonly Func<ulong, int, byte[]?>? _readGuestMemory;
    private readonly Func<ulong, ulong, HybridCpuManagedArrayStoreEcallResultV1>? _isInstance;
    private readonly Func<ulong, int, HybridCpuManagedArrayStoreEcallResultV1>? _stringCharacter;
    private readonly Func<ulong, ulong, HybridCpuManagedArrayStoreEcallResultV1>? _stringConcat2;
    private readonly Func<ulong, ulong, ulong, HybridCpuManagedArrayStoreEcallResultV1>? _stringConcat3;
    private readonly Func<ulong, HybridCpuManagedArrayStoreEcallResultV1>? _stringLength;
    private readonly Func<ulong, ulong, HybridCpuManagedArrayStoreEcallResultV1>? _initializeArray;
    private readonly Func<ulong, ulong, HybridCpuManagedArrayStoreEcallResultV1>? _stringFromUtf16Array;
    private readonly Func<ulong, int, HybridCpuManagedArrayStoreEcallResultV1>? _newArray;
    private readonly Func<ulong, HybridCpuManagedArrayStoreEcallResultV1>? _allocateObject;
    private readonly Func<ulong, HybridCpuManagedArrayStoreEcallResultV1>? _arrayEmpty;
    private readonly Func<ulong, HybridCpuManagedArrayStoreEcallResultV1>? _arrayLength;
    private readonly Func<uint, uint, HybridCpuManagedArrayStoreEcallResultV1>? _divideUInt32;
    private readonly Func<int, int, HybridCpuManagedArrayStoreEcallResultV1>? _divideInt32;
    private readonly Func<int, int, HybridCpuManagedArrayStoreEcallResultV1>? _remainderInt32;
    private readonly Func<long, long, HybridCpuManagedArrayStoreEcallResultV1>? _divideInt64;
    private readonly Func<long, HybridCpuManagedArrayStoreEcallResultV1>? _mathAbsInt64;
    private readonly Func<ulong, ulong, HybridCpuManagedArrayStoreEcallResultV1>? _argumentNullCtorParamName;
    private readonly Func<ulong, ulong, HybridCpuManagedArrayStoreEcallResultV1>? _argumentOutOfRangeCtorParamName;
    private readonly Func<ulong, ulong, HybridCpuManagedArrayStoreEcallResultV1>? _stringNotEquals;
    private readonly Func<ulong, HybridCpuManagedArrayStoreEcallResultV1>? _arrayClear;

    public int? LastProcessExitCode { get; private set; }
    public HybridCpuIseBootBlobEcallObservationV1? LastBootBlobObservation { get; private set; }
    public HybridCpuIseClockReadEcallObservationV1? LastClockReadObservation { get; private set; }
    public HybridCpuIseConsoleEcallObservationV1? LastConsoleObservation { get; private set; }
    public ulong ProcessExitRetireCount { get; private set; }
    public string LastRejectedReason { get; private set; } = string.Empty;
    public ulong LastManagedOperation { get; private set; }
    public HybridCpuManagedShapeStatusV1? LastManagedStatus { get; private set; }
    public string LastManagedReason { get; private set; } = string.Empty;
    public HybridCpuExternalServiceStatusV1? LastManagedExternalStatus { get; private set; }
    public int? LastManagedExternalError { get; private set; }
    public ulong LastManagedExternalValue { get; private set; }
    public ulong LastManagedReceiver { get; private set; }
    public ulong LastManagedArgument1 { get; private set; }
    public ulong LastManagedArgument2 { get; private set; }
    private readonly Queue<HybridCpuManagedEcallObservationV1> _managedObservations = new();
    public IReadOnlyList<HybridCpuManagedEcallObservationV1> ManagedObservations => _managedObservations.ToArray();

    public bool TryBeginCpuInitializer(ulong typeHandle) =>
        typeHandle != 0 && _activeCpuInitializerHandles.Add(typeHandle);

    public bool TryCompleteCpuInitializer(ulong typeHandle) =>
        typeHandle != 0 && _activeCpuInitializerHandles.Remove(typeHandle);

    public HybridCpuIseManagedEcallBridgeV1(IHybridCpuRuntimeKernelV1 kernel,
        Func<ulong, HybridCpuManagedShapeResultV1>? argumentMessage = null,
        Func<ulong, HybridCpuManagedShapeResultV1>? ensureTypeInitialized = null,
        Func<ulong, int, int, HybridCpuManagedShapeResultV1>? staticStoreInt32 = null,
        Func<ulong, int, ulong, HybridCpuManagedShapeResultV1>? staticStoreReference = null,
        Func<ulong, int, HybridCpuManagedShapeResultV1>? staticLoadInt32 = null,
        Func<ulong, int, HybridCpuManagedShapeResultV1>? staticLoadReference = null,
        Func<HybridCpuManagedShapeResultV1>? allocateNullReferenceException = null,
        Func<ulong, int, int, HybridCpuManagedArrayStoreEcallResultV1>? arrayStoreInt32 = null,
        Func<ulong, int, int, HybridCpuManagedArrayStoreEcallResultV1>? arrayStoreInt8 = null,
        Func<ulong, int, int, HybridCpuManagedArrayStoreEcallResultV1>? arrayStoreInt16 = null,
        Func<ulong, int, ulong, HybridCpuManagedArrayStoreEcallResultV1>? arrayStoreReference = null,
        Func<ulong, int, HybridCpuManagedArrayStoreEcallResultV1>? arrayLoadReference = null,
        Func<ulong, int, HybridCpuManagedArrayStoreEcallResultV1>? arrayLoadInt32 = null,
        Func<ulong, int, HybridCpuManagedArrayStoreEcallResultV1>? arrayLoadUInt8 = null,
        Func<ulong, int, HybridCpuManagedArrayStoreEcallResultV1>? arrayLoadInt16 = null,
        Func<ulong, int, HybridCpuManagedArrayStoreEcallResultV1>? arrayLoadUInt16 = null,
        Func<ulong, ulong, int, HybridCpuManagedArrayStoreEcallResultV1>? arrayCopyAll = null,
        Func<ulong, ulong, HybridCpuManagedArrayStoreEcallResultV1>? isInstance = null,
        Func<ulong, int, HybridCpuManagedArrayStoreEcallResultV1>? stringCharacter = null,
        Func<ulong, ulong, HybridCpuManagedArrayStoreEcallResultV1>? stringConcat2 = null,
        Func<ulong, ulong, ulong, HybridCpuManagedArrayStoreEcallResultV1>? stringConcat3 = null,
        Func<ulong, HybridCpuManagedArrayStoreEcallResultV1>? stringLength = null,
        Func<ulong, ulong, HybridCpuManagedArrayStoreEcallResultV1>? initializeArray = null,
        Func<ulong, ulong, HybridCpuManagedArrayStoreEcallResultV1>? stringFromUtf16Array = null,
        Func<ulong, int, HybridCpuManagedArrayStoreEcallResultV1>? newArray = null,
        Func<ulong, HybridCpuManagedArrayStoreEcallResultV1>? allocateObject = null,
        Func<ulong, HybridCpuManagedArrayStoreEcallResultV1>? arrayEmpty = null,
        Func<ulong, HybridCpuManagedArrayStoreEcallResultV1>? arrayLength = null,
        Func<uint, uint, HybridCpuManagedArrayStoreEcallResultV1>? divideUInt32 = null,
        Func<int, int, HybridCpuManagedArrayStoreEcallResultV1>? divideInt32 = null,
        Func<int, int, HybridCpuManagedArrayStoreEcallResultV1>? remainderInt32 = null,
        Func<long, long, HybridCpuManagedArrayStoreEcallResultV1>? divideInt64 = null,
        Func<long, HybridCpuManagedArrayStoreEcallResultV1>? mathAbsInt64 = null,
        Func<ulong, int, ulong, int, int, HybridCpuManagedArrayStoreEcallResultV1>? arrayCopy = null,
        Func<ulong, int, byte[]?>? readGuestMemory = null,
        Func<ulong, ulong, HybridCpuManagedArrayStoreEcallResultV1>? argumentNullCtorParamName = null,
        Func<ulong, ulong, HybridCpuManagedArrayStoreEcallResultV1>? argumentOutOfRangeCtorParamName = null,
        Func<ulong, ulong, HybridCpuManagedArrayStoreEcallResultV1>? stringNotEquals = null,
        Func<ulong, HybridCpuManagedArrayStoreEcallResultV1>? arrayClear = null)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        // ProcessExit is an intrinsic exact bridge operation. A restricted image that uses
        // only the terminal process boundary still requires a bridge even when it imports no
        // optional managed-runtime delegate operation.
        _argumentMessage = argumentMessage;
        _ensureTypeInitialized = ensureTypeInitialized;
        _staticStoreInt32 = staticStoreInt32;
        _staticStoreReference = staticStoreReference;
        _staticLoadInt32 = staticLoadInt32;
        _staticLoadReference = staticLoadReference;
        _allocateNullReferenceException = allocateNullReferenceException;
        _arrayStoreInt32 = arrayStoreInt32;
        _arrayStoreInt8 = arrayStoreInt8;
        _arrayStoreInt16 = arrayStoreInt16;
        _arrayStoreReference = arrayStoreReference;
        _arrayLoadReference = arrayLoadReference;
        _arrayLoadInt32 = arrayLoadInt32;
        _arrayLoadUInt8 = arrayLoadUInt8;
        _arrayLoadInt16 = arrayLoadInt16;
        _arrayLoadUInt16 = arrayLoadUInt16;
        _arrayCopyAll = arrayCopyAll;
        _arrayCopy = arrayCopy;
        _readGuestMemory = readGuestMemory;
        _isInstance = isInstance;
        _stringCharacter = stringCharacter;
        _stringConcat2 = stringConcat2;
        _stringConcat3 = stringConcat3;
        _stringLength = stringLength;
        _initializeArray = initializeArray;
        _stringFromUtf16Array = stringFromUtf16Array;
        _newArray = newArray;
        _allocateObject = allocateObject;
        _arrayEmpty = arrayEmpty;
        _arrayLength = arrayLength;
        _divideUInt32 = divideUInt32;
        _divideInt32 = divideInt32;
        _remainderInt32 = remainderInt32;
        _divideInt64 = divideInt64;
        _mathAbsInt64 = mathAbsInt64;
        _argumentNullCtorParamName = argumentNullCtorParamName;
        _argumentOutOfRangeCtorParamName = argumentOutOfRangeCtorParamName;
        _stringNotEquals = stringNotEquals;
        _arrayClear = arrayClear;
    }

    public bool TryHandle(EcallEvent ecall, ICanonicalCpuState state, PrivilegeLevel privilege)
    {
        ArgumentNullException.ThrowIfNull(ecall);
        ArgumentNullException.ThrowIfNull(state);
        ulong code = unchecked((ulong)ecall.EcallCode);
        ulong service = Read(state, ecall.VtId, HybridCpuExternalServiceEcallContractV1.ServiceRegister);
        if (code != HybridCpuExternalServiceEcallContractV1.EcallNumber)
        {
            LastRejectedReason = $"ECALL number 0x{code:x16} does not match HCSV 0x{HybridCpuExternalServiceEcallContractV1.EcallNumber:x16}.";
            return false;
        }

        // A handled ECALL must retire to the next fixed-width bundle.  Refuse the
        // bridge when that architectural PC cannot be represented, leaving the
        // ordinary trap path in sole control instead of throwing on the host.
        ulong pc = state.ReadPc(ecall.VtId);
        if (pc > ulong.MaxValue - VliwBundleBytes)
            return false;

        HybridCpuExecutionContextDescriptorV1? context = _kernel.CurrentContext();
        ulong operation = Read(state, ecall.VtId, HybridCpuExternalServiceEcallContractV1.OperationRegister);
        ulong buffer = Read(state, ecall.VtId, HybridCpuExternalServiceEcallContractV1.BufferAddressRegister);
        ulong length = Read(state, ecall.VtId, HybridCpuExternalServiceEcallContractV1.BufferLengthRegister);
        ulong access = Read(state, ecall.VtId, HybridCpuExternalServiceEcallContractV1.BufferAccessRegister);
        ulong count = Read(state, ecall.VtId, HybridCpuExternalServiceEcallContractV1.ArgumentCountRegister);
        ulong receiver = Read(state, ecall.VtId, HybridCpuExternalServiceEcallContractV1.FirstArgumentRegister);
        ulong argument1 = Read(state, ecall.VtId, HybridCpuExternalServiceEcallContractV1.SecondArgumentRegister);
        ulong argument2 = Read(state, ecall.VtId, HybridCpuExternalServiceEcallContractV1.ThirdArgumentRegister);
        if (service == (ulong)HybridCpuHostServiceV1.ProcessExit)
        {
            if (privilege != PrivilegeLevel.User || context is null ||
                context.VirtualThreadCarrier != ecall.VtId || operation != 0 || buffer != 0 || length != 0 ||
                access != (ulong)HybridCpuHostBufferAccessV1.None || count != 1 || argument1 != 0 || argument2 != 0 ||
                receiver != unchecked((ulong)(long)(int)(uint)receiver))
            {
                LastRejectedReason =
                    $"ProcessExit envelope rejected: privilege={privilege},context={(context is null ? "null" : "present")}," +
                    $"context-vt={(context?.VirtualThreadCarrier.ToString() ?? "none")},event-vt={ecall.VtId}," +
                    $"operation={operation},buffer=0x{buffer:x},length={length},access={access},count={count}," +
                    $"receiver=0x{receiver:x},argument1=0x{argument1:x},argument2=0x{argument2:x}.";
                return false;
            }
            int exitCode = unchecked((int)(uint)receiver);
            HybridCpuKernelResultV1 exited = _kernel.ProcessExit(exitCode);
            if (!exited.IsSuccess)
            {
                LastRejectedReason = "RuntimeKernel rejected ProcessExit: " + exited.Reason;
                return false;
            }
            LastProcessExitCode = exitCode;
            ProcessExitRetireCount++;
            state.WritePc(ecall.VtId, pc + VliwBundleBytes);
            return true;
        }
        if (service != (ulong)HybridCpuHostServiceV1.ManagedRuntime)
        {
            bool handled = HybridCpuIseBootBlobEcallBindingV1.TryHandle(_kernel, ecall, state, privilege, out var observation);
            if (observation is not null) LastBootBlobObservation = observation;
            if (!handled)
            {
                handled = HybridCpuIseClockReadEcallBindingV1.TryHandle(_kernel, ecall, state, privilege, out var clock);
                if (clock is not null) LastClockReadObservation = clock;
            }
            if (!handled)
            {
                handled = HybridCpuIseConsoleEcallBindingV1.TryHandle(_kernel, ecall, state, privilege, out var console);
                if (console is not null) LastConsoleObservation = console;
            }
            if (!handled)
                LastRejectedReason = $"HCSV service has no admitted ISE binding: service={service},operation=0x{operation:x}," +
                    $"pc=0x{pc:x},x1=0x{Read(state, ecall.VtId, 1):x},buffer=0x{buffer:x},length={length}," +
                    $"access={access},count={count},arg0=0x{receiver:x},privilege={privilege}.";
            return handled;
        }
        LastManagedReceiver = receiver;
        LastManagedArgument1 = argument1;
        LastManagedArgument2 = argument2;
        LastManagedStatus = null;
        LastManagedReason = string.Empty;
        ulong expectedCount = operation switch
        {
            HybridCpuManagedRuntimeEcallContractV1.StaticStoreInt32Operation => 3,
            HybridCpuManagedRuntimeEcallContractV1.StaticStoreReferenceOperation => 3,
            HybridCpuManagedRuntimeEcallContractV1.StaticLoadInt32Operation => 2,
            HybridCpuManagedRuntimeEcallContractV1.StaticLoadReferenceOperation => 2,
            HybridCpuManagedRuntimeEcallContractV1.AllocateNullReferenceExceptionOperation => 0,
            HybridCpuManagedRuntimeEcallContractV1.ArrayStoreInt32Operation => 3,
            HybridCpuManagedRuntimeEcallContractV1.ArrayStoreInt8Operation => 3,
            HybridCpuManagedRuntimeEcallContractV1.ArrayStoreInt16Operation => 3,
            HybridCpuManagedRuntimeEcallContractV1.ArrayStoreReferenceOperation => 3,
            HybridCpuManagedRuntimeEcallContractV1.ArrayLoadReferenceOperation => 2,
            HybridCpuManagedRuntimeEcallContractV1.ArrayLoadInt32Operation => 2,
            HybridCpuManagedRuntimeEcallContractV1.ArrayLoadUInt8Operation => 2,
            HybridCpuManagedRuntimeEcallContractV1.ArrayLoadInt16Operation => 2,
            HybridCpuManagedRuntimeEcallContractV1.ArrayLoadUInt16Operation => 2,
            HybridCpuManagedRuntimeEcallContractV1.ArrayCopyAllOperation => 3,
            HybridCpuManagedRuntimeEcallContractV1.ArrayCopyOperation => 0,
            HybridCpuManagedRuntimeEcallContractV1.IsInstanceOperation => 2,
            HybridCpuManagedRuntimeEcallContractV1.StringCharacterOperation => 2,
            HybridCpuManagedRuntimeEcallContractV1.StringConcat2Operation => 2,
            HybridCpuManagedRuntimeEcallContractV1.StringConcat3Operation => 3,
            HybridCpuManagedRuntimeEcallContractV1.StringLengthOperation => 1,
            HybridCpuManagedRuntimeEcallContractV1.InitializeArrayOperation => 2,
            HybridCpuManagedRuntimeEcallContractV1.StringFromUtf16ArrayOperation => 2,
            HybridCpuManagedRuntimeEcallContractV1.ArgumentNullCtorParamNameOperation => 2,
            HybridCpuManagedRuntimeEcallContractV1.ArgumentOutOfRangeCtorParamNameOperation => 2,
            HybridCpuManagedRuntimeEcallContractV1.StringNotEqualsOperation => 2,
            HybridCpuManagedRuntimeEcallContractV1.ArrayClearOperation => 1,
            HybridCpuManagedRuntimeEcallContractV1.NewArrayOperation => 2,
            HybridCpuManagedRuntimeEcallContractV1.AllocateObjectOperation => 1,
            HybridCpuManagedRuntimeEcallContractV1.ArrayEmptyOperation => 1,
            HybridCpuManagedRuntimeEcallContractV1.ArrayLengthOperation => 1,
            HybridCpuManagedRuntimeEcallContractV1.DivideUInt32CheckedOperation => 2,
            HybridCpuManagedRuntimeEcallContractV1.DivideInt32CheckedOperation => 2,
            HybridCpuManagedRuntimeEcallContractV1.RemainderInt32CheckedOperation => 2,
            HybridCpuManagedRuntimeEcallContractV1.DivideInt64CheckedOperation => 2,
            HybridCpuManagedRuntimeEcallContractV1.MathAbsInt64CheckedOperation => 1,
            _ => 1
        };
        HybridCpuExternalServiceStatusV1 status;
        ulong value = 0;
        int error = 0;
        bool isArrayCopyBlock = operation == HybridCpuManagedRuntimeEcallContractV1.ArrayCopyOperation;
        bool validBuffer = isArrayCopyBlock
            ? buffer != 0 && length == HybridCpuManagedRuntimeEcallContractV1.ArrayCopyArgumentBlockBytes &&
              access == (ulong)HybridCpuHostBufferAccessV1.Read
            : buffer == 0 && length == 0 && access == (ulong)HybridCpuHostBufferAccessV1.None;
        if (privilege != PrivilegeLevel.User || context is null || context.VirtualThreadCarrier != ecall.VtId ||
            !validBuffer ||
            count != expectedCount)
        {
            status = HybridCpuExternalServiceStatusV1.InvalidRequest;
            error = 22;
        }
        else
        {
            if (operation == HybridCpuManagedRuntimeEcallContractV1.ArgumentNullCtorParamNameOperation &&
                _argumentNullCtorParamName is not null)
            {
                HybridCpuManagedArrayStoreEcallResultV1 outcome;
                try { outcome = _argumentNullCtorParamName(receiver, argument1); }
                catch (Exception) { outcome = new(false, 0, "Managed ArgumentNullException constructor exception was contained."); }
                if (outcome.IsSuccess) { status = HybridCpuExternalServiceStatusV1.Success; value = outcome.ExceptionReference; error = outcome.IsManagedThrow ? 1 : 0; }
                else { status = HybridCpuExternalServiceStatusV1.ProviderFailure; error = 5; }
                goto Publish;
            }
            if (operation == HybridCpuManagedRuntimeEcallContractV1.ArgumentOutOfRangeCtorParamNameOperation &&
                _argumentOutOfRangeCtorParamName is not null)
            {
                HybridCpuManagedArrayStoreEcallResultV1 outcome;
                try { outcome = _argumentOutOfRangeCtorParamName(receiver, argument1); }
                catch (Exception) { outcome = new(false, 0, "Managed ArgumentOutOfRangeException constructor exception was contained."); }
                if (outcome.IsSuccess) { status = HybridCpuExternalServiceStatusV1.Success; value = outcome.ExceptionReference; error = outcome.IsManagedThrow ? 1 : 0; }
                else { status = HybridCpuExternalServiceStatusV1.ProviderFailure; error = 5; }
                goto Publish;
            }
            if (operation == HybridCpuManagedRuntimeEcallContractV1.StringNotEqualsOperation && _stringNotEquals is not null)
            {
                HybridCpuManagedArrayStoreEcallResultV1 outcome;
                try { outcome = _stringNotEquals(receiver, argument1); }
                catch (Exception) { outcome = new(false, 0, "Managed string inequality exception was contained."); }
                if (outcome.IsSuccess) { status = HybridCpuExternalServiceStatusV1.Success; value = outcome.ExceptionReference; error = 0; }
                else { status = HybridCpuExternalServiceStatusV1.ProviderFailure; error = 5; }
                goto Publish;
            }
            if (operation == HybridCpuManagedRuntimeEcallContractV1.ArrayClearOperation && _arrayClear is not null)
            {
                HybridCpuManagedArrayStoreEcallResultV1 outcome;
                try { outcome = _arrayClear(receiver); }
                catch (Exception) { outcome = new(false, 0, "Managed Array.Clear exception was contained."); }
                if (outcome.IsSuccess) { status = HybridCpuExternalServiceStatusV1.Success; value = outcome.ExceptionReference; error = outcome.IsManagedThrow ? 1 : 0; }
                else { status = HybridCpuExternalServiceStatusV1.ProviderFailure; error = 5; }
                goto Publish;
            }
            if (operation == HybridCpuManagedRuntimeEcallContractV1.ArrayCopyOperation &&
                _arrayCopy is not null && _readGuestMemory is not null)
            {
                byte[]? block;
                try { block = _readGuestMemory(buffer, HybridCpuManagedRuntimeEcallContractV1.ArrayCopyArgumentBlockBytes); }
                catch (Exception) { block = null; }
                if (block is null || block.Length != HybridCpuManagedRuntimeEcallContractV1.ArrayCopyArgumentBlockBytes)
                { status = HybridCpuExternalServiceStatusV1.InvalidRequest; error = 14; goto Publish; }
                ulong source = BinaryPrimitives.ReadUInt64LittleEndian(block.AsSpan(0, 8));
                long sourceIndex64 = BinaryPrimitives.ReadInt64LittleEndian(block.AsSpan(8, 8));
                ulong destination = BinaryPrimitives.ReadUInt64LittleEndian(block.AsSpan(16, 8));
                long destinationIndex64 = BinaryPrimitives.ReadInt64LittleEndian(block.AsSpan(24, 8));
                long copyLength64 = BinaryPrimitives.ReadInt64LittleEndian(block.AsSpan(32, 8));
                if (sourceIndex64 is < int.MinValue or > int.MaxValue ||
                    destinationIndex64 is < int.MinValue or > int.MaxValue || copyLength64 is < int.MinValue or > int.MaxValue)
                { status = HybridCpuExternalServiceStatusV1.InvalidRequest; error = 22; goto Publish; }
                HybridCpuManagedArrayStoreEcallResultV1 outcome;
                try { outcome = _arrayCopy(source, (int)sourceIndex64, destination, (int)destinationIndex64, (int)copyLength64); }
                catch (Exception) { outcome = new(false, 0, "Managed Array.Copy runtime exception was contained."); }
                if (outcome.IsSuccess) { status = HybridCpuExternalServiceStatusV1.Success; value = outcome.ExceptionReference; error = outcome.IsManagedThrow ? 1 : 0; }
                else { status = HybridCpuExternalServiceStatusV1.ProviderFailure; error = 5; }
                goto Publish;
            }
            if (operation == HybridCpuManagedRuntimeEcallContractV1.ArrayEmptyOperation && _arrayEmpty is not null)
            {
                HybridCpuManagedArrayStoreEcallResultV1 outcome;
                try { outcome = _arrayEmpty(receiver); }
                catch (Exception) { outcome = new(false, 0, "Managed Array.Empty runtime exception was contained."); }
                if (outcome.IsSuccess)
                {
                    status = HybridCpuExternalServiceStatusV1.Success;
                    value = outcome.ExceptionReference;
                    error = outcome.IsManagedThrow ? 1 : 0;
                }
                else { status = HybridCpuExternalServiceStatusV1.ProviderFailure; error = 5; }
                goto Publish;
            }
            if (operation == HybridCpuManagedRuntimeEcallContractV1.ArrayLengthOperation && _arrayLength is not null)
            {
                HybridCpuManagedArrayStoreEcallResultV1 outcome;
                try { outcome = _arrayLength(receiver); }
                catch (Exception) { outcome = new(false, 0, "Managed array-length runtime exception was contained."); }
                if (outcome.IsSuccess)
                {
                    status = HybridCpuExternalServiceStatusV1.Success;
                    value = outcome.ExceptionReference;
                    error = outcome.IsManagedThrow ? 1 : 0;
                }
                else { status = HybridCpuExternalServiceStatusV1.ProviderFailure; error = 5; }
                goto Publish;
            }
            if (operation == HybridCpuManagedRuntimeEcallContractV1.DivideUInt32CheckedOperation &&
                _divideUInt32 is not null && CanonicalUInt32(receiver) && CanonicalUInt32(argument1))
            {
                HybridCpuManagedArrayStoreEcallResultV1 outcome;
                try { outcome = _divideUInt32((uint)receiver, (uint)argument1); }
                catch (Exception) { outcome = new(false, 0, "Managed UInt32 division exception was contained."); }
                if (outcome.IsSuccess)
                {
                    status = HybridCpuExternalServiceStatusV1.Success;
                    value = outcome.ExceptionReference;
                    error = outcome.IsManagedThrow ? 1 : 0;
                }
                else { status = HybridCpuExternalServiceStatusV1.ProviderFailure; error = 5; }
                goto Publish;
            }
            if (operation == HybridCpuManagedRuntimeEcallContractV1.DivideInt32CheckedOperation &&
                _divideInt32 is not null && CanonicalInt32(receiver) && CanonicalInt32(argument1))
            {
                HybridCpuManagedArrayStoreEcallResultV1 outcome;
                try { outcome = _divideInt32(unchecked((int)(uint)receiver), unchecked((int)(uint)argument1)); }
                catch (Exception) { outcome = new(false, 0, "Managed Int32 division exception was contained."); }
                if (outcome.IsSuccess) { status = HybridCpuExternalServiceStatusV1.Success; value = outcome.ExceptionReference; error = outcome.IsManagedThrow ? 1 : 0; }
                else { status = HybridCpuExternalServiceStatusV1.ProviderFailure; error = 5; }
                goto Publish;
            }
            if (operation == HybridCpuManagedRuntimeEcallContractV1.RemainderInt32CheckedOperation &&
                _remainderInt32 is not null &&
                receiver == unchecked((ulong)(long)(int)(uint)receiver) &&
                argument1 == unchecked((ulong)(long)(int)(uint)argument1))
            {
                HybridCpuManagedArrayStoreEcallResultV1 outcome;
                try { outcome = _remainderInt32(unchecked((int)(uint)receiver), unchecked((int)(uint)argument1)); }
                catch (Exception) { outcome = new(false, 0, "Managed Int32 remainder exception was contained."); }
                if (outcome.IsSuccess) { status = HybridCpuExternalServiceStatusV1.Success; value = outcome.ExceptionReference; error = outcome.IsManagedThrow ? 1 : 0; }
                else { status = HybridCpuExternalServiceStatusV1.ProviderFailure; error = 5; }
                goto Publish;
            }
            if (operation == HybridCpuManagedRuntimeEcallContractV1.DivideInt64CheckedOperation && _divideInt64 is not null)
            {
                HybridCpuManagedArrayStoreEcallResultV1 outcome;
                try { outcome = _divideInt64(unchecked((long)receiver), unchecked((long)argument1)); }
                catch (Exception) { outcome = new(false, 0, "Managed Int64 division exception was contained."); }
                if (outcome.IsSuccess) { status = HybridCpuExternalServiceStatusV1.Success; value = outcome.ExceptionReference; error = outcome.IsManagedThrow ? 1 : 0; }
                else { status = HybridCpuExternalServiceStatusV1.ProviderFailure; error = 5; }
                goto Publish;
            }
            if (operation == HybridCpuManagedRuntimeEcallContractV1.MathAbsInt64CheckedOperation && _mathAbsInt64 is not null)
            {
                HybridCpuManagedArrayStoreEcallResultV1 outcome;
                try { outcome = _mathAbsInt64(unchecked((long)receiver)); }
                catch (Exception) { outcome = new(false, 0, "Managed Int64 Abs exception was contained."); }
                if (outcome.IsSuccess) { status = HybridCpuExternalServiceStatusV1.Success; value = outcome.ExceptionReference; error = outcome.IsManagedThrow ? 1 : 0; }
                else { status = HybridCpuExternalServiceStatusV1.ProviderFailure; error = 5; }
                goto Publish;
            }
            if (operation == HybridCpuManagedRuntimeEcallContractV1.ArrayLoadReferenceOperation &&
                _arrayLoadReference is not null &&
                argument1 == unchecked((ulong)(long)(int)(uint)argument1))
            {
                HybridCpuManagedArrayStoreEcallResultV1 outcome;
                try { outcome = _arrayLoadReference(receiver, unchecked((int)(uint)argument1)); }
                catch (Exception) { outcome = new(false, 0,
                    "Managed reference array-load runtime exception was contained."); }
                if (outcome.IsSuccess)
                {
                    status = HybridCpuExternalServiceStatusV1.Success;
                    value = outcome.ExceptionReference;
                    error = outcome.IsManagedThrow ? 1 : 0;
                }
                else { status = HybridCpuExternalServiceStatusV1.ProviderFailure; error = 5; }
                goto Publish;
            }
            if (operation == HybridCpuManagedRuntimeEcallContractV1.StringCharacterOperation && _stringCharacter is not null &&
                argument1 == unchecked((ulong)(long)(int)(uint)argument1))
            {
                HybridCpuManagedArrayStoreEcallResultV1 outcome;
                try { outcome = _stringCharacter(receiver, unchecked((int)(uint)argument1)); }
                catch (Exception) { outcome = new(false, 0, "Managed string-character runtime exception was contained."); }
                if (outcome.IsSuccess) { status = HybridCpuExternalServiceStatusV1.Success; value = outcome.ExceptionReference; error = outcome.IsManagedThrow ? 1 : 0; }
                else { status = HybridCpuExternalServiceStatusV1.ProviderFailure; error = 5; }
                goto Publish;
            }
            if (operation == HybridCpuManagedRuntimeEcallContractV1.ArrayLoadUInt8Operation && _arrayLoadUInt8 is not null &&
                argument1 == unchecked((ulong)(long)(int)(uint)argument1))
            {
                HybridCpuManagedArrayStoreEcallResultV1 outcome;
                try { outcome = _arrayLoadUInt8(receiver, unchecked((int)(uint)argument1)); }
                catch (Exception) { outcome = new(false, 0, "Managed array-load-u1 runtime exception was contained."); }
                if (outcome.IsSuccess) { status = HybridCpuExternalServiceStatusV1.Success; value = outcome.ExceptionReference; error = outcome.IsManagedThrow ? 1 : 0; }
                else { status = HybridCpuExternalServiceStatusV1.ProviderFailure; error = 5; }
                goto Publish;
            }
            if (operation == HybridCpuManagedRuntimeEcallContractV1.ArrayLoadInt16Operation && _arrayLoadInt16 is not null &&
                argument1 == unchecked((ulong)(long)(int)(uint)argument1))
            {
                HybridCpuManagedArrayStoreEcallResultV1 outcome;
                try { outcome = _arrayLoadInt16(receiver, unchecked((int)(uint)argument1)); }
                catch (Exception) { outcome = new(false, 0, "Managed array-load-i2 runtime exception was contained."); }
                if (outcome.IsSuccess) { status = HybridCpuExternalServiceStatusV1.Success; value = outcome.ExceptionReference; error = outcome.IsManagedThrow ? 1 : 0; }
                else { status = HybridCpuExternalServiceStatusV1.ProviderFailure; error = 5; }
                goto Publish;
            }
            if (operation == HybridCpuManagedRuntimeEcallContractV1.ArrayLoadUInt16Operation && _arrayLoadUInt16 is not null &&
                argument1 == unchecked((ulong)(long)(int)(uint)argument1))
            {
                HybridCpuManagedArrayStoreEcallResultV1 outcome;
                try { outcome = _arrayLoadUInt16(receiver, unchecked((int)(uint)argument1)); }
                catch (Exception) { outcome = new(false, 0, "Managed array-load-u2 runtime exception was contained."); }
                if (outcome.IsSuccess) { status = HybridCpuExternalServiceStatusV1.Success; value = outcome.ExceptionReference; error = outcome.IsManagedThrow ? 1 : 0; }
                else { status = HybridCpuExternalServiceStatusV1.ProviderFailure; error = 5; }
                goto Publish;
            }
            if (operation == HybridCpuManagedRuntimeEcallContractV1.ArrayCopyAllOperation && _arrayCopyAll is not null &&
                argument2 == unchecked((ulong)(long)(int)(uint)argument2))
            {
                HybridCpuManagedArrayStoreEcallResultV1 outcome;
                try { outcome = _arrayCopyAll(receiver, argument1, unchecked((int)(uint)argument2)); }
                catch (Exception) { outcome = new(false, 0, "Managed Array.Copy exception was contained."); }
                if (outcome.IsSuccess) { status = HybridCpuExternalServiceStatusV1.Success; value = outcome.ExceptionReference; error = outcome.IsManagedThrow ? 1 : 0; }
                else { status = HybridCpuExternalServiceStatusV1.ProviderFailure; error = 5; }
                goto Publish;
            }
            if (operation == HybridCpuManagedRuntimeEcallContractV1.IsInstanceOperation && _isInstance is not null)
            {
                HybridCpuManagedArrayStoreEcallResultV1 outcome;
                try { outcome = _isInstance(receiver, argument1); }
                catch (Exception) { outcome = new(false, 0, "Managed isinst runtime exception was contained."); }
                if (outcome.IsSuccess) { status = HybridCpuExternalServiceStatusV1.Success; value = outcome.ExceptionReference; }
                else { status = HybridCpuExternalServiceStatusV1.ProviderFailure; error = 5; }
                goto Publish;
            }
            if (operation == HybridCpuManagedRuntimeEcallContractV1.StringLengthOperation && _stringLength is not null)
            {
                HybridCpuManagedArrayStoreEcallResultV1 outcome;
                try { outcome = _stringLength(receiver); }
                catch (Exception) { outcome = new(false, 0, "Managed string-length runtime exception was contained."); }
                if (outcome.IsSuccess) { status = HybridCpuExternalServiceStatusV1.Success; value = outcome.ExceptionReference; error = outcome.IsManagedThrow ? 1 : 0; }
                else { status = HybridCpuExternalServiceStatusV1.ProviderFailure; error = 5; }
                goto Publish;
            }
            if (operation == HybridCpuManagedRuntimeEcallContractV1.StringConcat2Operation && _stringConcat2 is not null)
            {
                HybridCpuManagedArrayStoreEcallResultV1 outcome;
                try { outcome = _stringConcat2(receiver, argument1); }
                catch (Exception) { outcome = new(false, 0, "Managed string-concat2 runtime exception was contained."); }
                if (outcome.IsSuccess) { status = HybridCpuExternalServiceStatusV1.Success; value = outcome.ExceptionReference; error = outcome.IsManagedThrow ? 1 : 0; }
                else { status = HybridCpuExternalServiceStatusV1.ProviderFailure; error = 5; }
                goto Publish;
            }
            if (operation == HybridCpuManagedRuntimeEcallContractV1.StringConcat3Operation && _stringConcat3 is not null)
            {
                HybridCpuManagedArrayStoreEcallResultV1 outcome;
                try { outcome = _stringConcat3(receiver, argument1, argument2); }
                catch (Exception) { outcome = new(false, 0, "Managed string-concat3 exception was contained."); }
                if (outcome.IsSuccess) { status = HybridCpuExternalServiceStatusV1.Success; value = outcome.ExceptionReference; error = outcome.IsManagedThrow ? 1 : 0; }
                else { status = HybridCpuExternalServiceStatusV1.ProviderFailure; error = 5; }
                goto Publish;
            }
            if (operation == HybridCpuManagedRuntimeEcallContractV1.ArrayLoadInt32Operation &&
                _arrayLoadInt32 is not null &&
                argument1 == unchecked((ulong)(long)(int)(uint)argument1))
            {
                HybridCpuManagedArrayStoreEcallResultV1 outcome;
                try { outcome = _arrayLoadInt32(receiver, unchecked((int)(uint)argument1)); }
                catch (Exception) { outcome = new(false, 0,
                    "Managed i4 array-load runtime exception was contained."); }
                if (outcome.IsSuccess)
                {
                    status = HybridCpuExternalServiceStatusV1.Success;
                    value = outcome.ExceptionReference;
                    error = outcome.IsManagedThrow ? 1 : 0;
                }
                else { status = HybridCpuExternalServiceStatusV1.ProviderFailure; error = 5; }
                goto Publish;
            }
            if (operation == HybridCpuManagedRuntimeEcallContractV1.ArrayStoreInt32Operation &&
                _arrayStoreInt32 is not null && argument1 <= int.MaxValue)
            {
                HybridCpuManagedArrayStoreEcallResultV1 outcome;
                try
                {
                    outcome = _arrayStoreInt32(receiver, (int)argument1, unchecked((int)(uint)argument2));
                }
                catch (Exception)
                {
                    outcome = new(false, 0, "Managed array-store runtime exception was contained.");
                }
                if (outcome.IsSuccess)
                {
                    status = HybridCpuExternalServiceStatusV1.Success;
                    value = outcome.ExceptionReference;
                    error = outcome.ExceptionReference == 0 ? 0 : 1;
                }
                else
                {
                    status = HybridCpuExternalServiceStatusV1.ProviderFailure;
                    error = 5;
                }
                goto Publish;
            }
            if (operation == HybridCpuManagedRuntimeEcallContractV1.ArrayStoreInt8Operation &&
                _arrayStoreInt8 is not null && argument1 == unchecked((ulong)(long)(int)(uint)argument1))
            {
                HybridCpuManagedArrayStoreEcallResultV1 outcome;
                try { outcome = _arrayStoreInt8(receiver, unchecked((int)(uint)argument1), unchecked((int)(uint)argument2)); }
                catch (Exception) { outcome = new(false, 0, "Managed i1 array-store runtime exception was contained."); }
                if (outcome.IsSuccess) { status = HybridCpuExternalServiceStatusV1.Success; value = outcome.ExceptionReference; error = outcome.ExceptionReference == 0 ? 0 : 1; }
                else { status = HybridCpuExternalServiceStatusV1.ProviderFailure; error = 5; }
                goto Publish;
            }
            if (operation == HybridCpuManagedRuntimeEcallContractV1.ArrayStoreInt16Operation &&
                _arrayStoreInt16 is not null && argument1 == unchecked((ulong)(long)(int)(uint)argument1))
            {
                HybridCpuManagedArrayStoreEcallResultV1 outcome;
                try { outcome = _arrayStoreInt16(receiver, unchecked((int)(uint)argument1), unchecked((int)(uint)argument2)); }
                catch (Exception) { outcome = new(false, 0, "Managed i2 array-store runtime exception was contained."); }
                if (outcome.IsSuccess) { status = HybridCpuExternalServiceStatusV1.Success; value = outcome.ExceptionReference; error = outcome.IsManagedThrow ? 1 : 0; }
                else { status = HybridCpuExternalServiceStatusV1.ProviderFailure; error = 5; }
                goto Publish;
            }
            if (operation == HybridCpuManagedRuntimeEcallContractV1.ArrayStoreReferenceOperation &&
                _arrayStoreReference is not null && argument1 <= int.MaxValue)
            {
                HybridCpuManagedArrayStoreEcallResultV1 outcome;
                try { outcome = _arrayStoreReference(receiver, (int)argument1, argument2); }
                catch (Exception) { outcome = new(false, 0,
                    "Managed reference array-store runtime exception was contained."); }
                if (outcome.IsSuccess)
                {
                    status = HybridCpuExternalServiceStatusV1.Success;
                    value = outcome.ExceptionReference;
                    error = outcome.ExceptionReference == 0 ? 0 : 1;
                }
                else
                {
                    status = HybridCpuExternalServiceStatusV1.ProviderFailure;
                    error = 5;
                }
                goto Publish;
            }
            if (operation == HybridCpuManagedRuntimeEcallContractV1.InitializeArrayOperation &&
                _initializeArray is not null)
            {
                HybridCpuManagedArrayStoreEcallResultV1 outcome;
                try { outcome = _initializeArray(receiver, argument1); }
                catch (Exception) { outcome = new(false, 0,
                    "Managed InitializeArray runtime exception was contained."); }
                if (outcome.IsSuccess)
                {
                    status = HybridCpuExternalServiceStatusV1.Success;
                    value = outcome.ExceptionReference;
                    error = outcome.ExceptionReference == 0 ? 0 : 1;
                }
                else { status = HybridCpuExternalServiceStatusV1.ProviderFailure; error = 5; }
                goto Publish;
            }
            if (operation == HybridCpuManagedRuntimeEcallContractV1.StringFromUtf16ArrayOperation && _stringFromUtf16Array is not null)
            {
                HybridCpuManagedArrayStoreEcallResultV1 outcome;
                try { outcome = _stringFromUtf16Array(receiver, argument1); }
                catch (Exception) { outcome = new(false, 0, "Managed String(char[]) exception was contained."); }
                if (outcome.IsSuccess) { status = HybridCpuExternalServiceStatusV1.Success; value = outcome.ExceptionReference; error = outcome.IsManagedThrow ? 1 : 0; }
                else { status = HybridCpuExternalServiceStatusV1.ProviderFailure; error = 5; }
                goto Publish;
            }
            if (operation == HybridCpuManagedRuntimeEcallContractV1.NewArrayOperation && _newArray is not null &&
                argument1 == unchecked((ulong)(long)(int)(uint)argument1))
            {
                HybridCpuManagedArrayStoreEcallResultV1 outcome;
                try { outcome = _newArray(receiver, unchecked((int)(uint)argument1)); }
                catch (Exception) { outcome = new(false, 0, "Managed newarr runtime exception was contained."); }
                LastManagedReason = outcome.Reason;
                if (outcome.IsSuccess)
                {
                    status = HybridCpuExternalServiceStatusV1.Success;
                    value = outcome.ExceptionReference;
                    error = outcome.IsManagedThrow ? 1 : 0;
                }
                else { status = HybridCpuExternalServiceStatusV1.ProviderFailure; error = 5; }
                goto Publish;
            }
            if (operation == HybridCpuManagedRuntimeEcallContractV1.AllocateObjectOperation && _allocateObject is not null)
            {
                HybridCpuManagedArrayStoreEcallResultV1 outcome;
                try { outcome = _allocateObject(receiver); }
                catch (Exception) { outcome = new(false, 0, "Managed object allocation exception was contained."); }
                LastManagedReason = outcome.Reason;
                if (outcome.IsSuccess)
                {
                    status = HybridCpuExternalServiceStatusV1.Success;
                    value = outcome.ExceptionReference;
                    error = outcome.IsManagedThrow ? 1 : 0;
                }
                else { status = HybridCpuExternalServiceStatusV1.ProviderFailure; error = 5; }
                goto Publish;
            }
            HybridCpuManagedShapeResultV1 result;
            try
            {
                result = operation switch
                {
                    HybridCpuManagedRuntimeEcallContractV1.ArgumentExceptionGetMessageOperation
                        when _argumentMessage is not null => _argumentMessage(receiver),
                    HybridCpuManagedRuntimeEcallContractV1.EnsureTypeInitializedOperation
                        when _activeCpuInitializerHandles.Contains(receiver) =>
                            new(HybridCpuManagedShapeStatusV1.Success,
                                "Recursive same-CPU-context request observes image initialization in progress."),
                    HybridCpuManagedRuntimeEcallContractV1.EnsureTypeInitializedOperation
                        when _ensureTypeInitialized is not null => _ensureTypeInitialized(receiver),
                    HybridCpuManagedRuntimeEcallContractV1.StaticStoreInt32Operation
                        when _staticStoreInt32 is not null &&
                             argument1 <= int.MaxValue => _staticStoreInt32(
                                 receiver, (int)argument1, unchecked((int)(uint)argument2)),
                    HybridCpuManagedRuntimeEcallContractV1.StaticStoreReferenceOperation
                        when _staticStoreReference is not null && argument1 <= int.MaxValue =>
                            _staticStoreReference(receiver, (int)argument1, argument2),
                    HybridCpuManagedRuntimeEcallContractV1.StaticLoadInt32Operation
                        when _staticLoadInt32 is not null &&
                             argument1 <= int.MaxValue => _staticLoadInt32(receiver, (int)argument1),
                    HybridCpuManagedRuntimeEcallContractV1.StaticLoadReferenceOperation
                        when _staticLoadReference is not null && argument1 <= int.MaxValue =>
                            _staticLoadReference(receiver, (int)argument1),
                    HybridCpuManagedRuntimeEcallContractV1.AllocateNullReferenceExceptionOperation
                        when _allocateNullReferenceException is not null => _allocateNullReferenceException(),
                    _ => new(HybridCpuManagedShapeStatusV1.InvalidType,
                        "Managed runtime ECALL operation has no exact production binding.")
                };
            }
            catch (Exception) { result = new(HybridCpuManagedShapeStatusV1.HeapFailure,
                "Managed runtime helper exception was contained."); }
            LastManagedOperation = operation;
            LastManagedStatus = result.Status;
            LastManagedReason = result.Reason;
            if (result.IsSuccess && (operation is not (HybridCpuManagedRuntimeEcallContractV1.ArgumentExceptionGetMessageOperation or
                    HybridCpuManagedRuntimeEcallContractV1.AllocateNullReferenceExceptionOperation) ||
                result.ObjectReference != 0))
            {
                status = HybridCpuExternalServiceStatusV1.Success;
                value = operation switch
                {
                    HybridCpuManagedRuntimeEcallContractV1.StaticLoadInt32Operation =>
                        unchecked((ulong)(uint)result.ScalarValue),
                    HybridCpuManagedRuntimeEcallContractV1.StaticLoadReferenceOperation =>
                        unchecked((ulong)result.ScalarValue),
                    _ => result.ObjectReference
                };
            }
            else
            {
                status = HybridCpuExternalServiceStatusV1.ProviderFailure;
                error = 5;
            }
        }

    Publish:
        if (service == (ulong)HybridCpuHostServiceV1.ManagedRuntime)
        {
            LastManagedOperation = operation;
            LastManagedExternalStatus = status;
            LastManagedExternalError = error;
            LastManagedExternalValue = value;
            if (_managedObservations.Count == 32) _managedObservations.Dequeue();
            _managedObservations.Enqueue(new(operation, receiver, argument1, argument2, status, error, value));
        }
        state.WriteRegister(ecall.VtId, HybridCpuExternalServiceEcallContractV1.ResultValueRegister, value);
        state.WriteRegister(ecall.VtId, HybridCpuExternalServiceEcallContractV1.ResultStatusRegister, (ulong)status);
        state.WriteRegister(ecall.VtId, HybridCpuExternalServiceEcallContractV1.ResultErrorRegister,
            unchecked((ulong)(long)error));
        state.WritePc(ecall.VtId, pc + VliwBundleBytes);
        return true;
    }

    private static ulong Read(ICanonicalCpuState state, byte vt, int register) =>
        unchecked((ulong)state.ReadRegister(vt, register));

    private static bool CanonicalUInt32(ulong value) => value == (ulong)(uint)value ||
        value == unchecked((ulong)(long)(int)(uint)value);

    private static bool CanonicalInt32(ulong value) => value == unchecked((ulong)(long)(int)(uint)value);
}
