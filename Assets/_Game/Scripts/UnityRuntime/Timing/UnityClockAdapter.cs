// /Assets/_Game/Scripts/UnityRuntime/Timing/UnityClockAdapter.cs
// 공용코드 수정: F086 Unity realtime/DSP를 P01 Core 시간선에 연결. F084 입력 timestamp와 후속 TimingCoordinator가 사용.
using System;
using RP.Core.Foundation;
using RP.Core.Timing;
using UnityEngine;

namespace RP.UnityRuntime.Timing
{
    public readonly struct InputTimeMapping
    {
        public ClockEpoch Epoch { get; }
        public double DspTime { get; }
        public double MechanicTime { get; }
        public double JudgedSongTime { get; }
        public bool IsProvisional { get; }

        public InputTimeMapping(ClockEpoch epoch, double dspTime, double mechanicTime, double judgedSongTime, bool provisional)
        {
            if (!epoch.IsValid) throw new ArgumentException("A valid clock epoch is required.", nameof(epoch));
            DspTime = NumericGuard.NonNegative(dspTime, nameof(dspTime));
            MechanicTime = NumericGuard.NonNegative(mechanicTime, nameof(mechanicTime));
            JudgedSongTime = NumericGuard.Finite(judgedSongTime, nameof(judgedSongTime));
            Epoch = epoch;
            IsProvisional = provisional;
        }
    }

    public interface IInputEventTimeMapper
    {
        bool TryMapInputEvent(double realtime, out InputTimeMapping mapping);
    }

    public interface IUnityClockSource
    {
        double Realtime { get; }
        double DspTime { get; }
    }

    public sealed class SystemUnityClockSource : IUnityClockSource
    {
        public double Realtime => Time.realtimeSinceStartupAsDouble;
        public double DspTime => AudioSettings.dspTime;
    }

    /// <summary>
    /// Bridges Unity realtime and DSP time into the engine-independent ClockBridge/CombatClock.
    /// It never rewrites past judgments and never auto-resumes after focus/device recovery.
    /// </summary>
    public sealed class UnityClockAdapter : IInputEventTimeMapper, IDisposable
    {
        private readonly IUnityClockSource source;
        private readonly ClockBridge bridge;
        private readonly CombatClock combatClock;
        private TimingProfile profile;
        private bool started;
        private bool disposed;
        private bool recovering;
        private double dspAnchor;
        private double combatAnchor;
        private bool hasSongOrigin;
        private double songDspOrigin;
        private RecoveryCause pendingCause;

        public event Action<RecoveryCause> RecoveryRequested;

        public ClockBridge Bridge => bridge;
        public CombatClock CombatClock => combatClock;
        public ClockEpoch Epoch => bridge.Epoch;
        public ClockQuality Quality => bridge.GetQuality();
        public bool IsRecovering => recovering;
        public RecoveryCause PendingRecoveryCause => pendingCause;
        public bool HasSongOrigin => hasSongOrigin;
        public double SongDspOrigin => hasSongOrigin ? songDspOrigin : throw new InvalidOperationException("No song DSP origin is set.");

        public UnityClockAdapter(IUnityClockSource source = null, TimingProfile profile = null,
            ClockBridge bridge = null, CombatClock combatClock = null)
        {
            this.source = source ?? new SystemUnityClockSource();
            this.profile = profile ?? new TimingProfile();
            this.bridge = bridge ?? new ClockBridge();
            this.combatClock = combatClock ?? new CombatClock();
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
        }

        public void Begin()
        {
            ThrowIfDisposed();
            double realtime = CheckedTime(source.Realtime, nameof(source.Realtime));
            double dsp = CheckedTime(source.DspTime, nameof(source.DspTime));
            bridge.BeginEpoch(realtime, dsp);
            combatClock.Resume();
            dspAnchor = dsp;
            combatAnchor = combatClock.Current;
            recovering = false;
            pendingCause = default;
            started = true;
        }

