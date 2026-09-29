// /Assets/_Game/Tests/EditMode/Core/CalibrationEstimatorTests.cs
// 공용코드 수정: P01-A 직접 모듈 시험. Unity 및 standalone에서 동일 소스 실행.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using RP.Core.Foundation;
using RP.Core.Timing;
using RP.Core.Input;

namespace RP.Tests.Core
{
    public sealed class CalibrationEstimatorTests
    {

        [Test] public void WarmupAndOutliersAreExcluded()
        {
            var taps = new List<CalibrationTap>();
            for (int i = 0; i < 40; i++) taps.Add(new CalibrationTap(i + (i < 8 || i == 15 ? .22 : .07), i));
            var result = new CalibrationEstimator().Estimate(taps);
            Assert.That(result.Offset, Is.EqualTo(.07).Within(1e-10)); Assert.That(result.WarmupExcluded, Is.EqualTo(8));
            Assert.That(result.AcceptedCount, Is.EqualTo(31)); Assert.That(result.RejectedCount, Is.EqualTo(1));
            Assert.That(result.CanApply, Is.True);
        }
        [Test] public void NegativeOffsetIsSupported()
        {
            var taps = new List<CalibrationTap>(); for (int i = 0; i < 40; i++) taps.Add(new CalibrationTap(i - .05, i));
            Assert.That(new CalibrationEstimator().Estimate(taps).Offset, Is.EqualTo(-.05).Within(1e-10));
        }
        [Test] public void EmptyAndDefaultEstimatesAreNotApplicable()
        {
            Assert.That(default(CalibrationEstimate).CanApply, Is.False);
            Assert.That(new CalibrationEstimator().Estimate(Array.Empty<CalibrationTap>()).CanApply, Is.False);
        }
        [Test] public void HighVarianceIsReportedInsteadOfAutomaticApplication()
        {
            var taps = new List<CalibrationTap>(); for (int i = 0; i < 40; i++) taps.Add(new CalibrationTap(i + (i % 2 == 0 ? .02 : .12), i));
            var result = new CalibrationEstimator().Estimate(taps);
            Assert.That(result.CanApply, Is.False); Assert.That((result.Warning & CalibrationWarning.HighVariance) != 0, Is.True);
        }
        [Test] public void OutOfRangeErrorsAreRejected()
        {
            var taps = new List<CalibrationTap>(); for (int i = 0; i < 40; i++) taps.Add(new CalibrationTap(i + 2, i));
            var result = new CalibrationEstimator().Estimate(taps);
            Assert.That(result.RejectedCount, Is.EqualTo(32)); Assert.That(result.CanApply, Is.False);
        }
        [Test] public void PresentationOffsetIsNotDoubleSubtracted()
        {
            Assert.That(new TimingProfile(.07, .2).JudgeSongTime(100.5, 100), Is.EqualTo(.43).Within(1e-12));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TimingProfile(.51));
        }

    }
}
