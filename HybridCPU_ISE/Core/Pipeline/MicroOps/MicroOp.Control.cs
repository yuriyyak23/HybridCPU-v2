using HybridCPU_ISE.Arch;

using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core.Memory;
using YAKSys_Hybrid_CPU.Core.Registers;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;


namespace YAKSys_Hybrid_CPU.Core
{
    internal readonly struct BranchExecutionPayload
    {
        private BranchExecutionPayload(
            bool isExecutable,
            bool conditionMet,
            bool redirectsControlFlow,
            ulong resolvedRetireTargetAddress,
            ulong materializedResultValue)
        {
            IsExecutable = isExecutable;
            ConditionMet = conditionMet;
            RedirectsControlFlow = redirectsControlFlow;
            ResolvedRetireTargetAddress = resolvedRetireTargetAddress;
            MaterializedResultValue = materializedResultValue;
        }

        public bool IsExecutable { get; }

        public bool ConditionMet { get; }

        public bool RedirectsControlFlow { get; }

        public ulong ResolvedRetireTargetAddress { get; }

        public ulong MaterializedResultValue { get; }

        public static BranchExecutionPayload Invalid() =>
            new(
                isExecutable: false,
                conditionMet: false,
                redirectsControlFlow: false,
                resolvedRetireTargetAddress: 0,
                materializedResultValue: 0);

        public static BranchExecutionPayload CreateConditional(
            bool conditionMet,
            ulong resolvedRetireTargetAddress,
            ulong materializedResultValue) =>
            new(
                isExecutable: true,
                conditionMet,
                redirectsControlFlow: conditionMet,
                resolvedRetireTargetAddress: conditionMet
                    ? resolvedRetireTargetAddress
                    : 0,
                materializedResultValue);

        public static BranchExecutionPayload CreateUnconditional(
            ulong resolvedRetireTargetAddress,
            ulong materializedResultValue) =>
            new(
                isExecutable: true,
                conditionMet: true,
                redirectsControlFlow: true,
                resolvedRetireTargetAddress,
                materializedResultValue);
    }

    public sealed class BranchMicroOp : MicroOp
    {
        public ulong TargetAddress { get; set; }
        public bool IsConditional { get; set; }
        public bool ConditionMet { get; set; }
        public ushort Reg1ID { get; set; }
        public ushort Reg2ID { get; set; }
        public short RelativeTargetDisplacement { get; private set; }
        public bool HasRelativeTargetDisplacement { get; private set; }
        private bool _hasCapturedPrimaryWriteBackResult;
        private ulong _capturedPrimaryWriteBackResult;
        private bool _hasResolvedRetireTargetAddress;
        private ulong _resolvedRetireTargetAddress;

        public BranchMicroOp()
        {
            // FSP Metadata: Control flow operations CANNOT be stolen
            IsStealable = false;
            IsControlFlow = true;
            Class = MicroOpClass.Control;

            // ISA v4 Phase 02: control flow class; branches are Free ordering (no side effects)
            InstructionClass = Arch.InstructionClass.ControlFlow;
            SerializationClass = Arch.SerializationClass.Free;

            // Phase 01: Typed-slot taxonomy — branches pinned to lane 7
            SetHardPinnedPlacement(SlotClass.BranchControl, 7);
        }

        public override CanonicalDecodePublicationMode CanonicalDecodePublication =>
            CanonicalDecodePublicationMode.SelfPublishes;

        internal void ApplyCanonicalRuntimeOperandProjection(
            byte rd,
            byte rs1,
            byte rs2)
        {
            DestRegID = rd == VLIW_Instruction.NoArchReg
                ? VLIW_Instruction.NoReg
                : rd;
            Reg1ID = rs1 == VLIW_Instruction.NoArchReg
                ? VLIW_Instruction.NoReg
                : rs1;
            Reg2ID = rs2 == VLIW_Instruction.NoArchReg
                ? VLIW_Instruction.NoReg
                : rs2;
        }

        internal void ApplyCanonicalRuntimeTargetProjection(ulong targetAddress)
        {
            TargetAddress = targetAddress;
        }

        internal void ApplyCanonicalRuntimeRelativeTargetProjection(short relativeTargetDisplacement)
        {
            RelativeTargetDisplacement = relativeTargetDisplacement;
            HasRelativeTargetDisplacement = true;
        }

        private short ResolveRelativeTargetDisplacement()
        {
            if (!HasRelativeTargetDisplacement)
            {
                throw new InvalidOperationException(
                    "BranchMicroOp relative target resolution requires projected DecoderContext immediate handoff. " +
                    "Raw VLIW_Instruction.Immediate fallback is retired from the decoder-to-runtime ABI.");
            }

            return RelativeTargetDisplacement;
        }

