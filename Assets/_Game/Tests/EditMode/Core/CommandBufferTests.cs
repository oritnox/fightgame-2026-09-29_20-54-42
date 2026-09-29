// /Assets/_Game/Tests/EditMode/Core/CommandBufferTests.cs
// 공용코드 수정: P01-A 직접 모듈 시험. Unity 및 standalone에서 동일 소스 실행.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using RP.Core.Foundation;
using RP.Core.Timing;
using RP.Core.Input;

namespace RP.Tests.Core
{
    public sealed class CommandBufferTests
    {

        private static readonly ClockEpoch Epoch = new ClockEpoch(1);
        private static InputCommand C(long seq, double time, InputActionId action = InputActionId.LightAttack,
            InputPhase phase = InputPhase.Pressed) => new InputCommand(seq, 0, Epoch, action, time, time, time - .07, Float3.Zero, phase);
        [Test] public void QueueOrdersByTimeWhileSequenceTracksReception()
        {
            var b = new CommandBuffer(Epoch); b.Push(C(1, .2)); b.Push(C(2, .1)); var output = new InputCommand[2];
            Assert.That(b.DrainUntil(.2, output), Is.EqualTo(2));
            Assert.That(output[0].Sequence, Is.EqualTo(2)); Assert.That(output[1].Sequence, Is.EqualTo(1));
        }
        [Test] public void OverflowRejectsWithoutConsumingTheSequence()
        {
            var b = new CommandBuffer(Epoch, 1); b.Push(C(1, 0));
            Assert.That(b.Push(C(2, 1)), Is.EqualTo(ResultReason.CapacityExceeded));
            b.DrainUntil(0, new InputCommand[1]); Assert.That(b.Push(C(2, 1)), Is.EqualTo(ResultReason.None));
        }
        [Test] public void AlreadyDrainedSequenceCannotBeReplayed()
        {
            var b = new CommandBuffer(Epoch); b.Push(C(1, 0)); b.DrainUntil(0, new InputCommand[1]);
            Assert.That(b.Push(C(1, 0)), Is.EqualTo(ResultReason.Duplicate));
        }
        [Test] public void SmallDestinationDoesNotPartiallyDrainOrAdvanceTime()
        {
            var b = new CommandBuffer(Epoch); b.Push(C(1, .1)); b.Push(C(2, .2));
            Assert.Throws<ArgumentException>(() => b.DrainUntil(.2, new InputCommand[1]));
            Assert.That(b.Count, Is.EqualTo(2)); Assert.That(b.DrainUntil(.1, new InputCommand[2]), Is.EqualTo(1));
        }
        [TestCase(.100, true)][TestCase(.120, true)][TestCase(.130, false)]
        public void ReservationLifetimeUsesTheOriginalEvent(double delay, bool accepted)
        {
            var b = new CommandBuffer(Epoch); var c = C(1, 1);
            Assert.That(b.TryBufferAttack(c, 1), Is.EqualTo(ResultReason.None));
            Assert.That(b.TryTakeBufferedAttack(1 + delay, out var taken), Is.EqualTo(accepted));
            if (accepted) Assert.That(taken.JudgedSongTime, Is.EqualTo(.93).Within(1e-12));
        }
        [Test] public void OneSlotCannotBeReplacedByABetterLaterInput()
        {
            var b = new CommandBuffer(Epoch); b.TryBufferAttack(C(1, 1), 1);
            Assert.That(b.TryBufferAttack(C(2, 1.01), 1.01), Is.EqualTo(ResultReason.CapacityExceeded));
            Assert.That(b.TryTakeBufferedAttack(1.02, out var result), Is.True); Assert.That(result.Sequence, Is.EqualTo(1));
        }
        [Test] public void ExpiredAtArrivalDoesNotReceiveANewLifetime()
        { Assert.That(new CommandBuffer(Epoch).TryBufferAttack(C(1, 1), 1.13), Is.EqualTo(ResultReason.Expired)); }
        [Test] public void DefenseAndReleaseCannotBecomeBufferedAttacks()
        {
            var b = new CommandBuffer(Epoch);
            Assert.That(b.TryBufferAttack(C(1, 1, InputActionId.Parry), 1), Is.EqualTo(ResultReason.InvalidInput));
            Assert.That(b.TryBufferAttack(C(2, 1, InputActionId.LightAttack, InputPhase.Released), 1), Is.EqualTo(ResultReason.InvalidInput));
        }
        [Test] public void ApprovedDefenseCanCancelTheSingleAttackReservation()
        {
            var b = new CommandBuffer(Epoch); b.TryBufferAttack(C(1, 1), 1); b.CancelBufferedAttack();
            Assert.That(b.TryTakeBufferedAttack(1.01, out _), Is.False);
            Assert.That(b.TryBufferAttack(C(1, 1), 1.01), Is.EqualTo(ResultReason.Duplicate));
        }
        [Test] public void NewEpochFlushesOldInputAndReservations()
        {
            var b = new CommandBuffer(Epoch); b.Push(C(1, 1)); b.TryBufferAttack(C(1, 1), 1); b.ClearForEpoch(new ClockEpoch(2));
            Assert.That(b.Count, Is.EqualTo(0)); Assert.That(b.HasBufferedAttack, Is.False);
            Assert.That(b.Push(C(2, 2)), Is.EqualTo(ResultReason.StaleEpoch));
            Assert.Throws<ArgumentOutOfRangeException>(() => b.ClearForEpoch(Epoch));
        }
        [Test] public void ExpiryIsStrictlyBeforeAndDefaultInputIsRejected()
        {
            var b = new CommandBuffer(Epoch); b.Push(C(1, 1)); b.Push(C(2, 2));
            Assert.That(b.ExpireBefore(2), Is.EqualTo(1)); Assert.That(b.Count, Is.EqualTo(1));
            Assert.That(b.Push(default), Is.EqualTo(ResultReason.InvalidInput));
        }

    }
}
