// /Assets/_Game/Scripts/Core/Timing/CombatClock.cs
// 공용코드 수정: F010 단조 전투시각·음악 원점·정지. 실제 적분은 CombatSimulation 책임.
using System;
using RP.Core.Foundation;

namespace RP.Core.Timing
{
    public sealed class CombatClock
    {
        private readonly double maxCatchUp, maxStep;
        public double Current { get; private set; }
        public bool IsFrozen { get; private set; }
        public bool HasSong { get; private set; }
        public double SongOriginCombatTime { get; private set; }
        public CombatClock(double maxCatchUpSeconds = .1, double maxStepSeconds = 1.0 / 120)
        {
            NumericGuard.Positive(maxCatchUpSeconds, nameof(maxCatchUpSeconds)); NumericGuard.Positive(maxStepSeconds, nameof(maxStepSeconds));
            if (maxCatchUpSeconds > 1 || maxStepSeconds > maxCatchUpSeconds || maxCatchUpSeconds / maxStepSeconds > 4096)
                throw new ArgumentOutOfRangeException("step", "Invalid bounded step plan.");
            maxCatchUp = maxCatchUpSeconds; maxStep = maxStepSeconds;
        }
        // This clock is a target planner. Consumers keep a separate committed simulation time.
        public ClockAdvance AdvanceTarget(double target)
        {
            NumericGuard.NonNegative(target, nameof(target));
            if (target < Current) throw new ArgumentOutOfRangeException(nameof(target), "Combat time cannot run backwards.");
            if (IsFrozen) return new ClockAdvance(ClockAdvanceStatus.Frozen, Current, Current, 0);
            double from = Current, delta = target - Current;
            if (delta > maxCatchUp + 1e-12)
            { IsFrozen = true; return new ClockAdvance(ClockAdvanceStatus.RecoveryRequired, from, from, 0); }
            if (delta == 0) return new ClockAdvance(ClockAdvanceStatus.Unchanged, from, from, 0);
            int steps = Math.Max(1, (int)Math.Ceiling(delta / maxStep - 1e-10));
            Current = target;
            return new ClockAdvance(ClockAdvanceStatus.Advanced, from, target, steps);
        }
        public void Freeze() { IsFrozen = true; }
        // Adapter rebases its target after pause: menu elapsed time is not combat elapsed time.
        public void Resume() { IsFrozen = false; }
        public void StartSong() { SongOriginCombatTime = Current; HasSong = true; }
        public void StopSong() { HasSong = false; }
        public double GetSongTime()
        { if (!HasSong) throw new InvalidOperationException("No song origin."); return Current - SongOriginCombatTime; }
        public double SongToCombat(double songTime)
        {
            NumericGuard.Finite(songTime, nameof(songTime));
            if (!HasSong) throw new InvalidOperationException("No song origin.");
            return NumericGuard.NonNegative(SongOriginCombatTime + songTime, "combatTime");
        }
    }
}
