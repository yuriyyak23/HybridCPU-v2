using HybridCPU_ISE.Arch;
using HybridCPU_ISE.CloseToHSL.Memory.Timing;

using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core.Memory;
using YAKSys_Hybrid_CPU.Core.Registers;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;
using OpcodeValues = YAKSys_Hybrid_CPU.Processor.CPU_Core.IsaOpcodeValues;


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

        internal static ulong DecodeLoadValue(
            uint opcode,
            byte[] buffer,
            byte accessSize,
            string executionSurface)
        {
            int requiredSize = accessSize switch
            {
                1 => 1,
                2 => 2,
                4 => 4,
                0 or 8 => 8,
                _ => throw new InvalidOperationException(
                    $"{executionSurface} reached unsupported scalar load access size {accessSize}.")
            };

            if (buffer.Length < requiredSize)
            {
                throw new InvalidOperationException(
                    $"{executionSurface} reached a partial load buffer ({buffer.Length} byte(s)) for an access size of {requiredSize} byte(s). " +
                    "The authoritative memory lane must fail closed instead of decoding a partial load image across the boundary contour.");
            }

            ushort normalizedOpcode = unchecked((ushort)opcode);
            if (TryResolveTypedLoadAccessSize(normalizedOpcode, out byte typedAccessSize) &&
                typedAccessSize != requiredSize)
            {
                throw new InvalidOperationException(
                    $"{executionSurface} reached typed load opcode {OpcodeRegistry.GetMnemonicOrHex(opcode)} " +
                    $"with access size {requiredSize}, but the published runtime contour requires {typedAccessSize} byte(s).");
            }

            return normalizedOpcode switch
            {
                OpcodeValues.LB => unchecked((ulong)(long)(sbyte)buffer[0]),
                OpcodeValues.LH => unchecked((ulong)(long)(short)BitConverter.ToUInt16(buffer, 0)),
                OpcodeValues.LW => unchecked((ulong)(long)(int)BitConverter.ToUInt32(buffer, 0)),
                OpcodeValues.LBU => buffer[0],
                OpcodeValues.LHU => BitConverter.ToUInt16(buffer, 0),
                OpcodeValues.LWU => BitConverter.ToUInt32(buffer, 0),
                OpcodeValues.LD => BitConverter.ToUInt64(buffer, 0),
                _ => DecodeUntypedLoadValue(buffer, requiredSize)
            };
        }

        private static ulong DecodeUntypedLoadValue(byte[] buffer, int requiredSize)
        {
            return requiredSize switch
            {
                1 => buffer[0],
                2 => BitConverter.ToUInt16(buffer, 0),
                4 => BitConverter.ToUInt32(buffer, 0),
                _ => BitConverter.ToUInt64(buffer, 0)
            };
        }

        private static bool TryResolveTypedLoadAccessSize(ushort opcode, out byte accessSize)
        {
            accessSize = opcode switch
            {
                OpcodeValues.LB or OpcodeValues.LBU => 1,
                OpcodeValues.LH or OpcodeValues.LHU => 2,
                OpcodeValues.LW or OpcodeValues.LWU => 4,
                OpcodeValues.LD => 8,
                _ => (byte)0
            };

            return accessSize != 0;
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
        private MemoryCycleController? _requestController;
        private MemoryRequestId? _controllerRequestId;
        private bool _controllerAdmissionBackpressured;

        // RF-07.2g runtime evidence for the one scalar-load false contour that
        // is unambiguously a resource wait. The controller-qualified request
        // identity remains owned by this
        // MicroOp; this property does not publish readiness or architectural
        // completion and is not a replacement execution-state model.
        internal bool OwnsPendingMemoryCompletion =>
            _requestController != null &&
            _controllerRequestId.HasValue &&
            _requestController.OwnsOutstandingSingleLaneScalarLoad(
                _controllerRequestId.Value,
                deviceId: 0,
                Address,
                Size);

        // RF-10.3 no-ID admission wait. This is consumer-local observation of
        // a controller decision; it grants no request identity or queue state.
        internal bool HasControllerAdmissionBackpressure =>
            _controllerAdmissionBackpressured &&
            !_controllerRequestId.HasValue;

        // RF-07.2o observes, but does not own, the existing FSP silent-squash
        // carrier. Scheduler ProcessFaultedOperations retains cleanup authority.
        internal bool IsSpeculativeFaultSuppressed => IsSpeculative && Faulted;

        // RF-07.2h evidence for the distinct non-speculative fallback denial.
        // This query owns no readiness/outcome state: it only rechecks the exact
        // backend/range facts that made this concrete Execute call return false.
        internal bool HasNonSpeculativeFallbackBackendDenial(Processor.CPU_Core core)
        {
            return !_controllerRequestId.HasValue &&
                _requestController is null &&
                !IsSpeculative &&
                !Faulted &&
                core.GetBoundMemorySubsystem() is null &&
                !core.HasExactBoundMainMemoryRange(Address, Size);
        }

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
                ResourceMask |= ArchRegId.TryCreate(BaseRegID, out ArchRegId baseRegister)
                    ? ResourceMaskBuilder.ForArchitecturalRegisterRead(baseRegister)
                    : ResourceMaskBuilder.ForRegisterRead(BaseRegID);
            }
            // Add register write (destination register)
            if (WritesRegister && DestRegID != noReg)
            {
                ResourceMask |= ArchRegId.TryCreate(DestRegID, out ArchRegId destinationRegister)
                    ? ResourceMaskBuilder.ForArchitecturalRegisterWrite(destinationRegister)
                    : ResourceMaskBuilder.ForRegisterWrite(DestRegID);
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
            _controllerAdmissionBackpressured = false;
            try
            {
                // If MemorySubsystem is available, use the RF-10 controller
                // request/completion interface.
                var memSub = core.GetBoundMemorySubsystem();
                if (memSub != null)
                {
                    MemoryCycleController controller = memSub.CycleController;
                    if (!_controllerRequestId.HasValue)
                    {
                        MemoryAdmissionResult admission =
                            controller.TryAcceptSingleLaneScalarLoad(
                                0 /* CPU Device ID */,
                                Address,
                                Size);
                        if (admission.Status == MemoryAdmissionStatus.Backpressured)
                        {
                            _controllerAdmissionBackpressured = true;
                            return false;
                        }

                        if (admission.Status == MemoryAdmissionStatus.Rejected)
                        {
                            throw new InvalidOperationException(
                                admission.Reason ??
                                "MemoryCycleController rejected a single-lane scalar load without a reason.");
                        }

                        _requestController = controller;
                        _controllerRequestId = admission.RequestId;
                        return false;
                    }

                    if (!ReferenceEquals(_requestController, controller))
                    {
                        _requestController?.TryCancel(_controllerRequestId.Value);
                        ClearControllerRequestState();
                        throw new InvalidOperationException(
                            "LoadMicroOp controller binding changed while an accepted request was outstanding.");
                    }

                    if (!controller.TryTakeCompletion(
                            _controllerRequestId.Value,
                            out MemoryCompletion? completion))
                    {
                        return false;
                    }

                    ClearControllerRequestState();
                    if (completion == null || !completion.Succeeded)
                    {
                        throw new InvalidOperationException(
                            "LoadMicroOp.Execute(): accepted memory request did not materialize successfully. " +
                            (completion?.FailureReason ??
                             "MemoryCycleController returned an invalid single-lane scalar-load completion."));
                    }

                    byte[] resultBuffer = completion.Data.ToArray();
                    _loadedValue = DecodeLoadValue(
                        OpCode,
                        resultBuffer,
                        Size,
                        "LoadMicroOp.Execute()");
                    return true; // Operation complete
                }

                // Fallback to synchronous implementation if MemorySubsystem is not available
                if (HasExactMainMemoryRange(ref core, Address, Size))
                {
                    byte[] bytes = new byte[Size];
                    ReadMainMemoryExact(ref core, Address, bytes, "LoadMicroOp.Execute()");
                    _loadedValue = DecodeLoadValue(
                        OpCode,
                        bytes,
                        Size,
                        "LoadMicroOp.Execute()");
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

        internal bool CancelPendingControllerRequest()
        {
            bool canceled = _requestController != null &&
                _controllerRequestId.HasValue &&
                _requestController.TryCancel(_controllerRequestId.Value);
            ClearControllerRequestState();
            return canceled;
        }

        private void ClearControllerRequestState()
        {
            _requestController = null;
            _controllerRequestId = null;
            _controllerAdmissionBackpressured = false;
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
                int vtId = TryGetArchitecturalOwnerVtId(out VtId checkedOwner)
                    ? checkedOwner.Value
                    : NormalizeExecutionVtId(OwnerThreadId);
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
        private MemoryCycleController? _requestController;
        private MemoryRequestId? _controllerRequestId;
        private bool _controllerAdmissionBackpressured;

        // RF-07.2i runtime evidence for the one scalar-store false contour that
        // is unambiguously an owned pending readiness request. Exact controller,
        // device/address/size/data identity prevents a mutated MicroOp from
        // borrowing the retry disposition. This is evidence only, not mutable
        // readiness or retirement authority.
        internal bool OwnsPendingWriteCompletion
        {
            get
            {
                if (_requestController == null ||
                    !_controllerRequestId.HasValue ||
                    !TryCreateStoreBuffer(out byte[] buffer))
                {
                    return false;
                }

                return _requestController.OwnsOutstandingSingleLaneScalarStore(
                    _controllerRequestId.Value,
                    0,
                    Address,
                    Size,
                    buffer);
            }
        }

        internal bool HasControllerAdmissionBackpressure =>
            _controllerAdmissionBackpressured &&
            _requestController == null &&
            !_controllerRequestId.HasValue;

        // RF-07.2k evidence for the malformed scalar-store carrier. This is a
        // structural MicroOp invariant, not a backend/range observation and not
        // a speculative fault-suppression signal. It owns no mutable outcome,
        // readiness, scheduler or retirement state.
        internal bool HasInvalidTransferSize => Size is not (1 or 2 or 4 or 8);

        // RF-07.2l evidence for the existing FSP silent-suppression carrier.
        // This query does not select a squash, release a reservation or mutate
        // completion state: MicroOpScheduler.ProcessFaultedOperations remains
        // the sole FSP owner. Invalid size is intentionally excluded because
        // RF-07.2k owns that malformed carrier as FatalInvariantViolation.
        internal bool IsSpeculativeFaultSuppressed =>
            !HasInvalidTransferSize && IsSpeculative && Faulted;

        // RF-07.2j evidence for the distinct non-speculative fallback denial.
        // This query owns no readiness/outcome state: it only rechecks the exact
        // backend/range facts that made this concrete Execute call return false.
        internal bool HasNonSpeculativeFallbackBackendDenial(Processor.CPU_Core core)
        {
            return _requestController is null &&
                !_controllerRequestId.HasValue &&
                (Size is 1 or 2 or 4 or 8) &&
                !IsSpeculative &&
                !Faulted &&
                core.GetBoundMemorySubsystem() is null &&
                !core.HasExactBoundMainMemoryRange(Address, Size);
        }

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
            if (SrcRegID != noReg)
            {
                ResourceMask |= ArchRegId.TryCreate(SrcRegID, out ArchRegId sourceRegister)
                    ? ResourceMaskBuilder.ForArchitecturalRegisterRead(sourceRegister)
                    : ResourceMaskBuilder.ForRegisterRead(SrcRegID);
            }
            if (BaseRegID != noReg)
            {
                ResourceMask |= ArchRegId.TryCreate(BaseRegID, out ArchRegId baseRegister)
                    ? ResourceMaskBuilder.ForArchitecturalRegisterRead(baseRegister)
                    : ResourceMaskBuilder.ForRegisterRead(BaseRegID);
            }
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
            _controllerAdmissionBackpressured = false;
            try
            {
                // A bound subsystem uses RF-10 controller readiness. The
                // controller never publishes the store bytes; selected retire
                // remains the physical mutation owner.
                var memSub = core.GetBoundMemorySubsystem();
                if (memSub != null)
                {
                    if (!TryCreateStoreBuffer(out byte[] buffer))
                    {
                        return false;
                    }

                    MemoryCycleController controller = memSub.CycleController;
                    if (!_controllerRequestId.HasValue)
                    {
                        MemoryAdmissionResult admission =
                            controller.TryAcceptSingleLaneScalarStore(
                                0 /* CPU Device ID */,
                                Address,
                                Size,
                                buffer);
                        if (admission.Status == MemoryAdmissionStatus.Backpressured)
                        {
                            _controllerAdmissionBackpressured = true;
                            return false;
                        }
                        if (admission.Status == MemoryAdmissionStatus.Rejected)
                        {
                            throw new InvalidOperationException(
                                admission.Reason ??
                                "MemoryCycleController rejected a single-lane scalar store without a reason.");
                        }

                        _requestController = controller;
                        _controllerRequestId = admission.RequestId;
                        return false;
                    }

                    if (!ReferenceEquals(_requestController, controller))
                    {
                        _requestController?.TryCancel(_controllerRequestId.Value);
                        ClearControllerRequestState();
                        throw new InvalidOperationException(
                            "StoreMicroOp controller binding changed while an accepted request was outstanding.");
                    }

                    if (!controller.TryTakeCompletion(
                            _controllerRequestId.Value,
                            out MemoryCompletion? completion))
                    {
                        return false;
                    }

                    ClearControllerRequestState();
                    if (completion == null || !completion.Succeeded)
                    {
                        throw new InvalidOperationException(
                            "StoreMicroOp.Execute(): accepted readiness request did not complete successfully. " +
                            (completion?.FailureReason ??
                             "MemoryCycleController returned an invalid single-lane scalar-store completion."));
                    }

                    return true;
                }

                // Fallback to synchronous implementation if MemorySubsystem is not available
                if (HasExactMainMemoryRange(ref core, Address, Size))
                {
                    if (!TryCreateStoreBuffer(out byte[] buffer))
                        return false;

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

        internal bool CancelPendingControllerRequest()
        {
            bool canceled = _requestController != null &&
                _controllerRequestId.HasValue &&
                _requestController.TryCancel(_controllerRequestId.Value);
            ClearControllerRequestState();
            return canceled;
        }

        private void ClearControllerRequestState()
        {
            _requestController = null;
            _controllerRequestId = null;
            _controllerAdmissionBackpressured = false;
        }

        private bool TryCreateStoreBuffer(out byte[] buffer)
        {
            buffer = Size switch
            {
                1 => new[] { (byte)Value },
                2 => BitConverter.GetBytes((ushort)Value),
                4 => BitConverter.GetBytes((uint)Value),
                8 => BitConverter.GetBytes(Value),
                _ => Array.Empty<byte>(),
            };
            return buffer.Length != 0;
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
