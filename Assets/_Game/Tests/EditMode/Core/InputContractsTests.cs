// /Assets/_Game/Tests/EditMode/Core/InputContractsTests.cs
// 공용코드 수정: P01-A 직접 모듈 시험. Unity 및 standalone에서 동일 소스 실행.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using RP.Core.Foundation;
using RP.Core.Timing;
using RP.Core.Input;

namespace RP.Tests.Core
{
    public sealed class InputContractsTests
    {

        [Test] public void LateDeliveryPreservesOriginalJudgmentAndDirection()
        {
            var c = new InputCommand(1, 2, new ClockEpoch(1), InputActionId.Parry, 10, 1, .93, new Float3(1, 0, 0));
            var result = c.AtDelivery(1.1);
            Assert.That(result.ApplyTime, Is.EqualTo(1.1)); Assert.That(result.LateDelivery, Is.True);
            Assert.That(result.Original.JudgedSongTime, Is.EqualTo(.93)); Assert.That(result.Original.Realtime, Is.EqualTo(10));
            Assert.That(result.Original.Direction, Is.EqualTo(new Float3(1, 0, 0)));
        }
        [Test] public void AssistOriginAndReleaseAreNotHidden()
        {
            var c = new InputCommand(1, 0, new ClockEpoch(1), InputActionId.Dodge, 1, 1, -.1, Float3.Zero, InputPhase.Released, InputOrigin.Assist);
            Assert.That(c.IsAssist, Is.True); Assert.That(c.Phase, Is.EqualTo(InputPhase.Released));
        }
        [Test] public void DefaultCommandCannotBeApplied()
        { Assert.Throws<InvalidOperationException>(() => default(InputCommand).AtDelivery(1)); }
        [Test] public void InvalidCommandIdentityAndTimeAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new InputCommand(0, 0, new ClockEpoch(1), InputActionId.Parry, 0, 0, 0, Float3.Zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => new InputCommand(1, 0, default, InputActionId.Parry, 0, 0, 0, Float3.Zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => new InputCommand(1, 0, new ClockEpoch(1), InputActionId.Parry, 0, double.NaN, 0, Float3.Zero));
        }

    }
}
