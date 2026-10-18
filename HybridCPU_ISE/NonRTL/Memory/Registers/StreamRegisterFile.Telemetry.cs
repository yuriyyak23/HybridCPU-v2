using System.Runtime.CompilerServices;
using YAKSys_Hybrid_CPU.Core;

namespace YAKSys_Hybrid_CPU.Memory
{
    public readonly struct StreamIngressWarmTelemetry
    {
        public StreamIngressWarmTelemetry(
            ulong foregroundWarmAttempts,
            ulong foregroundWarmSuccesses,
            ulong foregroundWarmReuseHits,
            ulong foregroundBypassHits,
            ulong assistWarmAttempts,
            ulong assistWarmSuccesses,
            ulong assistWarmReuseHits,
            ulong assistBypassHits,
            ulong translationRejects,
            ulong backendRejects,
            ulong assistResidentBudgetRejects,
            ulong assistLoadingBudgetRejects,
            ulong assistNoVictimRejects)
        {
            ForegroundWarmAttempts = foregroundWarmAttempts;
            ForegroundWarmSuccesses = foregroundWarmSuccesses;
            ForegroundWarmReuseHits = foregroundWarmReuseHits;
            ForegroundBypassHits = foregroundBypassHits;
            AssistWarmAttempts = assistWarmAttempts;
            AssistWarmSuccesses = assistWarmSuccesses;
            AssistWarmReuseHits = assistWarmReuseHits;
            AssistBypassHits = assistBypassHits;
            TranslationRejects = translationRejects;
            BackendRejects = backendRejects;
            AssistResidentBudgetRejects = assistResidentBudgetRejects;
            AssistLoadingBudgetRejects = assistLoadingBudgetRejects;
            AssistNoVictimRejects = assistNoVictimRejects;
        }

        public ulong ForegroundWarmAttempts { get; }

        public ulong ForegroundWarmSuccesses { get; }

        public ulong ForegroundWarmReuseHits { get; }

        public ulong ForegroundBypassHits { get; }

        public ulong AssistWarmAttempts { get; }

        public ulong AssistWarmSuccesses { get; }

        public ulong AssistWarmReuseHits { get; }

        public ulong AssistBypassHits { get; }

        public ulong TranslationRejects { get; }

        public ulong BackendRejects { get; }

        public ulong AssistResidentBudgetRejects { get; }

        public ulong AssistLoadingBudgetRejects { get; }

        public ulong AssistNoVictimRejects { get; }
    }

    public partial class StreamRegisterFile
    {
        private ulong _foregroundWarmAttempts;
        private ulong _foregroundWarmSuccesses;
        private ulong _foregroundWarmReuseHits;
        private ulong _foregroundBypassHits;
        private ulong _assistWarmAttempts;
        private ulong _assistWarmSuccesses;
        private ulong _assistWarmReuseHits;
        private ulong _assistBypassHits;
        private ulong _translationRejects;
        private ulong _backendRejects;
        private ulong _assistResidentBudgetRejects;
        private ulong _assistLoadingBudgetRejects;
        private ulong _assistNoVictimRejects;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RecordWarmAttempt(bool assistOwned)
        {
            if (assistOwned)
            {
                _assistWarmAttempts++;
            }
            else
            {
                _foregroundWarmAttempts++;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RecordWarmSuccess(bool assistOwned)
        {
            if (assistOwned)
            {
                _assistWarmSuccesses++;
            }
            else
            {
                _foregroundWarmSuccesses++;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RecordWarmReuse(bool assistOwned)
        {
            RecordWarmSuccess(assistOwned);
            if (assistOwned)
            {
                _assistWarmReuseHits++;
            }
            else
            {
                _foregroundWarmReuseHits++;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RecordBypassHit(bool assistOwned)
        {
            _l1BypassHits++;
            if (assistOwned)
            {
                _assistBypassHits++;
            }
            else
            {
                _foregroundBypassHits++;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RecordWarmTranslationReject()
        {
            _translationRejects++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RecordWarmBackendReject()
        {
            _backendRejects++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RecordAssistScheduleReject(AssistStreamRegisterRejectKind rejectKind)
        {
            switch (rejectKind)
            {
                case AssistStreamRegisterRejectKind.ResidentBudget:
                    _assistResidentBudgetRejects++;
                    break;
                case AssistStreamRegisterRejectKind.LoadingBudget:
                    _assistLoadingBudgetRejects++;
                    break;
                case AssistStreamRegisterRejectKind.NoAssistVictim:
                    _assistNoVictimRejects++;
                    break;
            }
        }

        public StreamIngressWarmTelemetry GetIngressWarmTelemetry()
        {
            return new StreamIngressWarmTelemetry(
                _foregroundWarmAttempts,
                _foregroundWarmSuccesses,
                _foregroundWarmReuseHits,
                _foregroundBypassHits,
                _assistWarmAttempts,
                _assistWarmSuccesses,
                _assistWarmReuseHits,
                _assistBypassHits,
                _translationRejects,
                _backendRejects,
                _assistResidentBudgetRejects,
                _assistLoadingBudgetRejects,
                _assistNoVictimRejects);
        }
    }
}
