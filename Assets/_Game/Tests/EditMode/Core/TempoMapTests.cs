// /Assets/_Game/Tests/EditMode/Core/TempoMapTests.cs
// 공용코드 수정: P01-A 직접 모듈 시험. Unity 및 standalone에서 동일 소스 실행.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using RP.Core.Foundation;
using RP.Core.Timing;
using RP.Core.Input;

namespace RP.Tests.Core
{
    public sealed class TempoMapTests
    {

        [Test] public void TempoBoundaryUsesNewSegment()
        {
            var map = new TempoMap(new[] { new TempoSegment(0, 120), new TempoSegment(3840, 144) });
            Assert.That(map.TickToSeconds(3840), Is.EqualTo(2).Within(1e-12));
            Assert.That(map.TickToSeconds(4800), Is.EqualTo(2 + 5.0 / 12).Within(1e-12));
            Assert.That(map.GetBpmAt(3839), Is.EqualTo(120)); Assert.That(map.GetBpmAt(3840), Is.EqualTo(144));
            Assert.That(map.SecondsToTick(2), Is.EqualTo(3840));
        }
        [Test] public void ThirtyMinuteTickRoundTripsDoNotAccumulateDrift()
        {
            var map = new TempoMap(new[] { new TempoSegment(0, 120), new TempoSegment(3840, 144) });
            for (long tick = 0; tick < 4_200_000; tick += 37)
                Assert.That(map.SecondsToTick(map.TickToSeconds(tick)), Is.EqualTo(tick));
        }
        [Test] public void MixedTempoRoundTripsAtTheSupportedHorizon()
        {
            var map = new TempoMap(new[] { new TempoSegment(0, 1), new TempoSegment(TempoMap.MaximumTick - 960, 1000) });
            for (long tick = TempoMap.MaximumTick - 960; tick <= TempoMap.MaximumTick; tick++)
                Assert.That(map.SecondsToTick(map.TickToSeconds(tick)), Is.EqualTo(tick));
        }
        [Test] public void TempoDefinitionIsCopied()
        {
            var source = new[] { new TempoSegment(0, 120) }; var map = new TempoMap(source);
            source[0] = new TempoSegment(0, 240);
            Assert.That(map.TickToSeconds(960), Is.EqualTo(.5));
        }
        [Test] public void MeterChangesRequireABarBoundary()
        {
            var map = new TempoMap(new[] { new TempoSegment(0, 144), new TempoSegment(3840, 144, 3, 4) });
            Assert.That(map.GetMeterAt(3840).BarTicks, Is.EqualTo(2880));
            Assert.Throws<ArgumentException>(() => new TempoMap(new[] { new TempoSegment(0, 144), new TempoSegment(960, 144, 3, 4) }));
        }
        [Test] public void UnsortedDuplicateAndDefaultSegmentsAreRejected()
        {
            Assert.Throws<ArgumentException>(() => new TempoMap(new[] { new TempoSegment(960, 144) }));
            Assert.Throws<ArgumentException>(() => new TempoMap(new[] { new TempoSegment(0, 144), new TempoSegment(0, 120) }));
            Assert.Throws<ArgumentException>(() => new TempoMap(new[] { default(TempoSegment) }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TempoMap(Array.Empty<TempoSegment>()));
        }
        [TestCase(0)][TestCase(-1)][TestCase(1001)][TestCase(double.NaN)]
        public void InvalidTempoIsRejected(double bpm)
        { Assert.Throws<ArgumentOutOfRangeException>(() => new TempoSegment(0, bpm)); }
        [Test] public void InvalidQueriesCannotProduceWrappedTicks()
        {
            var map = new TempoMap(new[] { new TempoSegment(0, 120) });
            Assert.Throws<ArgumentOutOfRangeException>(() => map.TickToSeconds(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => map.SecondsToTick(double.MaxValue));
            Assert.Throws<ArgumentOutOfRangeException>(() => map.SecondsToTick(-.01));
        }

    }
}
