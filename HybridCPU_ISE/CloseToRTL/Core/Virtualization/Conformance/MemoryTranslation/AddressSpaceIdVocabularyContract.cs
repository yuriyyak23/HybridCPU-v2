namespace YAKSys_Hybrid_CPU.Core;

public enum AddressSpaceIdVocabularyViolation : byte
{
    None = 0,
    MissingSecondStageEpoch = 1,
    MissingAddressSpaceTagEpoch = 2,
}

public readonly record struct AddressSpaceIdVocabularyRequest(
    bool UsesSecondStageEpochName,
    bool UsesAddressSpaceTagEpochName);

public sealed partial class AddressSpaceIdVocabularyContract
{
    public AddressSpaceIdVocabularyViolation Evaluate(
        AddressSpaceIdVocabularyRequest request)
    {
        if (!request.UsesSecondStageEpochName)
        {
            return AddressSpaceIdVocabularyViolation.MissingSecondStageEpoch;
        }

        return request.UsesAddressSpaceTagEpochName
            ? AddressSpaceIdVocabularyViolation.None
            : AddressSpaceIdVocabularyViolation.MissingAddressSpaceTagEpoch;
    }

    public bool IsSatisfied(AddressSpaceIdVocabularyRequest request) =>
        Evaluate(request) == AddressSpaceIdVocabularyViolation.None;
}
