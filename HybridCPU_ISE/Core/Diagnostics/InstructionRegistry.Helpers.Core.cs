using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers;
using OpcodeValues = YAKSys_Hybrid_CPU.Processor.CPU_Core.IsaOpcodeValues;

namespace YAKSys_Hybrid_CPU.Core
{
    public static partial class InstructionRegistry
    {
        private static ulong SignExtendScalarImmediate(ushort rawImmediate) =>
            unchecked((ulong)(long)(short)rawImmediate);

        private static ushort GetRequiredDecoderImmediate(
            in DecoderContext ctx,
            string contourName)
        {
            if (!ctx.HasImmediate)
            {
                throw new DecodeProjectionFaultException(
                    $"{contourName} requires projected DecoderContext immediate handoff. " +
                    "Raw VLIW_Instruction.Immediate fallback is retired from the decoder-to-runtime ABI.");
            }

            return ctx.Immediate;
        }

        private static byte GetRequiredDecoderDataType(
            in DecoderContext ctx,
            string contourName)
        {
            if (!ctx.HasDataType)
            {
                throw new DecodeProjectionFaultException(
                    $"{contourName} requires projected DecoderContext data-type handoff. " +
                    "Raw VLIW_Instruction.DataType fallback is retired from the decoder-to-runtime ABI.");
            }

            return ctx.DataType;
        }

        private static ulong GetRequiredDecoderMemoryAddress(
            in DecoderContext ctx,
            string contourName)
        {
            if (ctx.HasMemoryAddress)
            {
                return ctx.MemoryAddress;
            }

            if (ctx.AuxData != 0)
            {
                return ctx.AuxData;
            }

            if (ctx.HasImmediate)
            {
                return SignExtendScalarImmediate(ctx.Immediate);
            }

            throw new DecodeProjectionFaultException(
                $"{contourName} requires projected DecoderContext memory-address handoff. " +
                "Raw VLIW_Instruction.Immediate fallback is retired from the decoder-to-runtime ABI.");
        }

        private static bool TryDecodeCanonicalArchRegister(
            ushort rawRegId,
            out byte registerId)
        {
            if (rawRegId == ArchRegisterTripletEncoding.NoReg)
            {
                registerId = ArchRegisterTripletEncoding.NoArchReg;
                return true;
            }

            if (ArchRegId.TryCreate(rawRegId, out ArchRegId archRegId))
            {
                registerId = archRegId.Value;
                return true;
            }

            registerId = ArchRegisterTripletEncoding.NoArchReg;
            return false;
        }

        private static bool TryDecodeCanonicalOrUnpackedNoArchRegister(
            ushort rawRegId,
            out byte registerId)
        {
            if (rawRegId == ArchRegisterTripletEncoding.NoArchReg)
            {
                registerId = ArchRegisterTripletEncoding.NoArchReg;
                return true;
            }

            return TryDecodeCanonicalArchRegister(rawRegId, out registerId);
        }

        private static bool TryResolveCanonicalBranchPackedRegisters(
            in DecoderContext ctx,
            out byte rd,
            out byte rs1,
            out byte rs2)
        {
            rd = ArchRegisterTripletEncoding.NoArchReg;
            rs1 = ArchRegisterTripletEncoding.NoArchReg;
            rs2 = ArchRegisterTripletEncoding.NoArchReg;

            if (ctx.HasPackedRegisterTriplet &&
                ArchRegisterTripletEncoding.TryUnpack(
                    ctx.PackedRegisterTriplet,
                    out rd,
                    out rs1,
                    out rs2))
            {
                return true;
            }

            return TryDecodeCanonicalArchRegister(ctx.Reg1ID, out rd) &&
                   TryDecodeCanonicalArchRegister(ctx.Reg2ID, out rs1) &&
                   TryDecodeCanonicalArchRegister(ctx.Reg3ID, out rs2);
        }

