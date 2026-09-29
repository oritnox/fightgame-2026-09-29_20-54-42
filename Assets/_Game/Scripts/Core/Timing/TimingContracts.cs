// /Assets/_Game/Scripts/Core/Timing/TimingContracts.cs
// 공용코드 수정: F007 시간 단위·epoch·품질·보정. 입력/템포/복구 계약 공유.
using System;
using RP.Core.Foundation;

namespace RP.Core.Timing
{
    public enum TransportState { Stopped, WarmingUp, Running, Paused, Recovering }
    public enum ClockObservation { Initialized, Accepted, Duplicate, IgnoredFrozen, RejectedOutlier, EpochReset, Suspended }
    public enum ClockAdvanceStatus { Advanced, Unchanged, Frozen, RecoveryRequired }
    public enum RecoveryCause { Resume, FocusLost, DeviceChanged, ClockDiscontinuity, Stall }

    public readonly struct ClockEpoch : IEquatable<ClockEpoch>
    {
        public ulong Value { get; }
        public bool IsValid => Value != 0;
        public ClockEpoch(ulong value) { if (value == 0) throw new ArgumentOutOfRangeException(nameof(value)); Value = value; }
        public bool Equals(ClockEpoch other) => Value == other.Value;
        public override bool Equals(object obj) => obj is ClockEpoch other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public static bool operator ==(ClockEpoch a, ClockEpoch b) => a.Equals(b);
        public static bool operator !=(ClockEpoch a, ClockEpoch b) => !a.Equals(b);
    }
    public readonly struct ClockSample
    {
        public double Realtime { get; }
        public double DspTime { get; }
        public ClockSample(double realtime, double dspTime)
        { Realtime = NumericGuard.NonNegative(realtime, nameof(realtime)); DspTime = NumericGuard.NonNegative(dspTime, nameof(dspTime)); }
    }
    public readonly struct ClockQuality
    {
        public ClockEpoch Epoch { get; }
        public int Samples { get; }
        public int RejectedSamples { get; }
        public double Slope { get; }
        public double RmsResidual { get; }
        public double Span { get; }
        public bool IsFrozen { get; }
        public bool IsReady { get; }
        internal ClockQuality(ClockEpoch epoch, int samples, int rejected, double slope, double rms, double span, bool frozen, bool ready)
        { Epoch = epoch; Samples = samples; RejectedSamples = rejected; Slope = slope; RmsResidual = rms; Span = span; IsFrozen = frozen; IsReady = ready; }
    }
    public sealed class TimingProfile
    {
        public double InputOffset { get; }
        public double PresentationOffset { get; }
        public TimingProfile(double inputOffset = 0, double presentationOffset = 0)
        {
            NumericGuard.Finite(inputOffset, nameof(inputOffset)); NumericGuard.Finite(presentationOffset, nameof(presentationOffset));
            if (Math.Abs(inputOffset) > .5 || Math.Abs(presentationOffset) > .5) throw new ArgumentOutOfRangeException("offset", "Offset exceeds 500ms.");
            InputOffset = inputOffset; PresentationOffset = presentationOffset;
        }
        public double JudgeSongTime(double mappedDsp, double dspSongOrigin)
        {
            NumericGuard.NonNegative(mappedDsp, nameof(mappedDsp)); NumericGuard.NonNegative(dspSongOrigin, nameof(dspSongOrigin));
            return NumericGuard.Finite(mappedDsp - dspSongOrigin - InputOffset, "judgedSongTime");
        }
    }
    public readonly struct Meter : IEquatable<Meter>
    {
        public int Numerator { get; }
        public int Denominator { get; }
        public bool IsValid => Numerator >= 1 && Numerator <= 32 && Denominator >= 1 && Denominator <= 32 && (Denominator & (Denominator - 1)) == 0;
        public long BarTicks => IsValid ? 960L * 4 * Numerator / Denominator : throw new InvalidOperationException("Uninitialized meter.");
        public Meter(int numerator, int denominator)
        {
            Numerator = numerator; Denominator = denominator;
            if (!IsValid) throw new ArgumentOutOfRangeException(nameof(denominator), "Meter requires numerator 1-32 and power-of-two denominator 1-32.");
        }
        public bool Equals(Meter other) => Numerator == other.Numerator && Denominator == other.Denominator;
        public override bool Equals(object obj) => obj is Meter other && Equals(other);
        public override int GetHashCode() => Numerator * 397 ^ Denominator;
    }
    public readonly struct TempoSegment
    {
        public long StartTick { get; }
        public double Bpm { get; }
        public Meter Meter { get; }
        public TempoSegment(long startTick, double bpm, int numerator = 4, int denominator = 4)
        {
            if (startTick < 0 || startTick > TempoMap.MaximumTick) throw new ArgumentOutOfRangeException(nameof(startTick));
            NumericGuard.Positive(bpm, nameof(bpm));
            if (bpm > 1000 || bpm < 1) throw new ArgumentOutOfRangeException(nameof(bpm), "Authored BPM must be 1-1000.");
            StartTick = startTick; Bpm = bpm; Meter = new Meter(numerator, denominator);
        }
    }
    public readonly struct ClockAdvance
    {
        public ClockAdvanceStatus Status { get; }
        public double From { get; }
        public double To { get; }
        public int SuggestedSteps { get; }
        internal ClockAdvance(ClockAdvanceStatus status, double from, double to, int steps)
        { Status = status; From = from; To = to; SuggestedSteps = steps; }
    }
}