        internal ulong ResolveConditionalTargetAddress(ulong executionPc)
        {
            return unchecked((ulong)((long)executionPc + ResolveRelativeTargetDisplacement()));
        }

        internal ulong ResolveUnconditionalTargetAddress(ulong executionPc, ulong baseValue = 0)
        {
            ushort opcode = unchecked((ushort)OpCode);
            return opcode switch
            {
                Processor.CPU_Core.IsaOpcodeValues.JAL =>
                    unchecked((ulong)((long)executionPc + ResolveRelativeTargetDisplacement())),
                Processor.CPU_Core.IsaOpcodeValues.JALR =>
                    unchecked((baseValue + (ulong)(long)ResolveRelativeTargetDisplacement()) & ~1UL),
                _ => TargetAddress
            };
        }

        internal ulong ResolveLinkRegisterValue(ulong executionPc) => executionPc + 4;

        internal void CaptureResolvedRetireTargetAddress(ulong targetAddress)
        {
            _resolvedRetireTargetAddress = targetAddress;
            _hasResolvedRetireTargetAddress = true;
        }

        private void ResetResolvedRetireTargetAddress()
        {
            _resolvedRetireTargetAddress = 0;
            _hasResolvedRetireTargetAddress = false;
        }

        internal BranchExecutionPayload ResolveExecutionPayload(
            ushort opcode,
            ulong executionPc,
            ulong operand1 = 0,
            ulong operand2 = 0,
            ulong baseValue = 0)
        {
            BranchExecutionPayload payload = IsConditional
                ? ResolveConditionalExecutionPayload(opcode, executionPc, operand1, operand2)
                : ResolveUnconditionalExecutionPayload(opcode, executionPc, baseValue);

            if (!payload.IsExecutable)
            {
                return payload;
            }

            ConditionMet = payload.ConditionMet;
            ResetResolvedRetireTargetAddress();
            if (payload.RedirectsControlFlow)
            {
                CaptureResolvedRetireTargetAddress(payload.ResolvedRetireTargetAddress);
            }

            return payload;
        }

        private BranchExecutionPayload ResolveConditionalExecutionPayload(
            ushort opcode,
            ulong executionPc,
            ulong operand1,
            ulong operand2)
        {
            bool branchTaken = EvaluateBranchCondition(opcode, operand1, operand2);
            ulong targetAddress = ResolveConditionalTargetAddress(executionPc);

            return BranchExecutionPayload.CreateConditional(
                branchTaken,
                targetAddress,
                branchTaken
                    ? targetAddress
                    : executionPc + 256);
        }

        private BranchExecutionPayload ResolveUnconditionalExecutionPayload(
            ushort opcode,
            ulong executionPc,
            ulong baseValue)
        {
            ulong targetPc = opcode switch
            {
                Processor.CPU_Core.IsaOpcodeValues.JAL =>
                    ResolveUnconditionalTargetAddress(executionPc),
                Processor.CPU_Core.IsaOpcodeValues.JALR =>
                    ResolveUnconditionalTargetAddress(executionPc, baseValue),
                _ => TargetAddress
            };

            if (targetPc == 0 &&
                opcode is not Processor.CPU_Core.IsaOpcodeValues.JAL &&
                opcode is not Processor.CPU_Core.IsaOpcodeValues.JALR)
            {
                return BranchExecutionPayload.Invalid();
            }

            return BranchExecutionPayload.CreateUnconditional(
                targetPc,
                WritesRegister
                    ? ResolveLinkRegisterValue(executionPc)
                    : 0);
        }

        private static bool EvaluateBranchCondition(
            ushort opcode,
            ulong operand1,
            ulong operand2)
        {
            return opcode switch
            {
                Processor.CPU_Core.IsaOpcodeValues.JumpIfEqual => operand1 == operand2,
                Processor.CPU_Core.IsaOpcodeValues.JumpIfNotEqual => operand1 != operand2,
                Processor.CPU_Core.IsaOpcodeValues.JumpIfBelow => operand1 < operand2,
                Processor.CPU_Core.IsaOpcodeValues.JumpIfBelowOrEqual => operand1 <= operand2,
                Processor.CPU_Core.IsaOpcodeValues.JumpIfAbove => operand1 > operand2,
                Processor.CPU_Core.IsaOpcodeValues.JumpIfAboveOrEqual => operand1 >= operand2,
                Processor.CPU_Core.IsaOpcodeValues.BEQ => operand1 == operand2,
                Processor.CPU_Core.IsaOpcodeValues.BNE => operand1 != operand2,
                Processor.CPU_Core.IsaOpcodeValues.BLT => (long)operand1 < (long)operand2,
                Processor.CPU_Core.IsaOpcodeValues.BGE => (long)operand1 >= (long)operand2,
                Processor.CPU_Core.IsaOpcodeValues.BLTU => operand1 < operand2,
                Processor.CPU_Core.IsaOpcodeValues.BGEU => operand1 >= operand2,
                _ => false
            };
        }

