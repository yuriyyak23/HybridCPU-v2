namespace YAKSys_Hybrid_CPU;

public partial struct Processor
{
    public sealed partial class CPU_Core
    {
        private Core.CompletionReasonQualificationVmReadScalarDeliveryCanonicalComposition
            _completionReasonQualificationVmRead = null!;
        private Core.CompletionSecondStageVmReadScalarDeliveryCanonicalComposition
            _completionSecondStageVmRead = null!;
        private Core.PinBasedControlsVmReadScalarDeliveryCanonicalComposition?
            _pinBasedControlsVmRead;

        public bool HasActiveCompletionReasonQualificationVmReadProfile =>
            _completionReasonQualificationVmRead.IsEnabled;

        public bool DisableCompletionReasonQualificationVmReadProfile() =>
            _completionReasonQualificationVmRead.Disable();

        public bool HasActivePinBasedControlsVmReadProfile =>
            _pinBasedControlsVmRead?.IsEnabled == true;

        public bool DisablePinBasedControlsVmReadProfile() =>
            _pinBasedControlsVmRead?.Disable() ?? true;
    }
}
