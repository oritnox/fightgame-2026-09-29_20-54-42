// /Assets/_Game/Scripts/Core/Input/CommandBuffer.cs
// 공용코드 수정: F014 유한 입력 큐·120ms 단일 공격 예약. 원래 timestamp 유지.
using System;
using System.Collections.Generic;
using RP.Core.Foundation;
using RP.Core.Timing;

namespace RP.Core.Input
{
    /// <summary>Single-threaded. Producer assigns sequence centrally in reception order, across devices.</summary>
    public sealed class CommandBuffer
    {
        public const double AttackReservationSeconds = .120;
        private readonly List<InputCommand> queue;
        private readonly int capacity;
        private long highestSequence, lastReservedSequence;
        private double lastDrainTime, reservationClock;
        private bool hasAttack;
        private InputCommand attack;
        public ClockEpoch Epoch { get; private set; }
        public int Count => queue.Count;
        public bool HasBufferedAttack => hasAttack;
        public CommandBuffer(ClockEpoch epoch, int capacity = 128)
        {
            if (!epoch.IsValid || capacity < 1 || capacity > 4096) throw new ArgumentOutOfRangeException("buffer");
            Epoch = epoch; this.capacity = capacity; queue = new List<InputCommand>(capacity);
        }
        public ResultReason Push(InputCommand command)
        {
            if (!command.IsValid) return ResultReason.InvalidInput;
            if (command.Epoch != Epoch) return ResultReason.StaleEpoch;
            if (command.Sequence <= highestSequence) return ResultReason.Duplicate;
            if (queue.Count >= capacity) return ResultReason.CapacityExceeded;
            int low = 0, high = queue.Count;
            while (low < high)
            {
                int mid = low + (high - low) / 2;
                var item = queue[mid];
                bool before = item.MappedMechanicTime < command.MappedMechanicTime ||
                    (item.MappedMechanicTime == command.MappedMechanicTime && item.Sequence < command.Sequence);
                if (before) low = mid + 1; else high = mid;
            }
            queue.Insert(low, command); highestSequence = command.Sequence;
            return ResultReason.None;
        }
        public int DrainUntil(double target, InputCommand[] destination, int offset = 0)
        {
            NumericGuard.NonNegative(target, nameof(target));
            if (target < lastDrainTime) throw new ArgumentOutOfRangeException(nameof(target), "Drain time cannot move backwards.");
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            int count = 0;
            while (count < queue.Count && queue[count].MappedMechanicTime <= target) count++;
            if (offset < 0 || offset > destination.Length || count > destination.Length - offset)
                throw new ArgumentException("Destination is too small; queue is unchanged.", nameof(destination));
            queue.CopyTo(0, destination, offset, count);
            queue.RemoveRange(0, count); lastDrainTime = target;
            return count;
        }
        public int ExpireBefore(double minimumMechanicTime)
        {
            NumericGuard.NonNegative(minimumMechanicTime, nameof(minimumMechanicTime));
            int count = 0;
            while (count < queue.Count && queue[count].MappedMechanicTime < minimumMechanicTime) count++;
            queue.RemoveRange(0, count); return count;
        }
        public ResultReason TryBufferAttack(InputCommand command, double now)
        {
            CheckReservationTime(now);
            if (!command.IsValid || command.Phase != InputPhase.Pressed ||
                (command.Action != InputActionId.LightAttack && command.Action != InputActionId.HeavyAttack)) return ResultReason.InvalidInput;
            if (command.Epoch != Epoch) return ResultReason.StaleEpoch;
            if (command.Sequence <= lastReservedSequence) return ResultReason.Duplicate;
            if (command.MappedMechanicTime > now) return ResultReason.NotReady;
            if (now - command.MappedMechanicTime > AttackReservationSeconds + 1e-12) return ResultReason.Expired;
            ClearExpiredReservation(now);
            if (hasAttack) return ResultReason.CapacityExceeded;
            attack = command; hasAttack = true; lastReservedSequence = command.Sequence; reservationClock = now;
            return ResultReason.None;
        }
        public bool TryTakeBufferedAttack(double now, out InputCommand command)
        {
            CheckReservationTime(now); ClearExpiredReservation(now); reservationClock = now;
            command = default;
            if (!hasAttack) return false;
            command = attack; hasAttack = false; attack = default; return true;
        }
        public void CancelBufferedAttack() { hasAttack = false; attack = default; }
        public void ClearForEpoch(ClockEpoch epoch)
        {
            if (!epoch.IsValid || epoch.Value <= Epoch.Value) throw new ArgumentOutOfRangeException(nameof(epoch), "A newer epoch is required.");
            Epoch = epoch; queue.Clear(); highestSequence = lastReservedSequence = 0;
            lastDrainTime = reservationClock = 0; CancelBufferedAttack();
        }
        private void CheckReservationTime(double now)
        {
            NumericGuard.NonNegative(now, nameof(now));
            if (now < reservationClock) throw new ArgumentOutOfRangeException(nameof(now), "Reservation time cannot run backwards.");
        }
        private void ClearExpiredReservation(double now)
        { if (hasAttack && now - attack.MappedMechanicTime > AttackReservationSeconds + 1e-12) CancelBufferedAttack(); }
    }
}
