using HybridCPU_ISE.Arch;

using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core.Memory;
using YAKSys_Hybrid_CPU.Core.Registers;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;


namespace YAKSys_Hybrid_CPU.Core
{
    public class NopMicroOp : MicroOp
    {
        public NopMicroOp()
        {
            // NOP doesn't read or write anything
            ReadRegisters = Array.Empty<int>();
            WriteRegisters = Array.Empty<int>();
            ReadMemoryRanges = Array.Empty<(ulong, ulong)>();
            WriteMemoryRanges = Array.Empty<(ulong, ulong)>();

            // Phase 8: Initialize ResourceMask for GRLB
            // NOP uses no resources
            ResourceMask = 0;

            // Phase 01: Typed-slot taxonomy — NOP placeholder, no class affinity
            SetClassFlexiblePlacement(SlotClass.Unclassified);
        }

        public override CanonicalDecodePublicationMode CanonicalDecodePublication =>
            CanonicalDecodePublicationMode.SelfPublishes;

        public override bool Execute(ref Processor.CPU_Core core)
        {
            return true;
        }

        public override string GetDescription()
        {
            return "NOP";
        }
    }

    /// <summary>
    /// Generic micro-operation for instructions that need custom execution
    /// </summary>
    public class GenericMicroOp : MicroOp
    {
        public VLIW_Instruction Instruction { get; set; }

        public GenericMicroOp()
        {
            // FSP Metadata: Generic operations — conservative, not stealable until properly classified
            IsStealable = false;

            // Cannot determine register/memory dependencies without instruction details
            ReadRegisters = Array.Empty<int>();
            WriteRegisters = Array.Empty<int>();
            ReadMemoryRanges = Array.Empty<(ulong, ulong)>();
            WriteMemoryRanges = Array.Empty<(ulong, ulong)>();

            // Phase 8: Initialize ResourceMask for GRLB
            // Conservative: assume it needs all resources (atomic operation)
            ResourceMask = ResourceMaskBuilder.ForAtomic();

            // Phase 01: Typed-slot taxonomy — conservative fallback
            SetPlacement(SlotClass.Unclassified, SlotPinningKind.HardPinned);
        }

        public override CanonicalDecodePublicationMode CanonicalDecodePublication =>
            CanonicalDecodePublicationMode.ProjectorPublishes;

        public override bool Execute(ref Processor.CPU_Core core)
        {
            // These operations are executed via legacy methods in decoder
            // This is a transitional MicroOp for instructions not yet fully migrated
            return true;
        }

        public override string GetDescription()
        {
            return $"Generic: OpCode={OpCode}";
        }
    }

    /// <summary>
    /// Retire-authoritative Atomic micro-operation.
    /// Execute resolves a non-mutating retire effect; memory mutation happens only
    /// when the WB retire contour applies that effect.

    public sealed class AtomicMicroOp : LoadStoreMicroOp
    {
        private AtomicRetireEffect _resolvedRetireEffect;

        public ulong Address { get; set; }

        public ushort BaseRegID { get; set; }

        public ushort SrcRegID { get; set; }

        public byte Size { get; set; }

        public override ulong MemoryAddress => Address;

        public AtomicMicroOp()
        {
            IsMemoryOp = true;
            HasSideEffects = true;
            Class = MicroOpClass.Lsu;

            InstructionClass = Arch.InstructionClass.Atomic;
            SerializationClass = Arch.SerializationClass.AtomicSerial;

            SetClassFlexiblePlacement(SlotClass.LsuClass);
        }

