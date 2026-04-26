using System;
using HybridCPU_ISE.Arch;

using YAKSys_Hybrid_CPU.Arch;

namespace YAKSys_Hybrid_CPU.Core
{
    public static partial class InstructionRegistry
    {
            private static ushort NormalizeRequiredLegacyMemoryRegister(uint opCode, ushort rawRegId, string operandName, string contourName)
            {
                if (!TryDecodeCanonicalArchRegister(rawRegId, out var registerId) || registerId == byte.MaxValue)
                {
                    throw new DecodeProjectionFaultException($"Retained legacy {contourName} direct/manual publication reached InstructionRegistry.CreateMicroOp(...) with a non-canonical or missing {operandName} register encoding. " + "This contour must fail closed instead of publishing phantom register truth or synthetic zero follow-through.");
                }
                return registerId;
            }
        
            private static string DescribeNonRepresentableVectorAddressingContour(
                bool indexed,
                bool is2D)
            {
                return indexed && is2D
                    ? "indexed+2D"
                    : indexed
                        ? "indexed"
                        : "2D";
            }

            private static string DescribeNonRepresentableVectorAddressingContour(in VLIW_Instruction instruction)
            {
                return DescribeNonRepresentableVectorAddressingContour(
                    instruction.Indexed,
                    instruction.Is2D);
            }

            private static bool TryResolveUnsupportedVectorAddressingContour(
                in DecoderContext ctx,
                out string addressingContour)
            {
                if (!ctx.HasVectorAddressingContour)
                {
                    throw new DecodeProjectionFaultException(
                        $"Opcode {(ushort)ctx.OpCode} reached InstructionRegistry.CreateMicroOp(...) without projected DecoderContext vector-addressing contour handoff. " +
                        "Raw VLIW_Instruction Indexed/Is2D fallback is retired from the decoder-to-runtime ABI, so this path must fail closed instead of reopening a non-representable compat surface.");
                }

                bool indexed = ctx.IndexedAddressing;
                bool is2D = ctx.Is2DAddressing;

                if (!indexed && !is2D)
                {
                    addressingContour = string.Empty;
                    return false;
                }

                addressingContour = DescribeNonRepresentableVectorAddressingContour(indexed, is2D);
                return true;
            }

            private static VLIW_Instruction GetRequiredProjectedVectorInstruction(
                in DecoderContext ctx)
            {
                Processor.CPU_Core.InstructionsEnum opcode =
                    (Processor.CPU_Core.InstructionsEnum)ctx.OpCode;
                if (!ctx.HasVectorPayload)
                {
                    throw new DecodeProjectionFaultException(
                        $"Vector opcode {opcode} requires projected DecoderContext vector payload handoff. " +
                        "Raw VLIW_Instruction vector payload fallback is retired from the decoder-to-runtime ABI.");
                }

                return new VLIW_Instruction
                {
                    OpCode = ctx.OpCode,
                    DataType = GetRequiredDecoderDataType(
                        in ctx,
                        $"Vector opcode {opcode}"),
                    PredicateMask = (byte)ctx.PredicateMask,
                    Immediate = ctx.HasImmediate ? ctx.Immediate : (ushort)0,
                    DestSrc1Pointer = ctx.VectorPrimaryPointer,
                    Src2Pointer = ctx.VectorSecondaryPointer,
                    RowStride = ctx.VectorRowStride,
                    StreamLength = ctx.VectorStreamLength,
                    Stride = ctx.VectorStride,
                    Indexed = ctx.HasVectorAddressingContour && ctx.IndexedAddressing,
                    Is2D = ctx.HasVectorAddressingContour && ctx.Is2DAddressing,
                    TailAgnostic = ctx.TailAgnostic,
                    MaskAgnostic = ctx.MaskAgnostic
                };
            }
        
