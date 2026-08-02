namespace YAKSys_Hybrid_CPU.Core.Pipeline
{
    // ─────────────────────────────────────────────────────────────────────────
    // Phase 2 (D23/D24): AbstractBundle — front-end bundle abstraction.
    //
    // These interfaces decouple the execution core from the concrete VLIW-8 bundle
    // layout (8 slots × 32 bytes = 256 bytes).  Any front-end — VLIW, scalar
    // issue, or DBT — presents its work as IAbstractBundle so that the admission,
    // execution, and commit stages can be written without assuming a fixed slot
    // count or slot width.
    //
    // V6 Phase 3 (D23) note: all internal bundle types (slot-carrier transport views,
    // BundleIssuePacket, ClusterIssuePreparation) should implement or be
    // convertible to IAbstractBundle.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A single decoded instruction slot within a bundle.
    ///
    /// Abstracts over the concrete VLIW instruction encoding; the execution
    /// core depends only on this interface.
    /// </summary>
    public interface IAbstractBundleSlot
    {
        /// <summary>Zero-based index of this slot within its bundle.</summary>
        int SlotIndex { get; }

        /// <summary>
        /// <see langword="true"/> when this slot carries a real instruction
        /// (not a NOP, empty, or already-consumed slot).
        /// </summary>
        bool IsOccupied { get; }

        /// <summary>
        /// The decoded instruction IR for this slot, or <see langword="null"/>
        /// when the slot is empty/NOP.
        ///
        /// <para>
        /// The execution core must call
        /// <see cref="Execution.ExecutionDispatcherV4.Execute"/> with this value
        /// — it must never inspect the raw opcode directly.
        /// </para>
        /// </summary>
        MicroOps.InstructionIR? Instruction { get; }
    }

    /// <summary>
    /// A front-end instruction bundle: an ordered, finite sequence of
    /// <see cref="IAbstractBundleSlot"/> instances.
    ///
    /// <para>
    /// Scalar front-ends produce single-slot bundles.
    /// VLIW front-ends produce up to eight populated slots per bundle.
    /// DBT (dynamic binary translation) front-ends may produce variable-width bundles.
    /// </para>
    ///
    /// <para>
    /// The admission and execution stages must use only this interface to
    /// avoid hard-coding the 8-slot VLIW assumption (D23).
    /// </para>
    /// </summary>
    public interface IAbstractBundle
    {
        /// <summary>
        /// Programme-counter address of the first byte of this bundle.
        /// For VLIW bundles this is 256-byte aligned; scalar bundles may be
        /// arbitrarily aligned.
        /// </summary>
        ulong BundleAddress { get; }

        /// <summary>
        /// Total number of slots in this bundle, including empty/NOP slots.
        /// </summary>
        int SlotCount { get; }

        /// <summary>
        /// Returns the slot at the given zero-based <paramref name="index"/>.
        /// </summary>
        /// <param name="index">Slot index in the range [0, <see cref="SlotCount"/>).</param>
        /// <returns>The slot descriptor at the requested index.</returns>
        IAbstractBundleSlot GetSlot(int index);

        /// <summary>
        /// Bundle serial number, unique within a single execution context.
        /// Used as the <c>BundleSerial</c> field of <see cref="PipelineEvent"/>
        /// instances generated during execution of this bundle.
        /// </summary>
        ulong BundleSerial { get; }

        /// <summary>
        /// <see langword="true"/> when every slot in the bundle is empty or NOP
        /// (no real work to issue).
        /// </summary>
        bool IsEmpty { get; }
    }
}
