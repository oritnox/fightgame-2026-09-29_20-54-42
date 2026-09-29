// /Assets/_Game/Tests/EditMode/UnityAdapters/UnityInputAdapterTests.cs
// 공용코드 수정: F084 직접 회귀. 원래 timestamp·sequence·binding ownership을 검증.
using System;
using NUnit.Framework;
using RP.Core.Foundation;
using RP.Core.Input;
using RP.Core.Timing;
using RP.UnityRuntime.Input;
using RP.UnityRuntime.Timing;
using UnityEngine.InputSystem;

namespace RP.Tests.UnityAdapters
{
    public sealed class UnityInputAdapterTests
    {
        private sealed class FakeMapper : IInputEventTimeMapper
        {
            public bool Accept = true;
            public bool TryMapInputEvent(double realtime, out InputTimeMapping mapping)
            {
                mapping = Accept ? new InputTimeMapping(new ClockEpoch(7), realtime + 10, realtime + 1, realtime - .25, false) : default;
                return Accept;
            }
        }

        [Test]
        public void CaptureRawPreservesOriginalRealtimeAndAllocatesMonotonicSequence()
        {
            var mapper = new FakeMapper();
            using (var adapter = new UnityInputAdapter(mapper))
            {
                InputCommand first = default, second = default;
                int count = 0;
                adapter.CommandCaptured += command => { if (count++ == 0) first = command; else second = command; };
                Assert.That(adapter.CaptureRaw(InputActionId.LightAttack, InputPhase.Pressed, 2, 3.5, Float3.Zero), Is.True);
                Assert.That(adapter.CaptureRaw(InputActionId.LightAttack, InputPhase.Released, 2, 3.7, Float3.Zero), Is.True);
                Assert.That(first.Realtime, Is.EqualTo(3.5));
                Assert.That(first.MappedMechanicTime, Is.EqualTo(4.5));
                Assert.That(first.JudgedSongTime, Is.EqualTo(3.25));
                Assert.That(second.Sequence, Is.EqualTo(first.Sequence + 1));
            }
        }

        [Test]
        public void UnavailableTimeMappingDoesNotConsumeSequence()
        {
            var mapper = new FakeMapper { Accept = false };
            using (var adapter = new UnityInputAdapter(mapper))
            {
                int rejected = 0;
                adapter.CommandRejected += _ => rejected++;
                Assert.That(adapter.CaptureRaw(InputActionId.Parry, InputPhase.Pressed, 1, 2, Float3.Zero), Is.False);
                mapper.Accept = true;
                InputCommand accepted = default;
                adapter.CommandCaptured += command => accepted = command;
                Assert.That(adapter.CaptureRaw(InputActionId.Parry, InputPhase.Pressed, 1, 2.1, Float3.Zero), Is.True);
                Assert.That(accepted.Sequence, Is.EqualTo(1));
                Assert.That(rejected, Is.EqualTo(1));
            }
        }

        [Test]
        public void NegativeDeviceIsRejectedWithoutThrowingFromCoreCommand()
        {
            using (var adapter = new UnityInputAdapter(new FakeMapper()))
                Assert.That(adapter.CaptureRaw(InputActionId.Dodge, InputPhase.Pressed, -1, 1, Float3.Zero), Is.False);
        }

        [Test]
        public void DuplicateActionBindingIsRejected()
        {
            using (var adapter = new UnityInputAdapter(new FakeMapper()))
            using (var action = new InputAction("Attack", InputActionType.Button))
            {
                adapter.BindButton(action, InputActionId.LightAttack);
                Assert.Throws<InvalidOperationException>(() => adapter.BindButton(action, InputActionId.HeavyAttack));
            }
        }

        [Test]
        public void AdapterNeverOwnsCallerActionEnabledState()
        {
            using (var adapter = new UnityInputAdapter(new FakeMapper()))
            using (var action = new InputAction("Attack", InputActionType.Button))
            {
                Assert.That(action.enabled, Is.False);
                adapter.BindButton(action, InputActionId.LightAttack);
                Assert.That(action.enabled, Is.False);
                action.Enable();
                adapter.Dispose();
                Assert.That(action.enabled, Is.True);
            }
        }

        [Test]
        public void InvalidActionIdCannotBeBoundOrCaptured()
        {
            using (var adapter = new UnityInputAdapter(new FakeMapper()))
            using (var action = new InputAction("Bad", InputActionType.Button))
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => adapter.BindButton(action, InputActionId.None));
                Assert.Throws<ArgumentOutOfRangeException>(() => adapter.CaptureRaw(InputActionId.None, InputPhase.Pressed, 1, 1, Float3.Zero));
            }
        }
    }
}