            private static void RegisterPublishedVectorBinaryOp(uint opCode)
            {
                RegisterSemanticFactory(opCode, delegate(DecoderContext ctx)
                {
                    if (TryResolveUnsupportedVectorAddressingContour(in ctx, out string value))
                    {
                        throw new DecodeProjectionFaultException($"Opcode {(ushort)ctx.OpCode} reached InstructionRegistry.CreateMicroOp(...) with unsupported {value} vector-binary addressing. " + "The authoritative binary carrier only publishes 1D in-place memory truth and must fail closed instead of reopening a non-representable compat surface.");
                    }
                    VectorBinaryOpMicroOp vectorBinaryOpMicroOp = new VectorBinaryOpMicroOp
                    {
                        OpCode = ctx.OpCode,
                        Instruction = GetRequiredProjectedVectorInstruction(in ctx),
                        PredicateMask = ctx.PredicateMask
                    };
                    vectorBinaryOpMicroOp.InitializeMetadata();
                    return vectorBinaryOpMicroOp;
                });
                OpcodeInfo opcodeInfo = RequirePublishedOpcodeInfo(opCode);
                if (!opcodeInfo.IsVector)
                {
                    throw new InvalidOperationException($"Opcode {(ushort)opCode} is not a published vector contour and cannot use the canonical vector-binary descriptor path.");
                }
                RegisterOpAttributes(opCode, new MicroOpDescriptor
                {
                    Latency = opcodeInfo.ExecutionLatency,
                    MemFootprintClass = 2,
                    IsMemoryOp = false,
                    WritesRegister = false
                });
            }
        
            private static void RegisterVectorTransferOp(uint opCode, byte latency)
            {
                RegisterSemanticFactory(opCode, delegate(DecoderContext ctx)
                {
                    if (TryResolveUnsupportedVectorAddressingContour(in ctx, out string value))
                    {
                        throw new DecodeProjectionFaultException($"Opcode {(ushort)ctx.OpCode} reached InstructionRegistry.CreateMicroOp(...) with unsupported {value} vector-transfer addressing. " + "The authoritative VLOAD/VSTORE carrier only publishes 1D transfer-shape truth and must fail closed instead of reopening a non-representable compat surface.");
                    }
                    VectorTransferMicroOp vectorTransferMicroOp = new VectorTransferMicroOp
                    {
                        OpCode = ctx.OpCode,
                        Instruction = GetRequiredProjectedVectorInstruction(in ctx),
                        PredicateMask = ctx.PredicateMask
                    };
                    vectorTransferMicroOp.InitializeMetadata();
                    return vectorTransferMicroOp;
                });
                RegisterOpAttributes(opCode, new MicroOpDescriptor
                {
                    Latency = latency,
                    MemFootprintClass = 3,
                    IsMemoryOp = false,
                    WritesRegister = false
                });
            }
        
            private static void RegisterPublishedVectorUnaryOp(uint opCode)
            {
                RegisterSemanticFactory(opCode, delegate(DecoderContext ctx)
                {
                    if (TryResolveUnsupportedVectorAddressingContour(in ctx, out string value))
                    {
                        throw new DecodeProjectionFaultException($"Opcode {(ushort)ctx.OpCode} reached InstructionRegistry.CreateMicroOp(...) with unsupported {value} vector-unary addressing. " + "The authoritative unary carrier only publishes 1D in-place memory truth and must fail closed instead of reopening a non-representable compat surface.");
                    }
                    VectorUnaryOpMicroOp vectorUnaryOpMicroOp = new VectorUnaryOpMicroOp
                    {
                        OpCode = ctx.OpCode,
                        Instruction = GetRequiredProjectedVectorInstruction(in ctx),
                        PredicateMask = ctx.PredicateMask
                    };
                    vectorUnaryOpMicroOp.InitializeMetadata();
                    return vectorUnaryOpMicroOp;
                });
                OpcodeInfo opcodeInfo = RequirePublishedOpcodeInfo(opCode);
                if (!opcodeInfo.IsVector)
                {
                    throw new InvalidOperationException($"Opcode {(ushort)opCode} is not a published vector contour and cannot use the canonical vector-unary descriptor path.");
                }
                RegisterOpAttributes(opCode, new MicroOpDescriptor
                {
                    Latency = opcodeInfo.ExecutionLatency,
                    MemFootprintClass = 2,
                    IsMemoryOp = false,
                    WritesRegister = false
                });
            }
        
