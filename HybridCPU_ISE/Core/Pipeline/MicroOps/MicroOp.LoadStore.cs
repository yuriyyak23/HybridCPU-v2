using HybridCPU_ISE.Arch;

using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core.Memory;
using YAKSys_Hybrid_CPU.Core.Registers;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;


namespace YAKSys_Hybrid_CPU.Core
{
    public abstract class LoadStoreMicroOp : MicroOp
    {
        /// <summary>
        /// Memory bank ID computed from the operation's address (Refactoring Pt. 3).
        /// Used by per-VT scoreboard for bank-level conflict detection during FSP scheduling.
        /// The bank geometry follows the live runtime memory subsystem when present.
        /// If runtime memory geometry has not been materialized yet, this returns the
        /// explicit uninitialized contour instead of a synthetic legacy bank id.
        /// </summary>
        public int MemoryBankId => Core.Memory.MemoryBankRouting.ResolveSchedulerVisibleBankId(MemoryAddress);

        /// <summary>
        /// Abstract property for the memory address of this operation.
        /// Implemented by LoadMicroOp (Address) and StoreMicroOp (Address).
        /// </summary>
        public abstract ulong MemoryAddress { get; }

        public override CanonicalDecodePublicationMode CanonicalDecodePublication =>
            CanonicalDecodePublicationMode.SelfPublishes;

        protected static bool HasExactMainMemoryRange(
            ref Processor.CPU_Core core,
            ulong address,
            int size)
        {
            return core.HasExactBoundMainMemoryRange(address, size);
        }

        protected static void ThrowIfMainMemoryRangeUnavailable(
            ref Processor.CPU_Core core,
            ulong address,
            int size,
            string executionSurface)
        {
            core.ThrowIfBoundMainMemoryRangeUnavailable(address, size, executionSurface);
        }

        protected static void ReadMainMemoryExact(
            ref Processor.CPU_Core core,
            ulong address,
            byte[] buffer,
            string executionSurface)
        {
            core.ReadBoundMainMemoryExact(address, buffer, executionSurface);
        }

        /// <summary>
        /// Mark this operation as speculative (stolen from another thread).
        /// Encapsulates speculative state management and allows for future extensions.
        /// </summary>
        public void MarkSpeculative()
        {
            IsSpeculative = true;
        }

        /// <summary>
        /// Clear speculative flag (operation is now executing in owner thread).
        /// </summary>
        public void ClearSpeculative()
        {
            IsSpeculative = false;
        }

        /// <summary>
        /// Mark this operation as faulted during speculative execution.
        /// </summary>
        public void MarkFaulted()
        {
            Faulted = true;
        }

        /// <summary>
        /// Clear faulted flag (operation ready to retry).
        /// </summary>
        public void ClearFaulted()
        {
            Faulted = false;
        }
    }

    /// <summary>
    /// Load micro-operation (memory read)
    /// Updated to use asynchronous memory subsystem
    /// </summary>
    public class LoadMicroOp : LoadStoreMicroOp
    {
        public ulong Address { get; set; }
        public byte Size { get; set; }
        public ushort BaseRegID { get; set; }
        private ulong _loadedValue;
        private YAKSys_Hybrid_CPU.Memory.MemorySubsystem.MemoryRequestToken? _requestToken;

        /// <inheritdoc />
        public override ulong MemoryAddress => Address;

        public LoadMicroOp()
        {
            IsMemoryOp = true;
            Class = MicroOpClass.Lsu;

            // ISA v4 Phase 02: loads are Memory class, free ordering (non-destructive)
            InstructionClass = Arch.InstructionClass.Memory;
            SerializationClass = Arch.SerializationClass.Free;

            // Phase 01: Typed-slot taxonomy
            SetClassFlexiblePlacement(SlotClass.LsuClass);
        }