        /// <summary>
        /// Initialize FSP metadata after register IDs are set.
        /// Phase: Safety Tags & Certificates - Mandatory safety mask computation.
        /// </summary>
        public void InitializeMetadata()
        {
            ushort opcode = unchecked((ushort)OpCode);
            bool publishesLinkRegister =
                (opcode is Processor.CPU_Core.IsaOpcodeValues.JAL or Processor.CPU_Core.IsaOpcodeValues.JALR) &&
                DestRegID != 0 &&
                DestRegID != VLIW_Instruction.NoReg;

            WritesRegister = publishesLinkRegister;

            if (IsConditional)
            {
                ReadRegisters = new[] { (int)Reg1ID, (int)Reg2ID };
            }
            else if (opcode == Processor.CPU_Core.IsaOpcodeValues.JALR &&
                     Reg1ID != VLIW_Instruction.NoReg)
            {
                ReadRegisters = new[] { (int)Reg1ID };
            }
            else
            {
                ReadRegisters = Array.Empty<int>();
            }

            WriteRegisters = publishesLinkRegister
                ? new[] { (int)DestRegID }
                : Array.Empty<int>();

            // Phase 8: Initialize ResourceMask for GRLB
            ResourceMask = ResourceBitset.Zero;
            // Control flow operations read from registers for conditional branches
            if (IsConditional)
            {
                ResourceMask |= ResourceMaskBuilder.ForRegisterRead(Reg1ID);
                ResourceMask |= ResourceMaskBuilder.ForRegisterRead(Reg2ID);
            }
            else if (opcode == Processor.CPU_Core.IsaOpcodeValues.JALR &&
                     Reg1ID != VLIW_Instruction.NoReg)
            {
                ResourceMask |= ResourceMaskBuilder.ForRegisterRead(Reg1ID);
            }

            if (publishesLinkRegister)
            {
                ResourceMask |= ResourceMaskBuilder.ForRegisterWrite(DestRegID);
            }

            // Note: Control flow operations are not stealable.
            // so they don't conflict with other operations in FSP scenarios

            PublishExplicitStructuralSafetyMask();
            RefreshAdmissionMetadata(this);
        }

        public override void RefreshWriteMetadata() => InitializeMetadata();

        public override bool Execute(ref Processor.CPU_Core core)
        {
            // Evaluate condition if conditional branch
            if (IsConditional)
            {
                // Condition evaluation depends on specific branch type
                // For now, delegate to existing control flow methods
            }

            // Control flow execution handled by existing methods in Decoder
            return true;
        }

        public override string GetDescription()
        {
            return $"Branch: OpCode={OpCode}, Target=0x{TargetAddress:X}, " +
                   (IsConditional ? $"Conditional={ConditionMet}" : "Unconditional");
        }

        public override void EmitWriteBackRetireRecords(
            ref Processor.CPU_Core core,
            Span<RetireRecord> retireRecords,
            ref int retireRecordCount)
        {
            bool hasArchitecturalDestinationRegister =
                DestRegID != 0 &&
                DestRegID != VLIW_Instruction.NoReg;
            bool redirectsControlFlow = !IsConditional || ConditionMet;
            if (!redirectsControlFlow &&
                (!WritesRegister || !hasArchitecturalDestinationRegister))
            {
                return;
            }

            int vtId = NormalizeExecutionVtId(OwnerThreadId);

            if (WritesRegister && hasArchitecturalDestinationRegister)
            {
                if (!_hasCapturedPrimaryWriteBackResult)
                {
                    throw new InvalidOperationException(
                        "BranchMicroOp retire emission requires an execute-stage captured primary write-back value for the architectural link register.");
                }

                AppendWriteBackRetireRecord(
                    retireRecords,
                    ref retireRecordCount,
                    RetireRecord.RegisterWrite(vtId, DestRegID, _capturedPrimaryWriteBackResult));
            }

            if (!redirectsControlFlow)
            {
                return;
            }

            if (!_hasResolvedRetireTargetAddress)
            {
                throw new InvalidOperationException(
                    "BranchMicroOp retire emission requires an execute-stage resolved control-flow target address. " +
                    "Production control-flow retirement must not reconstruct JAL/JALR redirects from legacy decoder placeholders.");
            }

            AppendWriteBackRetireRecord(
                retireRecords,
                ref retireRecordCount,
                RetireRecord.PcWrite(vtId, _resolvedRetireTargetAddress));
        }