        public ClockObservation Tick()
        {
            ThrowIfDisposed();
            if (!started) throw new InvalidOperationException("Begin must be called before Tick.");
            if (recovering) return ClockObservation.IgnoredFrozen;

            double realtime = CheckedTime(source.Realtime, nameof(source.Realtime));
            double dsp = CheckedTime(source.DspTime, nameof(source.DspTime));
            ClockObservation observation = bridge.Observe(realtime, dsp);
            if (observation == ClockObservation.Suspended || observation == ClockObservation.EpochReset)
            {
                RequestRecovery(observation == ClockObservation.Suspended ? RecoveryCause.Stall : RecoveryCause.ClockDiscontinuity);
                return observation;
            }
            if (observation != ClockObservation.Accepted && observation != ClockObservation.Duplicate)
                return observation;

            double target = combatAnchor + (dsp - dspAnchor);
            if (target < combatClock.Current)
            {
                RequestRecovery(RecoveryCause.ClockDiscontinuity);
                return ClockObservation.EpochReset;
            }
            ClockAdvance advance = combatClock.AdvanceTarget(target);
            if (advance.Status == ClockAdvanceStatus.RecoveryRequired)
                RequestRecovery(RecoveryCause.Stall);
            return observation;
        }

        public bool TryMapInputEvent(double realtime, out InputTimeMapping mapping)
        {
            ThrowIfDisposed();
            mapping = default;
            if (!started || recovering || !bridge.Epoch.IsValid) return false;
            CheckedTime(realtime, nameof(realtime));
            if (!bridge.TryMapRealtime(realtime, bridge.Epoch, out double mappedDsp, requireReady: false))
                return false;
            double mechanic = combatAnchor + (mappedDsp - dspAnchor);
            if (mechanic < 0 || double.IsNaN(mechanic) || double.IsInfinity(mechanic)) return false;
            double judgedSong = hasSongOrigin ? profile.JudgeSongTime(mappedDsp, songDspOrigin) : 0;
            mapping = new InputTimeMapping(bridge.Epoch, mappedDsp, mechanic, judgedSong, !bridge.GetQuality().IsReady);
            return true;
        }

        public void SetTimingProfile(TimingProfile value)
        {
            ThrowIfDisposed();
            profile = value ?? throw new ArgumentNullException(nameof(value));
        }

        public void SetSongDspOrigin(double dspTime)
        {
            ThrowIfDisposed();
            songDspOrigin = CheckedTime(dspTime, nameof(dspTime));
            hasSongOrigin = true;
        }

        public void ClearSongDspOrigin()
        {
            ThrowIfDisposed();
            hasSongOrigin = false;
            songDspOrigin = 0;
        }

        public void NotifyApplicationPause(bool paused)
        {
            ThrowIfDisposed();
            if (paused) RequestRecovery(RecoveryCause.FocusLost);
            else if (recovering) pendingCause = RecoveryCause.Resume;
        }

        public void NotifyFocusChanged(bool focused)
        {
            ThrowIfDisposed();
            if (!focused) RequestRecovery(RecoveryCause.FocusLost);
            else if (recovering) pendingCause = RecoveryCause.Resume;
        }

        public void ResumeFromRecovery()
        {
            ThrowIfDisposed();
            if (!started) { Begin(); return; }
            double realtime = CheckedTime(source.Realtime, nameof(source.Realtime));
            double dsp = CheckedTime(source.DspTime, nameof(source.DspTime));
            combatClock.Resume();
            bridge.BeginEpoch(realtime, dsp);
            dspAnchor = dsp;
            combatAnchor = combatClock.Current;
            recovering = false;
            pendingCause = default;
        }

        public void ForceRecovery(RecoveryCause cause)
        {
            ThrowIfDisposed();
            RequestRecovery(cause);
        }

        private void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            if (!disposed)
                RequestRecovery(deviceWasChanged ? RecoveryCause.DeviceChanged : RecoveryCause.ClockDiscontinuity);
        }

        private void RequestRecovery(RecoveryCause cause)
        {
            if (!Enum.IsDefined(typeof(RecoveryCause), cause)) throw new ArgumentOutOfRangeException(nameof(cause));
            if (recovering) return;
            recovering = true;
            pendingCause = cause;
            bridge.Freeze();
            combatClock.Freeze();
            RecoveryRequested?.Invoke(cause);
        }

        private static double CheckedTime(double value, string name)
        {
            NumericGuard.NonNegative(value, name);
            if (value > 1e12) throw new ArgumentOutOfRangeException(name, "Clock horizon exceeds precision policy.");
            return value;
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(UnityClockAdapter));
        }

        public void Dispose()
        {
            if (disposed) return;
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
            RecoveryRequested = null;
            disposed = true;
        }
    }
}
