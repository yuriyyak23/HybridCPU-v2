
using System;
using YAKSys_Hybrid_CPU.Arch;

namespace YAKSys_Hybrid_CPU.Core
{
    public static partial class InstructionRegistry
    {
        private static void RegisterBaseInstructionSet()
        {
            RegisterSemanticFactory((uint)Processor.CPU_Core.InstructionsEnum.Nope, ctx => new NopMicroOp
            {
                OpCode = ctx.OpCode,
                Latency = 0,
            });
            RegisterOpAttributes((uint)Processor.CPU_Core.InstructionsEnum.Nope, new MicroOpDescriptor
            {
                Latency = 0,
                MemFootprintClass = 0,
                IsMemoryOp = false,
            });

            RegisterRetainedMoveOp((uint)Processor.CPU_Core.InstructionsEnum.Move);
            RegisterRetainedMoveNumOp((uint)Processor.CPU_Core.InstructionsEnum.Move_Num);
            RegisterRetainedAbsoluteStoreOp((uint)Processor.CPU_Core.InstructionsEnum.Store);
            RegisterRetainedAbsoluteLoadOp((uint)Processor.CPU_Core.InstructionsEnum.Load);

            RegisterScalarImmediateOp((uint)Processor.CPU_Core.InstructionsEnum.ADDI);
            RegisterScalarImmediateOp((uint)Processor.CPU_Core.InstructionsEnum.ANDI);
            RegisterScalarImmediateOp((uint)Processor.CPU_Core.InstructionsEnum.ORI);
            RegisterScalarImmediateOp((uint)Processor.CPU_Core.InstructionsEnum.XORI);
            RegisterScalarImmediateOp((uint)Processor.CPU_Core.InstructionsEnum.SLTI);
            RegisterScalarImmediateOp((uint)Processor.CPU_Core.InstructionsEnum.SLTIU);
            RegisterScalarImmediateOp((uint)Processor.CPU_Core.InstructionsEnum.SLLI);
            RegisterScalarImmediateOp((uint)Processor.CPU_Core.InstructionsEnum.SRLI);
            RegisterScalarImmediateOp((uint)Processor.CPU_Core.InstructionsEnum.SRAI);
            RegisterScalarRegisterOp((uint)Processor.CPU_Core.InstructionsEnum.SLT);
            RegisterScalarRegisterOp((uint)Processor.CPU_Core.InstructionsEnum.SLTU);
            RegisterScalarImmediateOp((uint)Processor.CPU_Core.InstructionsEnum.LUI);
            RegisterScalarImmediateOp((uint)Processor.CPU_Core.InstructionsEnum.AUIPC);
            RegisterScalarRegisterOp((uint)Processor.CPU_Core.InstructionsEnum.MULH);
            RegisterScalarRegisterOp((uint)Processor.CPU_Core.InstructionsEnum.MULHU);
            RegisterScalarRegisterOp((uint)Processor.CPU_Core.InstructionsEnum.MULHSU);
            RegisterScalarRegisterOp((uint)Processor.CPU_Core.InstructionsEnum.DIVU);
            RegisterScalarRegisterOp((uint)Processor.CPU_Core.InstructionsEnum.REM);
            RegisterScalarRegisterOp((uint)Processor.CPU_Core.InstructionsEnum.REMU);

            RegisterTypedLoadOp((uint)Processor.CPU_Core.InstructionsEnum.LB);
            RegisterTypedLoadOp((uint)Processor.CPU_Core.InstructionsEnum.LBU);
            RegisterTypedLoadOp((uint)Processor.CPU_Core.InstructionsEnum.LH);
            RegisterTypedLoadOp((uint)Processor.CPU_Core.InstructionsEnum.LHU);
            RegisterTypedLoadOp((uint)Processor.CPU_Core.InstructionsEnum.LW);
            RegisterTypedLoadOp((uint)Processor.CPU_Core.InstructionsEnum.LWU);
            RegisterTypedLoadOp((uint)Processor.CPU_Core.InstructionsEnum.LD);

            RegisterTypedStoreOp((uint)Processor.CPU_Core.InstructionsEnum.SB);
            RegisterTypedStoreOp((uint)Processor.CPU_Core.InstructionsEnum.SH);
            RegisterTypedStoreOp((uint)Processor.CPU_Core.InstructionsEnum.SW);
            RegisterTypedStoreOp((uint)Processor.CPU_Core.InstructionsEnum.SD);
            RegisterSemanticFactory((uint)Processor.CPU_Core.InstructionsEnum.DmaStreamCompute, ctx =>
                throw new DecodeProjectionFaultException(
                    "DmaStreamCompute requires a guard-accepted descriptor sideband from native VLIW decode. " +
                    "InstructionRegistry raw factory publication is not the canonical lane6 descriptor path."));
            RegisterOpAttributes((uint)Processor.CPU_Core.InstructionsEnum.DmaStreamCompute, new MicroOpDescriptor
            {
                Latency = 8,
                MemFootprintClass = 3,
                IsMemoryOp = true,
                WritesRegister = false,
            });

            RegisterSystemDeviceCommandOp(
                (uint)Processor.CPU_Core.InstructionsEnum.ACCEL_QUERY_CAPS,
                static () => new AcceleratorQueryCapsMicroOp());
            RegisterSystemDeviceCommandOp(
                (uint)Processor.CPU_Core.InstructionsEnum.ACCEL_SUBMIT,
                static () => new AcceleratorSubmitMicroOp());
            RegisterSystemDeviceCommandOp(
                (uint)Processor.CPU_Core.InstructionsEnum.ACCEL_POLL,
                static () => new AcceleratorPollMicroOp());
            RegisterSystemDeviceCommandOp(
                (uint)Processor.CPU_Core.InstructionsEnum.ACCEL_WAIT,
                static () => new AcceleratorWaitMicroOp());
            RegisterSystemDeviceCommandOp(
                (uint)Processor.CPU_Core.InstructionsEnum.ACCEL_CANCEL,
                static () => new AcceleratorCancelMicroOp());
            RegisterSystemDeviceCommandOp(
                (uint)Processor.CPU_Core.InstructionsEnum.ACCEL_FENCE,
                static () => new AcceleratorFenceMicroOp());

            RegisterBranchOp((uint)Processor.CPU_Core.InstructionsEnum.JAL, isConditional: false);
            RegisterBranchOp((uint)Processor.CPU_Core.InstructionsEnum.JALR, isConditional: false);
            RegisterBranchOp((uint)Processor.CPU_Core.InstructionsEnum.BEQ, isConditional: true);
            RegisterBranchOp((uint)Processor.CPU_Core.InstructionsEnum.BNE, isConditional: true);
            RegisterBranchOp((uint)Processor.CPU_Core.InstructionsEnum.BLT, isConditional: true);
            RegisterBranchOp((uint)Processor.CPU_Core.InstructionsEnum.BGE, isConditional: true);
            RegisterBranchOp((uint)Processor.CPU_Core.InstructionsEnum.BLTU, isConditional: true);
            RegisterBranchOp((uint)Processor.CPU_Core.InstructionsEnum.BGEU, isConditional: true);
            RegisterPublishedDescriptorOnly((uint)Processor.CPU_Core.InstructionsEnum.JumpIfEqual, memFootprintClass: 0);

            RegisterAtomicOp((uint)Processor.CPU_Core.InstructionsEnum.LR_W);
            RegisterAtomicOp((uint)Processor.CPU_Core.InstructionsEnum.SC_W);
            RegisterAtomicOp((uint)Processor.CPU_Core.InstructionsEnum.LR_D);
            RegisterAtomicOp((uint)Processor.CPU_Core.InstructionsEnum.SC_D);
            RegisterAtomicOp((uint)Processor.CPU_Core.InstructionsEnum.AMOADD_W);
            RegisterAtomicOp((uint)Processor.CPU_Core.InstructionsEnum.AMOSWAP_W);
            RegisterAtomicOp((uint)Processor.CPU_Core.InstructionsEnum.AMOOR_W);
            RegisterAtomicOp((uint)Processor.CPU_Core.InstructionsEnum.AMOAND_W);
            RegisterAtomicOp((uint)Processor.CPU_Core.InstructionsEnum.AMOXOR_W);
            RegisterAtomicOp((uint)Processor.CPU_Core.InstructionsEnum.AMOMIN_W);
            RegisterAtomicOp((uint)Processor.CPU_Core.InstructionsEnum.AMOMAX_W);
            RegisterAtomicOp((uint)Processor.CPU_Core.InstructionsEnum.AMOMINU_W);
            RegisterAtomicOp((uint)Processor.CPU_Core.InstructionsEnum.AMOMAXU_W);
            RegisterAtomicOp((uint)Processor.CPU_Core.InstructionsEnum.AMOADD_D);
            RegisterAtomicOp((uint)Processor.CPU_Core.InstructionsEnum.AMOSWAP_D);
            RegisterAtomicOp((uint)Processor.CPU_Core.InstructionsEnum.AMOOR_D);
            RegisterAtomicOp((uint)Processor.CPU_Core.InstructionsEnum.AMOAND_D);
            RegisterAtomicOp((uint)Processor.CPU_Core.InstructionsEnum.AMOXOR_D);
            RegisterAtomicOp((uint)Processor.CPU_Core.InstructionsEnum.AMOMIN_D);
            RegisterAtomicOp((uint)Processor.CPU_Core.InstructionsEnum.AMOMAX_D);
            RegisterAtomicOp((uint)Processor.CPU_Core.InstructionsEnum.AMOMINU_D);
            RegisterAtomicOp((uint)Processor.CPU_Core.InstructionsEnum.AMOMAXU_D);

            RegisterSemanticFactory((uint)Processor.CPU_Core.InstructionsEnum.FENCE,
                ctx => CreateSystemEventMicroOp(ctx.OpCode, SysEventMicroOp.ForFence));
            RegisterOpAttributes((uint)Processor.CPU_Core.InstructionsEnum.FENCE, CreatePublishedSystemLikeDescriptor((uint)Processor.CPU_Core.InstructionsEnum.FENCE));

            RegisterSemanticFactory((uint)Processor.CPU_Core.InstructionsEnum.FENCE_I,
                ctx => CreateSystemEventMicroOp(ctx.OpCode, SysEventMicroOp.ForFenceI));
            RegisterOpAttributes((uint)Processor.CPU_Core.InstructionsEnum.FENCE_I, CreatePublishedSystemLikeDescriptor((uint)Processor.CPU_Core.InstructionsEnum.FENCE_I));

            RegisterCsrClearOp((uint)Processor.CPU_Core.InstructionsEnum.CSR_CLEAR);

            RegisterSemanticFactory((uint)Processor.CPU_Core.InstructionsEnum.ECALL,
                ctx => CreateSystemEventMicroOp(ctx.OpCode, SysEventMicroOp.ForEcall));
            RegisterOpAttributes((uint)Processor.CPU_Core.InstructionsEnum.ECALL, CreatePublishedSystemLikeDescriptor((uint)Processor.CPU_Core.InstructionsEnum.ECALL));
            RegisterSemanticFactory((uint)Processor.CPU_Core.InstructionsEnum.EBREAK,
                ctx => CreateSystemEventMicroOp(ctx.OpCode, SysEventMicroOp.ForEbreak));
            RegisterOpAttributes((uint)Processor.CPU_Core.InstructionsEnum.EBREAK, CreatePublishedSystemLikeDescriptor((uint)Processor.CPU_Core.InstructionsEnum.EBREAK));
            RegisterSemanticFactory((uint)Processor.CPU_Core.InstructionsEnum.MRET,
                ctx => CreateSystemEventMicroOp(ctx.OpCode, SysEventMicroOp.ForMret));
            RegisterOpAttributes((uint)Processor.CPU_Core.InstructionsEnum.MRET, CreatePublishedSystemLikeDescriptor((uint)Processor.CPU_Core.InstructionsEnum.MRET));
            RegisterSemanticFactory((uint)Processor.CPU_Core.InstructionsEnum.SRET,
                ctx => CreateSystemEventMicroOp(ctx.OpCode, SysEventMicroOp.ForSret));
            RegisterOpAttributes((uint)Processor.CPU_Core.InstructionsEnum.SRET, CreatePublishedSystemLikeDescriptor((uint)Processor.CPU_Core.InstructionsEnum.SRET));
            RegisterSemanticFactory((uint)Processor.CPU_Core.InstructionsEnum.WFI,
                ctx => CreateSystemEventMicroOp(ctx.OpCode, SysEventMicroOp.ForWfi));
            RegisterOpAttributes((uint)Processor.CPU_Core.InstructionsEnum.WFI, CreatePublishedSystemLikeDescriptor((uint)Processor.CPU_Core.InstructionsEnum.WFI));
            RegisterSemanticFactory((uint)Processor.CPU_Core.InstructionsEnum.WFE,
                ctx => CreateSystemEventMicroOp(ctx.OpCode, SysEventMicroOp.ForWfe));
            RegisterOpAttributes((uint)Processor.CPU_Core.InstructionsEnum.WFE, CreatePublishedSystemLikeDescriptor((uint)Processor.CPU_Core.InstructionsEnum.WFE));
            RegisterSemanticFactory((uint)Processor.CPU_Core.InstructionsEnum.SEV,
                ctx => CreateSystemEventMicroOp(ctx.OpCode, SysEventMicroOp.ForSev));
            RegisterOpAttributes((uint)Processor.CPU_Core.InstructionsEnum.SEV, CreatePublishedSystemLikeDescriptor((uint)Processor.CPU_Core.InstructionsEnum.SEV));
            RegisterSemanticFactory((uint)Processor.CPU_Core.InstructionsEnum.YIELD,
                ctx => CreateSystemEventMicroOp(ctx.OpCode, SysEventMicroOp.ForYield));
            RegisterOpAttributes((uint)Processor.CPU_Core.InstructionsEnum.YIELD, CreatePublishedSystemLikeDescriptor((uint)Processor.CPU_Core.InstructionsEnum.YIELD));
            RegisterSemanticFactory((uint)Processor.CPU_Core.InstructionsEnum.POD_BARRIER,
                ctx => CreateSystemEventMicroOp(ctx.OpCode, SysEventMicroOp.ForPodBarrier));
            RegisterOpAttributes((uint)Processor.CPU_Core.InstructionsEnum.POD_BARRIER, CreatePublishedSystemLikeDescriptor((uint)Processor.CPU_Core.InstructionsEnum.POD_BARRIER));
            RegisterSemanticFactory((uint)Processor.CPU_Core.InstructionsEnum.VT_BARRIER,
                ctx => CreateSystemEventMicroOp(ctx.OpCode, SysEventMicroOp.ForVtBarrier));
            RegisterOpAttributes((uint)Processor.CPU_Core.InstructionsEnum.VT_BARRIER, CreatePublishedSystemLikeDescriptor((uint)Processor.CPU_Core.InstructionsEnum.VT_BARRIER));

            RegisterSemanticFactory((uint)Processor.CPU_Core.InstructionsEnum.CSRRW, ctx => new CsrReadWriteMicroOp
            {
                OpCode = ctx.OpCode,
                CSRAddress = GetRequiredDecoderImmediate(
                    in ctx,
                    $"CSR opcode {Processor.CPU_Core.InstructionsEnum.CSRRW}"),
                DestRegID = NormalizeCanonicalOrSuppressedCsrDestinationRegister(ctx.OpCode, ctx.Reg1ID),
                SrcRegID = NormalizeCanonicalOrSuppressedCsrSourceRegister(ctx.OpCode, ctx.Reg2ID, "rs1"),
            });
            RegisterOpAttributes(
                (uint)Processor.CPU_Core.InstructionsEnum.CSRRW,
                CreatePublishedSystemLikeDescriptor(
                    (uint)Processor.CPU_Core.InstructionsEnum.CSRRW,
                    writesRegister: true,
                    isMemoryOp: false));

            RegisterSemanticFactory((uint)Processor.CPU_Core.InstructionsEnum.CSRRS, ctx => new CsrReadSetMicroOp
            {
                OpCode = ctx.OpCode,
                CSRAddress = GetRequiredDecoderImmediate(
                    in ctx,
                    $"CSR opcode {Processor.CPU_Core.InstructionsEnum.CSRRS}"),
                DestRegID = NormalizeCanonicalOrSuppressedCsrDestinationRegister(ctx.OpCode, ctx.Reg1ID),
                SrcRegID = NormalizeCanonicalOrSuppressedCsrSourceRegister(ctx.OpCode, ctx.Reg2ID, "rs1"),
            });
            RegisterOpAttributes(
                (uint)Processor.CPU_Core.InstructionsEnum.CSRRS,
                CreatePublishedSystemLikeDescriptor(
                    (uint)Processor.CPU_Core.InstructionsEnum.CSRRS,
                    writesRegister: true,
                    isMemoryOp: false));

            RegisterSemanticFactory((uint)Processor.CPU_Core.InstructionsEnum.CSRRC, ctx => new CsrReadClearMicroOp
            {
                OpCode = ctx.OpCode,
                CSRAddress = GetRequiredDecoderImmediate(
                    in ctx,
                    $"CSR opcode {Processor.CPU_Core.InstructionsEnum.CSRRC}"),
                DestRegID = NormalizeCanonicalOrSuppressedCsrDestinationRegister(ctx.OpCode, ctx.Reg1ID),
                SrcRegID = NormalizeCanonicalOrSuppressedCsrSourceRegister(ctx.OpCode, ctx.Reg2ID, "rs1"),
            });
            RegisterOpAttributes(
                (uint)Processor.CPU_Core.InstructionsEnum.CSRRC,
                CreatePublishedSystemLikeDescriptor(
                    (uint)Processor.CPU_Core.InstructionsEnum.CSRRC,
                    writesRegister: true,
                    isMemoryOp: false));

            RegisterSemanticFactory((uint)Processor.CPU_Core.InstructionsEnum.CSRRWI, ctx => new CsrReadWriteImmediateMicroOp
            {
                OpCode = ctx.OpCode,
                CSRAddress = GetRequiredDecoderImmediate(
                    in ctx,
                    $"CSR opcode {Processor.CPU_Core.InstructionsEnum.CSRRWI}"),
                DestRegID = NormalizeCanonicalOrSuppressedCsrDestinationRegister(ctx.OpCode, ctx.Reg1ID),
                WriteValue = NormalizeCanonicalCsrImmediateField(ctx.OpCode, ctx.Reg2ID, "zimm"),
            });
            RegisterOpAttributes(
                (uint)Processor.CPU_Core.InstructionsEnum.CSRRWI,
                CreatePublishedSystemLikeDescriptor(
                    (uint)Processor.CPU_Core.InstructionsEnum.CSRRWI,
                    writesRegister: true,
                    isMemoryOp: false));

            RegisterSemanticFactory((uint)Processor.CPU_Core.InstructionsEnum.CSRRSI, ctx => new CsrReadSetImmediateMicroOp
            {
                OpCode = ctx.OpCode,
                CSRAddress = GetRequiredDecoderImmediate(
                    in ctx,
                    $"CSR opcode {Processor.CPU_Core.InstructionsEnum.CSRRSI}"),
                DestRegID = NormalizeCanonicalOrSuppressedCsrDestinationRegister(ctx.OpCode, ctx.Reg1ID),
                WriteValue = NormalizeCanonicalCsrImmediateField(ctx.OpCode, ctx.Reg2ID, "zimm"),
            });
            RegisterOpAttributes(
                (uint)Processor.CPU_Core.InstructionsEnum.CSRRSI,
                CreatePublishedSystemLikeDescriptor(
                    (uint)Processor.CPU_Core.InstructionsEnum.CSRRSI,
                    writesRegister: true,
                    isMemoryOp: false));

            RegisterSemanticFactory((uint)Processor.CPU_Core.InstructionsEnum.CSRRCI, ctx => new CsrReadClearImmediateMicroOp
            {
                OpCode = ctx.OpCode,
                CSRAddress = GetRequiredDecoderImmediate(
                    in ctx,
                    $"CSR opcode {Processor.CPU_Core.InstructionsEnum.CSRRCI}"),
                DestRegID = NormalizeCanonicalOrSuppressedCsrDestinationRegister(ctx.OpCode, ctx.Reg1ID),
                WriteValue = NormalizeCanonicalCsrImmediateField(ctx.OpCode, ctx.Reg2ID, "zimm"),
            });
            RegisterOpAttributes(
                (uint)Processor.CPU_Core.InstructionsEnum.CSRRCI,
                CreatePublishedSystemLikeDescriptor(
                    (uint)Processor.CPU_Core.InstructionsEnum.CSRRCI,
                    writesRegister: true,
                    isMemoryOp: false));

            RegisterVectorExceptionControlCsrOp(
                (uint)Processor.CPU_Core.InstructionsEnum.VSETVEXCPMASK,
                CsrAddresses.VexcpMask);
            RegisterVectorExceptionControlCsrOp(
                (uint)Processor.CPU_Core.InstructionsEnum.VSETVEXCPPRI,
                CsrAddresses.VexcpPri);

            RegisterSemanticFactory((uint)Processor.CPU_Core.InstructionsEnum.STREAM_SETUP, ctx =>
            {
                var op = new StreamControlMicroOp
                {
                    OpCode = ctx.OpCode
                };
                op.InitializeMetadata();
                return op;
            });
            RegisterOpAttributes(
                (uint)Processor.CPU_Core.InstructionsEnum.STREAM_SETUP,
                CreatePublishedSystemLikeDescriptor(
                    (uint)Processor.CPU_Core.InstructionsEnum.STREAM_SETUP,
                    memFootprintClass: 3));
            RegisterSemanticFactory((uint)Processor.CPU_Core.InstructionsEnum.STREAM_START, ctx =>
            {
                var op = new StreamControlMicroOp
                {
                    OpCode = ctx.OpCode
                };
                op.InitializeMetadata();
                return op;
            });
            RegisterOpAttributes(
                (uint)Processor.CPU_Core.InstructionsEnum.STREAM_START,
                CreatePublishedSystemLikeDescriptor(
                    (uint)Processor.CPU_Core.InstructionsEnum.STREAM_START,
                    memFootprintClass: 3));
            RegisterSemanticFactory((uint)Processor.CPU_Core.InstructionsEnum.STREAM_WAIT, ctx =>
            {
                var op = new StreamControlMicroOp
                {
                    OpCode = ctx.OpCode
                };
                op.InitializeMetadata();
                return op;
            });
            RegisterOpAttributes(
                (uint)Processor.CPU_Core.InstructionsEnum.STREAM_WAIT,
                CreatePublishedSystemLikeDescriptor(
                    (uint)Processor.CPU_Core.InstructionsEnum.STREAM_WAIT,
                    memFootprintClass: 3));

            RegisterVmxOp((uint)Processor.CPU_Core.InstructionsEnum.VMXON);
            RegisterVmxOp((uint)Processor.CPU_Core.InstructionsEnum.VMXOFF);
            RegisterVmxOp((uint)Processor.CPU_Core.InstructionsEnum.VMLAUNCH);
            RegisterVmxOp((uint)Processor.CPU_Core.InstructionsEnum.VMRESUME);
            RegisterVmxOp((uint)Processor.CPU_Core.InstructionsEnum.VMREAD);
            RegisterVmxOp((uint)Processor.CPU_Core.InstructionsEnum.VMWRITE);
            RegisterVmxOp((uint)Processor.CPU_Core.InstructionsEnum.VMCLEAR);
            RegisterVmxOp((uint)Processor.CPU_Core.InstructionsEnum.VMPTRLD);
        }

        private static void RegisterSystemDeviceCommandOp(
            uint opCode,
            Func<SystemDeviceCommandMicroOp> factory)
        {
            RegisterSemanticFactory(opCode, ctx =>
            {
                SystemDeviceCommandMicroOp microOp = factory();
                if (microOp.OpCode != ctx.OpCode)
                {
                    throw new DecodeProjectionFaultException(
                        $"L7-SDC factory opcode mismatch: expected 0x{ctx.OpCode:X}, created 0x{microOp.OpCode:X}.");
                }

                return microOp;
            });

            RegisterOpAttributes(
                opCode,
                CreatePublishedSystemLikeDescriptor(
                    opCode,
                    writesRegister: false,
                    isMemoryOp: false));
        }
    }
}