            private static void RegisterPublishedVectorFmaOp(uint opCode)
            {
                RegisterSemanticFactory(opCode, delegate(DecoderContext ctx)
                {
                    if (TryResolveUnsupportedVectorAddressingContour(in ctx, out string value))
                    {
                        throw new DecodeProjectionFaultException($"Opcode {(ushort)ctx.OpCode} reached InstructionRegistry.CreateMicroOp(...) with unsupported {value} vector-FMA addressing. " + "The authoritative FMA carrier does not publish indexed/2D tri-operand follow-through truth and must fail closed instead of reopening a non-representable compat surface.");
                    }
                    VectorFmaMicroOp vectorFmaMicroOp = new VectorFmaMicroOp
                    {
                        OpCode = ctx.OpCode,
                        Instruction = GetRequiredProjectedVectorInstruction(in ctx),
                        PredicateMask = ctx.PredicateMask
                    };
                    vectorFmaMicroOp.InitializeMetadata();
                    return vectorFmaMicroOp;
                });
                OpcodeInfo opcodeInfo = RequirePublishedOpcodeInfo(opCode);
                if (!opcodeInfo.IsVector)
                {
                    throw new InvalidOperationException($"Opcode {(ushort)opCode} is not a published vector contour and cannot use the canonical vector-FMA descriptor path.");
                }
                RegisterOpAttributes(opCode, new MicroOpDescriptor
                {
                    Latency = opcodeInfo.ExecutionLatency,
                    MemFootprintClass = 2,
                    IsMemoryOp = false,
                    WritesRegister = false
                });
            }
        
            private static void RegisterPublishedVectorReductionOp(uint opCode)
            {
                RegisterSemanticFactory(opCode, delegate(DecoderContext ctx)
                {
                    if (TryResolveUnsupportedVectorAddressingContour(in ctx, out string value))
                    {
                        throw new DecodeProjectionFaultException($"Opcode {(ushort)ctx.OpCode} reached InstructionRegistry.CreateMicroOp(...) with unsupported {value} vector-reduction addressing. " + "The authoritative reduction carrier only publishes 1D scalar-footprint memory truth and must fail closed instead of reopening a non-representable compat surface.");
                    }
                    VectorReductionMicroOp vectorReductionMicroOp = new VectorReductionMicroOp
                    {
                        OpCode = ctx.OpCode,
                        Instruction = GetRequiredProjectedVectorInstruction(in ctx),
                        PredicateMask = ctx.PredicateMask
                    };
                    vectorReductionMicroOp.InitializeMetadata();
                    return vectorReductionMicroOp;
                });
                OpcodeInfo opcodeInfo = RequirePublishedOpcodeInfo(opCode);
                if (!opcodeInfo.IsVector)
                {
                    throw new InvalidOperationException($"Opcode {(ushort)opCode} is not a published vector contour and cannot use the canonical vector-reduction descriptor path.");
                }
                RegisterOpAttributes(opCode, new MicroOpDescriptor
                {
                    Latency = opcodeInfo.ExecutionLatency,
                    MemFootprintClass = 2,
                    IsMemoryOp = false,
                    WritesRegister = false
                });
            }
        
            private static void RegisterPublishedVectorComparisonOp(uint opCode)
            {
                RegisterSemanticFactory(opCode, delegate(DecoderContext ctx)
                {
                    if (TryResolveUnsupportedVectorAddressingContour(in ctx, out string value))
                    {
                        throw new DecodeProjectionFaultException($"Opcode {(ushort)ctx.OpCode} reached InstructionRegistry.CreateMicroOp(...) with unsupported {value} vector-comparison addressing. " + "The authoritative comparison carrier only publishes 1D predicate-state truth and must fail closed instead of reopening a non-representable compat surface.");
                    }
                    VectorComparisonMicroOp vectorComparisonMicroOp = new VectorComparisonMicroOp
                    {
                        OpCode = ctx.OpCode,
                        Instruction = GetRequiredProjectedVectorInstruction(in ctx),
                        PredicateMask = ctx.PredicateMask
                    };
                    vectorComparisonMicroOp.InitializeMetadata();
                    return vectorComparisonMicroOp;
                });
                OpcodeInfo opcodeInfo = RequirePublishedOpcodeInfo(opCode);
                if (!opcodeInfo.IsVector)
                {
                    throw new InvalidOperationException($"Opcode {(ushort)opCode} is not a published vector contour and cannot use the canonical vector-comparison descriptor path.");
                }
                RegisterOpAttributes(opCode, new MicroOpDescriptor
                {
                    Latency = opcodeInfo.ExecutionLatency,
                    MemFootprintClass = 2,
                    IsMemoryOp = false,
                    WritesRegister = false
                });
            }
        