        public void InitializeMetadata()
        {
            const ushort noReg = VLIW_Instruction.NoReg;

            var readRegs = new List<int>();
            if (BaseRegID != noReg)
            {
                readRegs.Add(BaseRegID);
            }

            if (UsesSourceRegister && SrcRegID != noReg)
            {
                readRegs.Add(SrcRegID);
            }

            ReadRegisters = readRegs;
            WriteRegisters = WritesRegister && DestRegID != 0 && DestRegID != noReg
                ? new[] { (int)DestRegID }
                : Array.Empty<int>();
            ReadMemoryRanges = new[] { (Address, (ulong)Math.Max(Size, (byte)4)) };
            WriteMemoryRanges = new[] { (Address, (ulong)Math.Max(Size, (byte)4)) };

            ResourceMask = ResourceBitset.Zero;
            if (BaseRegID != noReg)
            {
                ResourceMask |= ResourceMaskBuilder.ForRegisterRead(BaseRegID);
            }

            if (UsesSourceRegister && SrcRegID != noReg)
            {
                ResourceMask |= ResourceMaskBuilder.ForRegisterRead(SrcRegID);
            }

            if (WritesRegister && DestRegID != 0 && DestRegID != noReg)
            {
                ResourceMask |= ResourceMaskBuilder.ForRegisterWrite(DestRegID);
            }

            ResourceMask |= ResourceMaskBuilder.ForAtomic();
            ResourceMask |= ResourceMaskBuilder.ForMemoryDomain(OwnerThreadId);

            PublishExplicitStructuralSafetyMask();
            RefreshAdmissionMetadata(this);
        }

        public override void RefreshWriteMetadata() => InitializeMetadata();

        public override bool Execute(ref Processor.CPU_Core core)
        {
            int vtId = NormalizeExecutionVtId(OwnerThreadId);
            ulong address = ReadUnifiedScalarSourceOperand(ref core, vtId, BaseRegID);
            ulong sourceValue = UsesSourceRegister
                ? ReadUnifiedScalarSourceOperand(ref core, vtId, SrcRegID)
                : 0;

            Address = address;
            _resolvedRetireEffect = core.AtomicMemoryUnit.ResolveRetireEffect(
                unchecked((ushort)OpCode),
                DestRegID,
                address,
                sourceValue,
                (int)core.CoreID,
                vtId);
            return true;
        }

        public AtomicRetireEffect CreateRetireEffect() => _resolvedRetireEffect;

        public override string GetDescription() =>
            $"Atomic: OpCode={OpCode}, Addr=0x{Address:X}, Size={Size}, Dest=R{DestRegID}";

        private bool UsesSourceRegister =>
            OpCode is not (
                (uint)Processor.CPU_Core.IsaOpcodeValues.LR_W or
                (uint)Processor.CPU_Core.IsaOpcodeValues.LR_D);
    }

    /// <summary>
    /// Fail-closed placeholder for custom HLS accelerator contours.
    /// The executable runtime carrier is not implemented truthfully yet, so any
    /// attempt to execute this MicroOp must trap at the API boundary instead of
    /// publishing success.

    public class CustomAcceleratorMicroOp : MicroOp
    {
        public ICustomAccelerator Accelerator { get; set; } = null!;
        public ulong[] Operands { get; set; } = Array.Empty<ulong>();
        public byte[] Config { get; set; } = Array.Empty<byte>();
        private ulong[] _results = Array.Empty<ulong>();

        public CustomAcceleratorMicroOp()
        {
            // FSP metadata depends on accelerator characteristics (stealable by default)
            Class = MicroOpClass.Other;

            // ISA v4 Phase 02: custom accelerators are System class, FullSerial ordering
            InstructionClass = Arch.InstructionClass.System;
            SerializationClass = Arch.SerializationClass.FullSerial;

            // Phase 01: Typed-slot taxonomy — will be configurable per-accelerator in future phases
            SetPlacement(SlotClass.Unclassified, SlotPinningKind.HardPinned);
        }

        public override CanonicalDecodePublicationMode CanonicalDecodePublication =>
            CanonicalDecodePublicationMode.ProjectorPublishes;

