// /Assets/_Game/Tests/EditMode/Core/CommonTypesTests.cs
// 공용코드 수정: P01-A 직접 모듈 시험. Unity 및 standalone에서 동일 소스 실행.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using RP.Core.Foundation;
using RP.Core.Timing;
using RP.Core.Input;

namespace RP.Tests.Core
{
    public sealed class CommonTypesTests
    {

        [Test] public void ContentIdsHaveCanonicalCaseAndStableEquality()
        {
            var a = new ContentId("Hero_Android"); var b = new ContentId("hero_android");
            Assert.That(a, Is.EqualTo(b)); Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
            Assert.That(a.Value, Is.EqualTo("hero_android"));
            var table = new Dictionary<ContentId, int>(); table.Add(a, 1);
            Assert.Throws<ArgumentException>(() => table.Add(b, 2));
        }
        [TestCase("ab")][TestCase("a/b")][TestCase("캐릭터")][TestCase("")][TestCase("abc\n")]
        public void InvalidContentIdIsRejected(string value)
        { Assert.Throws<ArgumentException>(() => new ContentId(value)); }
        [Test] public void DefaultIdsAreExplicitlyEmpty()
        {
            Assert.That(default(ContentId).IsValid, Is.False); Assert.That(EntityId.None.IsValid, Is.False);
            Assert.That(RuntimeId.None.IsValid, Is.False); Assert.That(default(ClockEpoch).IsValid, Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => new EntityId(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RuntimeId(0));
        }
        [Test] public void Float3NormalizesAndRejectsNonfiniteValues()
        {
            Assert.That(new Float3(3, 0, 4).Normalized().LengthSquared, Is.EqualTo(1).Within(1e-6));
            Assert.That(Float3.Zero.Normalized(), Is.EqualTo(Float3.Zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Float3(float.NaN, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => { _ = new Float3(float.MaxValue, 0, 0) * 2; });
        }
        [Test] public void TimeRangeIsHalfOpenAndEmptyRangeDoesNotOverlap()
        {
            var range = new TimeRange(.02, .14);
            Assert.That(range.Contains(.02), Is.True); Assert.That(range.Contains(.14), Is.False);
            Assert.That(range.Overlaps(new TimeRange(.14, .2)), Is.False);
            Assert.That(range.Overlaps(new TimeRange(.03, .03)), Is.False);
            Assert.Throws<ArgumentException>(() => new TimeRange(1, 0));
        }
        [Test] public void ValidationReportCopiesInputAndPreservesErrors()
        {
            var issues = new List<ValidationIssue> { new ValidationIssue("range", "tempo", ">0", "0") };
            var report = new ValidationReport(issues); issues.Clear();
            Assert.That(report.Issues.Count, Is.EqualTo(1)); Assert.That(report.IsValid, Is.False);
            Assert.That(new ValidationReport(Array.Empty<ValidationIssue>()).IsValid, Is.True);
        }
        [TestCase(double.NaN)][TestCase(double.PositiveInfinity)][TestCase(double.NegativeInfinity)]
        public void NonfiniteTimeIsRejected(double value)
        { Assert.Throws<ArgumentOutOfRangeException>(() => NumericGuard.Finite(value, "time")); }

    }
}
