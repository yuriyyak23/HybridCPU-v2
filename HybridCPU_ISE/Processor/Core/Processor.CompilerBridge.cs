using HybridCPU_ISE.Arch;
using System;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Contracts;

namespace YAKSys_Hybrid_CPU
{
    /// <summary>
    /// Runtime-side bridge for compiler-mode instruction recording and typed-slot fact acceptance.
    /// <para>
    /// Records <see cref="VLIW_Instruction"/> entries emitted by compiler-facing seams such as
    /// canonical emitters and processor-level compat recording helpers when
    /// <see cref="Processor.CurrentProcessorMode"/> is <see cref="ProcessorMode.Compiler"/>.
    /// The recorded buffer can be retrieved via <see cref="GetRecordedInstructions"/> for
    /// canonical compilation.
    /// </para>
    /// <para>
    /// Also accepts <see cref="TypedSlotBundleFacts"/> from the compiler with optional
    /// validation against the Stage 7 agreement.
    /// </para>
    /// </summary>
    public sealed class ProcessorCompilerBridge
    {
        private const int InitialInstructionCapacity = 64;
        private VLIW_Instruction[] _instructions = Array.Empty<VLIW_Instruction>();
        private int _instructionCount;
        private int? _declaredCompilerContractVersion;
        private CompilerTypedSlotPolicy _typedSlotPolicy = CompilerContract.CurrentTypedSlotPolicy;

        /// <summary>
        /// When <see langword="true"/>, <see cref="AcceptTypedSlotFacts"/> validates
        /// incoming compiler facts via <see cref="SafetyVerifier.ValidateTypedSlotFacts"/>.
        /// Under the current compatibility policy, validation failures are recorded
        /// and quarantine-logged rather than rejected. <see cref="TypedSlotPolicy"/>
        /// selects stricter behavior when a proof surface requires it.
        /// </summary>
        public bool ValidateCompilerFacts { get; set; } = true;

        /// <summary>
        /// Last <see cref="TypedSlotBundleFacts"/> accepted from the compiler.
        /// <c>default</c> (IsEmpty == true) if no facts have been supplied.
        /// </summary>
        public TypedSlotBundleFacts LastAcceptedFacts { get; private set; }

        /// <summary>
        /// Last <see cref="AgreementViolationReport"/> recorded, if any.
        /// Only meaningful when <see cref="ValidateCompilerFacts"/> is <see langword="true"/>
        /// and validation failed.
        /// </summary>
        public AgreementViolationReport? LastViolation { get; private set; }

        /// <summary>Number of instructions recorded since last reset.</summary>
        public int InstructionCount => _instructionCount;

        /// <summary>
        /// Gets or sets the active typed-slot policy for this bridge.
        /// The future required-for-admission policy is intentionally not selectable
        /// in the current mainline.
        /// </summary>
        public CompilerTypedSlotPolicy TypedSlotPolicy
        {
            get => _typedSlotPolicy;
            set
            {
                if (!value.IsRuntimeSelectable)
                {
                    throw new NotSupportedException(
                        "Compiler typed-slot required-for-admission policy is a future seam and is not selectable in the current runtime mainline.");
                }

                _typedSlotPolicy = value;
            }
        }

        /// <summary>
        /// Gets the last policy action taken by typed-slot fact ingress.
        /// </summary>
        public CompilerTypedSlotIngressAction LastTypedSlotIngressAction { get; private set; }

        /// <summary>
        /// Gets whether this bridge has observed an explicit compiler/runtime contract handshake.
        /// </summary>
        public bool HasContractHandshake => _declaredCompilerContractVersion.HasValue;

        /// <summary>
        /// Gets the compiler contract version declared by the active producer, if any.
        /// </summary>
        public int? DeclaredCompilerContractVersion => _declaredCompilerContractVersion;

        /// <summary>
        /// Gets the producer surface that declared the current compiler contract handshake.
        /// </summary>
        public string? ContractHandshakeProducerSurface { get; private set; }