        private static void ApplyCanonicalDirectBranchProjection(
            BranchMicroOp branchMicroOp,
            in DecoderContext ctx)
        {
            if (!TryResolveCanonicalBranchPackedRegisters(ctx, out byte rd, out byte rs1, out byte rs2))
            {
                return;
            }

            switch (unchecked((ushort)ctx.OpCode))
            {
                case OpcodeValues.JAL:
                    branchMicroOp.ApplyCanonicalRuntimeOperandProjection(
                        rd,
                        ArchRegisterTripletEncoding.NoArchReg,
                        ArchRegisterTripletEncoding.NoArchReg);
                    break;
                case OpcodeValues.JALR:
                    branchMicroOp.ApplyCanonicalRuntimeOperandProjection(
                        rd,
                        rs1,
                        ArchRegisterTripletEncoding.NoArchReg);
                    break;
                case OpcodeValues.BEQ:
                case OpcodeValues.BNE:
                case OpcodeValues.BLT:
                case OpcodeValues.BGE:
                case OpcodeValues.BLTU:
                case OpcodeValues.BGEU:
                    branchMicroOp.ApplyCanonicalRuntimeOperandProjection(
                        ArchRegisterTripletEncoding.NoArchReg,
                        rs1,
                        rs2);
                    break;
            }
        }

        private static ushort ResolveOpcode(
            in InstructionIR instruction) =>
            instruction.CanonicalOpcode.Value;

        internal static bool TryCreatePublishedControlFlowMicroOp(
            in InstructionIR instruction,
            out BranchMicroOp? branchMicroOp)
        {
            branchMicroOp = null;

            OpcodeInfo? opcodeInfo = OpcodeRegistry.GetInfo((uint)instruction.CanonicalOpcode);
            if (!opcodeInfo.HasValue || opcodeInfo.Value.InstructionClass != InstructionClass.ControlFlow)
            {
                return false;
            }

            uint opCode = (uint)instruction.CanonicalOpcode;
            if (!IsRegistered(opCode))
            {
                return false;
            }

            ushort opcode = ResolveOpcode(in instruction);
            branchMicroOp = new BranchMicroOp
            {
                OpCode = opCode,
                IsConditional = opcode is
                    OpcodeValues.BEQ or
                    OpcodeValues.BNE or
                    OpcodeValues.BLT or
                    OpcodeValues.BGE or
                    OpcodeValues.BLTU or
                    OpcodeValues.BGEU,
                TargetAddress = instruction.HasAbsoluteAddressing ? (ulong)instruction.Imm : 0,
            };

            ApplyCanonicalPublishedBranchProjection(
                branchMicroOp,
                opcode,
                instruction.Rd,
                instruction.Rs1,
                instruction.Rs2);
            branchMicroOp.ApplyCanonicalRuntimeRelativeTargetProjection(unchecked((short)instruction.Imm));
            branchMicroOp.InitializeMetadata();
            return true;
        }

        internal static bool TryResolvePublishedSystemEventKind(
            in InstructionIR instruction,
            out SystemEventKind systemEventKind)
        {
            systemEventKind = default;

            OpcodeInfo? opcodeInfo = OpcodeRegistry.GetInfo((uint)instruction.CanonicalOpcode);
            if (!opcodeInfo.HasValue ||
                (opcodeInfo.Value.InstructionClass is not (InstructionClass.System or InstructionClass.SmtVt)))
            {
                return false;
            }

            return TryResolvePublishedSystemEventKindFromOpcode(
                ResolveOpcode(in instruction),
                out systemEventKind);
        }

        internal static bool TryResolvePublishedSystemReadRegisters(
            in InstructionIR instruction,
            out IReadOnlyList<int> readRegisters)
        {
            readRegisters = Array.Empty<int>();

            OpcodeInfo? opcodeInfo = OpcodeRegistry.GetInfo((uint)instruction.CanonicalOpcode);
            if (!opcodeInfo.HasValue || opcodeInfo.Value.InstructionClass != InstructionClass.System)
            {
                return false;
            }

            return TryResolvePublishedSystemReadRegistersFromOpcode(
                ResolveOpcode(in instruction),
                instruction.Rs1,
                instruction.Rs2,
                out readRegisters);
        }

