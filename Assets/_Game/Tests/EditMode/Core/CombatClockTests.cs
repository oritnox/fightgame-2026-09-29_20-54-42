// /Assets/_Game/Tests/EditMode/Core/CombatClockTests.cs
// 공용코드 수정: P01-A 직접 모듈 시험. Unity 및 standalone에서 동일 소스 실행.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using RP.Core.Foundation;
using RP.Core.Timing;
using RP.Core.Input;

namespace RP.Tests.Core
{
    public sealed class CombatClockTests
    {

        [Test] public void AdvancePlansBoundedStepsWithoutFrameCounting()
        {
            var clock = new CombatClock(); var plan = clock.AdvanceTarget(1.0 / 60);
            Assert.That(plan.SuggestedSteps, Is.EqualTo(2)); Assert.That(plan.From, Is.EqualTo(0));
            Assert.That(clock.Current, Is.EqualTo(1.0 / 60));
        }
        [Test] public void PauseDoesNotAccumulateMenuTime()
        {
            var clock = new CombatClock(); clock.AdvanceTarget(.05); clock.Freeze();
            Assert.That(clock.AdvanceTarget(30).Status, Is.EqualTo(ClockAdvanceStatus.Frozen));
            Assert.That(clock.Current, Is.EqualTo(.05)); clock.Resume();
            Assert.That(clock.AdvanceTarget(.06).Status, Is.EqualTo(ClockAdvanceStatus.Advanced));
        }
        [TestCase(.15)][TestCase(.5)]
        public void StallRequiresRecoveryWithoutAdvancing(double duration)
        {
            var clock = new CombatClock(); var result = clock.AdvanceTarget(duration);
            Assert.That(result.Status, Is.EqualTo(ClockAdvanceStatus.RecoveryRequired));
            Assert.That(clock.Current, Is.EqualTo(0)); Assert.That(clock.IsFrozen, Is.True);
        }
        [Test] public void SongOriginDoesNotResetCombatTime()
        {
            var clock = new CombatClock(); clock.AdvanceTarget(.1); clock.StartSong(); clock.AdvanceTarget(.15);
            Assert.That(clock.GetSongTime(), Is.EqualTo(.05).Within(1e-12));
            Assert.That(clock.SongToCombat(.2), Is.EqualTo(.3).Within(1e-12));
            clock.StopSong(); Assert.Throws<InvalidOperationException>(() => clock.GetSongTime());
            Assert.That(clock.Current, Is.EqualTo(.15));
        }
        [Test] public void InvalidTargetDoesNotMutateClock()
        {
            var clock = new CombatClock(); clock.AdvanceTarget(.05);
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.AdvanceTarget(.04));
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.AdvanceTarget(double.NaN));
            Assert.That(clock.Current, Is.EqualTo(.05));
        }

    }
}