            private static void RegisterPublishedVectorMaskOp(uint opCode)
            {
                RegisterSemanticFactory(opCode, delegate(DecoderContext ctx)
                {
                    if (TryResolveUnsupportedVectorAddressingContour(in ctx, out string value))
                    {
                        throw new DecodeProjectionFaultException($"Opcode {(ushort)ctx.OpCode} reached InstructionRegistry.CreateMicroOp(...) with unsupported {value} vector-mask addressing. " + "The authoritative predicate-mask carrier does not materialize any addressed vector surface and must fail closed instead of reopening an addressing-tag compat surface.");
                    }
                    VectorMaskOpMicroOp vectorMaskOpMicroOp = new VectorMaskOpMicroOp
                    {
                        OpCode = ctx.OpCode,
                        Instruction = GetRequiredProjectedVectorInstruction(in ctx),
                        PredicateMask = ctx.PredicateMask
                    };
                    vectorMaskOpMicroOp.InitializeMetadata();
                    return vectorMaskOpMicroOp;
                });
                OpcodeInfo opcodeInfo = RequirePublishedOpcodeInfo(opCode);
                if (!opcodeInfo.IsVector)
                {
                    throw new InvalidOperationException($"Opcode {(ushort)opCode} is not a published vector contour and cannot use the canonical vector-mask descriptor path.");
                }
                RegisterOpAttributes(opCode, new MicroOpDescriptor
                {
                    Latency = opcodeInfo.ExecutionLatency,
                    MemFootprintClass = 0,
                    IsMemoryOp = false,
                    WritesRegister = false
                });
            }
        
            private static void RegisterPublishedVectorMaskPopCountOp(uint opCode)
            {
                RegisterSemanticFactory(opCode, delegate(DecoderContext ctx)
                {
                    if (TryResolveUnsupportedVectorAddressingContour(in ctx, out string value))
                    {
                        throw new DecodeProjectionFaultException($"Opcode {(ushort)ctx.OpCode} reached InstructionRegistry.CreateMicroOp(...) with unsupported {value} vector-mask-popcount addressing. " + "The authoritative scalar-result VPOPC carrier only publishes predicate-to-scalar writeback truth and must fail closed instead of reopening an addressing-tag compat surface.");
                    }
                    VectorMaskPopCountMicroOp vectorMaskPopCountMicroOp = new VectorMaskPopCountMicroOp
                    {
                        OpCode = ctx.OpCode,
                        Instruction = GetRequiredProjectedVectorInstruction(in ctx),
                        PredicateMask = ctx.PredicateMask
                    };
                    vectorMaskPopCountMicroOp.InitializeMetadata();
                    return vectorMaskPopCountMicroOp;
                });
                OpcodeInfo opcodeInfo = RequirePublishedOpcodeInfo(opCode);
                if (!opcodeInfo.IsVector)
                {
                    throw new InvalidOperationException($"Opcode {(ushort)opCode} is not a published vector contour and cannot use the canonical vector-mask-popcount descriptor path.");
                }
                RegisterOpAttributes(opCode, new MicroOpDescriptor
                {
                    Latency = opcodeInfo.ExecutionLatency,
                    MemFootprintClass = 0,
                    IsMemoryOp = false,
                    WritesRegister = true
                });
            }
        
            private static void RegisterPublishedVectorPermutationOp(uint opCode)
            {
                RegisterSemanticFactory(opCode, delegate(DecoderContext ctx)
                {
                    if (TryResolveUnsupportedVectorAddressingContour(in ctx, out string value))
                    {
                        throw new DecodeProjectionFaultException($"Opcode {(ushort)ctx.OpCode} reached InstructionRegistry.CreateMicroOp(...) with unsupported {value} vector-permutation addressing. " + "The authoritative permutation carrier only publishes 1D two-surface memory truth and must fail closed instead of reopening a non-representable compat surface.");
                    }
                    VectorPermutationMicroOp vectorPermutationMicroOp = new VectorPermutationMicroOp
                    {
                        OpCode = ctx.OpCode,
                        Instruction = GetRequiredProjectedVectorInstruction(in ctx),
                        PredicateMask = ctx.PredicateMask
                    };
                    vectorPermutationMicroOp.InitializeMetadata();
                    return vectorPermutationMicroOp;
                });
                OpcodeInfo opcodeInfo = RequirePublishedOpcodeInfo(opCode);
                if (!opcodeInfo.IsVector)
                {
                    throw new InvalidOperationException($"Opcode {(ushort)opCode} is not a published vector contour and cannot use the canonical vector-permutation descriptor path.");
                }
                RegisterOpAttributes(opCode, new MicroOpDescriptor
                {
                    Latency = opcodeInfo.ExecutionLatency,
                    MemFootprintClass = 2,
                    IsMemoryOp = false,
                    WritesRegister = false
                });
            }
        