        internal static bool TryResolvePublishedStreamControlRetireContour(
            in InstructionIR instruction,
            out bool requiresSerializingBoundaryFollowThrough)
        {
            requiresSerializingBoundaryFollowThrough = false;

            OpcodeInfo? opcodeInfo = OpcodeRegistry.GetInfo((uint)instruction.CanonicalOpcode);
            if (!opcodeInfo.HasValue ||
                opcodeInfo.Value.InstructionClass is not (InstructionClass.System or InstructionClass.SmtVt))
            {
                return false;
            }

            return TryResolvePublishedStreamControlRetireContourFromOpcode(
                ResolveOpcode(in instruction),
                out requiresSerializingBoundaryFollowThrough);
        }

        internal static bool TryResolvePublishedAtomicAccessSize(
            in InstructionIR instruction,
            out byte accessSize)
        {
            accessSize = 0;

            OpcodeInfo? opcodeInfo = OpcodeRegistry.GetInfo((uint)instruction.CanonicalOpcode);
            if (!opcodeInfo.HasValue || opcodeInfo.Value.InstructionClass != InstructionClass.Atomic)
            {
                return false;
            }

            return TryResolvePublishedAtomicAccessSizeFromOpcode(
                ResolveOpcode(in instruction),
                out accessSize);
        }

        internal static bool TryResolvePublishedVmxOperationKind(
            in InstructionIR instruction,
            out VmxOperationKind operationKind)
        {
            operationKind = VmxOperationKind.None;

            OpcodeInfo? opcodeInfo = OpcodeRegistry.GetInfo((uint)instruction.CanonicalOpcode);
            if (!opcodeInfo.HasValue || opcodeInfo.Value.InstructionClass != InstructionClass.Vmx)
            {
                return false;
            }

            return TryResolvePublishedVmxOperationKindFromOpcode(
                ResolveOpcode(in instruction),
                out operationKind);
        }

        private static bool TryResolvePublishedSystemEventKindFromOpcode(
            ushort opcode,
            out SystemEventKind systemEventKind)
        {
            switch (opcode)
            {
                case OpcodeValues.FENCE:
                    systemEventKind = SystemEventKind.Fence;
                    return true;
                case OpcodeValues.FENCE_I:
                    systemEventKind = SystemEventKind.FenceI;
                    return true;
                case OpcodeValues.ECALL:
                    systemEventKind = SystemEventKind.Ecall;
                    return true;
                case OpcodeValues.EBREAK:
                    systemEventKind = SystemEventKind.Ebreak;
                    return true;
                case OpcodeValues.MRET:
                    systemEventKind = SystemEventKind.Mret;
                    return true;
                case OpcodeValues.SRET:
                    systemEventKind = SystemEventKind.Sret;
                    return true;
                case OpcodeValues.WFI:
                    systemEventKind = SystemEventKind.Wfi;
                    return true;
                case OpcodeValues.WFE:
                    systemEventKind = SystemEventKind.Wfe;
                    return true;
                case OpcodeValues.SEV:
                    systemEventKind = SystemEventKind.Sev;
                    return true;
                case OpcodeValues.YIELD:
                    systemEventKind = SystemEventKind.Yield;
                    return true;
                case OpcodeValues.POD_BARRIER:
                    systemEventKind = SystemEventKind.PodBarrier;
                    return true;
                case OpcodeValues.VT_BARRIER:
                    systemEventKind = SystemEventKind.VtBarrier;
                    return true;
                default:
                    systemEventKind = default;
                    return false;
            }
        }

        private static bool TryResolvePublishedSystemReadRegistersFromOpcode(
            ushort opcode,
            byte rs1,
            byte rs2,
            out IReadOnlyList<int> readRegisters)
        {
            switch (opcode)
            {
                case OpcodeValues.ECALL:
                    readRegisters = new[] { 17 };
                    return true;
                case OpcodeValues.FENCE:
                case OpcodeValues.FENCE_I:
                case OpcodeValues.EBREAK:
                case OpcodeValues.MRET:
                case OpcodeValues.SRET:
                case OpcodeValues.WFI:
                    readRegisters = Array.Empty<int>();
                    return true;
                case OpcodeValues.VSETVL:
                    readRegisters = BuildArchitecturalReadRegisterList(rs1, rs2);
                    return true;
                case OpcodeValues.VSETVLI:
                    readRegisters = BuildArchitecturalReadRegisterList(rs1);
                    return true;
                case OpcodeValues.VSETIVLI:
                    readRegisters = Array.Empty<int>();
                    return true;
                default:
                    readRegisters = Array.Empty<int>();
                    return false;
            }
        }

