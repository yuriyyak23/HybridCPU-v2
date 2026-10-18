namespace YAKSys_Hybrid_CPU.Core;

public enum SecureCompletionFenceState : byte
{
    Missing = 0,
    Pending = 1,
    CompletionAllowed = 2,
    RetireAllowed = 3,
}

public enum SecureRetirePublicationRule : byte
{
    Denied = 0,
    CompletionFenceRequired = 1,
    ExplicitRetireFenceRequired = 2,
}

public sealed partial class SecureCompletionPublicationFence
{
    public SecureCompletionPublicationFence()
        : this(SecureCompletionFenceState.Missing, SecureRetirePublicationRule.Denied)
    {
    }

    public SecureCompletionPublicationFence(
        SecureCompletionFenceState state,
        SecureRetirePublicationRule retireRule)
    {
        State = state;
        RetireRule = retireRule;
    }

    public static SecureCompletionPublicationFence Denied { get; } = new();

    public SecureCompletionFenceState State { get; }

    public SecureRetirePublicationRule RetireRule { get; }

    public bool CanPublishCompletion =>
        State is SecureCompletionFenceState.CompletionAllowed
            or SecureCompletionFenceState.RetireAllowed;

    public bool CanPublishRetire =>
        State == SecureCompletionFenceState.RetireAllowed &&
        RetireRule == SecureRetirePublicationRule.ExplicitRetireFenceRequired;
}
