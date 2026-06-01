using HybridCPU_ISE.Arch;

using System;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core.Registers;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;
using YAKSys_Hybrid_CPU.Execution;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public partial struct CPU_Core
        {
            // REF-12 / EA-01: retained system helpers remain on the runtime surface for
            // compat callers, but compiler-mode container recording now routes through
            // CpuCoreSystemInstructionEmitter and Processor.RecordCompilerInstruction(...)
            // so helper expansion does not grow direct bridge append sites.
            public byte Nope()
            {
                if (IsEmulationExecutionMode())
                {
                    return (byte)InstructionsEnum.Nope;
                }
                if (IsCompilerExecutionMode())
                {
                    RecordCompatSystemInstruction(CpuCoreSystemInstructionEmitter.EncodeNope());
                }

                return (byte)InstructionsEnum.Nope;
            }

            public byte Move_Num(IntRegister IntRegister_Destination, ulong ulong_Number)
            {
                if (IsEmulationExecutionMode())
                {
                    return Move_Num(
                        RequireArchRegId(IntRegister_Destination, nameof(IntRegister_Destination)),
                        ulong_Number);
                }

                if (IsCompilerExecutionMode())
                {
                    RecordCompatSystemInstruction(
                        CpuCoreSystemInstructionEmitter.EncodeMoveImmediate(
                            RequireArchRegId(IntRegister_Destination, nameof(IntRegister_Destination)),
                            ulong_Number));
                }

                return (byte)InstructionsEnum.Move;
            }

            public byte Move_Num(ArchRegId destinationRegisterId, ulong value)
            {
                if (IsEmulationExecutionMode())
                {
                    WriteActiveArchValue(destinationRegisterId, value);
                }

                if (IsCompilerExecutionMode())
                {
                    RecordCompatSystemInstruction(
                        CpuCoreSystemInstructionEmitter.EncodeMoveImmediate(
                            destinationRegisterId,
                            value));
                }

                return (byte)InstructionsEnum.Move;
            }

            public byte Move(IntRegister IntRegister_Source, IntRegister IntRegister_Destination)
            {
                if (IsEmulationExecutionMode())
                {
                    return Move(
                        RequireArchRegId(IntRegister_Source, nameof(IntRegister_Source)),
                        RequireArchRegId(IntRegister_Destination, nameof(IntRegister_Destination)));
                }

                if (IsCompilerExecutionMode())
                {
                    RecordCompatSystemInstruction(
                        CpuCoreSystemInstructionEmitter.EncodeRegisterMove(
                            RequireArchRegId(IntRegister_Source, nameof(IntRegister_Source)),
                            RequireArchRegId(IntRegister_Destination, nameof(IntRegister_Destination))));
                }

                return (byte)InstructionsEnum.Move;
            }

            public byte Move(ArchRegId sourceRegisterId, ArchRegId destinationRegisterId)
            {
                if (IsEmulationExecutionMode())
                {
                    WriteActiveArchValue(destinationRegisterId, ReadActiveArchValue(sourceRegisterId));
                }

                if (IsCompilerExecutionMode())
                {
                    RecordCompatSystemInstruction(
                        CpuCoreSystemInstructionEmitter.EncodeRegisterMove(
                            sourceRegisterId,
                            destinationRegisterId));
                }

                return (byte)InstructionsEnum.Move;
            }

            public byte Move(IntRegister IntRegister_Source, ulong MemoryAddress_Destination)
            {
                if (IsEmulationExecutionMode())
                {
                    return Move(
                        RequireArchRegId(IntRegister_Source, nameof(IntRegister_Source)),
                        MemoryAddress_Destination);
                }

                if (IsCompilerExecutionMode())
                {
                    RecordCompatSystemInstruction(
                        CpuCoreSystemInstructionEmitter.EncodeRegisterToMemoryMove(
                            RequireArchRegId(IntRegister_Source, nameof(IntRegister_Source)),
                            MemoryAddress_Destination));
                }

                return (byte)InstructionsEnum.Move;
            }

            public byte Move(ArchRegId sourceRegisterId, ulong memoryAddressDestination)
            {
                if (IsEmulationExecutionMode())
                {
                    WriteBoundMainMemory(
                        memoryAddressDestination,
                        BitConverter.GetBytes(ReadActiveArchValue(sourceRegisterId)),
                        "Move(reg\u2192mem)");
                }

                if (IsCompilerExecutionMode())
                {
                    RecordCompatSystemInstruction(
                        CpuCoreSystemInstructionEmitter.EncodeRegisterToMemoryMove(
                            sourceRegisterId,
                            memoryAddressDestination));
                }

                return (byte)InstructionsEnum.Move;
            }

            public byte Move(ulong MemoryAddress_Source, IntRegister IntRegister_Destination)
            {
                if (IsEmulationExecutionMode())
                {
                    return Move(
                        MemoryAddress_Source,
                        RequireArchRegId(IntRegister_Destination, nameof(IntRegister_Destination)));
                }

                if (IsCompilerExecutionMode())
                {
                    RecordCompatSystemInstruction(
                        CpuCoreSystemInstructionEmitter.EncodeMemoryToRegisterMove(
                            MemoryAddress_Source,
                            RequireArchRegId(IntRegister_Destination, nameof(IntRegister_Destination))));
                }

                return (byte)InstructionsEnum.Move;
            }

            public byte Move(ulong memoryAddressSource, ArchRegId destinationRegisterId)
            {
                if (IsEmulationExecutionMode())
                {
                    byte[] data = ReadBoundMainMemory(
                        memoryAddressSource,
                        new byte[sizeof(ulong)],
                        sizeof(ulong),
                        "Move(mem\u2192reg)");
                    WriteActiveArchValue(destinationRegisterId, BitConverter.ToUInt64(data, 0));
                }

                if (IsCompilerExecutionMode())
                {
                    RecordCompatSystemInstruction(
                        CpuCoreSystemInstructionEmitter.EncodeMemoryToRegisterMove(
                            memoryAddressSource,
                            destinationRegisterId));
                }

                return (byte)InstructionsEnum.Move;
            }

            public byte Store(IntRegister IntRegister_Source, ulong MemoryAddress_Destination)
            {
                if (IsEmulationExecutionMode())
                {
                    return Store(
                        RequireArchRegId(IntRegister_Source, nameof(IntRegister_Source)),
                        MemoryAddress_Destination);
                }

                if (IsCompilerExecutionMode())
                {
                    RecordCompatSystemInstruction(
                        CpuCoreSystemInstructionEmitter.EncodeRegisterToMemoryMove(
                            RequireArchRegId(IntRegister_Source, nameof(IntRegister_Source)),
                            MemoryAddress_Destination));
                }

                return (byte)InstructionsEnum.Move; 
            }

            public byte Store(ArchRegId sourceRegisterId, ulong memoryAddressDestination)
            {
                if (IsEmulationExecutionMode())
                {
                    WriteBoundMainMemory(
                        memoryAddressDestination,
                        BitConverter.GetBytes(ReadActiveArchValue(sourceRegisterId)),
                        "Store(reg\u2192mem)");
                }

                if (IsCompilerExecutionMode())
                {
                    RecordCompatSystemInstruction(
                        CpuCoreSystemInstructionEmitter.EncodeRegisterToMemoryMove(
                            sourceRegisterId,
                            memoryAddressDestination));
                }

                return (byte)InstructionsEnum.Move;
            }

            public byte Load(ulong MemoryAddress_Source, IntRegister IntRegister_Destination)
            {
                if (IsEmulationExecutionMode())
                {
                    return Load(
                        MemoryAddress_Source,
                        RequireArchRegId(IntRegister_Destination, nameof(IntRegister_Destination)));
                }

                if (IsCompilerExecutionMode())
                {
                    RecordCompatSystemInstruction(
                        CpuCoreSystemInstructionEmitter.EncodeMemoryToRegisterMove(
                            MemoryAddress_Source,
                            RequireArchRegId(IntRegister_Destination, nameof(IntRegister_Destination))));
                }
                return (byte)InstructionsEnum.Move; 
            }

            public byte Load(ulong memoryAddressSource, ArchRegId destinationRegisterId)
            {
                if (IsEmulationExecutionMode())
                {
                    byte[] data = ReadBoundMainMemory(
                        memoryAddressSource,
                        new byte[sizeof(ulong)],
                        sizeof(ulong),
                        "Load(mem\u2192reg)");
                    WriteActiveArchValue(destinationRegisterId, BitConverter.ToUInt64(data, 0));
                }

                if (IsCompilerExecutionMode())
                {
                    RecordCompatSystemInstruction(
                        CpuCoreSystemInstructionEmitter.EncodeMemoryToRegisterMove(
                            memoryAddressSource,
                            destinationRegisterId));
                }

                return (byte)InstructionsEnum.Move;
            }

            private static void RecordCompatSystemInstruction(in VLIW_Instruction instruction)
            {
                Processor.RecordCompilerInstruction(in instruction);
            }
        }
    }
}