        /// <summary>
        /// Declares the compiler contract version expected by the producer feeding this bridge.
        /// Canonical ingress paths must publish the handshake exactly once before
        /// any instruction recording or typed-slot fact publication occurs.
        /// </summary>
        public void DeclareCompilerContractVersion(int contractVersion, string producerSurface = "unspecified producer")
        {
            string resolvedSurface = string.IsNullOrWhiteSpace(producerSurface)
                ? "unspecified producer"
                : producerSurface;

            CompilerContract.ThrowIfVersionMismatch(contractVersion, producerSurface);

            if (_declaredCompilerContractVersion.HasValue)
            {
                string existingSurface = string.IsNullOrWhiteSpace(ContractHandshakeProducerSurface)
                    ? "unspecified producer"
                    : ContractHandshakeProducerSurface!;
                throw new InvalidOperationException(
                    $"ProcessorCompilerBridge already has compiler contract handshake version {_declaredCompilerContractVersion.Value} from {existingSurface}. Duplicate declaration from {resolvedSurface} is not allowed.");
            }

            _declaredCompilerContractVersion = contractVersion;
            ContractHandshakeProducerSurface = resolvedSurface;
        }

        /// <summary>
        /// Accept typed-slot facts emitted by the compiler for a compiled bundle.
        /// Optionally validates the facts against the bundle if <see cref="ValidateCompilerFacts"/> is enabled.
        /// </summary>
        public void AcceptTypedSlotFacts(
            TypedSlotBundleFacts facts,
            MicroOp?[]? bundle = null,
            int bundleWidth = 8)
        {
            EnsureCompilerContractHandshake(nameof(AcceptTypedSlotFacts));
            LastAcceptedFacts = facts;
            LastViolation = null;
            LastTypedSlotIngressAction = CompilerTypedSlotIngressAction.None;

            if (facts.IsEmpty)
            {
                LastTypedSlotIngressAction = CompilerTypedSlotIngressAction.AcceptedMissingFacts;
                return;
            }

            bool mustValidate = TypedSlotPolicy.RequiresValidationForPresentFacts || ValidateCompilerFacts;
            if (!mustValidate)
            {
                LastTypedSlotIngressAction = CompilerTypedSlotIngressAction.RecordedWithoutValidation;
                return;
            }

            if (bundle is null)
            {
                LastTypedSlotIngressAction = CompilerTypedSlotIngressAction.RecordedWithoutValidation;
                if (TypedSlotPolicy.RequiresRuntimeBundleForPresentFacts)
                {
                    LastTypedSlotIngressAction = CompilerTypedSlotIngressAction.RejectedAgreementFailure;
                    throw new InvalidOperationException(
                        "Strict typed-slot verification requires a runtime bundle to verify present compiler facts.");
                }

                return;
            }

            bool valid = SafetyVerifier.ValidateTypedSlotFacts(facts, bundle, bundleWidth);
            if (valid)
            {
                LastTypedSlotIngressAction = CompilerTypedSlotIngressAction.RecordedValidatedFacts;
                return;
            }

            RecordTypedSlotViolation(
                facts,
                TypedSlotRejectReason.StaticClassOvercommit,
                operationName: nameof(AcceptTypedSlotFacts));
        }

        /// <summary>
        /// Records an agreement violation when runtime rejects an op
        /// that compiler declared structurally admissible.
        /// </summary>
        public void RecordAgreementViolation(
            TypedSlotBundleFacts facts,
            TypedSlotRejectReason runtimeReject)
        {
            EnsureCompilerContractHandshake(nameof(RecordAgreementViolation));
            RecordTypedSlotViolation(facts, runtimeReject, nameof(RecordAgreementViolation));
        }