        public override bool TryGetPrimaryWriteBackResult(out ulong value)
        {
            value = _capturedPrimaryWriteBackResult;
            return _hasCapturedPrimaryWriteBackResult &&
                   WritesRegister &&
                   DestRegID != 0 &&
                   DestRegID != VLIW_Instruction.NoReg;
        }

        public override void CapturePrimaryWriteBackResult(ulong value)
        {
            _capturedPrimaryWriteBackResult = value;
            _hasCapturedPrimaryWriteBackResult = true;
        }
    }

    public enum CsrStorageSurface : byte
    {
        None = 0,
        VectorPodPlane = 1,
        WiredCsrFile = 2
    }

    /// <summary>
    /// Typed CSR retire payload carried by EX/MEM/WB lane state.
    /// Separates CSR read register-writeback from deferred CSR side effects.
    /// </summary>
    public readonly struct CsrRetireEffect
    {
        private CsrRetireEffect(
            bool clearsArchitecturalExceptionState,
            CsrStorageSurface storageSurface,
            ushort csrAddress,
            ulong readValue,
            bool hasRegisterWriteback,
            ushort destRegId,
            bool hasCsrWrite,
            ulong csrWriteValue)
        {
            ClearsArchitecturalExceptionState = clearsArchitecturalExceptionState;
            StorageSurface = storageSurface;
            CsrAddress = csrAddress;
            ReadValue = readValue;
            HasRegisterWriteback = hasRegisterWriteback;
            DestRegId = destRegId;
            HasCsrWrite = hasCsrWrite;
            CsrWriteValue = csrWriteValue;
        }

        public bool ClearsArchitecturalExceptionState { get; }

        public CsrStorageSurface StorageSurface { get; }

        public ushort CsrAddress { get; }

        public ulong ReadValue { get; }

        public bool HasRegisterWriteback { get; }

        public ushort DestRegId { get; }

        public bool HasCsrWrite { get; }

        public ulong CsrWriteValue { get; }

        public static CsrRetireEffect ClearExceptionCounters() =>
            new(
                clearsArchitecturalExceptionState: true,
                storageSurface: CsrStorageSurface.None,
                csrAddress: 0,
                readValue: 0,
                hasRegisterWriteback: false,
                destRegId: 0,
                hasCsrWrite: false,
                csrWriteValue: 0);

        public static CsrRetireEffect Create(
            CsrStorageSurface storageSurface,
            ushort csrAddress,
            ulong readValue,
            bool hasRegisterWriteback,
            ushort destRegId,
            bool hasCsrWrite,
            ulong csrWriteValue) =>
            new(
                clearsArchitecturalExceptionState: false,
                storageSurface,
                csrAddress,
                readValue,
                hasRegisterWriteback,
                destRegId,
                hasCsrWrite,
                csrWriteValue);
    }

    /// <summary>
    /// Typed Atomic retire payload carried by EX/MEM/WB lane state and by
    /// direct compat retire transactions.
    /// </summary>
    public readonly struct AtomicRetireEffect
    {
        private AtomicRetireEffect(
            bool isValid,
            ushort opcode,
            byte accessSize,
            ulong address,
            ulong sourceValue,
            ushort destinationRegister,
            ushort coreId,
            int virtualThreadId)
        {
            IsValid = isValid;
            Opcode = opcode;
            AccessSize = accessSize;
            Address = address;
            SourceValue = sourceValue;
            DestinationRegister = destinationRegister;
            CoreId = coreId;
            VirtualThreadId = virtualThreadId;
        }

        public bool IsValid { get; }

        public ushort Opcode { get; }

        public byte AccessSize { get; }

        public ulong Address { get; }

        public ulong SourceValue { get; }

        public ushort DestinationRegister { get; }

        public ushort CoreId { get; }

        public int VirtualThreadId { get; }

        public bool HasRegisterDestination =>
            DestinationRegister != 0 &&
            DestinationRegister != VLIW_Instruction.NoReg;

        public static AtomicRetireEffect Create(
            ushort opcode,
            byte accessSize,
            ulong address,
            ulong sourceValue,
            ushort destinationRegister,
            ushort coreId,
            int virtualThreadId) =>
            new(
                isValid: true,
                opcode,
                accessSize,
                address,
                sourceValue,
                destinationRegister,
                coreId,
                virtualThreadId);
    }

