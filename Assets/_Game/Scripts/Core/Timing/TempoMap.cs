// /Assets/_Game/Scripts/Core/Timing/TempoMap.cs
// 공용코드 수정: F009 PPQ960 구간 변환. 리듬 슬롯·음악 예약의 공통 시간 계산.
using System;
using System.Collections.Generic;
using RP.Core.Foundation;

namespace RP.Core.Timing
{
    public sealed class TempoMap
    {
        public const int Ppq = 960;
        // Conservative horizon keeps mixed 1-1000 BPM round trips below half a tick.
        public const long MaximumTick = 1_000_000_000_000L;
        private readonly TempoSegment[] segments;
        private readonly double[] seconds;
        public int Count => segments.Length;
        public TempoMap(IReadOnlyList<TempoSegment> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (source.Count == 0 || source.Count > 4096) throw new ArgumentOutOfRangeException(nameof(source));
            segments = new TempoSegment[source.Count]; seconds = new double[source.Count];
            long meterOrigin = 0;
            for (int i = 0; i < source.Count; i++)
            {
                var item = source[i];
                if (!item.Meter.IsValid || item.Bpm < 1 || item.Bpm > 1000 || double.IsNaN(item.Bpm)) throw new ArgumentException("Uninitialized tempo segment.", nameof(source));
                if (i == 0 && item.StartTick != 0) throw new ArgumentException("First tempo segment must begin at tick zero.");
                if (i > 0)
                {
                    var previous = segments[i - 1];
                    if (item.StartTick <= previous.StartTick) throw new ArgumentException("Tempo starts must be strictly increasing.");
                    if (!previous.Meter.Equals(item.Meter))
                    {
                        if ((item.StartTick - meterOrigin) % previous.Meter.BarTicks != 0) throw new ArgumentException("Meter change must occur at a bar boundary.");
                        meterOrigin = item.StartTick;
                    }
                    seconds[i] = seconds[i - 1] + DeltaSeconds(item.StartTick - previous.StartTick, previous.Bpm);
                    if (seconds[i] <= seconds[i - 1] || double.IsInfinity(seconds[i])) throw new ArgumentException("Tempo boundary exceeds time precision.");
                }
                segments[i] = item;
            }
        }
        private static double DeltaSeconds(double ticks, double bpm) => ticks * (60.0 / (Ppq * bpm));
        public TempoSegment GetSegment(int index)
        { if (index < 0 || index >= segments.Length) throw new ArgumentOutOfRangeException(nameof(index)); return segments[index]; }
        public double TickToSeconds(long tick)
        {
            CheckTick(tick);
            int index = FindTick(tick);
            return seconds[index] + DeltaSeconds(tick - segments[index].StartTick, segments[index].Bpm);
        }
        public long SecondsToTick(double value)
        {
            NumericGuard.NonNegative(value, nameof(value));
            int low = 0, high = seconds.Length - 1;
            while (low < high) { int mid = low + (high - low + 1) / 2; if (seconds[mid] <= value) low = mid; else high = mid - 1; }
            double tick = segments[low].StartTick + (value - seconds[low]) * (Ppq * segments[low].Bpm / 60.0);
            if (tick > MaximumTick + .5 || double.IsInfinity(tick)) throw new ArgumentOutOfRangeException(nameof(value), "Time exceeds supported tick precision.");
            long rounded = checked((long)Math.Round(tick, MidpointRounding.AwayFromZero));
            CheckTick(rounded);
            return rounded;
        }
        public Meter GetMeterAt(long tick) { CheckTick(tick); return segments[FindTick(tick)].Meter; }
        public double GetBpmAt(long tick) { CheckTick(tick); return segments[FindTick(tick)].Bpm; }
        private int FindTick(long tick)
        {
            int low = 0, high = segments.Length - 1;
            while (low < high) { int mid = low + (high - low + 1) / 2; if (segments[mid].StartTick <= tick) low = mid; else high = mid - 1; }
            return low;
        }
        private static void CheckTick(long tick)
        { if (tick < 0 || tick > MaximumTick) throw new ArgumentOutOfRangeException(nameof(tick)); }
    }
}
