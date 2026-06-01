using System;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Auth;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Backends;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Capabilities;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;
using YAKSys_Hybrid_CPU.Core.Registers;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;

namespace YAKSys_Hybrid_CPU.Core
{
    public enum SystemDeviceCommandKind : byte
    {
        QueryCaps,
        Submit,
        Poll,
        Status,
        Wait,
        Cancel,
        Fence
    }

    /// <summary>
    /// Runtime-owned L7-SDC lane7 system-device command carrier for the Phase 08
    /// current production contour plus the Phase 08A ACCEL_STATUS contour.
    /// It remains a control-plane SystemSingleton op:
    /// no scalar/vector arithmetic plane and no legacy custom-accelerator fallback.
    /// </summary>
    public abstract class SystemDeviceCommandMicroOp : MicroOp
    {
        private ExternalAcceleratorRuntimeCommandResult? _lastCommandResult;
        private ulong _capturedWriteBackValue;
        private AcceleratorTokenHandle _capturedFenceHandle;

        protected SystemDeviceCommandMicroOp(
            ushort opCode,
            SystemDeviceCommandKind commandKind,
            Arch.SerializationClass serializationClass,
            ushort destinationRegister = VLIW_Instruction.NoReg,
            ushort tokenRegister = VLIW_Instruction.NoReg,
            AcceleratorCommandDescriptor? commandDescriptor = null)
        {
            OpCode = opCode;
            CommandKind = commandKind;
            CommandDescriptor = commandDescriptor;
            CommandDescriptorReference = commandDescriptor?.DescriptorReference;
            DestinationRegister = NormalizeOptionalArchRegister(
                destinationRegister,
                $"{commandKind} rd");
            TokenRegister = NormalizeOptionalArchRegister(
                tokenRegister,
                $"{commandKind} rs1 token");
            DestRegID = DestinationRegister;
            IsStealable = false;
            IsMemoryOp = false;
            IsControlFlow = false;
            HasSideEffects = true;
            Class = MicroOpClass.Other;
            WritesRegister = DestinationRegister != 0;

            InstructionClass = Arch.InstructionClass.System;
            SerializationClass = serializationClass;
            SetHardPinnedPlacement(SlotClass.SystemSingleton, 7);

            InitializeMetadata();
        }

        public SystemDeviceCommandKind CommandKind { get; }

        public AcceleratorCommandDescriptor? CommandDescriptor { get; }

        public AcceleratorDescriptorReference? CommandDescriptorReference { get; }

        public ushort DestinationRegister { get; }

        public ushort TokenRegister { get; }

        public ExternalAcceleratorRuntimeCommandResult? LastCommandResult => _lastCommandResult;

        public AcceleratorTokenAdmissionResult? LastSubmitAdmission =>
            _lastCommandResult?.SubmitAdmission;

        public AcceleratorTokenLookupResult? LastTokenLookup =>
            _lastCommandResult?.TokenLookup;

        public bool UsedLegacyCustomAcceleratorFallback => false;

        public bool UsedArithmeticExecutionPlane => false;

        public override CanonicalDecodePublicationMode CanonicalDecodePublication =>
            CanonicalDecodePublicationMode.SelfPublishes;

        public void InitializeMetadata()
        {
            ReadRegisters = UsesTokenRegister(CommandKind) && TokenRegister != 0
                ? new[] { (int)TokenRegister }
                : Array.Empty<int>();
            WriteRegisters = WritesRegister
                ? new[] { (int)DestinationRegister }
                : Array.Empty<int>();
            ReadMemoryRanges = Array.Empty<(ulong, ulong)>();
            WriteMemoryRanges = Array.Empty<(ulong, ulong)>();
            ResourceMask = BuildResourceMask(
                Placement.DomainTag,
                ReadRegisters,
                WriteRegisters);

            PublishExplicitStructuralSafetyMask();
            RefreshAdmissionMetadata(this);
        }

        public override void RefreshWriteMetadata() => InitializeMetadata();