            private static void RegisterPublishedVectorPredicativeMovementOp(uint opCode)
            {
                RegisterSemanticFactory(opCode, delegate(DecoderContext ctx)
                {
                    if (TryResolveUnsupportedVectorAddressingContour(in ctx, out string value))
                    {
                        throw new DecodeProjectionFaultException($"Opcode {(ushort)ctx.OpCode} reached InstructionRegistry.CreateMicroOp(...) with unsupported {value} vector-predicative-movement addressing. " + "The authoritative predicative-movement carrier only publishes 1D single-surface truth and must fail closed instead of reopening a non-representable compat surface.");
                    }
                    VectorPredicativeMovementMicroOp vectorPredicativeMovementMicroOp = new VectorPredicativeMovementMicroOp
                    {
                        OpCode = ctx.OpCode,
                        Instruction = GetRequiredProjectedVectorInstruction(in ctx),
                        PredicateMask = ctx.PredicateMask
                    };
                    vectorPredicativeMovementMicroOp.InitializeMetadata();
                    return vectorPredicativeMovementMicroOp;
                });
                OpcodeInfo opcodeInfo = RequirePublishedOpcodeInfo(opCode);
                if (!opcodeInfo.IsVector)
                {
                    throw new InvalidOperationException($"Opcode {(ushort)opCode} is not a published vector contour and cannot use the canonical vector-predicative-movement descriptor path.");
                }
                RegisterOpAttributes(opCode, new MicroOpDescriptor
                {
                    Latency = opcodeInfo.ExecutionLatency,
                    MemFootprintClass = 2,
                    IsMemoryOp = false,
                    WritesRegister = false
                });
            }
        
            private static void RegisterPublishedVectorSlideOp(uint opCode)
            {
                RegisterSemanticFactory(opCode, delegate(DecoderContext ctx)
                {
                    if (TryResolveUnsupportedVectorAddressingContour(in ctx, out string value))
                    {
                        throw new DecodeProjectionFaultException($"Opcode {(ushort)ctx.OpCode} reached InstructionRegistry.CreateMicroOp(...) with unsupported {value} vector-slide addressing. " + "The authoritative slide carrier only publishes 1D single-surface memory truth and must fail closed instead of reopening a non-representable compat surface.");
                    }
                    VectorSlideMicroOp vectorSlideMicroOp = new VectorSlideMicroOp
                    {
                        OpCode = ctx.OpCode,
                        Instruction = GetRequiredProjectedVectorInstruction(in ctx),
                        PredicateMask = ctx.PredicateMask
                    };
                    vectorSlideMicroOp.InitializeMetadata();
                    return vectorSlideMicroOp;
                });
                OpcodeInfo opcodeInfo = RequirePublishedOpcodeInfo(opCode);
                if (!opcodeInfo.IsVector)
                {
                    throw new InvalidOperationException($"Opcode {(ushort)opCode} is not a published vector contour and cannot use the canonical vector-slide descriptor path.");
                }
                RegisterOpAttributes(opCode, new MicroOpDescriptor
                {
                    Latency = opcodeInfo.ExecutionLatency,
                    MemFootprintClass = 2,
                    IsMemoryOp = false,
                    WritesRegister = false
                });
            }
        
            private static void RegisterPublishedVectorDotProductOp(uint opCode)
            {
                RegisterSemanticFactory(opCode, delegate(DecoderContext ctx)
                {
                    if (TryResolveUnsupportedVectorAddressingContour(in ctx, out string value))
                    {
                        throw new DecodeProjectionFaultException($"Opcode {(ushort)ctx.OpCode} reached InstructionRegistry.CreateMicroOp(...) with unsupported {value} vector-dot-product addressing. " + "The authoritative dot-product carrier only publishes 1D scalar-footprint truth and must fail closed instead of reopening a non-representable compat surface.");
                    }
                    VectorDotProductMicroOp vectorDotProductMicroOp = new VectorDotProductMicroOp
                    {
                        OpCode = ctx.OpCode,
                        Instruction = GetRequiredProjectedVectorInstruction(in ctx),
                        PredicateMask = ctx.PredicateMask
                    };
                    vectorDotProductMicroOp.InitializeMetadata();
                    return vectorDotProductMicroOp;
                });
                OpcodeInfo opcodeInfo = RequirePublishedOpcodeInfo(opCode);
                if (!opcodeInfo.IsVector)
                {
                    throw new InvalidOperationException($"Opcode {(ushort)opCode} is not a published vector contour and cannot use the canonical vector-dot-product descriptor path.");
                }
                RegisterOpAttributes(opCode, new MicroOpDescriptor
                {
                    Latency = opcodeInfo.ExecutionLatency,
                    MemFootprintClass = 2,
                    IsMemoryOp = false,
                    WritesRegister = false
                });
            }
        
