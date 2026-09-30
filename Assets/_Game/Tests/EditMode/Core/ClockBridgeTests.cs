// /Assets/_Game/Tests/EditMode/Core/ClockBridgeTests.cs
// 공용코드 수정: P01-A ClockBridge 고빈도·소용량·최신성·품질 회귀 시험. Unity 및 standalone 동일 소스 실행.
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
        [TestCase(30)]
        [TestCase(60)]
        [TestCase(120)]
        [TestCase(127)]
        [TestCase(128)]
        [TestCase(144)]
        [TestCase(240)]
        [TestCase(1000)]
        public void CommonAndHighSampleRatesBecomeAndStayReady(int rate)
        {
            var bridge = new ClockBridge();
            var epoch = bridge.BeginEpoch(10, 100);
            for (int i = 1; i <= rate * 8; i++)
            {
                double elapsed = (double)i / rate;
                double realtime = 10 + elapsed, dsp = 100 + elapsed * 1.0001;
                Assert.That(bridge.Observe(realtime, dsp), Is.EqualTo(ClockObservation.Accepted));
                var quality = bridge.GetQuality();
                Assert.That(quality.Samples, Is.LessThanOrEqualTo(128));
                if (elapsed < 1) Assert.That(quality.IsReady, Is.False);
                if (elapsed >= 1.5)
                {
                    Assert.That(quality.IsReady, Is.True, "Rate: " + rate + ", sample: " + i);
                    Assert.That(quality.Span, Is.GreaterThanOrEqualTo(1));
                    Assert.That(bridge.MapRealtime(realtime, epoch), Is.EqualTo(dsp).Within(1e-9));
                }
            }
            Assert.That(bridge.GetQuality().Samples, Is.EqualTo(128));
            Assert.That(bridge.Epoch, Is.EqualTo(epoch));
        }
        [TestCase(8, 30)]
        [TestCase(8, 144)]
        [TestCase(8, 1000)]
        [TestCase(16, 240)]
        public void SmallCapacityPreservesFitSpanAndFreshMappingHorizon(int capacity, int rate)
        {
            var bridge = new ClockBridge(capacity);
            var epoch = bridge.BeginEpoch(10, 100);
            for (int i = 1; i <= rate * 4; i++)
            {
                double realtime = 10 + (double)i / rate;
                bridge.Observe(realtime, realtime + 90);
                Assert.That(bridge.GetQuality().Samples, Is.LessThanOrEqualTo(capacity));
                if (i >= rate * 1.5)
                {
                    Assert.That(bridge.GetQuality().IsReady, Is.True);
                    Assert.That(bridge.GetQuality().Span, Is.GreaterThanOrEqualTo(1));
                    // Even samples replacing the live endpoint must advance the mapping horizon.
                    Assert.That(bridge.MapRealtime(realtime + .5, epoch), Is.EqualTo(realtime + 90.5).Within(1e-9));
                    Assert.That(bridge.TryMapRealtime(realtime + .5001, epoch, out _, false), Is.False);
                }
            }
            Assert.That(bridge.GetQuality().Samples, Is.EqualTo(capacity));
            Assert.That(bridge.GetQuality().Span, Is.LessThan(1.5));
            Assert.That(bridge.TryMapRealtime(10, epoch, out _, false), Is.False);
            Assert.That(bridge.TryMapRealtime(12, epoch, out _, false), Is.False);
        }
        [Test] public void ReadyHistorySurvivesAnIncreaseInSampleRate()
        {
            var bridge = Ready(); var epoch = bridge.Epoch;
            for (int i = 1; i <= 4000; i++)
            {
                double realtime = 12 + i * .001;
                Assert.That(bridge.Observe(realtime, realtime + 90), Is.EqualTo(ClockObservation.Accepted));
                Assert.That(bridge.GetQuality().IsReady, Is.True);
                Assert.That(bridge.GetQuality().Samples, Is.LessThanOrEqualTo(128));
            }
            Assert.That(bridge.Epoch, Is.EqualTo(epoch));
            Assert.That(bridge.GetQuality().Span, Is.LessThan(1.1));
        }
        [Test] public void HighRateReadinessIsRecomputedWhenTheFitDegradesAndRecovers()
        {
            const int rate = 240;
            var bridge = new ClockBridge(); var epoch = bridge.BeginEpoch(10, 100);
            for (int i = 1; i <= rate * 2; i++) bridge.Observe(10 + (double)i / rate, 100 + (double)i / rate);
            Assert.That(bridge.GetQuality().IsReady, Is.True);
            for (int i = 1; i <= rate * 4; i++)
            {
                double elapsed = (double)i / rate;
                Assert.That(bridge.Observe(12 + elapsed, 102 + elapsed * 1.004), Is.EqualTo(ClockObservation.Accepted));
            }
            var degraded = bridge.GetQuality();
            Assert.That(degraded.Span, Is.GreaterThanOrEqualTo(1));
            Assert.That(degraded.Slope, Is.EqualTo(1.004).Within(1e-9));
            Assert.That(degraded.IsReady, Is.False);
            Assert.That(bridge.TryMapRealtime(16, epoch, out _), Is.False);
            Assert.That(bridge.TryMapRealtime(16, epoch, out _, false), Is.True);
            for (int i = 1; i <= rate * 4; i++)
            {
                double elapsed = (double)i / rate;
                Assert.That(bridge.Observe(16 + elapsed, 106.016 + elapsed), Is.EqualTo(ClockObservation.Accepted));
            }
            Assert.That(bridge.GetQuality().IsReady, Is.True);
            Assert.That(bridge.Epoch, Is.EqualTo(epoch));
            Assert.That(bridge.MapRealtime(20, epoch), Is.EqualTo(110.016).Within(1e-9));
        }
        [TestCase(8)]
        [TestCase(128)]
        public void HighRateSafetyChecksUseLatestAcceptedSample(int capacity)
        {
            var bridge = new ClockBridge(capacity); var epoch = bridge.BeginEpoch(10, 100);
            for (int i = 1; i <= 2000; i++) bridge.Observe(10 + i * .001, 100 + i * .001);
            Assert.That(bridge.GetQuality().IsReady, Is.True);
            int samples = bridge.GetQuality().Samples;
            Assert.That(bridge.Observe(12, 102), Is.EqualTo(ClockObservation.Duplicate));
            Assert.That(bridge.GetQuality().Samples, Is.EqualTo(samples));
            Assert.That(bridge.Observe(12.001, 102.101), Is.EqualTo(ClockObservation.RejectedOutlier));
            Assert.That(bridge.GetQuality().RejectedSamples, Is.EqualTo(1));
            Assert.That(bridge.TryMapRealtime(12.5001, epoch, out _, false), Is.False);
            Assert.That(bridge.Observe(12.002, 102.002), Is.EqualTo(ClockObservation.Accepted));
            Assert.That(bridge.MapRealtime(12.002, epoch), Is.EqualTo(102.002).Within(1e-9));
            Assert.That(bridge.Observe(12.003, 102.002), Is.EqualTo(ClockObservation.Suspended));
            Assert.That(bridge.GetQuality().IsReady, Is.False);
            Assert.That(bridge.Observe(12.004, 102.004), Is.EqualTo(ClockObservation.IgnoredFrozen));
            Assert.That(bridge.TryMapRealtime(12.002, epoch, out _, false), Is.False);
            bridge.BeginEpoch(20, 200);
            Assert.That(bridge.GetQuality().Samples, Is.EqualTo(1));
            Assert.That(bridge.GetQuality().Span, Is.Zero);
            Assert.That(bridge.GetQuality().IsReady, Is.False);
            Assert.That(bridge.TryMapRealtime(20, epoch, out _, false), Is.False);
            Assert.That(bridge.MapRealtime(20, bridge.Epoch, false), Is.EqualTo(200));
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