        /// <summary>
        /// Initialize FSP metadata after register IDs and address are set.
        /// Phase: Safety Tags & Certificates - Mandatory safety mask computation.
        /// </summary>
        public void InitializeMetadata()
        {
            // FSP MMIO Security Policy (Phase: Safety Tags & Certificates):
            // MMIO reads are often state-mutating (e.g., clear-on-read, FIFO pop).
            // Speculative or FSP-stolen execution of MMIO breaks system state.
            // Mark operations targeting MMIO space as strictly non-stealable.
            const ulong MMIO_BASE = 0xFFFF000000000000UL;
            if (Address >= MMIO_BASE)
            {
                IsStealable = false;
            }

            // Blueprint §3.69 / §5: guard against NoReg sentinels (0xFFFF)
            const ushort noReg = VLIW_Instruction.NoReg;

            if (BaseRegID != noReg)
            {
                ReadRegisters = new[] { (int)BaseRegID };
            }
            else
            {
                ReadRegisters = Array.Empty<int>();
            }

            // Write to destination register — skip NoReg sentinels
            if (WritesRegister && DestRegID != noReg)
            {
                WriteRegisters = new[] { (int)DestRegID };
            }

            // Memory range will be updated during Execute
            ReadMemoryRanges = new[] { (Address, (ulong)Size) };

            // Phase 8: Initialize ResourceMask for GRLB
            ResourceMask = ResourceBitset.Zero;
            // Add register read (base address register)
            if (BaseRegID != noReg)
            {
                ResourceMask |= ResourceMaskBuilder.ForRegisterRead(BaseRegID);
            }
            // Add register write (destination register)
            if (WritesRegister && DestRegID != noReg)
            {
                ResourceMask |= ResourceMaskBuilder.ForRegisterWrite(DestRegID);
            }
            // Add LSU load channel
            ResourceMask |= ResourceMaskBuilder.ForLoad();
            // Add memory domain (use owner thread ID as domain)
            ResourceMask |= ResourceMaskBuilder.ForMemoryDomain(OwnerThreadId);

            PublishExplicitStructuralSafetyMask();
            RefreshAdmissionMetadata(this);
        }

        /// <inheritdoc/>
        public override void RefreshWriteMetadata() => InitializeMetadata();

        public override bool Execute(ref Processor.CPU_Core core)
        {
            try
            {
                // If MemorySubsystem is available, use async interface
                var memSub = core.GetBoundMemorySubsystem();
                if (memSub != null)
                {
                    // First call - initiate async request
                    if (_requestToken == null)
                    {
                        byte[] buffer = new byte[Size];
                        _requestToken = memSub.EnqueueRead(0 /* CPU Device ID */, Address, Size, buffer);
                        return false; // Operation not complete, need to retry
                    }

                    // Subsequent calls - check if request is complete
                    if (!_requestToken.IsComplete)
                    {
                        return false; // Still waiting, retry next cycle
                    }

                    _requestToken.ThrowIfFailed("LoadMicroOp.Execute()");

                    // Request complete - decode result
                    byte[] resultBuffer = _requestToken.GetBuffer();
                    switch (Size)
                    {
                        case 1:
                            _loadedValue = resultBuffer[0];
                            break;
                        case 2:
                            _loadedValue = BitConverter.ToUInt16(resultBuffer, 0);
                            break;
                        case 4:
                            _loadedValue = BitConverter.ToUInt32(resultBuffer, 0);
                            break;
                        case 8:
                            _loadedValue = BitConverter.ToUInt64(resultBuffer, 0);
                            break;
                        default:
                            _loadedValue = 0;
                            break;
                    }
                    return true; // Operation complete
                }

                // Fallback to synchronous implementation if MemorySubsystem is not available
                if (HasExactMainMemoryRange(ref core, Address, Size))
                {
                    switch (Size)
                    {
                        case 1:
                            {
                                byte[] bytes = new byte[1];
                                ReadMainMemoryExact(ref core, Address, bytes, "LoadMicroOp.Execute()");
                                _loadedValue = bytes[0];
                                break;
                            }
                        case 2:
                            {
                                byte[] bytes = new byte[2];
                                ReadMainMemoryExact(ref core, Address, bytes, "LoadMicroOp.Execute()");
                                _loadedValue = BitConverter.ToUInt16(bytes, 0);
                                break;
                            }
                        case 4:
                            {
                                byte[] bytes = new byte[4];
                                ReadMainMemoryExact(ref core, Address, bytes, "LoadMicroOp.Execute()");
                                _loadedValue = BitConverter.ToUInt32(bytes, 0);
                                break;
                            }
                        case 8:
                            {
                                byte[] bytes = new byte[8];
                                ReadMainMemoryExact(ref core, Address, bytes, "LoadMicroOp.Execute()");
                                _loadedValue = BitConverter.ToUInt64(bytes, 0);
                                break;
                            }
                        default:
                            _loadedValue = 0;
                            break;
                    }
                    return true;
                }

                // Phase 7: Out-of-bounds access handling for speculative operations
                if (this.IsSpeculative)
                {
                    this.MarkFaulted();
                    return false; // Not ready to commit
                }

                return false;
            }
            catch (PageFaultException)
            {
                // Phase 7: Speculative FSP with Silent Squash
                // If this is a speculative operation, suppress the exception and mark as faulted
                if (this.IsSpeculative)
                {
                    this.MarkFaulted();
                    return false; // Not ready to commit
                }
                else
                {
                    // Non-speculative operation: propagate exception normally
                    throw;
                }
            }
            catch (Exception ex)
            {
                // Phase 7: Convert other memory exceptions to PageFaultException for speculative handling
                // This handles cases like IndexOutOfRangeException, ArgumentOutOfRangeException, etc.
                if (this.IsSpeculative)
                {
                    this.MarkFaulted();
                    return false; // Not ready to commit
                }
                else
                {
                    // Wrap and propagate for non-speculative operations
                    throw new PageFaultException($"Memory access error at 0x{Address:X}", ex, Address, false);
                }
            }
        }