            private static void RegisterVectorConfigOp(uint opCode)
            {
                RegisterSemanticFactory(opCode, delegate(DecoderContext ctx)
                {
                    Processor.CPU_Core.InstructionsEnum instructionsEnum = (Processor.CPU_Core.InstructionsEnum)ctx.OpCode;
                    if (!TryDecodeCanonicalArchRegister(ctx.Reg1ID, out var registerId))
                    {
                        throw new DecodeProjectionFaultException($"Opcode {instructionsEnum} reached InstructionRegistry.CreateMicroOp(...) with a non-canonical rd encoding. " + "Vector-config manual publication requires flat architectural register ids.");
                    }
                    ushort destRegID = ((registerId == byte.MaxValue) ? ushort.MaxValue : registerId);
                    VConfigMicroOp vConfigMicroOp = new VConfigMicroOp
                    {
                        OpCode = ctx.OpCode,
                        DestRegID = destRegID
                    };
                    switch (instructionsEnum)
                    {
                    case Processor.CPU_Core.InstructionsEnum.VSETVL:
                    {
                        if (!TryDecodeCanonicalArchRegister(ctx.Reg2ID, out var registerId3) || !TryDecodeCanonicalArchRegister(ctx.Reg3ID, out var registerId4) || registerId3 == byte.MaxValue || registerId4 == byte.MaxValue)
                        {
                            throw new DecodeProjectionFaultException("VSETVL manual publication requires canonical rs1/rs2 architectural registers.");
                        }
                        vConfigMicroOp.ConfigureForRegisterVType(registerId3, registerId4);
                        break;
                    }
                    case Processor.CPU_Core.InstructionsEnum.VSETVLI:
                    {
                        if (!TryDecodeCanonicalArchRegister(ctx.Reg2ID, out var registerId2) || registerId2 == byte.MaxValue)
                        {
                            throw new DecodeProjectionFaultException("VSETVLI manual publication requires a canonical rs1 architectural register.");
                        }
                        vConfigMicroOp.ConfigureForImmediateVType(
                            VectorConfigOperationKind.Vsetvli,
                            registerId2,
                            GetRequiredDecoderDataType(
                                in ctx,
                                "Vector-config opcode VSETVLI"));
                        break;
                    }
                    case Processor.CPU_Core.InstructionsEnum.VSETIVLI:
                        vConfigMicroOp.ConfigureForImmediateAvlAndVType(
                            GetRequiredDecoderImmediate(
                                in ctx,
                                "Vector-config opcode VSETIVLI"),
                            GetRequiredDecoderDataType(
                                in ctx,
                                "Vector-config opcode VSETIVLI"));
                        break;
                    default:
                        throw new InvalidOperationException($"Unsupported vector-config opcode {instructionsEnum}.");
                    }
                    vConfigMicroOp.InitializeMetadata();
                    return vConfigMicroOp;
                });
                bool? isMemoryOp = false;
                RegisterOpAttributes(opCode, CreatePublishedSystemLikeDescriptor(opCode, null, isMemoryOp));
            }
        
