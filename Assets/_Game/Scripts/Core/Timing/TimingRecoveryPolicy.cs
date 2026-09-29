// /Assets/_Game/Scripts/Core/Timing/TimingRecoveryPolicy.cs
// 공용코드 수정: F012 정지·장치 변경·stall 복구. 이미 확정된 HP/보상은 보존.
using System;
using RP.Core.Foundation;

namespace RP.Core.Timing
{
    public readonly struct TimingRecoveryPlan
    {
        public bool IsValid { get; }
        public bool ResetClockEpoch { get; }
        public bool CancelUnresolvedThreats { get; }
        public bool RejoinNeutralPhrase { get; }
        public bool PreserveCommittedState => true;
        public bool GrantCancellationRewards => false;
        public bool DiscardHeldPresses => true;
        internal TimingRecoveryPlan(bool resetClockEpoch, bool cancel, bool rejoin)
        { IsValid = true; ResetClockEpoch = resetClockEpoch; CancelUnresolvedThreats = cancel; RejoinNeutralPhrase = rejoin; }
    }
    public sealed class TimingRecoveryPolicy
    {
        public double MinimumResumePreview { get; }
        public TimingRecoveryPolicy(double minimumResumePreview = .55)
        { MinimumResumePreview = NumericGuard.Positive(minimumResumePreview, nameof(minimumResumePreview)); }
        public TimingRecoveryPlan Evaluate(RecoveryCause cause, bool hasThreat, double remainingPreviewSeconds)
        {
            if (!Enum.IsDefined(typeof(RecoveryCause), cause)) throw new ArgumentOutOfRangeException(nameof(cause));
            NumericGuard.NonNegative(remainingPreviewSeconds, nameof(remainingPreviewSeconds));
            if (cause != RecoveryCause.Resume) return new TimingRecoveryPlan(true, true, true);
            bool cancel = hasThreat && remainingPreviewSeconds < MinimumResumePreview;
            return new TimingRecoveryPlan(true, cancel, cancel);
        }
    }
}
