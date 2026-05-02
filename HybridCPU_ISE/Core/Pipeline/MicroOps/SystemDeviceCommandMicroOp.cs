using System;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Auth;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;

namespace YAKSys_Hybrid_CPU.Core
{
    public enum SystemDeviceCommandKind : byte
    {
        QueryCaps,
        Submit,
        Poll,
        Wait,
        Cancel,
        Fence
    }

    /// <summary>
    /// Fail-closed L7-SDC lane7 system-device command carrier.
    /// Phase 03 publishes opcode identity and hard-pinned placement. Later phases
    /// layer descriptor and guarded token/register ABI model APIs around the
    /// carrier while keeping direct execution, backend execution, staged writes,
    /// architectural commit, and fallback routing unavailable.
    /// </summary>
    public abstract class SystemDeviceCommandMicroOp : MicroOp
    {
        protected SystemDeviceCommandMicroOp(
            ushort opCode,
            SystemDeviceCommandKind commandKind,
            Arch.SerializationClass serializationClass,
            AcceleratorCommandDescriptor? commandDescriptor = null)
        {
            OpCode = opCode;
            CommandKind = commandKind;
            CommandDescriptor = commandDescriptor;
            CommandDescriptorReference = commandDescriptor?.DescriptorReference;
            IsStealable = false;
            IsMemoryOp = false;
            IsControlFlow = false;
            HasSideEffects = true;
            Class = MicroOpClass.Other;
            WritesRegister = false;

            InstructionClass = Arch.InstructionClass.System;
            SerializationClass = serializationClass;
            SetHardPinnedPlacement(SlotClass.SystemSingleton, 7);

            InitializeMetadata();
        }

        public SystemDeviceCommandKind CommandKind { get; }

        public AcceleratorCommandDescriptor? CommandDescriptor { get; }

        public AcceleratorDescriptorReference? CommandDescriptorReference { get; }

        public override CanonicalDecodePublicationMode CanonicalDecodePublication =>
            CanonicalDecodePublicationMode.SelfPublishes;

        public void InitializeMetadata()
        {
            ReadRegisters = Array.Empty<int>();
            WriteRegisters = Array.Empty<int>();
            ReadMemoryRanges = Array.Empty<(ulong, ulong)>();
            WriteMemoryRanges = Array.Empty<(ulong, ulong)>();
            ResourceMask = ResourceBitset.Zero;

            PublishExplicitStructuralSafetyMask();
            RefreshAdmissionMetadata(this);
        }

        public override void RefreshWriteMetadata() => InitializeMetadata();

        public override bool Execute(ref Processor.CPU_Core core)
        {
            throw new InvalidOperationException(
                $"{GetType().Name} direct execution is unsupported and must fail closed. " +
                "L7-SDC carriers publish lane7 SystemSingleton placement and Phase 04 descriptor ABI parsing sideband evidence only; " +
                "guarded token lifecycle/register ABI models are explicit runtime-side APIs; " +
                "direct micro-op execution, backend execution, staged writes, staged write publication, commit, architectural rd writeback, and fallback routing are not implemented.");
        }

        public override string GetDescription() =>
            $"L7-SDC {CommandKind}: OpCode={OpcodeRegistry.GetMnemonicOrHex(OpCode)}";
    }

    public sealed class AcceleratorQueryCapsMicroOp : SystemDeviceCommandMicroOp
    {
        public AcceleratorQueryCapsMicroOp()
            : base(
                Processor.CPU_Core.IsaOpcodeValues.ACCEL_QUERY_CAPS,
                SystemDeviceCommandKind.QueryCaps,
                Arch.SerializationClass.CsrOrdered)
        {
        }
    }

    public sealed class AcceleratorSubmitMicroOp : SystemDeviceCommandMicroOp
    {
        public AcceleratorGuardDecision? SubmitGuardDecision { get; }

        public AcceleratorSubmitMicroOp()
            : base(
                Processor.CPU_Core.IsaOpcodeValues.ACCEL_SUBMIT,
                SystemDeviceCommandKind.Submit,
                Arch.SerializationClass.MemoryOrdered)
        {
        }

        public AcceleratorSubmitMicroOp(AcceleratorCommandDescriptor commandDescriptor)
            : base(
                Processor.CPU_Core.IsaOpcodeValues.ACCEL_SUBMIT,
                SystemDeviceCommandKind.Submit,
                Arch.SerializationClass.MemoryOrdered,
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
    }

    public sealed class AcceleratorPollMicroOp : SystemDeviceCommandMicroOp
    {
        public AcceleratorPollMicroOp()
            : base(
                Processor.CPU_Core.IsaOpcodeValues.ACCEL_POLL,
                SystemDeviceCommandKind.Poll,
                Arch.SerializationClass.CsrOrdered)
        {
        }
    }

    public sealed class AcceleratorWaitMicroOp : SystemDeviceCommandMicroOp
    {
        public AcceleratorWaitMicroOp()
            : base(
                Processor.CPU_Core.IsaOpcodeValues.ACCEL_WAIT,
                SystemDeviceCommandKind.Wait,
                Arch.SerializationClass.FullSerial)
        {
        }
    }

    public sealed class AcceleratorCancelMicroOp : SystemDeviceCommandMicroOp
    {
        public AcceleratorCancelMicroOp()
            : base(
                Processor.CPU_Core.IsaOpcodeValues.ACCEL_CANCEL,
                SystemDeviceCommandKind.Cancel,
                Arch.SerializationClass.FullSerial)
        {
        }
    }

    public sealed class AcceleratorFenceMicroOp : SystemDeviceCommandMicroOp
    {
        public AcceleratorFenceMicroOp()
            : base(
                Processor.CPU_Core.IsaOpcodeValues.ACCEL_FENCE,
                SystemDeviceCommandKind.Fence,
                Arch.SerializationClass.FullSerial)
        {
        }
    }
}
