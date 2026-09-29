// /Assets/_Game/Tests/EditMode/UnityAdapters/AdapterBoundaryTests.cs
// 공용코드 수정: F084/F086 경계 회귀. 가상 Input System 장치 시험이며 실제 장치 지연 측정이 아님.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using RP.Core.Foundation;
using RP.Core.Input;
using RP.Core.Timing;
using RP.UnityRuntime.Input;
using RP.UnityRuntime.Timing;
using UnityEngine.InputSystem;

namespace RP.Tests.UnityAdapters
{
    public sealed class AdapterBoundaryTests
    {
        private sealed class FakeClock : IUnityClockSource
        {
            public double Realtime { get; set; }
            public double DspTime { get; set; }
        }

        private sealed class FakeMapper : IInputEventTimeMapper
        {
            public bool TryMapInputEvent(double realtime, out InputTimeMapping mapping)
            {
                mapping = new InputTimeMapping(new ClockEpoch(7), realtime + 10, realtime + 1, realtime - .25, false);
                return true;
            }
        }

        [Test]
        public void ShortRepeatedDspReadsDoNotRequestRecovery()
        {
            var source = new FakeClock { Realtime = 10, DspTime = 100 };
            using (var adapter = new UnityClockAdapter(source))
            {
                adapter.Begin();
                source.Realtime = 10.002;
                Assert.That(adapter.Tick(), Is.EqualTo(ClockObservation.Duplicate));
                source.Realtime = 10.004;
                Assert.That(adapter.Tick(), Is.EqualTo(ClockObservation.Duplicate));
                Assert.That(adapter.IsRecovering, Is.False);
                source.Realtime = 10.010; source.DspTime = 100.010;
                Assert.That(adapter.Tick(), Is.EqualTo(ClockObservation.Accepted));
                Assert.That(adapter.CombatClock.Current, Is.EqualTo(.010).Within(1e-9));
            }
        }

        [Test]
        public void RecoveryInvalidatesSongOriginUntilExplicitRebind()
        {
            var source = new FakeClock { Realtime = 10, DspTime = 100 };
            using (var adapter = new UnityClockAdapter(source))
            {
                adapter.Begin(); adapter.SetSongDspOrigin(99.5);
                adapter.ForceRecovery(RecoveryCause.DeviceChanged);
                Assert.That(adapter.HasSongOrigin, Is.False);
                source.Realtime = 20; source.DspTime = 0;
                adapter.ResumeFromRecovery();
                Assert.That(adapter.HasSongOrigin, Is.False);
                Assert.That(adapter.TryMapInputEvent(20, out InputTimeMapping input), Is.True);
                Assert.That(input.JudgedSongTime, Is.EqualTo(0));
                adapter.SetSongDspOrigin(0);
                Assert.That(adapter.HasSongOrigin, Is.True);
            }
        }

        [Test]
        public void PreviousEpochInputIsNotRelabeledAfterResume()
        {
            var source = new FakeClock { Realtime = 10, DspTime = 100 };
            using (var adapter = new UnityClockAdapter(source))
            {
                adapter.Begin();
                source.Realtime = 10.05; source.DspTime = 100.05; adapter.Tick();
                adapter.ForceRecovery(RecoveryCause.FocusLost);
                source.Realtime = 20; source.DspTime = 100.05;
                adapter.ResumeFromRecovery();
                Assert.That(adapter.TryMapInputEvent(19.99, out _), Is.False);
                Assert.That(adapter.TryMapInputEvent(20, out _), Is.True);
            }
        }

        [Test]
        public void SongOriginRequiresAnActiveEpochAndBeginClearsIt()
        {
            var source = new FakeClock { Realtime = 1, DspTime = 20 };
            using (var adapter = new UnityClockAdapter(source))
            {
                Assert.Throws<InvalidOperationException>(() => adapter.SetSongDspOrigin(20));
                adapter.Begin(); adapter.SetSongDspOrigin(20);
                adapter.Begin();
                Assert.That(adapter.HasSongOrigin, Is.False);
                adapter.ForceRecovery(RecoveryCause.Stall);
                Assert.Throws<InvalidOperationException>(() => adapter.SetSongDspOrigin(20));
            }
        }

