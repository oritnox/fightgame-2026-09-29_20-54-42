// /Assets/_Game/Scripts/Core/Timing/ClockBridge.cs
// 공용코드 수정: F008 고빈도 입력시계-DSP 샘플의 유한 시간 간격 보존. F084/F086 준비 상태·매핑 영향.
using System;
using RP.Core.Foundation;

namespace RP.Core.Timing
{
    /// <summary>Caller supplies samples. Never reads a device clock or rewrites past judgments.</summary>
    public sealed class ClockBridge
    {
        private const double MinimumFitSpan = 1;
        private readonly ClockSample[] samples;
        private readonly double minimumSampleInterval;
        private int count, next, rejected, consecutiveRejected;
        private double anchorRealtime, anchorDsp, slope = 1, rms, span;
        private bool frozen;
        private ClockEpoch epoch;
        public ClockEpoch Epoch => epoch;
        public ClockBridge(int capacity = 128)
        {
            if (capacity < 8 || capacity > 4096) throw new ArgumentOutOfRangeException(nameof(capacity));
            samples = new ClockSample[capacity];
            minimumSampleInterval = MinimumFitSpan / (capacity - 2);
        }
        public ClockQuality GetQuality() => new ClockQuality(epoch, count, rejected, slope, rms, span, frozen, Ready);
        private bool Ready => !frozen && count >= 8 && span >= MinimumFitSpan && Math.Abs(slope - 1) <= .002 && rms <= .005;
        private ClockSample Oldest => samples[(next - count + samples.Length) % samples.Length];
        private ClockSample Latest => samples[(next - 1 + samples.Length) % samples.Length];
        public ClockEpoch BeginEpoch(double realtime, double dspTime)
        {
            var sample = CheckedSample(realtime, dspTime);
            ulong id = checked(epoch.Value + 1);
            epoch = new ClockEpoch(id);
            count = next = rejected = consecutiveRejected = 0;
            slope = 1; rms = span = 0; frozen = false;
            Add(sample);
            return epoch;
        }
        public void Freeze() { frozen = true; }
        public ClockObservation Observe(double realtime, double dspTime)
        {
            var sample = CheckedSample(realtime, dspTime);
            if (frozen) return ClockObservation.IgnoredFrozen;
            if (!epoch.IsValid) { BeginEpoch(realtime, dspTime); return ClockObservation.Initialized; }
            var latest = Latest;
            if (realtime == latest.Realtime && dspTime == latest.DspTime) return ClockObservation.Duplicate;
            if (realtime <= latest.Realtime || dspTime < latest.DspTime)
            { BeginEpoch(realtime, dspTime); return ClockObservation.EpochReset; }
            if (dspTime == latest.DspTime)
            { frozen = true; return ClockObservation.Suspended; }
            double residual = dspTime - Predict(realtime);
            if (Math.Abs(residual) > .25)
            { BeginEpoch(realtime, dspTime); return ClockObservation.EpochReset; }
            if (count >= 8 && Math.Abs(residual) > Math.Max(.020, 6 * rms))
            {
                if (rejected < int.MaxValue) rejected++;
                consecutiveRejected++;
                if (consecutiveRejected >= 3) { BeginEpoch(realtime, dspTime); return ClockObservation.EpochReset; }
                return ClockObservation.RejectedOutlier;
            }
            consecutiveRejected = 0; Add(sample);
            return ClockObservation.Accepted;
        }
        public bool TryMapRealtime(double realtime, ClockEpoch expectedEpoch, out double dspTime, bool requireReady = true)
        {
            NumericGuard.NonNegative(realtime, nameof(realtime));
            dspTime = 0;
            if (!epoch.IsValid || expectedEpoch != epoch || frozen || (requireReady && !Ready)) return false;
            if (realtime < Oldest.Realtime - .5 || realtime > Latest.Realtime + .5) return false;
            double mapped = Predict(realtime);
            if (mapped < 0 || double.IsNaN(mapped) || double.IsInfinity(mapped)) return false;
            dspTime = mapped; return true;
        }
        public double MapRealtime(double realtime, ClockEpoch expectedEpoch, bool requireReady = true)
        {
            if (!TryMapRealtime(realtime, expectedEpoch, out double mapped, requireReady))
                throw new InvalidOperationException("Clock is unready, frozen, outside its horizon or belongs to another epoch.");
            return mapped;
        }
        private static ClockSample CheckedSample(double realtime, double dspTime)
        {
            var sample = new ClockSample(realtime, dspTime);
            if (realtime > 1e12 || dspTime > 1e12) throw new ArgumentOutOfRangeException("sample", "Clock horizon exceeds numeric precision policy.");
            return sample;
        }
        private double Predict(double realtime) => anchorDsp + slope * (realtime - anchorRealtime);
        private void Add(ClockSample sample)
        {
            // Keep the newest accepted endpoint live for the horizon and all observation checks.
            // Promote it to history only after a complete interval; a full buffer then contains
            // capacity - 2 completed intervals plus the live endpoint, even at high sample rates.
            int previous = (next - 2 + samples.Length) % samples.Length;
            if (count >= 2 && Latest.Realtime - samples[previous].Realtime < minimumSampleInterval)
                samples[(next - 1 + samples.Length) % samples.Length] = sample;
            else
            {
                samples[next] = sample; next = (next + 1) % samples.Length;
                if (count < samples.Length) count++;
            }
            var origin = Oldest;
            span = sample.Realtime - origin.Realtime;
            double sx = 0, sy = 0;
            for (int i = 0; i < count; i++)
            { var s = samples[(next - count + i + samples.Length) % samples.Length]; sx += s.Realtime - origin.Realtime; sy += s.DspTime - origin.DspTime; }
            double mx = sx / count, my = sy / count, xx = 0, xy = 0;
            for (int i = 0; i < count; i++)
            {
                var s = samples[(next - count + i + samples.Length) % samples.Length];
                double x = s.Realtime - origin.Realtime - mx, y = s.DspTime - origin.DspTime - my;
                xx += x * x; xy += x * y;
            }
            slope = span >= MinimumFitSpan && xx > 1e-12 ? xy / xx : 1;
            anchorRealtime = origin.Realtime + mx; anchorDsp = origin.DspTime + my;
            double squared = 0;
            for (int i = 0; i < count; i++)
            { var s = samples[(next - count + i + samples.Length) % samples.Length]; double e = s.DspTime - Predict(s.Realtime); squared += e * e; }
            rms = Math.Sqrt(squared / count);
        }
    }
}