            private static void RegisterRetainedMoveOp(uint opCode)
            {
                RegisterSemanticFactory(opCode, delegate(DecoderContext ctx)
                {
                    byte dataType = GetRequiredDecoderDataType(
                        in ctx,
                        "Retained legacy Move");
                    if (dataType == 2 || dataType == 3)
                    {
                        throw new DecodeProjectionFaultException($"Retained legacy Move DT={dataType} memory contour reached InstructionRegistry.CreateMicroOp(...) directly. " + "This contour must be canonicalized to Load/Store before runtime materialization/manual publication.");
                    }
                    if (dataType == 4)
                    {
                        throw new DecodeProjectionFaultException("Retained legacy Move DT=4 dual-write contour is unsupported and must fail closed.");
                    }
                    if (dataType == 5)
                    {
                        throw new DecodeProjectionFaultException("Retained legacy Move DT=5 triple-destination contour is unsupported and must fail closed.");
                    }
                    ushort num = dataType switch
                    {
                        0 => ctx.Reg2ID,
                        1 => ctx.Reg1ID,
                        _ => throw new DecodeProjectionFaultException($"Retained legacy Move DT={dataType} is unsupported on the direct/manual runtime publication path."),
                    };
                    ushort destRegID = num;
                    MoveMicroOp moveMicroOp = new MoveMicroOp
                    {
                        OpCode = ctx.OpCode,
                        DestRegID = destRegID,
                        IsMemoryOp = false,
                        WritesRegister = dataType == 0 || dataType == 1
                    };
                    moveMicroOp.ApplyCanonicalRuntimeMoveShapeProjection(dataType, ctx.Reg1ID, ctx.Reg2ID, ctx.AuxData);
                    moveMicroOp.RefreshWriteMetadata();
                    return moveMicroOp;
                });
                MicroOpDescriptor descriptor = new MicroOpDescriptor
                {
                    Latency = 1,
                    MemFootprintClass = 1
                };
                RegisterOpAttributes(opCode, descriptor);
            }
        
            private static void RegisterRetainedMoveNumOp(uint opCode)
            {
                RegisterSemanticFactory(opCode, delegate(DecoderContext ctx)
                {
                    MoveMicroOp moveMicroOp = new MoveMicroOp
                    {
                        OpCode = ctx.OpCode,
                        DestRegID = ctx.Reg1ID,
                        IsMemoryOp = false,
                        WritesRegister = true
                    };
                    moveMicroOp.ApplyCanonicalRuntimeMoveShapeProjection(dataType: 1, ctx.Reg1ID, ctx.Reg2ID, ctx.AuxData);
                    moveMicroOp.RefreshWriteMetadata();
                    return moveMicroOp;
                });
                RegisterOpAttributes(opCode, new MicroOpDescriptor
                {
                    Latency = 1,
                    MemFootprintClass = 1
                });
            }
        
            private static void RegisterRetainedAbsoluteLoadOp(uint opCode)
            {
                RegisterSemanticFactory(opCode, delegate(DecoderContext ctx)
                {
                    LoadMicroOp loadMicroOp = new LoadMicroOp
                    {
                        OpCode = ctx.OpCode,
                        DestRegID = NormalizeRequiredLegacyMemoryRegister(ctx.OpCode, ctx.Reg1ID, "destination", "Load"),
                        BaseRegID = ushort.MaxValue,
                        Address = GetRequiredDecoderMemoryAddress(
                            in ctx,
                            "Retained legacy Load"),
                        Size = 8,
                        WritesRegister = true
                    };
                    loadMicroOp.InitializeMetadata();
                    return loadMicroOp;
                });
                RegisterOpAttributes(opCode, new MicroOpDescriptor
                {
                    Latency = 1,
                    MemFootprintClass = 1,
                    IsMemoryOp = true,
                    WritesRegister = true
                });
            }
        
            private static void RegisterRetainedAbsoluteStoreOp(uint opCode)
            {
                RegisterSemanticFactory(opCode, delegate(DecoderContext ctx)
                {
                    ushort rawRegId = ((ctx.Reg1ID != ushort.MaxValue) ? ctx.Reg1ID : ctx.Reg3ID);
                    StoreMicroOp storeMicroOp = new StoreMicroOp
                    {
                        OpCode = ctx.OpCode,
                        SrcRegID = NormalizeRequiredLegacyMemoryRegister(ctx.OpCode, rawRegId, "source", "Store"),
                        BaseRegID = ushort.MaxValue,
                        Address = GetRequiredDecoderMemoryAddress(
                            in ctx,
                            "Retained legacy Store"),
                        Size = 8
                    };
                    storeMicroOp.InitializeMetadata();
                    return storeMicroOp;
                });
                RegisterOpAttributes(opCode, new MicroOpDescriptor
                {
                    Latency = 1,
                    MemFootprintClass = 1,
                    IsMemoryOp = true,
                    WritesRegister = false
                });
            }
    }
}