        public override void EmitWriteBackRetireRecords(
            ref Processor.CPU_Core core,
            Span<RetireRecord> retireRecords,
            ref int retireRecordCount)
        {
            if (this.IsSpeculative && this.Faulted)
            {
                return;
            }

            if (WritesRegister && DestRegID != VLIW_Instruction.NoReg)
            {
                int vtId = NormalizeExecutionVtId(OwnerThreadId);
                AppendWriteBackRetireRecord(
                    retireRecords,
                    ref retireRecordCount,
                    RetireRecord.RegisterWrite(vtId, DestRegID, _loadedValue));
            }
        }

        public override bool TryGetPrimaryWriteBackResult(out ulong value)
        {
            value = _loadedValue;
            return WritesRegister && DestRegID != VLIW_Instruction.NoReg;
        }

        public override void CapturePrimaryWriteBackResult(ulong value) => _loadedValue = value;

        public override string GetDescription()
        {
            return $"Load: Addr=0x{Address:X}, Size={Size}, Dest=R{DestRegID}";
        }
    }

    /// <summary>
    /// Store micro-operation (memory write)
    /// Updated to use asynchronous memory subsystem
    /// </summary>
    public class StoreMicroOp : LoadStoreMicroOp
    {
        public ulong Address { get; set; }
        public ulong Value { get; set; }
        public byte Size { get; set; }
        public ushort SrcRegID { get; set; }
        public ushort BaseRegID { get; set; }
        private YAKSys_Hybrid_CPU.Memory.MemorySubsystem.MemoryRequestToken? _requestToken;

        /// <inheritdoc />
        public override ulong MemoryAddress => Address;

        public StoreMicroOp()
        {
            IsMemoryOp = true;
            HasSideEffects = true; // Memory writes have side effects
            Class = MicroOpClass.Lsu;

            // ISA v4 Phase 02: stores are Memory class, MemoryOrdered serialization
            InstructionClass = Arch.InstructionClass.Memory;
            SerializationClass = Arch.SerializationClass.MemoryOrdered;

            // Phase 01: Typed-slot taxonomy
            SetClassFlexiblePlacement(SlotClass.LsuClass);
        }

        /// <summary>
        /// Initialize FSP metadata after register IDs and address are set.
        /// Phase: Safety Tags & Certificates - Mandatory safety mask computation.
        /// </summary>
        public void InitializeMetadata()
        {
            // FSP MMIO Security Policy (Phase: Safety Tags & Certificates):
            // MMIO writes interact directly with hardware states.
            // Mark operations targeting MMIO space as strictly non-stealable.
            const ulong MMIO_BASE = 0xFFFF000000000000UL;
            if (Address >= MMIO_BASE)
            {
                IsStealable = false;
            }

            // Blueprint §3.69 / §5: guard against NoReg sentinels (0xFFFF)
            const ushort noReg = VLIW_Instruction.NoReg;

            // Read from source register (value) and base register (address)
            var readRegs = new List<int>();
            if (SrcRegID != noReg) readRegs.Add(SrcRegID);
            if (BaseRegID != noReg) readRegs.Add(BaseRegID);
            ReadRegisters = readRegs;

            // No register writes for store operations
            WriteRegisters = Array.Empty<int>();

            // Memory range to write
            WriteMemoryRanges = new[] { (Address, (ulong)Size) };

            // Phase 8: Initialize ResourceMask for GRLB
            ResourceMask = ResourceBitset.Zero;
            // Add register reads (source and base address registers)
            if (SrcRegID != noReg) ResourceMask |= ResourceMaskBuilder.ForRegisterRead(SrcRegID);
            if (BaseRegID != noReg) ResourceMask |= ResourceMaskBuilder.ForRegisterRead(BaseRegID);
            // Add LSU store channel
            ResourceMask |= ResourceMaskBuilder.ForStore();
            // Add memory domain (use owner thread ID as domain)
            ResourceMask |= ResourceMaskBuilder.ForMemoryDomain(OwnerThreadId);

            PublishExplicitStructuralSafetyMask();
            RefreshAdmissionMetadata(this);
        }