        /// <summary>
        /// Initialize metadata for custom accelerator operation.
        /// Call this after setting up Accelerator and operand register IDs.
        /// Phase: Safety Tags & Certificates - Mandatory safety mask computation.
        /// </summary>
        public void InitializeMetadata(int acceleratorId, int[] inputRegIds, int outputRegId)
        {
            // Set register dependencies
            ReadRegisters = inputRegIds;
            if (WritesRegister)
            {
                WriteRegisters = new[] { outputRegId };
            }

            // Phase 8: Initialize ResourceMask for GRLB
            ResourceMask = ResourceBitset.Zero;

            // Add accelerator resource
            ResourceMask |= ResourceMaskBuilder.ForAccelerator(acceleratorId);

            // Add register read resources
            foreach (int regId in inputRegIds)
            {
                ResourceMask |= ResourceMaskBuilder.ForRegisterRead(regId);
            }

            // Add register write resource
            if (WritesRegister)
            {
                ResourceMask |= ResourceMaskBuilder.ForRegisterWrite(outputRegId);
            }

            PublishExplicitStructuralSafetyMask();
            RefreshAdmissionMetadata(this);
        }

        public override bool Execute(ref Processor.CPU_Core core)
        {
            throw InstructionRegistry.CreateUnsupportedCustomAcceleratorException(OpCode);
        }

        public override void EmitWriteBackRetireRecords(
            ref Processor.CPU_Core core,
            Span<RetireRecord> retireRecords,
            ref int retireRecordCount)
        {
            if (WritesRegister && _results != null && _results.Length > 0 &&
                DestRegID != VLIW_Instruction.NoReg)
            {
                int vtId = NormalizeExecutionVtId(OwnerThreadId);
                AppendWriteBackRetireRecord(
                    retireRecords,
                    ref retireRecordCount,
                    RetireRecord.RegisterWrite(vtId, DestRegID, _results[0]));
            }
        }

        public override bool TryGetPrimaryWriteBackResult(out ulong value)
        {
            if (WritesRegister && _results != null && _results.Length > 0)
            {
                value = _results[0];
                return true;
            }

            value = 0;
            return false;
        }

        public override void CapturePrimaryWriteBackResult(ulong value)
        {
            if (_results == null || _results.Length == 0)
            {
                _results = new[] { value };
                return;
            }

            _results[0] = value;
        }

        public override string GetDescription()
        {
            return $"CustomAccel[{Accelerator.Name}]: OpCode=0x{OpCode:X}";
        }
    }

    /// <summary>
    /// Halt micro-operation — stops pipeline execution (CPU_Stop).
    /// Sets the active live PC past memory end so the pipeline loop terminates.
    /// </summary>
    public class HaltMicroOp : MicroOp
    {
        public HaltMicroOp()
        {
            IsStealable = false;
            IsControlFlow = true;
            HasSideEffects = true;
            Class = MicroOpClass.Other;
            ResourceMask = ResourceBitset.Zero;

            // ISA v4 Phase 02: halt is System class, FullSerial serialization
            InstructionClass = Arch.InstructionClass.System;
            SerializationClass = Arch.SerializationClass.FullSerial;

            // Phase 01: Typed-slot taxonomy — system singleton pinned to lane 7
            SetHardPinnedPlacement(SlotClass.SystemSingleton, 7);
            PublishExplicitStructuralSafetyMask();
        }

        public override CanonicalDecodePublicationMode CanonicalDecodePublication =>
            CanonicalDecodePublicationMode.ProjectorPublishes;

        public override bool Execute(ref Processor.CPU_Core core)
        {
            core.RedirectActiveExecutionForControlFlow(core.GetBoundMainMemoryLength());
            return true;
        }

        public override string GetDescription() => "HALT (CPU_Stop)";
    }

    /// <summary>
    /// Retained move-family micro-operation.
    /// Handles canonical single-destination register Move (DT=0) and
    /// Move_Num (DT=1). Retired DT=2/3 memory shapes must be canonicalized
    /// to Load/Store before runtime execution, while DT=4/5 fail closed.
    /// </summary>
    public class MoveMicroOp : MicroOp
    {
        public VLIW_Instruction Instruction { get; set; }
        private byte _projectedDataType;
        private ushort _projectedReg1Id;
        private ushort _projectedReg2Id;
        private ulong _projectedPrimaryPayload;
        private bool _hasProjectedMoveShape;
        private ulong _loadedValue;
        private ulong _primaryWriteValue;
        public MoveMicroOp()
        {
            Class = MicroOpClass.Alu;
            ResourceMask = ResourceBitset.Zero;

            // ISA v4 Phase 02: Move is ScalarAlu class, Free ordering
            InstructionClass = Arch.InstructionClass.ScalarAlu;
            SerializationClass = Arch.SerializationClass.Free;

            // Phase 01: Typed-slot taxonomy — static classification by predominant pattern (DT=0,1).
            // DT=2,3 runtime reclassification to LSU deferred to future phases.
            SetClassFlexiblePlacement(SlotClass.AluClass);
        }

