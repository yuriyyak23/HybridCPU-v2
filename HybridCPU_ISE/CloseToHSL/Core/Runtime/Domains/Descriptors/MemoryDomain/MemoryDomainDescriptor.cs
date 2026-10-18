namespace YAKSys_Hybrid_CPU.Core;

public sealed partial class MemoryDomainDescriptor
{
    public MemoryDomainDescriptor()
        : this(
            addressSpace: null,
            translationPolicy: null,
            translationControl: MemoryDomainTranslationControl.Disabled,
            dirtyTracking: null,
            ownsSecondStageTranslation: true)
    {
    }

    public MemoryDomainDescriptor(
        AddressSpaceDescriptor? addressSpace,
        MemoryTranslationPolicy? translationPolicy,
        MemoryDomainTranslationControl translationControl,
        DirtyTrackingServiceDescriptor? dirtyTracking,
        bool ownsSecondStageTranslation)
    {
        AddressSpace = addressSpace;
        TranslationPolicy = translationPolicy;
        TranslationControl = translationControl;
        DirtyTracking = dirtyTracking;
        OwnsSecondStageTranslation = ownsSecondStageTranslation;
    }

    public AddressSpaceDescriptor? AddressSpace { get; }

    public MemoryTranslationPolicy? TranslationPolicy { get; }

    public MemoryDomainTranslationControl TranslationControl { get; }

    public DirtyTrackingServiceDescriptor? DirtyTracking { get; }

    public bool OwnsSecondStageTranslation { get; }

    public bool IsAuthoritativeMemoryStateOwner => true;

    public bool HasAddressSpace => AddressSpace is not null;

    public bool HasTranslationPolicy => TranslationPolicy is not null;

    public bool HasDirtyTracking => DirtyTracking is not null;

    public bool HasValidTranslationControl => TranslationControl.IsValid;

    public bool TryCreateReadOnlyTranslationView(
        out MemoryDomainReadOnlyTranslationView view,
        out string reason)
    {
        if (!HasValidTranslationControl)
        {
            view = default;
            reason = "Memory domain translation control is not valid.";
            return false;
        }

        view = MemoryDomainReadOnlyTranslationView.From(
            TranslationControl,
            OwnsSecondStageTranslation);
        reason = "Memory domain descriptor exposed a read-only translation view.";
        return true;
    }

    public MemoryDomainDescriptor WithTranslationControl(
        MemoryDomainTranslationControl translationControl) =>
        new(AddressSpace, TranslationPolicy, translationControl, DirtyTracking, OwnsSecondStageTranslation);
}
