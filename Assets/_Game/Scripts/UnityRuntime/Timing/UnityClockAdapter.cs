// /Assets/_Game/Scripts/UnityRuntime/Timing/UnityClockAdapter.cs
// 공용코드 수정: F086 DSP 정체 샘플 유예·epoch 경계·곡 원점 무효화. F084와 후속 TimingCoordinator 영향.
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
    /// Main-thread realtime/DSP bridge. Repeated reads of one DSP sample do not immediately
    /// imply suspension. Every recovery invalidates the song origin; the transport owner
    /// must explicitly rebind a new origin before assigning rhythm slots.
    /// </summary>
    public sealed class UnityClockAdapter : IInputEventTimeMapper, IDisposable
    {
        private const double StallTimeout = .100;
        private const double BoundaryEpsilon = 1e-12;
        private readonly IUnityClockSource source;
        private readonly ClockBridge bridge;
        private readonly CombatClock combatClock;
        private TimingProfile profile;
        private bool started;
        private bool disposed;
        private bool recovering;
        private double dspAnchor;
        private double combatAnchor;
        private double epochRealtimeStart;
        private double lastReadRealtime;
        private double lastProgressRealtime;
        private double lastDsp;
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
            StartEpoch();
        }

        private void StartEpoch()
        {
            double realtime = CheckedTime(source.Realtime, nameof(source.Realtime));
            double dsp = CheckedTime(source.DspTime, nameof(source.DspTime));
            bridge.BeginEpoch(realtime, dsp);
            combatClock.Resume();
            dspAnchor = lastDsp = dsp;
            combatAnchor = combatClock.Current;
            epochRealtimeStart = lastReadRealtime = lastProgressRealtime = realtime;
            hasSongOrigin = false;
            songDspOrigin = 0;
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
            if (realtime < lastReadRealtime || dsp < lastDsp)
            {
                RequestRecovery(RecoveryCause.ClockDiscontinuity);
                return ClockObservation.EpochReset;
            }
            lastReadRealtime = realtime;
            if (dsp == lastDsp)
            {
                // Sample-based clocks may return the same value during adjacent render updates.
                // Do not feed these repeated reads into the core's explicit suspend detector.
                if (realtime - lastProgressRealtime + BoundaryEpsilon >= StallTimeout)
                {
                    RequestRecovery(RecoveryCause.Stall);
                    return ClockObservation.Suspended;
                }
                return ClockObservation.Duplicate;
            }

            lastDsp = dsp;
            lastProgressRealtime = realtime;
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
            // An old callback must not be relabeled with the new epoch after a resume.
            if (realtime + BoundaryEpsilon < epochRealtimeStart) return false;
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
            if (!started || recovering)
                throw new InvalidOperationException("Bind the song origin only in an active clock epoch.");
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
            if (!started || recovering) StartEpoch();
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
            hasSongOrigin = false;
            songDspOrigin = 0;
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
