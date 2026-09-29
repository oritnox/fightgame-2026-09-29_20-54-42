// /Assets/_Game/Tests/EditMode/Core/InputArbitratorTests.cs
// 공용코드 수정: P01-A 직접 모듈 시험. Unity 및 standalone에서 동일 소스 실행.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using RP.Core.Foundation;
using RP.Core.Timing;
using RP.Core.Input;

namespace RP.Tests.Core
{
    public sealed class InputArbitratorTests
    {

        private static readonly ClockEpoch Epoch = new ClockEpoch(1);
        private static InputCommand C(long seq, double time, InputActionId action, InputPhase phase = InputPhase.Pressed)
            => new InputCommand(seq, 0, Epoch, action, time, time, time, Float3.Zero, phase);
        [Test] public void SameTimeDefensePicksOnlyDodge()
        {
            var result = new InputArbitrator().Select(new[] { C(1, 1, InputActionId.Parry), C(2, 1, InputActionId.Dodge) }, Epoch);
            Assert.That(result.Selected.Action, Is.EqualTo(InputActionId.Dodge)); Assert.That(result.DiscardedCount, Is.EqualTo(1));
        }
        [Test] public void SameTimeAttacksPreferHeavy()
        {
            var result = new InputArbitrator().Select(new[] { C(1, 1, InputActionId.LightAttack), C(2, 1, InputActionId.HeavyAttack) }, Epoch);
            Assert.That(result.Selected.Action, Is.EqualTo(InputActionId.HeavyAttack));
        }
        [Test] public void LaterGroupIsDeferredNotDiscarded()
        {
            var result = new InputArbitrator().Select(new[] { C(1, 1, InputActionId.LightAttack), C(2, 1.002, InputActionId.Dodge) }, Epoch);
            Assert.That(result.Selected.Action, Is.EqualTo(InputActionId.LightAttack));
            Assert.That(result.DeferredCount, Is.EqualTo(1)); Assert.That(result.DiscardedCount, Is.EqualTo(0));
        }
        [Test] public void TieWindowDoesNotChainTransitively()
        {
            var result = new InputArbitrator().Select(new[] { C(1, 1, InputActionId.LightAttack), C(2, 1.0008, InputActionId.Parry), C(3, 1.0016, InputActionId.Dodge) }, Epoch);
            Assert.That(result.Selected.Action, Is.EqualTo(InputActionId.Parry)); Assert.That(result.DeferredCount, Is.EqualTo(1));
        }
        [Test] public void InputPermutationDoesNotChangeTheDecision()
        {
            var a = C(1, 1, InputActionId.LightAttack); var b = C(2, 1.0005, InputActionId.HeavyAttack); var engine = new InputArbitrator();
            Assert.That(engine.Select(new[] { a, b }, Epoch).Selected.Sequence, Is.EqualTo(engine.Select(new[] { b, a }, Epoch).Selected.Sequence));
        }
        [Test] public void MaskReleaseAndOldEpochCannotGainAnAction()
        {
            var engine = new InputArbitrator();
            Assert.That(engine.Select(new[] { C(1, 1, InputActionId.Dodge, InputPhase.Released) }, Epoch).HasSelection, Is.False);
            Assert.That(engine.Select(new[] { C(1, 1, InputActionId.Dodge) }, new ClockEpoch(2)).HasSelection, Is.False);
            Assert.That(engine.Select(new[] { C(1, 1, InputActionId.Dodge) }, Epoch, CombatInputMask.Parry).HasSelection, Is.False);
        }
        [Test] public void NoneAndDefaultDoNotFabricateASelection()
        { Assert.That(new InputArbitrator().Select(new[] { default(InputCommand) }, Epoch).HasSelection, Is.False); }
        [Test] public void InvalidMaskIsRejected()
        { Assert.Throws<ArgumentOutOfRangeException>(() => new InputArbitrator().Select(Array.Empty<InputCommand>(), Epoch, (CombatInputMask)16)); }

    }
}