        public override CanonicalDecodePublicationMode CanonicalDecodePublication =>
            CanonicalDecodePublicationMode.SelfPublishes;

        public override void RefreshWriteMetadata()
        {
            InitializeMetadata();
            ApplyCanonicalDirectFactoryProjection();
        }

        internal void ApplyCanonicalRuntimeMoveShapeProjection(
            byte dataType,
            ushort reg1Id,
            ushort reg2Id,
            ulong primaryPayload)
        {
            ThrowIfUnsupportedRetainedRegisterContour(dataType);
            _projectedDataType = dataType;
            _projectedReg1Id = reg1Id;
            _projectedReg2Id = reg2Id;
            _projectedPrimaryPayload = primaryPayload;
            _hasProjectedMoveShape = true;
        }

        private byte ResolveMoveDataType() =>
            _hasProjectedMoveShape
                ? _projectedDataType
                : Instruction.DataType;

        private ushort ResolveMoveReg1Id() =>
            _hasProjectedMoveShape
                ? _projectedReg1Id
                : Instruction.Reg1ID;

        private ushort ResolveMoveReg2Id() =>
            _hasProjectedMoveShape
                ? _projectedReg2Id
                : Instruction.Reg2ID;

        private ulong ResolveMovePrimaryPayload() =>
            _hasProjectedMoveShape
                ? _projectedPrimaryPayload
                : Instruction.Src2Pointer;

        private void InitializeMetadata()
        {
            const ushort noReg = VLIW_Instruction.NoReg;
            byte dataType = ResolveMoveDataType();
            ushort reg1Id = ResolveMoveReg1Id();
            ushort reg2Id = ResolveMoveReg2Id();

            var readRegs = new List<int>();
            var writeRegs = new List<int>();

            switch (dataType)
            {
                case 0:
                    if (reg1Id != noReg) readRegs.Add(reg1Id);
                    if (reg2Id != noReg) writeRegs.Add(reg2Id);
                    break;
                case 1:
                case 3:
                    if (reg1Id != noReg) writeRegs.Add(reg1Id);
                    break;
                case 2:
                    if (reg1Id != noReg) readRegs.Add(reg1Id);
                    break;
                case 4:
                case 5:
                    throw CreateUnsupportedRetainedRegisterContourException(dataType);
                default:
                    throw new InvalidOperationException(
                        $"Move DT={dataType} is unsupported on the runtime MoveMicroOp surface.");
            }

            ReadRegisters = readRegs;
            WriteRegisters = writeRegs.Count == 0 ? Array.Empty<int>() : writeRegs.ToArray();

            ResourceMask = ResourceBitset.Zero;
            for (int i = 0; i < readRegs.Count; i++)
                ResourceMask |= ResourceMaskBuilder.ForRegisterRead(readRegs[i]);
            for (int i = 0; i < writeRegs.Count; i++)
                ResourceMask |= ResourceMaskBuilder.ForRegisterWrite(writeRegs[i]);

            if (dataType == 2)
                ResourceMask |= ResourceMaskBuilder.ForStore();
            else if (dataType == 3)
                ResourceMask |= ResourceMaskBuilder.ForLoad();
        }