        private static bool TryResolvePublishedStreamControlRetireContourFromOpcode(
            ushort opcode,
            out bool requiresSerializingBoundaryFollowThrough)
        {
            requiresSerializingBoundaryFollowThrough = opcode == OpcodeValues.STREAM_WAIT;

            return opcode is
                OpcodeValues.STREAM_SETUP or
                OpcodeValues.STREAM_START or
                OpcodeValues.STREAM_WAIT;
        }

        private static bool TryResolvePublishedAtomicAccessSizeFromOpcode(
            ushort opcode,
            out byte accessSize)
        {
            accessSize = opcode switch
            {
                OpcodeValues.LR_W or
                OpcodeValues.SC_W or
                OpcodeValues.AMOADD_W or
                OpcodeValues.AMOSWAP_W or
                OpcodeValues.AMOOR_W or
                OpcodeValues.AMOAND_W or
                OpcodeValues.AMOXOR_W or
                OpcodeValues.AMOMIN_W or
                OpcodeValues.AMOMAX_W or
                OpcodeValues.AMOMINU_W or
                OpcodeValues.AMOMAXU_W => 4,
                OpcodeValues.LR_D or
                OpcodeValues.SC_D or
                OpcodeValues.AMOADD_D or
                OpcodeValues.AMOSWAP_D or
                OpcodeValues.AMOOR_D or
                OpcodeValues.AMOAND_D or
                OpcodeValues.AMOXOR_D or
                OpcodeValues.AMOMIN_D or
                OpcodeValues.AMOMAX_D or
                OpcodeValues.AMOMINU_D or
                OpcodeValues.AMOMAXU_D => 8,
                _ => (byte)0,
            };

            return accessSize != 0;
        }

        private static bool TryResolvePublishedVmxOperationKindFromOpcode(
            ushort opcode,
            out VmxOperationKind operationKind)
        {
            operationKind = opcode switch
            {
                OpcodeValues.VMXON => VmxOperationKind.VmxOn,
                OpcodeValues.VMXOFF => VmxOperationKind.VmxOff,
                OpcodeValues.VMLAUNCH => VmxOperationKind.VmLaunch,
                OpcodeValues.VMRESUME => VmxOperationKind.VmResume,
                OpcodeValues.VMREAD => VmxOperationKind.VmRead,
                OpcodeValues.VMWRITE => VmxOperationKind.VmWrite,
                OpcodeValues.VMCLEAR => VmxOperationKind.VmClear,
                OpcodeValues.VMPTRLD => VmxOperationKind.VmPtrLd,
                _ => VmxOperationKind.None,
            };

            return operationKind != VmxOperationKind.None;
        }

        private static IReadOnlyList<int> BuildArchitecturalReadRegisterList(
            byte first,
            byte second = ArchRegisterTripletEncoding.NoArchReg)
        {
            bool hasFirst = HasArchitecturalRegister(first);
            bool hasSecond = HasArchitecturalRegister(second);

            if (hasFirst && hasSecond)
            {
                return new[] { (int)first, (int)second };
            }

            if (hasFirst)
            {
                return new[] { (int)first };
            }

            if (hasSecond)
            {
                return new[] { (int)second };
            }

            return Array.Empty<int>();
        }

        private static bool HasArchitecturalRegister(byte registerId) =>
            registerId != 0 && registerId != ArchRegisterTripletEncoding.NoArchReg;

        private static ushort ToLegacyDecoderField(byte registerId) =>
            registerId == ArchRegisterTripletEncoding.NoArchReg
                ? ArchRegisterTripletEncoding.NoReg
                : registerId;