        public override bool Execute(ref Processor.CPU_Core core)
        {
            InitializeMetadata();

            ExternalAcceleratorRuntime runtime =
                core.GetExternalAcceleratorRuntime();
            int vtId = NormalizeExecutionVtId(OwnerThreadId);
            RejectGuestCompatibilityExecution(ref core, vtId);

            _lastCommandResult = CommandKind switch
            {
                SystemDeviceCommandKind.QueryCaps =>
                    runtime.QueryCaps(
                        BuildOwnerBinding(ref core, vtId),
                        BuildGuardEvidence(ref core, vtId)),
                SystemDeviceCommandKind.Submit =>
                    ExecuteSubmit(runtime),
                SystemDeviceCommandKind.Poll =>
                    runtime.Poll(ReadTokenHandle(ref core, vtId)),
                SystemDeviceCommandKind.Status =>
                    runtime.Status(ReadTokenHandle(ref core, vtId)),
                SystemDeviceCommandKind.Wait =>
                    runtime.Wait(ReadTokenHandle(ref core, vtId)),
                SystemDeviceCommandKind.Cancel =>
                    runtime.Cancel(ReadTokenHandle(ref core, vtId)),
                SystemDeviceCommandKind.Fence =>
                    ExecuteFenceObserve(runtime, ReadTokenHandle(ref core, vtId)),
                _ => throw new InvalidOperationException(
                    $"Unsupported L7-SDC command kind {CommandKind}.")
            };

            if (_lastCommandResult.RegisterAbi.WritesRegister)
            {
                _capturedWriteBackValue =
                    _lastCommandResult.RegisterAbi.RegisterValue;
            }

            return true;
        }

        public override void EmitWriteBackRetireRecords(
            ref Processor.CPU_Core core,
            Span<RetireRecord> retireRecords,
            ref int retireRecordCount)
        {
            ExternalAcceleratorRuntimeCommandResult result =
                ResolveRetireResult(ref core);
            AcceleratorRegisterAbiResult abi = result.RegisterAbi;
            if (abi.RequiresPreciseFault)
            {
                throw new InvalidOperationException(abi.Message);
            }

            if (!abi.WritesRegister || !WritesRegister)
            {
                return;
            }

            int vtId = NormalizeExecutionVtId(OwnerThreadId);
            AppendWriteBackRetireRecord(
                retireRecords,
                ref retireRecordCount,
                RetireRecord.RegisterWrite(vtId, DestinationRegister, abi.RegisterValue));
        }

        public override bool TryGetPrimaryWriteBackResult(out ulong value)
        {
            value = _capturedWriteBackValue;
            return CommandKind != SystemDeviceCommandKind.Fence &&
                   _lastCommandResult?.RegisterAbi.WritesRegister == true &&
                   WritesRegister;
        }

        public override void CapturePrimaryWriteBackResult(ulong value) =>
            _capturedWriteBackValue = value;

        private ExternalAcceleratorRuntimeCommandResult ExecuteSubmit(
            ExternalAcceleratorRuntime runtime)
        {
            if (CommandDescriptor is null)
            {
                throw new InvalidOperationException(
                    "ACCEL_SUBMIT execution requires a guard-backed AcceleratorCommandDescriptor sideband from native lane7 decode; descriptorless raw factory execution remains fail-closed.");
            }

            return runtime.Submit(CommandDescriptor);
        }

        private ExternalAcceleratorRuntimeCommandResult ExecuteFenceObserve(
            ExternalAcceleratorRuntime runtime,
            AcceleratorTokenHandle handle)
        {
            _capturedFenceHandle = handle;
            return runtime.FenceObserve(handle);
        }

        private ExternalAcceleratorRuntimeCommandResult ResolveRetireResult(
            ref Processor.CPU_Core core)
        {
            if (CommandKind == SystemDeviceCommandKind.Fence)
            {
                _lastCommandResult =
                    core.GetExternalAcceleratorRuntime().FenceCommit(_capturedFenceHandle);
                if (_lastCommandResult.RegisterAbi.WritesRegister)
                {
                    _capturedWriteBackValue =
                        _lastCommandResult.RegisterAbi.RegisterValue;
                }
            }

            return _lastCommandResult ?? throw new InvalidOperationException(
                $"{GetType().Name} retire requires a prior execute/capture result.");
        }

        private static void RejectGuestCompatibilityExecution(
            ref Processor.CPU_Core core,
            int vtId)
        {
            if (core.Csr.Read(CsrAddresses.VmxEnable, PrivilegeLevel.Machine) == 0)
            {
                return;
            }

            bool compatibilityGuestExecution;
            try
            {
                compatibilityGuestExecution =
                    core.ReadVirtualThreadPipelineState(vtId) == PipelineState.GuestExecution;
            }
            catch (ArgumentOutOfRangeException)
            {
                compatibilityGuestExecution =
                    core.HasAnyVirtualThreadPipelineState(PipelineState.GuestExecution);
            }

            if (compatibilityGuestExecution)
            {
                throw new InvalidOperationException(
                    "Guest Lane7 compatibility execution is fail-closed: no runtime-owned Lane7 domain binding is admitted for the frozen VMX frontend.");
            }
        }