        private void ApplyCanonicalDirectFactoryProjection()
        {
            byte dataType = ResolveMoveDataType();
            bool isStoreShape = OpCode == Processor.CPU_Core.IsaOpcodeValues.Store || dataType == 2;
            bool isLoadShape = OpCode == Processor.CPU_Core.IsaOpcodeValues.Load || dataType == 3;
            bool isMemoryMoveFamilyOp = IsMemoryOp || isStoreShape || isLoadShape;
            bool writesRegister = WriteRegisters.Count > 0;

            // Defensive classification only: if a manually projected MoveMicroOp still
            // carries a retired DT=2/3 memory shape, keep metadata aligned with the
            // memory contour until the fail-closed execute guard rejects it.
            Class = isMemoryMoveFamilyOp
                ? MicroOpClass.Lsu
                : MicroOpClass.Alu;
            HasSideEffects = isStoreShape;

            ApplyCanonicalDecodeProjection(
                isMemoryMoveFamilyOp
                    ? Arch.InstructionClass.Memory
                    : Arch.InstructionClass.ScalarAlu,
                Arch.SerializationClass.Free,
                new SlotPlacementMetadata
                {
                    RequiredSlotClass = isMemoryMoveFamilyOp
                        ? SlotClass.LsuClass
                        : SlotClass.AluClass,
                    PinningKind = SlotPinningKind.ClassFlexible,
                    PinnedLaneId = 0,
                    DomainTag = Placement.DomainTag
                },
                isMemoryMoveFamilyOp,
                isControlFlow: false,
                writesRegister,
                ReadRegisters,
                WriteRegisters);
        }

        private void ResetStagedRegisterWrites()
        {
            _primaryWriteValue = 0;
        }

        private static InvalidOperationException CreateRetiredMemoryContourException(
            byte dataType,
            ulong address)
        {
            return new InvalidOperationException(
                $"Retired legacy Move DT={dataType} memory contour reached MoveMicroOp.Execute() at 0x{address:X}. " +
                "This path must fail closed and be canonicalized to Load/Store before runtime execution.");
        }

        private static InvalidOperationException CreateUnsupportedRetainedRegisterContourException(byte dataType)
        {
            return dataType switch
            {
                4 => new InvalidOperationException(
                    "Retained legacy Move DT=4 dual-write contour is unsupported and must fail closed before runtime materialization."),
                5 => new InvalidOperationException(
                    "Retained legacy Move DT=5 triple-destination contour is unsupported and must fail closed before runtime materialization."),
                _ => new InvalidOperationException(
                    $"Move DT={dataType} is unsupported on the runtime MoveMicroOp surface.")
            };
        }

        private static void ThrowIfUnsupportedRetainedRegisterContour(byte dataType)
        {
            if (dataType == 4 || dataType == 5)
                throw CreateUnsupportedRetainedRegisterContourException(dataType);
        }

        private void StagePrimaryRegisterWrite(ushort destRegId, ulong value)
        {
            DestRegID = destRegId;
            WritesRegister = destRegId != VLIW_Instruction.NoReg;
            _primaryWriteValue = value;
        }

        public override bool Execute(ref Processor.CPU_Core core)
        {
            byte dt = ResolveMoveDataType();
            ushort reg1 = ResolveMoveReg1Id();
            ushort reg2 = ResolveMoveReg2Id();
            ulong addr = ResolveMovePrimaryPayload(); // word2 = memory address or immediate
            int vtId = Math.Clamp(OwnerThreadId, 0, Processor.CPU_Core.SmtWays - 1);

            switch (dt)
            {
                case 0: // reg-to-reg Move
                    if (reg1 != VLIW_Instruction.NoReg &&
                        reg2 != VLIW_Instruction.NoReg)
                    {
                        ulong srcVal = core.ReadArch(vtId, reg1);
                        StagePrimaryRegisterWrite(reg2, srcVal);
                    }
                    break;

                case 1: // Move_Num (immediate → reg)
                    if (reg1 != VLIW_Instruction.NoReg)
                    {
                        StagePrimaryRegisterWrite(reg1, addr);
                    }
                    break;

                case 2: // Retired store contour must already be canonicalized to StoreMicroOp.
                case 3: // Retired load contour must already be canonicalized to LoadMicroOp.
                    throw CreateRetiredMemoryContourException(dt, addr);

                case 4:
                case 5:
                    throw CreateUnsupportedRetainedRegisterContourException(dt);
                default:
                    throw new InvalidOperationException(
                        $"Move DT={dt} is unsupported on the runtime MoveMicroOp surface.");
            }

            return true;
        }

