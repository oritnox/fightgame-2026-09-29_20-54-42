// /Assets/_Game/Scripts/Core/Input/InputContracts.cs
// 공용코드 수정: F013 원시·기계적·판정 시각과 입력 출처를 구분. 보상/피해는 변경하지 않음.
using System;
using RP.Core.Foundation;
using RP.Core.Timing;

namespace RP.Core.Input
{
    public enum InputActionId { None, LightAttack, HeavyAttack, Parry, Dodge, Finisher, Heal, Interact, Move, Look, Lock, Menu }
    public enum InputPhase { Pressed, Released }
    public enum InputOrigin { Hardware, Assist }
    [Flags] public enum CombatInputMask { None = 0, LightAttack = 1, HeavyAttack = 2, Parry = 4, Dodge = 8, All = 15 }

    public readonly struct InputCommand
    {
        public long Sequence { get; }
        public int DeviceId { get; }
        public ClockEpoch Epoch { get; }
        public InputActionId Action { get; }
        public InputPhase Phase { get; }
        public InputOrigin Origin { get; }
        public double Realtime { get; }
        public double MappedMechanicTime { get; }
        public double JudgedSongTime { get; }
        public Float3 Direction { get; }
        public bool IsAssist => Origin == InputOrigin.Assist;
        public bool IsValid => Sequence > 0 && Epoch.IsValid && Action != InputActionId.None;
        public InputCommand(long sequence, int deviceId, ClockEpoch epoch, InputActionId action,
            double realtime, double mappedMechanicTime, double judgedSongTime, Float3 direction,
            InputPhase phase = InputPhase.Pressed, InputOrigin origin = InputOrigin.Hardware)
        {
            if (sequence <= 0 || deviceId < 0 || !epoch.IsValid) throw new ArgumentOutOfRangeException("identity");
            if (action == InputActionId.None || !Enum.IsDefined(typeof(InputActionId), action)) throw new ArgumentOutOfRangeException(nameof(action));
            if (!Enum.IsDefined(typeof(InputPhase), phase) || !Enum.IsDefined(typeof(InputOrigin), origin)) throw new ArgumentOutOfRangeException("inputKind");
            Sequence = sequence; DeviceId = deviceId; Epoch = epoch; Action = action;
            Realtime = NumericGuard.NonNegative(realtime, nameof(realtime));
            MappedMechanicTime = NumericGuard.NonNegative(mappedMechanicTime, nameof(mappedMechanicTime));
            JudgedSongTime = NumericGuard.Finite(judgedSongTime, nameof(judgedSongTime));
            Direction = direction; Phase = phase; Origin = origin;
        }
        public ScheduledInput AtDelivery(double committedCombatTime)
        {
            NumericGuard.NonNegative(committedCombatTime, nameof(committedCombatTime));
            if (!IsValid) throw new InvalidOperationException("Uninitialized command.");
            return new ScheduledInput(this, Math.Max(MappedMechanicTime, committedCombatTime), MappedMechanicTime < committedCombatTime);
        }
    }
    public readonly struct ScheduledInput
    {
        public InputCommand Original { get; }
        public double ApplyTime { get; }
        public bool LateDelivery { get; }
        internal ScheduledInput(InputCommand original, double applyTime, bool late)
        { Original = original; ApplyTime = applyTime; LateDelivery = late; }
    }
    public readonly struct ArbitrationResult
    {
        public bool HasSelection { get; }
        public InputCommand Selected { get; }
        public int DiscardedCount { get; }
        public int DeferredCount { get; }
        public double GroupStartTime { get; }
        public double GroupEndTime { get; }
        internal ArbitrationResult(InputCommand selected, int discarded, int deferred, double start, double end)
        {
            Selected = selected; HasSelection = selected.IsValid; DiscardedCount = discarded;
            DeferredCount = deferred; GroupStartTime = start; GroupEndTime = end;
        }
    }
}