        [Test]
        public void AnalogButtonOnlyEmitsAfterPressThresholdAndPreservesCallbackTime()
        {
            Gamepad device = InputSystem.AddDevice<Gamepad>();
            try
            {
                using (var adapter = new UnityInputAdapter(new FakeMapper()))
                using (var action = new InputAction("RP_TestTrigger", InputActionType.Button,
                    binding: device.rightTrigger.path))
                {
                    var commands = new List<InputCommand>();
                    double performedTime = -1;
                    action.performed += context => performedTime = context.time;
                    adapter.CommandCaptured += commands.Add;
                    adapter.BindButton(action, InputActionId.HeavyAttack);
                    action.Enable();
                    DriveTrigger(device, device.rightTrigger.pressPointOrDefault * .25f);
                    Assert.That(commands, Is.Empty, "started must not be treated as a full press");
                    DriveTrigger(device, 0);
                    Assert.That(commands, Is.Empty, "subthreshold cancellation must not emit an orphan release");
                    DriveTrigger(device, 1);
                    Assert.That(commands.Count, Is.EqualTo(1));
                    Assert.That(commands[0].Phase, Is.EqualTo(InputPhase.Pressed));
                    Assert.That(commands[0].Realtime, Is.EqualTo(performedTime).Within(1e-9));
                    DriveTrigger(device, 1);
                    Assert.That(commands.Count, Is.EqualTo(1));
                    DriveTrigger(device, 0);
                    Assert.That(commands.Count, Is.EqualTo(2));
                    Assert.That(commands[1].Phase, Is.EqualTo(InputPhase.Released));
                }
            }
            finally { InputSystem.RemoveDevice(device); }
        }

        [Test]
        public void ActionHoldInteractionIsRejectedInsteadOfRetimingPress()
        {
            using (var adapter = new UnityInputAdapter(new FakeMapper()))
            using (var action = new InputAction("Hold", InputActionType.Button, interactions: "hold"))
            {
                Assert.Throws<ArgumentException>(() => adapter.BindButton(action, InputActionId.LightAttack));
                Assert.That(action.enabled, Is.False);
            }
        }

        [Test]
        public void BindingHoldInteractionIsRejectedBeforeSubscription()
        {
            using (var adapter = new UnityInputAdapter(new FakeMapper()))
            using (var action = new InputAction("HoldBinding", InputActionType.Button))
            {
                action.AddBinding("<Keyboard>/space", interactions: "hold");
                Assert.Throws<ArgumentException>(() => adapter.BindButton(action, InputActionId.LightAttack));
            }
        }

        [Test]
        public void ButtonAndVectorActionTypesAreValidated()
        {
            using (var adapter = new UnityInputAdapter(new FakeMapper()))
            using (var button = new InputAction("Button", InputActionType.Button))
            using (var value = new InputAction("Value", InputActionType.Value))
            {
                Assert.Throws<ArgumentException>(() => adapter.BindButton(value, InputActionId.LightAttack));
                Assert.Throws<ArgumentException>(() => adapter.BindVector2(button, InputActionId.Move, InputVectorPlane.XZ));
            }
        }

        [Test]
        public void InvalidRawTimestampDoesNotConsumeSequence()
        {
            using (var adapter = new UnityInputAdapter(new FakeMapper()))
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => adapter.CaptureRaw(
                    InputActionId.LightAttack, InputPhase.Pressed, 1, -1, Float3.Zero));
                InputCommand captured = default;
                adapter.CommandCaptured += value => captured = value;
                Assert.That(adapter.CaptureRaw(InputActionId.LightAttack, InputPhase.Pressed, 1, 1, Float3.Zero), Is.True);
                Assert.That(captured.Sequence, Is.EqualTo(1));
            }
        }

        [Test]
        public void DisposedBindingDoesNotCaptureAndLeavesCallerActionEnabled()
        {
            Gamepad device = InputSystem.AddDevice<Gamepad>();
            try
            {
                using (var action = new InputAction("RP_TestDispose", InputActionType.Button,
                    binding: device.rightTrigger.path))
                {
                    var adapter = new UnityInputAdapter(new FakeMapper());
                    try
                    {
                        int count = 0;
                        adapter.CommandCaptured += _ => count++;
                        adapter.BindButton(action, InputActionId.LightAttack);
                        action.Enable();
                        DriveTrigger(device, 1);
                        Assert.That(count, Is.EqualTo(1));
                        adapter.Dispose();
                        Assert.That(action.enabled, Is.True);
                        DriveTrigger(device, 0); DriveTrigger(device, 1);
                        Assert.That(count, Is.EqualTo(1));
                    }
                    finally { adapter.Dispose(); }
                }
            }
            finally { InputSystem.RemoveDevice(device); }
        }

        private static void DriveTrigger(Gamepad device, float value)
        {
            InputSystem.QueueDeltaStateEvent(device.rightTrigger, value);
            InputSystem.Update();
        }
    }
}