        public override void EmitWriteBackRetireRecords(
            ref Processor.CPU_Core core,
            Span<RetireRecord> retireRecords,
            ref int retireRecordCount)
        {
            if (!WritesRegister || DestRegID == VLIW_Instruction.NoReg)
                return;

            int vtId = NormalizeExecutionVtId(OwnerThreadId);
            ulong primaryValue = ResolveMoveDataType() == 3 ? _loadedValue : _primaryWriteValue;
            AppendWriteBackRetireRecord(
                retireRecords,
                ref retireRecordCount,
                RetireRecord.RegisterWrite(vtId, DestRegID, primaryValue));
        }

        public override bool TryGetPrimaryWriteBackResult(out ulong value)
        {
            if (!WritesRegister || DestRegID == VLIW_Instruction.NoReg)
            {
                value = 0;
                return false;
            }

            value = ResolveMoveDataType() == 3 ? _loadedValue : _primaryWriteValue;
            return true;
        }

        public override void CapturePrimaryWriteBackResult(ulong value)
        {
            if (ResolveMoveDataType() == 3)
                _loadedValue = value;
            else
                _primaryWriteValue = value;
        }

        public override string GetDescription()
        {
            return $"Move: DT={ResolveMoveDataType()}, Reg1={ResolveMoveReg1Id()}";
        }
    }

    /// <summary>
    /// Increment/Decrement micro-operation.
    /// </summary>
    public class IncrDecrMicroOp : MicroOp
    {
        public bool IsDecrement { get; set; }
        private ulong _result;

        public IncrDecrMicroOp()
        {
            WritesRegister = true;
            Class = MicroOpClass.Alu;
            ResourceMask = ResourceBitset.Zero;

            // ISA v4 Phase 02: incr/decr is ScalarAlu class, Free ordering
            InstructionClass = Arch.InstructionClass.ScalarAlu;
            SerializationClass = Arch.SerializationClass.Free;

            // Phase 01: Typed-slot taxonomy
            SetClassFlexiblePlacement(SlotClass.AluClass);
        }

        public override CanonicalDecodePublicationMode CanonicalDecodePublication =>
            CanonicalDecodePublicationMode.ProjectorPublishes;

        public override bool Execute(ref Processor.CPU_Core core)
        {
            if (DestRegID != VLIW_Instruction.NoReg)
            {
                int vtId = Math.Clamp(OwnerThreadId, 0, Processor.CPU_Core.SmtWays - 1);
                ulong val = core.ReadArch(vtId, DestRegID);
                _result = IsDecrement ? val - 1 : val + 1;
            }
            return true;
        }

        public override void EmitWriteBackRetireRecords(
            ref Processor.CPU_Core core,
            Span<RetireRecord> retireRecords,
            ref int retireRecordCount)
        {
            if (!WritesRegister || DestRegID == VLIW_Instruction.NoReg)
                return;

            int vtId = NormalizeExecutionVtId(OwnerThreadId);
            AppendWriteBackRetireRecord(
                retireRecords,
                ref retireRecordCount,
                RetireRecord.RegisterWrite(vtId, DestRegID, _result));
        }

        public override bool TryGetPrimaryWriteBackResult(out ulong value)
        {
            value = _result;
            return WritesRegister && DestRegID != VLIW_Instruction.NoReg;
        }

        public override void CapturePrimaryWriteBackResult(ulong value) => _result = value;

        public override string GetDescription()
        {
            return $"{(IsDecrement ? "DEC" : "INC")} R{DestRegID}";
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SystemEventKind — V6 Phase 3 (A5)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Identifies the architectural event kind produced by a <see cref="SysEventMicroOp"/>.
    /// One value per canonical system or typed SMT/VT event instruction that flows
    /// through the explicit lane-generated event retire contour.
}