        private ulong ReadTokenRegisterValue(ref Processor.CPU_Core core, int ownerVirtualThreadId)
        {
            if (TokenRegister == 0)
            {
                return 0;
            }

            return ReadUnifiedScalarSourceOperand(ref core, ownerVirtualThreadId, TokenRegister);
        }

        private AcceleratorTokenHandle ReadTokenHandle(
            ref Processor.CPU_Core core,
            int vtId)
        {
            ulong rawHandle = ReadUnifiedScalarSourceOperand(ref core, vtId, TokenRegister);
            return rawHandle == 0
                ? AcceleratorTokenHandle.Invalid
                : AcceleratorTokenHandle.FromOpaqueValue(rawHandle);
        }

        private AcceleratorOwnerBinding BuildOwnerBinding(
            ref Processor.CPU_Core core,
            int vtId)
        {
            if (OwnerContextId < 0)
            {
                throw new InvalidOperationException(
                    $"{CommandKind} owner context id is not materialized.");
            }

            return new AcceleratorOwnerBinding
            {
                OwnerVirtualThreadId = (ushort)vtId,
                OwnerContextId = (uint)OwnerContextId,
                OwnerCoreId = core.CoreID,
                OwnerPodId = 0,
                DomainTag = Placement.DomainTag
            };
        }

        private AcceleratorGuardEvidence BuildGuardEvidence(
            ref Processor.CPU_Core core,
            int vtId)
        {
            AcceleratorOwnerBinding ownerBinding = BuildOwnerBinding(ref core, vtId);
            return AcceleratorGuardEvidence.FromGuardPlane(
                ownerBinding,
                activeDomainCertificate: ownerBinding.DomainTag);
        }

        public override string GetDescription() =>
            $"L7-SDC {CommandKind}: OpCode={OpcodeRegistry.GetMnemonicOrHex(OpCode)}, rd=x{DestinationRegister}, rs1=x{TokenRegister}";

        private static bool UsesTokenRegister(SystemDeviceCommandKind commandKind) =>
            commandKind is SystemDeviceCommandKind.Poll
                or SystemDeviceCommandKind.Status
                or SystemDeviceCommandKind.Wait
                or SystemDeviceCommandKind.Cancel
                or SystemDeviceCommandKind.Fence;

        private static ResourceBitset BuildResourceMask(
            ulong ownerDomainTag,
            System.Collections.Generic.IReadOnlyList<int> readRegisters,
            System.Collections.Generic.IReadOnlyList<int> writeRegisters)
        {
            int resourceDomainBucket = (int)(ownerDomainTag & 0xFUL);
            ResourceBitset mask = ResourceMaskBuilder.ForAccelerator(0)
                                  | ResourceMaskBuilder.ForMemoryDomain(resourceDomainBucket);

            for (int index = 0; index < readRegisters.Count; index++)
            {
                mask |= ResourceMaskBuilder.ForRegisterRead(readRegisters[index]);
            }

            for (int index = 0; index < writeRegisters.Count; index++)
            {
                mask |= ResourceMaskBuilder.ForRegisterWrite(writeRegisters[index]);
            }

            return mask;
        }

        private static ushort NormalizeOptionalArchRegister(
            ushort rawRegister,
            string operandName)
        {
            if (rawRegister == VLIW_Instruction.NoReg ||
                rawRegister == VLIW_Instruction.NoArchReg)
            {
                return 0;
            }

            if (!TryNormalizeFlatArchRegId(rawRegister, out int archRegId))
            {
                throw new DecodeProjectionFaultException(
                    $"L7-SDC {operandName} requires a flat architectural register id.");
            }

            return (ushort)archRegId;
        }
    }

    public sealed class AcceleratorQueryCapsMicroOp : SystemDeviceCommandMicroOp
    {
        public AcceleratorQueryCapsMicroOp(ushort destinationRegister = VLIW_Instruction.NoReg)
            : base(
                Processor.CPU_Core.IsaOpcodeValues.ACCEL_QUERY_CAPS,
                SystemDeviceCommandKind.QueryCaps,
                Arch.SerializationClass.CsrOrdered,
                destinationRegister)
        {
        }
    }

