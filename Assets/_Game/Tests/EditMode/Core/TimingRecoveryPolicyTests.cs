// /Assets/_Game/Tests/EditMode/Core/TimingRecoveryPolicyTests.cs
// 공용코드 수정: P01-A 직접 모듈 시험. Unity 및 standalone에서 동일 소스 실행.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using RP.Core.Foundation;
using RP.Core.Timing;
using RP.Core.Input;

namespace RP.Tests.Core
{
    public sealed class TimingRecoveryPolicyTests
    {

        [TestCase(.549, true)][TestCase(.55, false)][TestCase(1, false)]
        public void ResumePreviewBoundaryIsExplicit(double remaining, bool cancel)
        {
            var result = new TimingRecoveryPolicy().Evaluate(RecoveryCause.Resume, true, remaining);
            Assert.That(result.CancelUnresolvedThreats, Is.EqualTo(cancel)); Assert.That(result.RejoinNeutralPhrase, Is.EqualTo(cancel));
            Assert.That(result.PreserveCommittedState, Is.True); Assert.That(result.GrantCancellationRewards, Is.False);
        }
        [TestCase(RecoveryCause.DeviceChanged)][TestCase(RecoveryCause.Stall)][TestCase(RecoveryCause.FocusLost)][TestCase(RecoveryCause.ClockDiscontinuity)]
        public void DisruptionsRequireANewEpochAndNeutralRejoin(RecoveryCause cause)
        {
            var result = new TimingRecoveryPolicy().Evaluate(cause, false, 0);
            Assert.That(result.ResetClockEpoch && result.CancelUnresolvedThreats && result.RejoinNeutralPhrase, Is.True);
        }
        [Test] public void NoThreatResumeDoesNotInventACancellation()
        { Assert.That(new TimingRecoveryPolicy().Evaluate(RecoveryCause.Resume, false, 0).CancelUnresolvedThreats, Is.False); }
        [Test] public void InvalidCauseIsRejected()
        { Assert.Throws<ArgumentOutOfRangeException>(() => new TimingRecoveryPolicy().Evaluate((RecoveryCause)99, false, 0)); }

    }
}