    /// <summary>
    /// Atomic retire/apply outcome materialized only at retire.
    /// </summary>
    public readonly struct AtomicRetireOutcome
    {
        private AtomicRetireOutcome(
            bool hasRegisterWriteback,
            ushort registerDestination,
            ulong registerWritebackValue,
            bool memoryMutated,
            TraceEventKind traceEventKind)
        {
            HasRegisterWriteback = hasRegisterWriteback;
            RegisterDestination = registerDestination;
            RegisterWritebackValue = registerWritebackValue;
            MemoryMutated = memoryMutated;
            TraceEventKind = traceEventKind;
        }

        public bool HasRegisterWriteback { get; }

        public ushort RegisterDestination { get; }

        public ulong RegisterWritebackValue { get; }

        public bool MemoryMutated { get; }

        public TraceEventKind TraceEventKind { get; }

        public static AtomicRetireOutcome Create(
            in AtomicRetireEffect effect,
            ulong registerWritebackValue,
            bool hasRegisterWriteback,
            bool memoryMutated) =>
            new(
                hasRegisterWriteback,
                effect.DestinationRegister,
                registerWritebackValue,
                memoryMutated,
                ClassifyTraceEventKind(effect, registerWritebackValue));

        private static TraceEventKind ClassifyTraceEventKind(
            in AtomicRetireEffect effect,
            ulong registerWritebackValue)
        {
            return effect.Opcode switch
            {
                Processor.CPU_Core.IsaOpcodeValues.LR_W or
                Processor.CPU_Core.IsaOpcodeValues.LR_D => TraceEventKind.LrExecuted,
                Processor.CPU_Core.IsaOpcodeValues.SC_W or
                Processor.CPU_Core.IsaOpcodeValues.SC_D =>
                    registerWritebackValue == 0 ? TraceEventKind.ScSucceeded : TraceEventKind.ScFailed,
                Processor.CPU_Core.IsaOpcodeValues.AMOSWAP_D or
                Processor.CPU_Core.IsaOpcodeValues.AMOADD_D or
                Processor.CPU_Core.IsaOpcodeValues.AMOXOR_D or
                Processor.CPU_Core.IsaOpcodeValues.AMOAND_D or
                Processor.CPU_Core.IsaOpcodeValues.AMOOR_D or
                Processor.CPU_Core.IsaOpcodeValues.AMOMIN_D or
                Processor.CPU_Core.IsaOpcodeValues.AMOMAX_D or
                Processor.CPU_Core.IsaOpcodeValues.AMOMINU_D or
                Processor.CPU_Core.IsaOpcodeValues.AMOMAXU_D => TraceEventKind.AmoDwordExecuted,
                _ => TraceEventKind.AmoWordExecuted
            };
        }
    }

    /// <summary>
     /// CSR (Control & Status Register) micro-operation
     /// </summary>
    public abstract class CSRMicroOp : MicroOp
    {
        private ulong _readValue;
        private bool _registerWritebackConfigured;
        private bool _registerWritebackCapabilitySeeded;

        public ulong CSRAddress { get; set; }
        public ulong WriteValue { get; set; }
        public ushort SrcRegID { get; set; }

        protected virtual bool ReadsCsr => true;
        protected virtual bool WritesCsr => false;
        protected virtual bool WritesFromSourceRegister => false;
        protected virtual bool UsesSourceRegisterWriteValue => false;
        protected virtual bool ClearsArchitecturalExceptionState => false;
        protected virtual string DescriptionVerb => "CSR";
        private static bool HasArchitecturalRegister(ushort registerId) =>
            registerId != 0 &&
            registerId != VLIW_Instruction.NoReg &&
            registerId != VLIW_Instruction.NoArchReg;
        private bool HasArchitecturalSourceRegister =>
            HasArchitecturalRegister(SrcRegID);
        private bool HasArchitecturalDestinationRegister =>
            HasArchitecturalRegister(DestRegID);

        protected CSRMicroOp()
        {
            IsStealable = false;
            HasSideEffects = true;

            InstructionClass = Arch.InstructionClass.Csr;
            SerializationClass = Arch.SerializationClass.CsrOrdered;

            SetHardPinnedPlacement(SlotClass.SystemSingleton, 7);
        }

        public override CanonicalDecodePublicationMode CanonicalDecodePublication =>
            CanonicalDecodePublicationMode.SelfPublishes;

