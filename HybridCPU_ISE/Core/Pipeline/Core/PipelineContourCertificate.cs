using System;

namespace YAKSys_Hybrid_CPU.Core
{
    public enum PipelineContourKind : byte
    {
        None = 0,
        DecodePublication = 1,
        ExecuteCompletion = 2,
        RetireVisibility = 3
    }

    public enum PipelineContourOwner : byte
    {
        None = 0,
        DecodedBundleTransportPublication = 1,
        SingleLaneMicroOpExecution = 2,
        ExplicitPacketExecution = 3,
        ReferenceExecution = 4,
        WriteBackRetireWindow = 5
    }

    public enum PipelineContourVisibilityStage : byte
    {
        None = 0,
        Decode = 1,
        Execute = 2,
        Memory = 3,
        WriteBack = 4,
        DirectRetire = 5
    }

    public readonly struct PipelineContourCertificate
    {
        private PipelineContourCertificate(
            PipelineContourKind kind,
            PipelineContourOwner owner,
            PipelineContourVisibilityStage visibilityStage,
            ulong pc,
            byte slotMask,
            bool isPublished)
        {
            Kind = kind;
            Owner = owner;
            VisibilityStage = visibilityStage;
            Pc = pc;
            SlotMask = slotMask;
            IsPublished = isPublished;
        }

        public PipelineContourKind Kind { get; }
        public PipelineContourOwner Owner { get; }
        public PipelineContourVisibilityStage VisibilityStage { get; }
        public ulong Pc { get; }
        public byte SlotMask { get; }
        public bool IsPublished { get; }

        public static PipelineContourCertificate CreateEmpty(
            PipelineContourKind kind)
        {
            if (kind == PipelineContourKind.None)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(kind),
                    kind,
                    "Contour certificate empties must still name the contour kind.");
            }

            return new PipelineContourCertificate(
                kind,
                PipelineContourOwner.None,
                PipelineContourVisibilityStage.None,
                pc: 0,
                slotMask: 0,
                isPublished: false);
        }

        public static PipelineContourCertificate CreatePublished(
            PipelineContourKind kind,
            PipelineContourOwner owner,
            PipelineContourVisibilityStage visibilityStage,
            ulong pc,
            byte slotMask)
        {
            if (kind == PipelineContourKind.None)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(kind),
                    kind,
                    "Published contour certificates must name the contour kind.");
            }

            if (owner == PipelineContourOwner.None)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(owner),
                    owner,
                    "Published contour certificates must name an explicit owner.");
            }

            if (visibilityStage == PipelineContourVisibilityStage.None)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(visibilityStage),
                    visibilityStage,
                    "Published contour certificates must name the publication stage.");
            }

            return new PipelineContourCertificate(
                kind,
                owner,
                visibilityStage,
                pc,
                slotMask,
                isPublished: true);
        }
    }
}