        private void RecordTypedSlotViolation(
            TypedSlotBundleFacts facts,
            TypedSlotRejectReason runtimeReject,
            string operationName)
        {
            bool isStructuralMismatch = AgreementViolationReport.IsStructuralReject(runtimeReject);
            LastViolation = new AgreementViolationReport
            {
                CompilerFacts = facts,
                RuntimeReject = runtimeReject,
                IsStructuralMismatch = isStructuralMismatch
            };

            if (isStructuralMismatch)
            {
                LastTypedSlotIngressAction = TypedSlotPolicy.RejectsStructuralAgreementFailures
                    ? CompilerTypedSlotIngressAction.RejectedAgreementFailure
                    : CompilerTypedSlotIngressAction.QuarantinedAgreementFailure;

                if (TypedSlotPolicy.RejectsStructuralAgreementFailures)
                {
                    throw new InvalidOperationException(
                        $"Strict typed-slot verification rejected structural agreement failure during {operationName}: {runtimeReject}.");
                }

                return;
            }

            LastTypedSlotIngressAction = CompilerTypedSlotIngressAction.RecordedDynamicRuntimeReject;
        }

        /// <summary>
        /// Records a standard VLIW instruction into the bridge buffer.
        /// Compat-only bit-container recording: opcode semantics remain authoritative
        /// only in canonical IR / OpcodeRegistry layers, not in this bridge buffer.
        /// </summary>
        public void RecordInstruction(in VLIW_Instruction instruction)
        {
            EnsureCompilerContractHandshake(nameof(RecordInstruction));
            EnsureInstructionCapacityForAppend();

            _instructions[_instructionCount] = instruction;
            _instructionCount++;
        }

        /// <summary>
        /// Legacy compat wrapper that appends a raw VLIW container built from scalar fields.
        /// New production code should prefer <see cref="RecordInstruction"/> with an emitter-built
        /// <see cref="VLIW_Instruction"/> so helper layers do not duplicate bridge append logic.
        /// </summary>
        public void Add_VLIW_Instruction(
            uint opCode,
            byte dataType,
            byte predicate,
            ushort immediate,
            ulong destSrc1,
            ulong src2,
            ulong streamLength,
            ushort stride)
        {
            RecordInstruction(new VLIW_Instruction
            {
                OpCode = opCode,
                DataType = dataType,
                PredicateMask = predicate,
                Immediate = immediate,
                DestSrc1Pointer = destSrc1,
                Src2Pointer = src2,
                StreamLength = (uint)streamLength,
                Stride = stride
            });
        }

        /// <summary>
        /// Returns the instructions recorded since the last reset.
        /// </summary>
        public ReadOnlySpan<VLIW_Instruction> GetRecordedInstructions()
        {
            return new ReadOnlySpan<VLIW_Instruction>(_instructions, 0, _instructionCount);
        }

        /// <summary>
        /// Clears the recorded instruction buffer.
        /// </summary>
        public void ResetInstructionBuffer()
        {
            _instructionCount = 0;
        }

        private void EnsureInstructionCapacityForAppend()
        {
            int requiredCapacity = checked(_instructionCount + 1);
            if (requiredCapacity <= _instructions.Length)
            {
                return;
            }

            // The bridge is a compat-only recording surface, but it must not silently
            // impose a legacy hard stop on canonical compilation throughput.
            int nextCapacity = _instructions.Length == 0
                ? InitialInstructionCapacity
                : checked(_instructions.Length * 2);
            if (nextCapacity < requiredCapacity)
            {
                nextCapacity = requiredCapacity;
            }

            Array.Resize(ref _instructions, nextCapacity);
        }

        private void EnsureCompilerContractHandshake(string operationName)
        {
            if (_declaredCompilerContractVersion.HasValue)
            {
                return;
            }

            throw new InvalidOperationException(
                $"ProcessorCompilerBridge requires an explicit compiler contract handshake before {operationName}. Call {nameof(DeclareCompilerContractVersion)}({nameof(CompilerContract)}.{nameof(CompilerContract.Version)}, ...) from the compiler/runtime ingress path.");
        }
    }
}