    public sealed class AcceleratorSubmitMicroOp : SystemDeviceCommandMicroOp
    {
        public AcceleratorGuardDecision? SubmitGuardDecision { get; }

        public AcceleratorSubmitMicroOp(ushort destinationRegister = VLIW_Instruction.NoReg)
            : base(
                Processor.CPU_Core.IsaOpcodeValues.ACCEL_SUBMIT,
                SystemDeviceCommandKind.Submit,
                Arch.SerializationClass.MemoryOrdered,
                destinationRegister)
        {
        }

        public AcceleratorSubmitMicroOp(
            ushort destinationRegister,
            AcceleratorCommandDescriptor commandDescriptor)
            : base(
                Processor.CPU_Core.IsaOpcodeValues.ACCEL_SUBMIT,
                SystemDeviceCommandKind.Submit,
                Arch.SerializationClass.MemoryOrdered,
                destinationRegister,
                VLIW_Instruction.NoReg,
                commandDescriptor)
        {
            ArgumentNullException.ThrowIfNull(commandDescriptor);
            AcceleratorGuardDecision submitGuardDecision =
                AcceleratorOwnerDomainGuard.Default.EnsureBeforeSubmit(
                    commandDescriptor,
                    commandDescriptor.OwnerGuardDecision.Evidence);
            if (!submitGuardDecision.IsAllowed)
            {
                throw new InvalidOperationException(
                    "ACCEL_SUBMIT admission requires guard-backed owner/domain evidence. " +
                    submitGuardDecision.Message);
            }

            SubmitGuardDecision = submitGuardDecision;
        }

        public AcceleratorSubmitMicroOp(AcceleratorCommandDescriptor commandDescriptor)
            : this(VLIW_Instruction.NoReg, commandDescriptor)
        {
        }
    }

    public sealed class AcceleratorPollMicroOp : SystemDeviceCommandMicroOp
    {
        public AcceleratorPollMicroOp(
            ushort destinationRegister = VLIW_Instruction.NoReg,
            ushort tokenRegister = VLIW_Instruction.NoReg)
            : base(
                Processor.CPU_Core.IsaOpcodeValues.ACCEL_POLL,
                SystemDeviceCommandKind.Poll,
                Arch.SerializationClass.CsrOrdered,
                destinationRegister,
                tokenRegister)
        {
        }
    }

    public sealed class AcceleratorWaitMicroOp : SystemDeviceCommandMicroOp
    {
        public AcceleratorWaitMicroOp(
            ushort destinationRegister = VLIW_Instruction.NoReg,
            ushort tokenRegister = VLIW_Instruction.NoReg)
            : base(
                Processor.CPU_Core.IsaOpcodeValues.ACCEL_WAIT,
                SystemDeviceCommandKind.Wait,
                Arch.SerializationClass.FullSerial,
                destinationRegister,
                tokenRegister)
        {
        }
    }

    public sealed class AcceleratorStatusMicroOp : SystemDeviceCommandMicroOp
    {
        public AcceleratorStatusMicroOp(
            ushort destinationRegister = VLIW_Instruction.NoReg,
            ushort tokenRegister = VLIW_Instruction.NoReg)
            : base(
                Processor.CPU_Core.IsaOpcodeValues.ACCEL_STATUS,
                SystemDeviceCommandKind.Status,
                Arch.SerializationClass.CsrOrdered,
                destinationRegister,
                tokenRegister)
        {
        }
    }

    public sealed class AcceleratorCancelMicroOp : SystemDeviceCommandMicroOp
    {
        public AcceleratorCancelMicroOp(
            ushort destinationRegister = VLIW_Instruction.NoReg,
            ushort tokenRegister = VLIW_Instruction.NoReg)
            : base(
                Processor.CPU_Core.IsaOpcodeValues.ACCEL_CANCEL,
                SystemDeviceCommandKind.Cancel,
                Arch.SerializationClass.FullSerial,
                destinationRegister,
                tokenRegister)
        {
        }
    }

    public sealed class AcceleratorFenceMicroOp : SystemDeviceCommandMicroOp
    {
        public AcceleratorFenceMicroOp(
            ushort destinationRegister = VLIW_Instruction.NoReg,
            ushort tokenRegister = VLIW_Instruction.NoReg)
            : base(
                Processor.CPU_Core.IsaOpcodeValues.ACCEL_FENCE,
                SystemDeviceCommandKind.Fence,
                Arch.SerializationClass.FullSerial,
                destinationRegister,
                tokenRegister)
        {
        }
    }
}
