using HybridCPU.Compiler.Core.IR;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Registers;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU.Compiler.Core.Threading
{
    /// <summary>
    /// Thread-specific compilation context for 4-way SMT.
    /// Each HybridCpuThreadCompilerContext represents one virtual thread (0-3) within a physical core
    /// and compiles through <see cref="HybridCpuCanonicalCompiler"/> directly.
    /// </summary>
    public partial class HybridCpuThreadCompilerContext
    {
        private readonly VtId _virtualThreadId;
        private FrontendMode _frontendMode = FrontendMode.NativeVLIW;
        private ulong _domainTag;

        private const int MAX_INSTRUCTIONS_PER_THREAD = 1024;
        private readonly VLIW_Instruction[] _instructions = new VLIW_Instruction[MAX_INSTRUCTIONS_PER_THREAD];
        private readonly InstructionSlotMetadata[] _instructionSlotMetadata = new InstructionSlotMetadata[MAX_INSTRUCTIONS_PER_THREAD];
        private int _instructionCount = 0;

        /// <summary>
        /// Creates a new thread compiler context for a specific virtual thread.
        /// </summary>
        /// <param name="virtualThreadId">VT ID (0-3) for 4-way SMT</param>
        public HybridCpuThreadCompilerContext(byte virtualThreadId)
        {
            _virtualThreadId = VtId.Create(virtualThreadId);
            _domainTag = 0;
        }

        /// <summary>
        /// Virtual Thread ID (0-3) assigned to this context.
        /// </summary>
        public byte VirtualThreadId => _virtualThreadId.Value;

        /// <summary>
        /// Typed virtual thread identity used by the phase-02 register model.
        /// </summary>
        public VtId TypedVirtualThreadId => _virtualThreadId;

        /// <summary>
        /// Frontend profile selected for canonical compilation of this virtual thread.
        /// </summary>
        public FrontendMode FrontendMode
        {
            get => _frontendMode;
            set
            {
                if (_frontendMode == value)
                {
                    return;
                }

                _frontendMode = value;
                InvalidateCanonicalCompileCache();
            }
        }

        /// <summary>
        /// Security domain tag for this VT (enforced by MEM_DOMAIN_CERT CSR at runtime).
        /// </summary>
        public ulong DomainTag
        {
            get => _domainTag;
            set
            {
                if (_domainTag == value)
                {
                    return;
                }

                _domainTag = value;
                RefreshInstructionDomainTags(value);
                InvalidateCanonicalCompileCache();
            }
        }

        /// <summary>
        /// Number of instructions compiled for this VT so far.
        /// </summary>
        public int InstructionCount => _instructionCount;

        /// <summary>
        /// Compiles a VLIW instruction with automatic VT sideband metadata.
        /// This is the primary API for thread-aware compilation.
        /// </summary>
        public void CompileInstruction(
            uint opCode, byte dataType, byte predicate, ushort immediate,
            ulong destSrc1, ulong src2, ulong streamLength, ushort stride,
            StealabilityPolicy stealabilityPolicy)
        {
            if (_instructionCount >= MAX_INSTRUCTIONS_PER_THREAD)
                throw new InvalidOperationException($"VT-{_virtualThreadId.Value}: Instruction buffer overflow (max {MAX_INSTRUCTIONS_PER_THREAD})");

            var inst = new VLIW_Instruction
            {
                OpCode = opCode,
                DataType = dataType,
                PredicateMask = predicate,
                Immediate = immediate,
                DestSrc1Pointer = destSrc1,
                Src2Pointer = src2,
                StreamLength = (uint)streamLength,
                Stride = stride
            };

            _instructions[_instructionCount] = inst;
            _instructionSlotMetadata[_instructionCount] = new InstructionSlotMetadata(
                _virtualThreadId,
                BuildSlotMetadata(opCode, stealabilityPolicy, _domainTag));
            _instructionCount++;
            InvalidateCanonicalCompileCache();
        }

        /// <summary>
        /// Retrieves the compiled instruction buffer for this VT.
        /// Returns a span to avoid allocation (HLS-compatible).
        /// </summary>
        public ReadOnlySpan<VLIW_Instruction> GetCompiledInstructions()
        {
            return new ReadOnlySpan<VLIW_Instruction>(_instructions, 0, _instructionCount);
        }

        /// <summary>
        /// Retrieves the canonical bundle annotations accumulated for the compiled instructions.
        /// </summary>
        public VliwBundleAnnotations GetBundleAnnotations()
        {
            if (_instructionCount == 0)
            {
                return VliwBundleAnnotations.Empty;
            }

            var slotMetadata = new InstructionSlotMetadata[_instructionCount];
            Array.Copy(_instructionSlotMetadata, slotMetadata, _instructionCount);
            return new VliwBundleAnnotations(slotMetadata);
        }

        private static SlotMetadata BuildSlotMetadata(
            uint opCode,
            StealabilityPolicy stealabilityPolicy,
            ulong domainTag)
        {
            MicroOpAdmissionMetadata admissionMetadata =
                BuildAdmissionMetadata(opCode, stealabilityPolicy, domainTag);

            return new SlotMetadata
            {
                StealabilityPolicy = stealabilityPolicy,
                AdmissionMetadata = admissionMetadata
            };
        }

        private static MicroOpAdmissionMetadata BuildAdmissionMetadata(
            uint opCode,
            StealabilityPolicy stealabilityPolicy,
            ulong domainTag)
        {
            IrOpcodeExecutionProfile executionProfile =
                HybridCpuHazardModel.GetExecutionProfile((InstructionsEnum)opCode);
            SlotPinningKind runtimePinning =
                IrSlotClassMapping.ToRuntimePinningKind(executionProfile.DerivedBindingKind);
            SlotPlacementMetadata placement = new()
            {
                RequiredSlotClass = executionProfile.DerivedSlotClass,
                PinningKind = runtimePinning,
                PinnedLaneId = ResolvePinnedLaneId(executionProfile.DerivedSlotClass, runtimePinning),
                DomainTag = domainTag
            };

            // Thread-ingress metadata only publishes the placement/domain subset that is
            // already known from the opcode profile plus thread context. Register and
            // memory facts remain deferred to IR/decode truth surfaces.
            return MicroOpAdmissionMetadata.Default with
            {
                IsStealable = stealabilityPolicy == StealabilityPolicy.Stealable,
                DomainTag = domainTag,
                Placement = placement
            };
        }

        private static byte ResolvePinnedLaneId(
            SlotClass requiredSlotClass,
            SlotPinningKind pinningKind)
        {
            if (pinningKind != SlotPinningKind.HardPinned)
            {
                return 0;
            }

            return requiredSlotClass switch
            {
                SlotClass.BranchControl => 7,
                SlotClass.SystemSingleton => 7,
                _ => 0
            };
        }

        private void RefreshInstructionDomainTags(ulong domainTag)
        {
            for (int instructionIndex = 0; instructionIndex < _instructionCount; instructionIndex++)
            {
                InstructionSlotMetadata slotMetadata = _instructionSlotMetadata[instructionIndex];
                SlotMetadata coreSlotMetadata = slotMetadata.SlotMetadata ?? SlotMetadata.Default;
                MicroOpAdmissionMetadata refreshedAdmission = coreSlotMetadata.AdmissionMetadata with
                {
                    DomainTag = domainTag,
                    Placement = coreSlotMetadata.AdmissionMetadata.Placement with { DomainTag = domainTag }
                };

                _instructionSlotMetadata[instructionIndex] = slotMetadata with
                {
                    SlotMetadata = coreSlotMetadata with { AdmissionMetadata = refreshedAdmission }
                };
            }
        }

        /// <summary>
        /// Resets this thread context (clears all compiled instructions and metadata).
        /// </summary>
        public void Reset()
        {
            _instructionCount = 0;
            Array.Clear(_instructionSlotMetadata, 0, _instructionSlotMetadata.Length);

            ResetIrMetadataDeclarations();
            InvalidateCanonicalCompileCache();
        }
    }
}
