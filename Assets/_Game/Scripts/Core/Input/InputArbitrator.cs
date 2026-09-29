// /Assets/_Game/Scripts/Core/Input/InputArbitrator.cs
// 공용코드 수정: F015 동시 입력 중재. 원자적 행동/자원 승인은 후속 ActionResolver 책임.
using System;
using System.Collections.Generic;
using RP.Core.Timing;

namespace RP.Core.Input
{
    public sealed class InputArbitrator
    {
        public const double TieWindowSeconds = .001;
        public ArbitrationResult Select(IReadOnlyList<InputCommand> candidates, ClockEpoch epoch,
            CombatInputMask allowed = CombatInputMask.All)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            if (!epoch.IsValid || candidates.Count > 4096 || (allowed & ~CombatInputMask.All) != 0)
                throw new ArgumentOutOfRangeException("arbitration");
            double earliest = double.PositiveInfinity;
            int eligibleCount = 0;
            foreach (var command in candidates)
                if (Eligible(command, epoch, allowed))
                { earliest = Math.Min(earliest, command.MappedMechanicTime); eligibleCount++; }
            InputCommand selected = default;
            int priority = int.MinValue, groupCount = 0;
            foreach (var command in candidates)
            {
                if (!Eligible(command, epoch, allowed) || command.MappedMechanicTime - earliest > TieWindowSeconds + 1e-12) continue;
                groupCount++;
                int candidatePriority = Priority(command.Action);
                if (!selected.IsValid || candidatePriority > priority ||
                    (candidatePriority == priority && command.Sequence < selected.Sequence))
                { selected = command; priority = candidatePriority; }
            }
            return new ArbitrationResult(selected, Math.Max(0, groupCount - (selected.IsValid ? 1 : 0)),
                eligibleCount - groupCount, selected.IsValid ? earliest : 0,
                selected.IsValid ? earliest + TieWindowSeconds : 0);
        }
        private static bool Eligible(InputCommand command, ClockEpoch epoch, CombatInputMask allowed)
            => command.IsValid && command.Epoch == epoch && command.Phase == InputPhase.Pressed && (Tag(command.Action) & allowed) != 0;
        private static CombatInputMask Tag(InputActionId action)
        {
            switch (action)
            {
                case InputActionId.LightAttack: return CombatInputMask.LightAttack;
                case InputActionId.HeavyAttack: return CombatInputMask.HeavyAttack;
                case InputActionId.Parry: return CombatInputMask.Parry;
                case InputActionId.Dodge: return CombatInputMask.Dodge;
                default: return CombatInputMask.None;
            }
        }
        private static int Priority(InputActionId action)
        {
            switch (action)
            {
                case InputActionId.Dodge: return 4;
                case InputActionId.Parry: return 3;
                case InputActionId.HeavyAttack: return 2;
                case InputActionId.LightAttack: return 1;
                default: return 0;
            }
        }
    }
}
