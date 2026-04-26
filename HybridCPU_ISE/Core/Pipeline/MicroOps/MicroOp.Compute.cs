using HybridCPU_ISE.Arch;

using YAKSys_Hybrid_CPU.Core.Registers.Retire;


namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Scalar ALU micro-operation (register-to-register arithmetic)
    /// </summary>
    public class ScalarALUMicroOp : MicroOp
    {
        public ushort Src1RegID { get; set; }
        public ushort Src2RegID { get; set; }
        public ulong Immediate { get; set; }
        public bool UsesImmediate { get; set; }

        private ulong _result;

        public ScalarALUMicroOp()
        {
            Class = MicroOpClass.Alu;

            // ISA v4 Phase 02: instruction classification
            InstructionClass = Arch.InstructionClass.ScalarAlu;
            SerializationClass = Arch.SerializationClass.Free;

            // Phase 01: Typed-slot taxonomy
            SetClassFlexiblePlacement(SlotClass.AluClass);
        }

        public override CanonicalDecodePublicationMode CanonicalDecodePublication =>
            CanonicalDecodePublicationMode.SelfPublishes;

        /// <summary>
        /// Initialize FSP metadata after register IDs are set.
        /// Phase: Safety Tags & Certificates - Mandatory safety mask computation.
        /// </summary>
        public void InitializeMetadata()
        {
            const ushort noReg = VLIW_Instruction.NoReg;

            // Read from source registers — skip NoReg sentinels
            var readRegs = new List<int>();
            if (Src1RegID != noReg) readRegs.Add(Src1RegID);
            if (!UsesImmediate && Src2RegID != noReg) readRegs.Add(Src2RegID);
            ReadRegisters = readRegs;

            // Write to destination register — skip NoReg sentinels
            if (WritesRegister && DestRegID != noReg)
            {
                WriteRegisters = new[] { (int)DestRegID };
            }

            // Phase 8: Initialize ResourceMask for GRLB
            ResourceMask = ResourceBitset.Zero;
            // Add register read resources (guard against NoReg)
            if (Src1RegID != noReg) ResourceMask |= ResourceMaskBuilder.ForRegisterRead(Src1RegID);
            if (!UsesImmediate && Src2RegID != noReg)
            {
                ResourceMask |= ResourceMaskBuilder.ForRegisterRead(Src2RegID);
            }
            // Add register write resources (guard against NoReg)
            if (WritesRegister && DestRegID != noReg)
            {
                ResourceMask |= ResourceMaskBuilder.ForRegisterWrite(DestRegID);
            }

            // Phase: Safety Tags & Certificates - Mandatory SafetyMask initialization
            // SafetyMask must be computed for all operations to enable fast verification
            RefreshAdmissionMetadata(this);
        }

        /// <inheritdoc/>
        /// Calls <see cref="InitializeMetadata"/> so that <see cref="MicroOp.WriteRegisters"/>
        /// and <see cref="MicroOp.ResourceMask"/> are re-populated after <see cref="MicroOp.WritesRegister"/>
        /// is promoted by a descriptor override in <see cref="InstructionRegistry.CreateMicroOp"/>.
        public override void RefreshWriteMetadata() => InitializeMetadata();

        public override bool Execute(ref Processor.CPU_Core core)
        {
            // Blueprint §6 / §8: Read operands via Physical Register File through RenameMap.
            // "вместо чтения Processor.CPU_Core.IntRegisters, получать Phys = RenameMap[OwnerThreadId][SrcReg]
            //  и читать PhysicalRegisters[Phys]."
            int vtId = NormalizeExecutionVtId(OwnerThreadId);
            ulong op1 = ReadUnifiedScalarSourceOperand(ref core, vtId, Src1RegID);
            ulong op2 = UsesImmediate
                ? Immediate
                : ReadUnifiedScalarSourceOperand(ref core, vtId, Src2RegID);
            ulong executionPc = core.ResolveCurrentScalarMicroOpExecutionPc();

            // Execute based on opcode
            _result = ExecuteScalarOp(OpCode, op1, op2, executionPc);
            return true;
        }

        public override void EmitWriteBackRetireRecords(
            ref Processor.CPU_Core core,
            Span<RetireRecord> retireRecords,
            ref int retireRecordCount)
        {
            if (!WritesRegister)
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
            return WritesRegister;
        }

        public override void CapturePrimaryWriteBackResult(ulong value) => _result = value;

        /// <summary>
        /// Delegates all scalar ALU computation to the canonical <see cref="ScalarAluOps.Compute"/>
        /// helper (Blueprint §7: unified ALU, removes duplicated switch).
        ///
        /// When <see cref="UsesImmediate"/> is <see langword="true"/>, <paramref name="op2"/>
        /// already contains the immediate value sourced from the instruction's <c>Immediate</c>
        /// field (see the <c>Execute()</c> method above).  In that case we pass <paramref name="op2"/>
        /// as <em>both</em> the second register operand <em>and</em> the <c>immediate</c>
        /// parameter so that shift instructions and <c>Move_Num</c> use the correct value on
        /// both the MicroOp-path and the pipeline-path.
        /// </summary>
        private ulong ExecuteScalarOp(uint opCode, ulong op1, ulong op2, ulong executionPc)
        {
            return ScalarAluOps.Compute(opCode, op1, op2, op2, executionPc);
        }

        public override string GetDescription()
        {
            return $"ScalarALU: OpCode={OpCode}, Dest=R{DestRegID}, Src1=R{Src1RegID}, " +
                   (UsesImmediate ? $"Imm=0x{Immediate:X}" : $"Src2=R{Src2RegID}");
        }
    }

    /// <summary>
    /// Vector ALU micro-operation (memory-to-memory stream operations)
    /// </summary>
    public class VectorALUMicroOp : MicroOp
    {
        public VLIW_Instruction Instruction { get; set; }

        public VectorALUMicroOp()
        {
            // Memory ranges will be populated by StreamEngine during execution
            IsMemoryOp = true;
            Class = MicroOpClass.Vector;

            // ISA v4 Phase 02: vector ops use ScalarAlu class (extension slot, not mandatory core)
            InstructionClass = Arch.InstructionClass.ScalarAlu;
            SerializationClass = Arch.SerializationClass.Free;

            // Phase 01: Typed-slot taxonomy — stream/vector ops routed through ALU lanes
            SetClassFlexiblePlacement(SlotClass.AluClass);
        }

        public override CanonicalDecodePublicationMode CanonicalDecodePublication =>
            CanonicalDecodePublicationMode.ProjectorPublishes;

        /// <summary>
        /// Initialize FSP metadata for vector operations.
        /// Vector operations typically work on memory ranges, not individual registers.
        /// Phase: Safety Tags & Certificates - Mandatory safety mask computation.
        /// </summary>
        public void InitializeMetadata()
        {
            // Vector operations may use vector registers, but metadata is complex
            // For now, mark as empty - will be enhanced in Phase 3
            ReadRegisters = Array.Empty<int>();
            WriteRegisters = Array.Empty<int>();

            // Memory ranges will be determined by StreamEngine based on addressing mode
            // These will be populated during Execute phase

            // Phase 8: Initialize ResourceMask for GRLB
            // Vector operations use stream engine 0 by default
            ResourceMask = ResourceMaskBuilder.ForStreamEngine(0);
            // Also mark as memory operation (both load and store for stream ops)
            ResourceMask |= ResourceMaskBuilder.ForLoad();
            ResourceMask |= ResourceMaskBuilder.ForStore();

            PublishExplicitStructuralSafetyMask();
            RefreshAdmissionMetadata(this);
        }

        public override bool Execute(ref Processor.CPU_Core core)
        {
            // Delegate to StreamEngine for execution
            int vtId = Math.Clamp(OwnerThreadId, 0, Processor.CPU_Core.SmtWays - 1);
            ResetStagedRegisterWrites();
            YAKSys_Hybrid_CPU.Execution.StreamEngine.Execute(ref core, Instruction, vtId);
            return true;
        }

        private void ResetStagedRegisterWrites()
        {
            WritesRegister = false;
            DestRegID = VLIW_Instruction.NoReg;
        }

        public override string GetDescription()
        {
            return $"VectorALU: OpCode={OpCode}, StreamLength={Instruction.StreamLength}";
        }
    }

    /// <summary>
    /// Abstract base class for Load/Store micro-operations.
    /// Provides explicit methods for managing speculative execution state.
    /// Phase 7: Speculative FSP with Silent Squash - Refactored for clarity and extensibility.
}