        private static void ApplyCanonicalPublishedBranchProjection(
            BranchMicroOp branchMicroOp,
            ushort opcode,
            byte rd,
            byte rs1,
            byte rs2)
        {
            switch (opcode)
            {
                case OpcodeValues.JAL:
                    branchMicroOp.ApplyCanonicalRuntimeOperandProjection(
                        rd,
                        ArchRegisterTripletEncoding.NoArchReg,
                        ArchRegisterTripletEncoding.NoArchReg);
                    break;
                case OpcodeValues.JALR:
                    branchMicroOp.ApplyCanonicalRuntimeOperandProjection(
                        rd,
                        rs1,
                        ArchRegisterTripletEncoding.NoArchReg);
                    break;
                case OpcodeValues.BEQ:
                case OpcodeValues.BNE:
                case OpcodeValues.BLT:
                case OpcodeValues.BGE:
                case OpcodeValues.BLTU:
                case OpcodeValues.BGEU:
                    branchMicroOp.ApplyCanonicalRuntimeOperandProjection(
                        ArchRegisterTripletEncoding.NoArchReg,
                        rs1,
                        rs2);
                    break;
            }
        }

        private static OpcodeInfo RequirePublishedOpcodeInfo(uint opCode)
        {
            OpcodeInfo? info = OpcodeRegistry.GetInfo(opCode);
            if (info is null)
            {
                throw new InvalidOperationException(
                    $"Opcode {OpcodeRegistry.GetMnemonicOrHex(opCode)} does not have a published canonical descriptor in OpcodeRegistry.");
            }

            return info.Value;
        }

        private static MicroOpDescriptor CreatePublishedSystemLikeDescriptor(
            uint opCode,
            bool? writesRegister = null,
            bool? isMemoryOp = null,
            int memFootprintClass = 0)
        {
            OpcodeInfo info = RequirePublishedOpcodeInfo(opCode);
            if (info.InstructionClass is not
                (InstructionClass.System or InstructionClass.Csr or InstructionClass.SmtVt or InstructionClass.Vmx))
            {
                throw new InvalidOperationException(
                    $"Opcode {OpcodeRegistry.GetMnemonicOrHex(opCode)} is not a published system/CSR/VMX contour and cannot use the canonical system-like descriptor path.");
            }

            return new MicroOpDescriptor(
                latency: info.ExecutionLatency,
                memFootprintClass: memFootprintClass)
            {
                WritesRegister = writesRegister,
                IsMemoryOp = isMemoryOp,
            };
        }

        private static MicroOpDescriptor CreatePublishedScalarDescriptor(uint opCode)
        {
            OpcodeInfo info = RequirePublishedOpcodeInfo(opCode);
            if (info.InstructionClass != InstructionClass.ScalarAlu)
            {
                throw new InvalidOperationException(
                    $"Opcode {OpcodeRegistry.GetMnemonicOrHex(opCode)} is not a published scalar contour and cannot use the canonical scalar descriptor path.");
            }

            return new MicroOpDescriptor
            {
                Latency = info.ExecutionLatency,
                MemFootprintClass = 0,
                WritesRegister = true,
                IsMemoryOp = false,
            };
        }

        private static void RegisterPublishedDescriptorOnly(
            uint opCode,
            int memFootprintClass,
            bool? writesRegister = null,
            bool? isMemoryOp = null)
        {
            OpcodeInfo info = RequirePublishedOpcodeInfo(opCode);
            RegisterOpAttributes(opCode, new MicroOpDescriptor
            {
                Latency = info.ExecutionLatency,
                MemFootprintClass = memFootprintClass,
                WritesRegister = writesRegister,
                IsMemoryOp = isMemoryOp,
            });
        }

        private static void RegisterScalarRegisterOp(uint opCode)
        {
            RegisterSemanticFactory(opCode, ctx =>
            {
                var op = new ScalarALUMicroOp
                {
                    OpCode = ctx.OpCode,
                    DestRegID = ctx.Reg1ID,
                    Src1RegID = ctx.Reg2ID,
                    Src2RegID = ctx.Reg3ID,
                    UsesImmediate = false,
                };

                op.InitializeMetadata();
                return op;
            });
            RegisterOpAttributes(opCode, CreatePublishedScalarDescriptor(opCode));
        }

