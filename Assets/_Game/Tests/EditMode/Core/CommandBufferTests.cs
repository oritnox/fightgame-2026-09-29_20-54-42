// /Assets/_Game/Tests/EditMode/Core/CommandBufferTests.cs
// 공용코드 수정: P01-A 직접 모듈 시험. timestamp 재정렬·유한 예약 중복 이력 회귀 검증.
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
        [Test] public void PushStillRequiresReceptionSequenceOrder()
        {
            var b = new CommandBuffer(Epoch);
            Assert.That(b.Push(C(2, 1.01)), Is.EqualTo(ResultReason.None));
            Assert.That(b.Push(C(1, 1.02)), Is.EqualTo(ResultReason.Duplicate));
            Assert.That(b.Count, Is.EqualTo(1));
        }
        [Test] public void TimestampReorderedDrainedAttacksCanBothBeReservedExactlyOnce()
        {
            var b = new CommandBuffer(Epoch, 2); var output = new InputCommand[2];
            Assert.That(b.Push(C(1, 1.02)), Is.EqualTo(ResultReason.None));
            Assert.That(b.Push(C(2, 1.01)), Is.EqualTo(ResultReason.None));
            Assert.That(b.DrainUntil(1.02, output), Is.EqualTo(2));
            Assert.That(output[0].Sequence, Is.EqualTo(2)); Assert.That(output[1].Sequence, Is.EqualTo(1));
            foreach (var command in output)
            {
                Assert.That(b.TryBufferAttack(command, 1.02), Is.EqualTo(ResultReason.None));
                Assert.That(b.TryTakeBufferedAttack(1.02, out var taken), Is.True);
                Assert.That(taken.Sequence, Is.EqualTo(command.Sequence));
                Assert.That(taken.MappedMechanicTime, Is.EqualTo(command.MappedMechanicTime));
                Assert.That(taken.JudgedSongTime, Is.EqualTo(command.JudgedSongTime));
            }
            foreach (var command in output)
                Assert.That(b.TryBufferAttack(command, 1.03), Is.EqualTo(ResultReason.Duplicate));
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
        [Test] public void SlotRejectionDoesNotConsumeATimestampReorderedReservation()
        {
            var b = new CommandBuffer(Epoch);
            Assert.That(b.TryBufferAttack(C(2, 1.01), 1.01), Is.EqualTo(ResultReason.None));
            Assert.That(b.TryBufferAttack(C(1, 1.02), 1.02), Is.EqualTo(ResultReason.CapacityExceeded));
            Assert.That(b.TryTakeBufferedAttack(1.02, out var first), Is.True);
            Assert.That(first.Sequence, Is.EqualTo(2));
            Assert.That(b.TryBufferAttack(C(1, 1.02), 1.02), Is.EqualTo(ResultReason.None));
        }
        [Test] public void FullReplayHistoryRejectsWithoutEvictingLiveIdentities()
        {
            var b = new CommandBuffer(Epoch, 2);
            Assert.That(b.TryBufferAttack(C(3, 1), 1), Is.EqualTo(ResultReason.None));
            Assert.That(b.TryTakeBufferedAttack(1, out _), Is.True);
            Assert.That(b.TryBufferAttack(C(2, 1.01), 1.01), Is.EqualTo(ResultReason.None));
            b.CancelBufferedAttack();
            Assert.That(b.TryBufferAttack(C(1, 1.02), 1.02), Is.EqualTo(ResultReason.CapacityExceeded));
            Assert.That(b.TryBufferAttack(C(3, 1), 1.02), Is.EqualTo(ResultReason.Duplicate));
            Assert.That(b.TryBufferAttack(C(2, 1.01), 1.02), Is.EqualTo(ResultReason.Duplicate));
            Assert.That(b.HasBufferedAttack, Is.False);
            Assert.That(b.TryBufferAttack(C(1, 1.02), 1.121), Is.EqualTo(ResultReason.None));
            Assert.That(b.TryBufferAttack(C(3, 1), 1.121), Is.EqualTo(ResultReason.Expired));
            Assert.That(b.TryBufferAttack(C(2, 1.01), 1.121), Is.EqualTo(ResultReason.Duplicate));
        }
        [Test] public void ReplayHistoryRetainsTheInclusive120msBoundary()
        {
            var b = new CommandBuffer(Epoch, 1);
            Assert.That(b.TryBufferAttack(C(2, 1), 1), Is.EqualTo(ResultReason.None));
            Assert.That(b.TryTakeBufferedAttack(1, out _), Is.True);
            Assert.That(b.TryBufferAttack(C(1, 1.1), 1.12), Is.EqualTo(ResultReason.CapacityExceeded));
            Assert.That(b.TryBufferAttack(C(2, 1), 1.12), Is.EqualTo(ResultReason.Duplicate));
            Assert.That(b.TryBufferAttack(C(1, 1.1), 1.121), Is.EqualTo(ResultReason.None));
        }
        [Test] public void ReplayHistoryExpiresByOriginalTimestampRatherThanAdmissionOrder()
        {
            var b = new CommandBuffer(Epoch, 2);
            Assert.That(b.TryBufferAttack(C(1, 1.02), 1.02), Is.EqualTo(ResultReason.None));
            b.CancelBufferedAttack();
            Assert.That(b.TryBufferAttack(C(2, 1.01), 1.02), Is.EqualTo(ResultReason.None));
            b.CancelBufferedAttack();
            Assert.That(b.TryBufferAttack(C(3, 1.131), 1.131), Is.EqualTo(ResultReason.None));
            Assert.That(b.TryBufferAttack(C(1, 1.02), 1.131), Is.EqualTo(ResultReason.Duplicate));
            Assert.That(b.TryBufferAttack(C(2, 1.01), 1.131), Is.EqualTo(ResultReason.Expired));
        }
        [Test] public void ReplayHistoryCanBeReusedAcrossManyExpiredWindows()
        {
            var b = new CommandBuffer(Epoch, 1);
            for (int i = 0; i < 1000; i++)
            {
                double time = 1 + i * .13;
                Assert.That(b.TryBufferAttack(C(i + 1, time), time), Is.EqualTo(ResultReason.None));
                b.CancelBufferedAttack();
            }
            Assert.That(b.TryBufferAttack(C(1, 1), 131), Is.EqualTo(ResultReason.Expired));
        }
        [Test] public void FailedFutureReservationDoesNotDiscardReplayHistoryOrAdvanceTime()
        {
            var b = new CommandBuffer(Epoch, 1);
            Assert.That(b.TryBufferAttack(C(2, 1), 1), Is.EqualTo(ResultReason.None));
            b.CancelBufferedAttack();
            Assert.That(b.TryBufferAttack(C(3, 2), 1.13), Is.EqualTo(ResultReason.NotReady));
            Assert.That(b.TryBufferAttack(C(2, 1), 1.01), Is.EqualTo(ResultReason.Duplicate));
        }
        [Test] public void ExpiredReplayHistoryCannotBeReusedByRewindingTime()
        {
            var b = new CommandBuffer(Epoch, 1);
            Assert.That(b.TryBufferAttack(C(2, 1), 1), Is.EqualTo(ResultReason.None));
            b.CancelBufferedAttack();
            Assert.That(b.TryBufferAttack(C(1, 1.13), 1.13), Is.EqualTo(ResultReason.None));
            Assert.Throws<ArgumentOutOfRangeException>(() => b.TryBufferAttack(C(2, 1), 1.01));
            Assert.That(b.TryBufferAttack(C(2, 1), 1.13), Is.EqualTo(ResultReason.Expired));
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
            Assert.That(b.TryBufferAttack(C(1, 1), 1), Is.EqualTo(ResultReason.StaleEpoch));
            var fresh = new InputCommand(1, 0, new ClockEpoch(2), InputActionId.LightAttack, 0, 0, 0, Float3.Zero);
            Assert.That(b.Push(fresh), Is.EqualTo(ResultReason.None));
            Assert.That(b.TryBufferAttack(fresh, 0), Is.EqualTo(ResultReason.None));
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