        /// <summary>
        /// Initialize FSP metadata after register IDs are set.
        /// Phase: Safety Tags & Certificates - Mandatory safety mask computation.
        /// </summary>
        public void InitializeMetadata()
        {
            bool publishesSourceRegisterRead = WritesFromSourceRegister && HasArchitecturalSourceRegister;
            bool publishesRegisterWriteback = ReadsCsr &&
                                             IsRegisterWritebackConfigured() &&
                                             HasArchitecturalDestinationRegister;

            WritesRegister = publishesRegisterWriteback;

            ReadRegisters = publishesSourceRegisterRead
                ? new[] { (int)SrcRegID }
                : Array.Empty<int>();

            WriteRegisters = publishesRegisterWriteback
                ? new[] { (int)DestRegID }
                : Array.Empty<int>();

            ResourceMask = ResourceBitset.Zero;
            ResourceMask |= ResourceMaskBuilder.ForAtomic();

            if (publishesSourceRegisterRead)
            {
                ResourceMask |= ResourceMaskBuilder.ForRegisterRead(SrcRegID);
            }

            if (publishesRegisterWriteback)
            {
                ResourceMask |= ResourceMaskBuilder.ForRegisterWrite(DestRegID);
            }

            PublishExplicitStructuralSafetyMask();
            RefreshAdmissionMetadata(this);
        }

        /// <inheritdoc/>
        public override void RefreshWriteMetadata() => InitializeMetadata();

        public override bool Execute(ref Processor.CPU_Core core)
        {
            if (ClearsArchitecturalExceptionState)
            {
                return true;
            }

            if (ReadsCsr)
            {
                CsrStorageSurface storageSurface = ResolveStorageSurface(ref core, CSRAddress);
                _readValue = ReadCsr(ref core, storageSurface, CSRAddress);
            }

            return true;
        }

        public CsrRetireEffect CreateRetireEffect(ref Processor.CPU_Core core)
        {
            if (ClearsArchitecturalExceptionState)
            {
                return CsrRetireEffect.ClearExceptionCounters();
            }

            CsrStorageSurface storageSurface = ResolveStorageSurface(ref core, CSRAddress);
            ulong priorValue = ReadCsr(ref core, storageSurface, CSRAddress);
            bool hasRegisterWriteback = ReadsCsr &&
                                        WritesRegister &&
                                        HasArchitecturalDestinationRegister;
            bool hasCsrWrite = WritesCsr;
            ulong csrWriteValue = hasCsrWrite
                ? ResolveWriteValue(ref core, priorValue)
                : 0;

            return CsrRetireEffect.Create(
                storageSurface,
                (ushort)(CSRAddress & 0xFFFF),
                priorValue,
                hasRegisterWriteback,
                DestRegID,
                hasCsrWrite,
                csrWriteValue);
        }

        public override void EmitWriteBackRetireRecords(
            ref Processor.CPU_Core core,
            Span<RetireRecord> retireRecords,
            ref int retireRecordCount)
        {
            if (ReadsCsr &&
                WritesRegister &&
                HasArchitecturalDestinationRegister)
            {
                int vtId = NormalizeExecutionVtId(OwnerThreadId);
                AppendWriteBackRetireRecord(
                    retireRecords,
                    ref retireRecordCount,
                    RetireRecord.RegisterWrite(vtId, DestRegID, _readValue));
            }
        }

        public override bool TryGetPrimaryWriteBackResult(out ulong value)
        {
            value = _readValue;
            return ReadsCsr &&
                   WritesRegister &&
                   HasArchitecturalDestinationRegister;
        }

        public override void CapturePrimaryWriteBackResult(ulong value) => _readValue = value;

        public override string GetDescription()
        {
            if (ClearsArchitecturalExceptionState)
            {
                return "CSR_CLEAR";
            }

            return ReadsCsr
                ? $"{DescriptionVerb}: CSR=0x{CSRAddress:X}, Dest=R{DestRegID}"
                : $"{DescriptionVerb}: CSR=0x{CSRAddress:X}, Value=0x{WriteValue:X}";
        }

        protected virtual ulong ResolveWriteValue(ref Processor.CPU_Core core, ulong priorValue) => WriteValue;

        private bool IsRegisterWritebackConfigured()
        {
            if (!_registerWritebackCapabilitySeeded || WritesRegister)
            {
                _registerWritebackConfigured = WritesRegister;
                _registerWritebackCapabilitySeeded = true;
            }

            return _registerWritebackConfigured;
        }

        protected ulong ResolveConfiguredWriteValue(ref Processor.CPU_Core core)
        {
            if (!UsesSourceRegisterWriteValue)
            {
                return WriteValue;
            }

            if (!HasArchitecturalSourceRegister)
            {
                return WriteValue;
            }

            int vtId = NormalizeExecutionVtId(OwnerThreadId);
            return TryReadUnifiedArchValue(ref core, vtId, SrcRegID, out ulong value)
                ? value
                : WriteValue;
        }