        private static void RegisterScalarImmediateOp(uint opCode)
        {
            RegisterSemanticFactory(opCode, ctx =>
            {
                var op = new ScalarALUMicroOp
                {
                    OpCode = ctx.OpCode,
                    DestRegID = ctx.Reg1ID,
                    Src1RegID = ctx.Reg2ID,
                    // Direct factory/manual-publication callers must see the same canonical
                    // signed scalar-immediate payload as the materialized mainline carrier path.
                    Immediate = SignExtendScalarImmediate(GetRequiredDecoderImmediate(
                        in ctx,
                        $"Scalar-immediate opcode {OpcodeRegistry.GetMnemonicOrHex(ctx.OpCode)}")),
                    UsesImmediate = true,
                };

                op.InitializeMetadata();
                return op;
            });
            RegisterOpAttributes(opCode, CreatePublishedScalarDescriptor(opCode));
        }

        private static void RegisterTypedLoadOp(uint opCode)
        {
            RegisterSemanticFactory(opCode, ctx =>
            {
                var op = new LoadMicroOp
                {
                    OpCode = ctx.OpCode,
                    DestRegID = ctx.Reg1ID,
                    BaseRegID = ctx.Reg2ID,
                    Address = GetRequiredDecoderMemoryAddress(
                        in ctx,
                        $"Typed load opcode {OpcodeRegistry.GetMnemonicOrHex(ctx.OpCode)}"),
                    Size = opCode switch
                    {
                        OpcodeValues.LB or
                        OpcodeValues.LBU => 1,
                        OpcodeValues.LH or
                        OpcodeValues.LHU => 2,
                        OpcodeValues.LW or
                        OpcodeValues.LWU => 4,
                        _ => 8
                    },
                };

                op.InitializeMetadata();
                return op;
            });
            OpcodeInfo info = RequirePublishedOpcodeInfo(opCode);
            if (info.InstructionClass != InstructionClass.Memory)
            {
                throw new InvalidOperationException(
                    $"Opcode {OpcodeRegistry.GetMnemonicOrHex(opCode)} is not a published memory-load contour and cannot use the canonical typed-load descriptor path.");
            }

            RegisterOpAttributes(opCode, new MicroOpDescriptor
            {
                Latency = info.ExecutionLatency,
                MemFootprintClass = 1,
                IsMemoryOp = true,
                WritesRegister = true,
            });
        }

        private static void RegisterTypedStoreOp(uint opCode)
        {
            RegisterSemanticFactory(opCode, ctx =>
            {
                var op = new StoreMicroOp
                {
                    OpCode = ctx.OpCode,
                    SrcRegID = ctx.Reg3ID,
                    BaseRegID = ctx.Reg2ID,
                    Address = GetRequiredDecoderMemoryAddress(
                        in ctx,
                        $"Typed store opcode {OpcodeRegistry.GetMnemonicOrHex(ctx.OpCode)}"),
                    Size = opCode switch
                    {
                        OpcodeValues.SB => 1,
                        OpcodeValues.SH => 2,
                        OpcodeValues.SW => 4,
                        _ => 8
                    },
                };

                op.InitializeMetadata();
                return op;
            });
            OpcodeInfo info = RequirePublishedOpcodeInfo(opCode);
            if (info.InstructionClass != InstructionClass.Memory)
            {
                throw new InvalidOperationException(
                    $"Opcode {OpcodeRegistry.GetMnemonicOrHex(opCode)} is not a published memory-store contour and cannot use the canonical typed-store descriptor path.");
            }

            RegisterOpAttributes(opCode, new MicroOpDescriptor
            {
                Latency = info.ExecutionLatency,
                MemFootprintClass = 1,
                IsMemoryOp = true,
                WritesRegister = false,
            });
        }

        private static void RegisterBranchOp(uint opCode, bool isConditional)
        {
            RegisterSemanticFactory(opCode, ctx =>
            {
                var op = new BranchMicroOp
                {
                    OpCode = ctx.OpCode,
                    IsConditional = isConditional,
                    TargetAddress = ctx.AuxData,
                    Reg1ID = ctx.Reg1ID,
                    Reg2ID = ctx.Reg2ID
                };

                ApplyCanonicalDirectBranchProjection(op, in ctx);
                op.ApplyCanonicalRuntimeRelativeTargetProjection(
                    unchecked((short)GetRequiredDecoderImmediate(
                        in ctx,
                        $"Control-flow opcode {OpcodeRegistry.GetMnemonicOrHex(ctx.OpCode)}")));

                op.InitializeMetadata();
                return op;
            });
            OpcodeInfo info = RequirePublishedOpcodeInfo(opCode);
            if (info.InstructionClass != InstructionClass.ControlFlow)
            {
                throw new InvalidOperationException(
                    $"Opcode {OpcodeRegistry.GetMnemonicOrHex(opCode)} is not a published control-flow contour and cannot use the canonical branch descriptor path.");
            }

            RegisterOpAttributes(opCode, new MicroOpDescriptor
            {
                Latency = info.ExecutionLatency,
                MemFootprintClass = 0,
            });
        }

