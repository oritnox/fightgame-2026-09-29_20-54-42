// /Assets/_Game/Tests/EditMode/UnityAdapters/UnityClockAdapterTests.cs
// 공용코드 수정: F086 직접 회귀. 실제 출력 지연 측정이 아니라 adapter/Core 연결 시험.
using NUnit.Framework;
using RP.Core.Timing;
using RP.UnityRuntime.Timing;

namespace RP.Tests.UnityAdapters
{
    public sealed class UnityClockAdapterTests
    {
        private sealed class FakeClock : IUnityClockSource
        {
            public double Realtime { get; set; }
            public double DspTime { get; set; }
        }

        [Test]
        public void BeginCreatesEpochAndProvisionalInputMapping()
        {
            var source = new FakeClock { Realtime = 10, DspTime = 100 };
            using (var adapter = new UnityClockAdapter(source))
            {
                adapter.Begin();
                Assert.That(adapter.Epoch.IsValid, Is.True);
                Assert.That(adapter.TryMapInputEvent(10, out InputTimeMapping mapping), Is.True);
                Assert.That(mapping.DspTime, Is.EqualTo(100).Within(1e-9));
                Assert.That(mapping.MechanicTime, Is.EqualTo(0).Within(1e-9));
                Assert.That(mapping.IsProvisional, Is.True);
            }
        }

        [Test]
        public void StableSamplesAdvanceCombatAndBecomeReady()
        {
            var source = new FakeClock { Realtime = 1, DspTime = 20 };
            using (var adapter = new UnityClockAdapter(source))
            {
                adapter.Begin();
                for (int i = 1; i <= 10; i++)
                {
                    source.Realtime = 1 + i * .125;
                    source.DspTime = 20 + i * .125;
                    adapter.Tick();
                }
                Assert.That(adapter.Quality.IsReady, Is.True);
                Assert.That(adapter.CombatClock.Current, Is.EqualTo(1.25).Within(1e-9));
            }
        }

        [Test]
        public void SongOriginAndInputOffsetAffectOnlyJudgedSongTime()
        {
            var source = new FakeClock { Realtime = 2, DspTime = 50 };
            using (var adapter = new UnityClockAdapter(source, new TimingProfile(.07, .12)))
            {
                adapter.Begin();
                adapter.SetSongDspOrigin(49.5);
                Assert.That(adapter.TryMapInputEvent(2, out InputTimeMapping mapping), Is.True);
                Assert.That(mapping.MechanicTime, Is.EqualTo(0).Within(1e-9));
                Assert.That(mapping.JudgedSongTime, Is.EqualTo(.43).Within(1e-9));
            }
        }

        [Test]
        public void DspStallRequestsRecoveryAndRejectsInputUntilExplicitResume()
        {
            var source = new FakeClock { Realtime = 5, DspTime = 5 };
            using (var adapter = new UnityClockAdapter(source))
            {
                adapter.Begin();
                source.Realtime = 5.1;
                Assert.That(adapter.Tick(), Is.EqualTo(ClockObservation.Suspended));
                Assert.That(adapter.IsRecovering, Is.True);
                Assert.That(adapter.TryMapInputEvent(5.1, out _), Is.False);
                source.Realtime = 8;
                source.DspTime = 5.1;
                adapter.ResumeFromRecovery();
                Assert.That(adapter.IsRecovering, Is.False);
                Assert.That(adapter.CombatClock.Current, Is.EqualTo(0).Within(1e-9));
            }
        }

        [Test]
        public void ClockDiscontinuityDoesNotApplyNegativeDelta()
        {
            var source = new FakeClock { Realtime = 10, DspTime = 10 };
            using (var adapter = new UnityClockAdapter(source))
            {
                adapter.Begin();
                source.Realtime = 10.1; source.DspTime = 10.1; adapter.Tick();
                double committed = adapter.CombatClock.Current;
                source.Realtime = 10.2; source.DspTime = 9.0;
                Assert.That(adapter.Tick(), Is.EqualTo(ClockObservation.EpochReset));
                Assert.That(adapter.CombatClock.Current, Is.EqualTo(committed));
                Assert.That(adapter.IsRecovering, Is.True);
            }
        }

        [Test]
        public void FocusLossIsRecoveryNotAutomaticResume()
        {
            var source = new FakeClock { Realtime = 1, DspTime = 1 };
            using (var adapter = new UnityClockAdapter(source))
            {
                adapter.Begin();
                adapter.NotifyFocusChanged(false);
                Assert.That(adapter.IsRecovering, Is.True);
                adapter.NotifyFocusChanged(true);
                Assert.That(adapter.PendingRecoveryCause, Is.EqualTo(RecoveryCause.Resume));
                Assert.That(adapter.IsRecovering, Is.True);
            }
        }

        [Test]
        public void DisposedAdapterRejectsFurtherUse()
        {
            var source = new FakeClock { Realtime = 1, DspTime = 1 };
            var adapter = new UnityClockAdapter(source);
            adapter.Dispose();
            Assert.Throws<System.ObjectDisposedException>(() => adapter.Begin());
        }
    }
}