        /// <summary>
        /// Create rollback token for store operation.
        /// Captures pre-execution memory state.
        /// </summary>
        public override HybridCPU_ISE.Core.ReplayToken CreateRollbackToken(
            int ownerThreadId,
            Processor.MainMemoryArea? mainMemory = null)
        {
            var token = new HybridCPU_ISE.Core.ReplayToken(mainMemory)
            {
                OwnerThreadId = ownerThreadId,
                HasSideEffects = true
            };

            // Capture memory state before write
            token.CaptureMemoryState(Address, Size);

            return token;
        }

        public override bool Execute(ref Processor.CPU_Core core)
        {
            try
            {
                // If MemorySubsystem is available, use async interface
                var memSub = core.GetBoundMemorySubsystem();
                if (memSub != null)
                {
                    // First call - initiate async request
                    if (_requestToken == null)
                    {
                        byte[] buffer;
                        switch (Size)
                        {
                            case 1:
                                buffer = new byte[] { (byte)Value };
                                break;
                            case 2:
                                buffer = BitConverter.GetBytes((ushort)Value);
                                break;
                            case 4:
                                buffer = BitConverter.GetBytes((uint)Value);
                                break;
                            case 8:
                                buffer = BitConverter.GetBytes(Value);
                                break;
                            default:
                                return false; // Invalid size
                        }

                        _requestToken = memSub.EnqueueWrite(0 /* CPU Device ID */, Address, Size, buffer);
                        return false; // Operation not complete, need to retry
                    }

                    // Subsequent calls - check if request is complete
                    if (!_requestToken.IsComplete)
                    {
                        return false; // Still waiting, retry next cycle
                    }

                    _requestToken.ThrowIfFailed("StoreMicroOp.Execute()");

                    // Request complete
                    return true;
                }

                // Fallback to synchronous implementation if MemorySubsystem is not available
                if (HasExactMainMemoryRange(ref core, Address, Size))
                {
                    byte[] buffer;
                    switch (Size)
                    {
                        case 1:
                            buffer = new[] { (byte)Value };
                            break;
                        case 2:
                            buffer = BitConverter.GetBytes((ushort)Value);
                            break;
                        case 4:
                            buffer = BitConverter.GetBytes((uint)Value);
                            break;
                        case 8:
                            buffer = BitConverter.GetBytes(Value);
                            break;
                        default:
                            return false;
                    }

                    core.WriteBoundMainMemoryExact(Address, buffer, "StoreMicroOp.Execute()");
                    return true;
                }

                // Phase 7: Out-of-bounds access handling for speculative operations
                if (this.IsSpeculative)
                {
                    this.MarkFaulted();
                    return false; // Not ready to commit
                }

                return false;
            }
            catch (PageFaultException)
            {
                // Phase 7: Speculative FSP with Silent Squash
                // If this is a speculative operation, suppress the exception and mark as faulted
                if (this.IsSpeculative)
                {
                    this.MarkFaulted();
                    return false; // Not ready to commit
                }
                else
                {
                    // Non-speculative operation: propagate exception normally
                    throw;
                }
            }
            catch (Exception ex)
            {
                // Phase 7: Convert other memory exceptions to PageFaultException for speculative handling
                // This handles cases like IndexOutOfRangeException, ArgumentOutOfRangeException, etc.
                if (this.IsSpeculative)
                {
                    this.MarkFaulted();
                    return false; // Not ready to commit
                }
                else
                {
                    // Wrap and propagate for non-speculative operations
                    throw new PageFaultException($"Memory write error at 0x{Address:X}", ex, Address, true);
                }
            }
        }

        public override string GetDescription()
        {
            return $"Store: Addr=0x{Address:X}, Value=0x{Value:X}, Size={Size}";
        }
    }

    /// <summary>
    /// Branch scheduling micro-operation (jumps, branches, calls, returns).
    /// V6-final scheduling-only carrier; execution is routed through
    /// <c>ExecutionDispatcherV4.ExecuteControlFlow()</c> via <c>InstructionIR</c>.
    /// Do NOT add execution logic (Execute / Commit) to this class.
}