        private static void RegisterAtomicOp(uint opCode)
        {
            RegisterSemanticFactory(opCode, ctx =>
            {
                var op = new AtomicMicroOp
                {
                    OpCode = ctx.OpCode,
                    DestRegID = ctx.Reg1ID,
                    BaseRegID = ctx.Reg2ID,
                    SrcRegID = ctx.Reg3ID,
                    Address = ctx.AuxData,
                    Size = opCode switch
                    {
                        OpcodeValues.LR_W or
                        OpcodeValues.SC_W or
                        OpcodeValues.AMOSWAP_W or
                        OpcodeValues.AMOADD_W or
                        OpcodeValues.AMOXOR_W or
                        OpcodeValues.AMOAND_W or
                        OpcodeValues.AMOOR_W or
                        OpcodeValues.AMOMIN_W or
                        OpcodeValues.AMOMAX_W or
                        OpcodeValues.AMOMINU_W or
                        OpcodeValues.AMOMAXU_W => 4,
                        _ => 8
                    }
                };

                op.InitializeMetadata();
                return op;
            });
            OpcodeInfo info = RequirePublishedOpcodeInfo(opCode);
            if (info.InstructionClass != InstructionClass.Atomic)
            {
                throw new InvalidOperationException(
                    $"Opcode {OpcodeRegistry.GetMnemonicOrHex(opCode)} is not a published atomic contour and cannot use the canonical atomic descriptor path.");
            }

            RegisterOpAttributes(opCode, new MicroOpDescriptor
            {
                Latency = info.ExecutionLatency,
                MemFootprintClass = 1,
                IsMemoryOp = true,
                WritesRegister = true,
            });
        }

        /// <summary>
        /// Register a VMX plane instruction that produces a <see cref="VmxMicroOp"/>.
        /// VMX instructions are always fully serialised, cannot be stolen, and require
        /// Machine-mode privilege. Execution is delegated to <see cref="VmxExecutionUnit"/>
        /// by the pipeline dispatch stage.
        /// </summary>
        private static void RegisterVmxOp(uint opCode)
        {
            RegisterSemanticFactory(opCode, ctx =>
            {
                ushort opcode = unchecked((ushort)ctx.OpCode);
                if (!TryDecodeCanonicalOrUnpackedNoArchRegister(ctx.Reg1ID, out byte rd) ||
                    !TryDecodeCanonicalOrUnpackedNoArchRegister(ctx.Reg2ID, out byte rs1) ||
                    !TryDecodeCanonicalOrUnpackedNoArchRegister(ctx.Reg3ID, out byte rs2))
                {
                    throw new DecodeProjectionFaultException(
                        $"Opcode {OpcodeRegistry.GetMnemonicOrHex(opcode)} reached InstructionRegistry.CreateMicroOp(...) with a non-canonical VMX register encoding. " +
                        "VMX manual publication requires flat architectural register ids or an explicit NoReg/NoArchReg sentinel.");
                }

                var op = new VmxMicroOp
                {
                    OpCode = ctx.OpCode,
                    Rd = rd,
                    DestRegID = rd == ArchRegisterTripletEncoding.NoArchReg
                        ? ArchRegisterTripletEncoding.NoReg
                        : rd,
                    Rs1 = rs1,
                    Rs2 = rs2,
                    WritesRegister = opcode == OpcodeValues.VMREAD &&
                                     rd != 0 &&
                                     rd != ArchRegisterTripletEncoding.NoArchReg,
                };

                op.RefreshWriteMetadata();
                return op;
            });
            RegisterOpAttributes(opCode, CreatePublishedSystemLikeDescriptor(opCode));
        }
    }
}
