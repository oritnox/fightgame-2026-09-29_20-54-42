// /Assets/_Game/Tests/EditMode/Core/ClockBridgeTests.cs
// 공용코드 수정: P01-A 직접 모듈 시험. Unity 및 standalone에서 동일 소스 실행.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using RP.Core.Foundation;
using RP.Core.Timing;
using RP.Core.Input;

namespace RP.Tests.Core
{
    public sealed class ClockBridgeTests
    {

        private static ClockBridge Ready()
        {
            var bridge = new ClockBridge();
            for (int i = 0; i <= 20; i++) bridge.Observe(10 + i * .1, 100 + i * .1);
            return bridge;
        }
        [Test] public void OriginalTimeContractMapsAndAppliesOffsetOnce()
        {
            var bridge = new ClockBridge(); var epoch = bridge.BeginEpoch(10, 100);
            double mapped = bridge.MapRealtime(10.5, epoch, false);
            Assert.That(new TimingProfile(.070, .123).JudgeSongTime(mapped, 100), Is.EqualTo(.430).Within(1e-10));
            Assert.That(bridge.GetQuality().IsReady, Is.False);
        }
        [Test] public void ColdAndFarFutureMappingsAreRejected()
        {
            var bridge = new ClockBridge(); Assert.That(bridge.TryMapRealtime(0, default, out _), Is.False);
            bridge.BeginEpoch(10, 100);
            Assert.That(bridge.TryMapRealtime(10, bridge.Epoch, out _), Is.False);
            Assert.That(bridge.TryMapRealtime(11, bridge.Epoch, out _, false), Is.False);
        }
        [Test] public void FreezeExcludesPauseSamplesAndOldEpoch()
        {
            var bridge = Ready(); var old = bridge.Epoch; int count = bridge.GetQuality().Samples;
            bridge.Freeze(); Assert.That(bridge.Observe(42, 102), Is.EqualTo(ClockObservation.IgnoredFrozen));
            Assert.That(bridge.GetQuality().Samples, Is.EqualTo(count));
            Assert.That(bridge.TryMapRealtime(12, old, out _), Is.False);
            bridge.BeginEpoch(42, 102);
            Assert.That(bridge.TryMapRealtime(42, old, out _, false), Is.False);
            Assert.That(bridge.MapRealtime(42, bridge.Epoch, false), Is.EqualTo(102));
        }
        [Test] public void StoppedDspIsNotFittedAsSlowMotion()
        {
            var bridge = Ready();
            Assert.That(bridge.Observe(12.1, 102), Is.EqualTo(ClockObservation.Suspended));
            Assert.That(bridge.GetQuality().IsFrozen, Is.True);
        }
        [Test] public void DuplicateDoesNotPolluteTheFit()
        {
            var bridge = Ready(); int count = bridge.GetQuality().Samples;
            Assert.That(bridge.Observe(12, 102), Is.EqualTo(ClockObservation.Duplicate));
            Assert.That(bridge.GetQuality().Samples, Is.EqualTo(count));
        }
        [Test] public void SingleOutlierDoesNotMoveAcceptedAnchors()
        {
            var bridge = Ready(); var epoch = bridge.Epoch;
            Assert.That(bridge.Observe(12.1, 102.2), Is.EqualTo(ClockObservation.RejectedOutlier));
            Assert.That(bridge.Observe(12.2, 102.2), Is.EqualTo(ClockObservation.Accepted));
            Assert.That(bridge.Epoch, Is.EqualTo(epoch));
            Assert.That(bridge.MapRealtime(12.2, epoch), Is.EqualTo(102.2).Within(1e-9));
        }
        [Test] public void ThreePersistentOutliersCreateANewEpoch()
        {
            var bridge = Ready(); var old = bridge.Epoch;
            bridge.Observe(12.1, 102.2); bridge.Observe(12.2, 102.3);
            Assert.That(bridge.Observe(12.3, 102.4), Is.EqualTo(ClockObservation.EpochReset));
            Assert.That(bridge.Epoch, Is.Not.EqualTo(old)); Assert.That(bridge.GetQuality().IsReady, Is.False);
        }
        [Test] public void BackwardClockCreatesNewEpochWithoutChangingPastValue()
        {
            var bridge = Ready(); double historical = bridge.MapRealtime(12, bridge.Epoch); var old = bridge.Epoch;
            Assert.That(bridge.Observe(1, 5), Is.EqualTo(ClockObservation.EpochReset));
            Assert.That(bridge.Epoch, Is.Not.EqualTo(old)); Assert.That(historical, Is.EqualTo(102).Within(1e-9));
        }
        [Test] public void ThirtyMinutesOfDriftAndJitterRemainBounded()
        {
            var bridge = new ClockBridge();
            for (int i = 0; i <= 18000; i++)
            {
                double r = 1000 + i * .1, ideal = 5000 + i * .1 * 1.0001;
                bridge.Observe(r, ideal + .002 * Math.Sin(i * .7));
                if (i > 200 && i % 50 == 0)
                    Assert.That(bridge.MapRealtime(r, bridge.Epoch), Is.EqualTo(ideal).Within(.002));
            }
            Assert.That(bridge.GetQuality().Samples, Is.EqualTo(128));
            Assert.That(bridge.GetQuality().IsReady, Is.True);
        }
        [Test] public void InvalidSampleDoesNotChangeEpochOrModel()
        {
            var bridge = Ready(); var before = bridge.GetQuality();
            Assert.Throws<ArgumentOutOfRangeException>(() => bridge.Observe(double.NaN, 102));
            Assert.Throws<ArgumentOutOfRangeException>(() => bridge.BeginEpoch(1e13, 10));
            Assert.That(bridge.Epoch, Is.EqualTo(before.Epoch)); Assert.That(bridge.GetQuality().Samples, Is.EqualTo(before.Samples));
        }

    }
}
