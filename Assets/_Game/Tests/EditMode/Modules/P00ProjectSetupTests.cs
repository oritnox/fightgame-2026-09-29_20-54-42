// /Assets/_Game/Tests/EditMode/Modules/P00ProjectSetupTests.cs
// 공용코드 수정: P00 레이어 준비의 무변경 실패·반복 실행 회귀.
using System;
using NUnit.Framework;
using RP.Editor;

namespace RP.Tests.EditMode
{
    public sealed class P00ProjectSetupTests
    {
        [Test] public void EmptySlotsArePlannedWithoutMutatingSource()
        {
            var original = new string[32]; original[0] = "Default"; original[20] = "UserLayer";
            var result = P00ProjectSetup.PlanLayers(original);
            Assert.That(original[8], Is.Null);
            Assert.That(result[8], Is.EqualTo("RP_World"));
            Assert.That(result[20], Is.EqualTo("UserLayer"));
        }
        [Test] public void ExistingRequiredLayersAreIdempotent()
        {
            var first = P00ProjectSetup.PlanLayers(new string[32]);
            Assert.That(P00ProjectSetup.PlanLayers(first), Is.EqualTo(first));
        }
        [Test] public void OccupiedLaterSlotCannotCausePartialEarlierChanges()
        {
            var original = new string[32]; original[13] = "KeepMe";
            Assert.Throws<InvalidOperationException>(() => P00ProjectSetup.PlanLayers(original));
            Assert.That(original[8], Is.Null); Assert.That(original[13], Is.EqualTo("KeepMe"));
        }
        [Test] public void SameRequiredNameAtAnotherIndexIsRejected()
        {
            var original = new string[32]; original[22] = "RP_Player";
            Assert.Throws<InvalidOperationException>(() => P00ProjectSetup.PlanLayers(original));
        }
        [Test] public void InvalidLayoutIsRejected()
        {
            Assert.Throws<ArgumentException>(() => P00ProjectSetup.PlanLayers(null));
            Assert.Throws<ArgumentException>(() => P00ProjectSetup.PlanLayers(new string[31]));
        }
    }
}