        internal static CsrStorageSurface ResolveStorageSurface(ref Processor.CPU_Core core, ulong addr)
        {
            int csrAddr = (int)addr;

            if (csrAddr >= Processor.CPU_Core.CSR_POD_ID && csrAddr <= Processor.CPU_Core.CSR_NOC_ROUTE_CFG)
            {
                return CsrStorageSurface.VectorPodPlane;
            }

            switch (csrAddr)
            {
                case CsrAddresses.VexcpMask:
                case CsrAddresses.VexcpPri:
                case (int)Processor.CPU_Core.VectorCSR.VLIW_STEAL_ENABLE:
                case (int)Processor.CPU_Core.VectorCSR.VLIW_STEAL_MASK:
                case (int)Processor.CPU_Core.VectorCSR.VLIW_STEAL_POLICY:
                    return CsrStorageSurface.VectorPodPlane;
            }

            ushort wiredCsrAddress = (ushort)(addr & 0xFFFF);
            if (core.Csr.IsRegistered(wiredCsrAddress))
            {
                return CsrStorageSurface.WiredCsrFile;
            }

            throw new InvalidOperationException(
                $"CSR address 0x{wiredCsrAddress:X3} does not expose an authoritative CSR storage surface on the current mainline retire path; refusing hidden success/no-op.");
        }

        internal static ulong ReadCsr(
            ref Processor.CPU_Core core,
            CsrStorageSurface storageSurface,
            ulong addr)
        {
            int csrAddr = (int)addr;

            return storageSurface switch
            {
                CsrStorageSurface.VectorPodPlane => ReadVectorPodPlaneCsr(ref core, csrAddr),
                CsrStorageSurface.WiredCsrFile => core.Csr.Read((ushort)(addr & 0xFFFF), PrivilegeLevel.Machine),
                _ => throw new InvalidOperationException(
                    $"Unsupported CSR storage surface {storageSurface} for read at 0x{((ushort)(addr & 0xFFFF)):X3}.")
            };
        }

        internal static void WriteCsr(
            ref Processor.CPU_Core core,
            CsrStorageSurface storageSurface,
            ulong addr,
            ulong value)
        {
            int csrAddr = (int)addr;

            switch (storageSurface)
            {
                case CsrStorageSurface.VectorPodPlane:
                    WriteVectorPodPlaneCsr(ref core, csrAddr, value);
                    return;

                case CsrStorageSurface.WiredCsrFile:
                    core.Csr.Write((ushort)(addr & 0xFFFF), value, PrivilegeLevel.Machine);
                    return;

                default:
                    throw new InvalidOperationException(
                        $"Unsupported CSR storage surface {storageSurface} for write at 0x{((ushort)(addr & 0xFFFF)):X3}.");
            }
        }

        private static ulong ReadVectorPodPlaneCsr(ref Processor.CPU_Core core, int csrAddr)
        {
            if (csrAddr >= Processor.CPU_Core.CSR_POD_ID && csrAddr <= Processor.CPU_Core.CSR_NOC_ROUTE_CFG)
            {
                return core.ReadPodCSR(csrAddr);
            }

            return csrAddr switch
            {
                CsrAddresses.VexcpMask => core.ExceptionStatus.GetMask(),
                CsrAddresses.VexcpPri => ReadPackedExceptionPriorities(ref core),
                (int)Processor.CPU_Core.VectorCSR.VLIW_STEAL_ENABLE => (ulong)core.VectorConfig.FSP_Enabled,
                (int)Processor.CPU_Core.VectorCSR.VLIW_STEAL_MASK => (ulong)core.VectorConfig.FSP_StealMask,
                (int)Processor.CPU_Core.VectorCSR.VLIW_STEAL_POLICY => (ulong)core.VectorConfig.FSP_Policy,
                _ => throw new InvalidOperationException(
                    $"CSR address 0x{((ushort)csrAddr):X3} is not backed by the vector/pod CSR plane.")
            };
        }

        private static void WriteVectorPodPlaneCsr(ref Processor.CPU_Core core, int csrAddr, ulong value)
        {
            if (csrAddr >= Processor.CPU_Core.CSR_POD_ID && csrAddr <= Processor.CPU_Core.CSR_NOC_ROUTE_CFG)
            {
                core.WritePodCSR(csrAddr, value);
                return;
            }

            switch (csrAddr)
            {
                case CsrAddresses.VexcpMask:
                    core.ExceptionStatus.SetMask((byte)value);
                    return;
                case CsrAddresses.VexcpPri:
                    WritePackedExceptionPriorities(ref core, value);
                    return;
                case (int)Processor.CPU_Core.VectorCSR.VLIW_STEAL_ENABLE:
                    core.VectorConfig.FSP_Enabled = (byte)(value & 1);
                    return;
                case (int)Processor.CPU_Core.VectorCSR.VLIW_STEAL_MASK:
                    core.VectorConfig.FSP_StealMask = (byte)(value & 0xFF);
                    return;
                case (int)Processor.CPU_Core.VectorCSR.VLIW_STEAL_POLICY:
                    core.VectorConfig.FSP_Policy = (byte)(value & 0xFF);
                    return;
                default:
                    throw new InvalidOperationException(
                        $"CSR address 0x{((ushort)csrAddr):X3} is not writable via the vector/pod CSR plane.");
            }
        }

