// /Assets/_Game/Scripts/Core/Timing/CalibrationEstimator.cs
// 공용코드 수정: F011 유효 탭 보정·MAD. 개별 하드웨어 지연 분리 측정이 아님.
using System;
using System.Collections.Generic;
using RP.Core.Foundation;

namespace RP.Core.Timing
{
    public readonly struct CalibrationTap
    {
        public double ObservedTime { get; }
        public double TargetTime { get; }
        public CalibrationTap(double observedTime, double targetTime)
        { ObservedTime = NumericGuard.Finite(observedTime, nameof(observedTime)); TargetTime = NumericGuard.Finite(targetTime, nameof(targetTime)); }
    }
    [Flags] public enum CalibrationWarning { None = 0, InsufficientSamples = 1, HighVariance = 2, SamplesRejected = 4 }
    public readonly struct CalibrationEstimate
    {
        public double Offset { get; }
        public double MedianAbsoluteDeviation { get; }
        public int WarmupExcluded { get; }
        public int AcceptedCount { get; }
        public int RejectedCount { get; }
        public CalibrationWarning Warning { get; }
        public bool CanApply => AcceptedCount > 0 && (Warning & (CalibrationWarning.InsufficientSamples | CalibrationWarning.HighVariance)) == 0;
        internal CalibrationEstimate(double offset, double mad, int warmup, int accepted, int rejected, CalibrationWarning warning)
        { Offset = offset; MedianAbsoluteDeviation = mad; WarmupExcluded = warmup; AcceptedCount = accepted; RejectedCount = rejected; Warning = warning; }
    }
    public sealed class CalibrationEstimator
    {
        public CalibrationEstimate Estimate(IReadOnlyList<CalibrationTap> taps, int warmup = 8, int minimumAccepted = 8)
        {
            if (taps == null) throw new ArgumentNullException(nameof(taps));
            if (taps.Count > 4096 || warmup < 0 || warmup > 4096 || minimumAccepted < 1 || minimumAccepted > 4096)
                throw new ArgumentOutOfRangeException("calibration", "Invalid calibration sample bounds.");
            int excluded = Math.Min(warmup, taps.Count), rejected = 0;
            var errors = new List<double>();
            for (int i = excluded; i < taps.Count; i++)
            {
                double error = taps[i].ObservedTime - taps[i].TargetTime;
                if (double.IsNaN(error) || double.IsInfinity(error) || Math.Abs(error) > .5) rejected++;
                else errors.Add(error);
            }
            if (errors.Count == 0) return new CalibrationEstimate(0, 0, excluded, 0, rejected, CalibrationWarning.InsufficientSamples | (rejected > 0 ? CalibrationWarning.SamplesRejected : CalibrationWarning.None));
            double center = Median(errors), mad = Mad(errors, center);
            double threshold = Math.Max(.010, 3 * 1.4826 * mad);
            var accepted = new List<double>();
            foreach (double error in errors) { if (Math.Abs(error - center) <= threshold) accepted.Add(error); else rejected++; }
            double offset = accepted.Count > 0 ? Median(accepted) : 0;
            double finalMad = accepted.Count > 0 ? Mad(accepted, offset) : 0;
            var warning = CalibrationWarning.None;
            if (accepted.Count < minimumAccepted) warning |= CalibrationWarning.InsufficientSamples;
            if (finalMad * 1.4826 > .025) warning |= CalibrationWarning.HighVariance;
            if (rejected > 0) warning |= CalibrationWarning.SamplesRejected;
            return new CalibrationEstimate(offset, finalMad, excluded, accepted.Count, rejected, warning);
        }
        private static double Mad(List<double> values, double center)
        { var deviations = new List<double>(values.Count); foreach (double value in values) deviations.Add(Math.Abs(value - center)); return Median(deviations); }
        private static double Median(List<double> values)
        {
            var copy = values.ToArray(); Array.Sort(copy); int middle = copy.Length / 2;
            return copy.Length % 2 == 1 ? copy[middle] : copy[middle - 1] / 2 + copy[middle] / 2;
        }
    }
}