        private static ulong ReadPackedExceptionPriorities(ref Processor.CPU_Core core)
        {
            ulong packed = 0;
            for (int i = 0; i < 5; i++)
            {
                packed |= (ulong)(core.ExceptionStatus.GetPriority(i) & 0x7) << (i * 3);
            }

            return packed;
        }

        private static void WritePackedExceptionPriorities(ref Processor.CPU_Core core, ulong value)
        {
            for (int i = 0; i < 5; i++)
            {
                core.ExceptionStatus.SetPriority(i, (byte)((value >> (i * 3)) & 0x7));
            }
        }
    }

    public sealed class CsrReadWriteMicroOp : CSRMicroOp
    {
        protected override bool WritesCsr => true;
        protected override bool WritesFromSourceRegister => true;
        protected override bool UsesSourceRegisterWriteValue => true;
        protected override string DescriptionVerb => "CSR_READ_WRITE";

        protected override ulong ResolveWriteValue(ref Processor.CPU_Core core, ulong priorValue) =>
            ResolveConfiguredWriteValue(ref core);
    }

    public sealed class CsrReadSetMicroOp : CSRMicroOp
    {
        protected override bool WritesCsr => UsesSourceRegisterWriteValue;
        protected override bool WritesFromSourceRegister => UsesSourceRegisterWriteValue;
        protected override bool UsesSourceRegisterWriteValue =>
            SrcRegID != 0 && SrcRegID != VLIW_Instruction.NoReg;
        protected override string DescriptionVerb => "CSR_READ_SET";

        protected override ulong ResolveWriteValue(ref Processor.CPU_Core core, ulong priorValue) =>
            priorValue | ResolveConfiguredWriteValue(ref core);
    }

    public sealed class CsrReadClearMicroOp : CSRMicroOp
    {
        protected override bool WritesCsr => UsesSourceRegisterWriteValue;
        protected override bool WritesFromSourceRegister => UsesSourceRegisterWriteValue;
        protected override bool UsesSourceRegisterWriteValue =>
            SrcRegID != 0 && SrcRegID != VLIW_Instruction.NoReg;
        protected override string DescriptionVerb => "CSR_READ_CLEAR";

        protected override ulong ResolveWriteValue(ref Processor.CPU_Core core, ulong priorValue) =>
            priorValue & ~ResolveConfiguredWriteValue(ref core);
    }

    public sealed class CsrReadWriteImmediateMicroOp : CSRMicroOp
    {
        protected override bool WritesCsr => true;
        protected override bool WritesFromSourceRegister => false;
        protected override bool UsesSourceRegisterWriteValue => false;
        protected override string DescriptionVerb => "CSR_READ_WRITE_IMM";
    }

    public sealed class CsrReadSetImmediateMicroOp : CSRMicroOp
    {
        protected override bool WritesCsr => WriteValue != 0;
        protected override bool WritesFromSourceRegister => false;
        protected override bool UsesSourceRegisterWriteValue => false;
        protected override string DescriptionVerb => "CSR_READ_SET_IMM";

        protected override ulong ResolveWriteValue(ref Processor.CPU_Core core, ulong priorValue) =>
            priorValue | WriteValue;
    }

    public sealed class CsrReadClearImmediateMicroOp : CSRMicroOp
    {
        protected override bool WritesCsr => WriteValue != 0;
        protected override bool WritesFromSourceRegister => false;
        protected override bool UsesSourceRegisterWriteValue => false;
        protected override string DescriptionVerb => "CSR_READ_CLEAR_IMM";

        protected override ulong ResolveWriteValue(ref Processor.CPU_Core core, ulong priorValue) =>
            priorValue & ~WriteValue;
    }

    public sealed class CsrClearMicroOp : CSRMicroOp
    {
        protected override bool ReadsCsr => false;
        protected override bool ClearsArchitecturalExceptionState => true;
        protected override string DescriptionVerb => "CSR_CLEAR";
    }

    /// <summary>
    /// NOP (No Operation) micro-operation
}
