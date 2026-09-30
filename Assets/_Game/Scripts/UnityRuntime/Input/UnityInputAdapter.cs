// /Assets/_Game/Scripts/UnityRuntime/Input/UnityInputAdapter.cs
// 공용코드 수정: F084 Button의 press/release threshold 전이를 원래 callback 시각으로 보존. 부분 해제·재누름 회귀 대응.
using System;
using System.Collections.Generic;
using RP.Core.Foundation;
using RP.Core.Input;
using RP.UnityRuntime.Timing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RP.UnityRuntime.Input
{
    public enum InputVectorPlane { XY, XZ }
    public enum InputCaptureFailure { TimeMappingUnavailable, SequenceOverflow, InvalidDevice, Disposed }

    public readonly struct InputCaptureRejection
    {
        public InputActionId Action { get; }
        public double Realtime { get; }
        public InputCaptureFailure Failure { get; }
        internal InputCaptureRejection(InputActionId action, double realtime, InputCaptureFailure failure)
        { Action = action; Realtime = realtime; Failure = failure; }
    }

    /// <summary>
    /// Main-thread adapter for default Button and Vector2 Value actions.
    /// A default Button's started callback can precede its press threshold, so only performed
    /// captures a press. A subsequent started callback is a partial release below the release
    /// threshold. Custom interactions require a separate reviewed adapter contract.
    /// Caller retains ownership of actions and their enabled state.
    /// </summary>
    public sealed class UnityInputAdapter : IDisposable
    {
        private sealed class Binding : IDisposable
        {
            private readonly InputAction action;
            private readonly Action<InputAction.CallbackContext> pressOrValue;
            private readonly Action<InputAction.CallbackContext> release;
            private readonly bool vector;
            private bool buttonDown;

            public Binding(InputAction action, Action<InputAction.CallbackContext> pressOrValue,
                Action<InputAction.CallbackContext> release, bool vector)
            {
                this.action = action;
                this.pressOrValue = pressOrValue;
                this.release = release;
                this.vector = vector;
                action.started += OnStarted;
                action.performed += OnPerformed;
                action.canceled += OnCanceled;
            }

            private void OnStarted(InputAction.CallbackContext context)
            {
                // Default buttons return Performed -> Started when released below the
                // release threshold without reaching zero. Initial actuation is not a release.
                if (vector || !buttonDown) return;
                buttonDown = false;
                release(context);
            }

            private void OnPerformed(InputAction.CallbackContext context)
            {
                if (!vector)
                {
                    if (buttonDown) return;
                    buttonDown = true;
                }
                pressOrValue(context);
            }

            private void OnCanceled(InputAction.CallbackContext context)
            {
                if (!vector)
                {
                    // Returning to zero before the press threshold is not a button release.
                    if (!buttonDown) return;
                    buttonDown = false;
                }
                release(context);
            }

            public void Dispose()
            {
                action.started -= OnStarted;
                action.performed -= OnPerformed;
                action.canceled -= OnCanceled;
                buttonDown = false;
            }
        }

        private readonly IInputEventTimeMapper timeMapper;
        private readonly List<Binding> bindings = new List<Binding>();
        private readonly HashSet<InputAction> boundActions = new HashSet<InputAction>();
        private long sequence;
        private bool disposed;

        public event Action<InputCommand> CommandCaptured;
        public event Action<InputCaptureRejection> CommandRejected;
        public event Action<int, InputDeviceChange> DeviceChanged;

        public UnityInputAdapter(IInputEventTimeMapper timeMapper)
        {
            this.timeMapper = timeMapper ?? throw new ArgumentNullException(nameof(timeMapper));
            InputSystem.onDeviceChange += OnDeviceChange;
        }

        public void BindButton(InputAction action, InputActionId actionId)
        {
            ValidateBinding(action, actionId, InputActionType.Button);
            Action<InputAction.CallbackContext> press = ctx => CaptureContext(ctx, actionId, InputPhase.Pressed, InputVectorPlane.XY, false);
            Action<InputAction.CallbackContext> release = ctx => CaptureContext(ctx, actionId, InputPhase.Released, InputVectorPlane.XY, false);
            AddBinding(action, new Binding(action, press, release, false));
        }

        public void BindVector2(InputAction action, InputActionId actionId, InputVectorPlane plane)
        {
            ValidateBinding(action, actionId, InputActionType.Value);
            if (!Enum.IsDefined(typeof(InputVectorPlane), plane)) throw new ArgumentOutOfRangeException(nameof(plane));
            Action<InputAction.CallbackContext> value = ctx => CaptureContext(ctx, actionId, InputPhase.Pressed, plane, true);
            Action<InputAction.CallbackContext> release = ctx => CaptureContext(ctx, actionId, InputPhase.Released, plane, true);
            AddBinding(action, new Binding(action, value, release, true));
        }

        public bool CaptureRaw(InputActionId actionId, InputPhase phase, int deviceId, double realtime, Float3 direction)
        {
            ThrowIfDisposed();
            if (actionId == InputActionId.None || !Enum.IsDefined(typeof(InputActionId), actionId))
                throw new ArgumentOutOfRangeException(nameof(actionId));
            if (!Enum.IsDefined(typeof(InputPhase), phase)) throw new ArgumentOutOfRangeException(nameof(phase));
            NumericGuard.NonNegative(realtime, nameof(realtime));
            if (realtime > 1e12) throw new ArgumentOutOfRangeException(nameof(realtime));
            if (deviceId < 0)
            {
                Reject(actionId, realtime, InputCaptureFailure.InvalidDevice);
                return false;
            }
            if (!timeMapper.TryMapInputEvent(realtime, out InputTimeMapping mapping))
            {
                Reject(actionId, realtime, InputCaptureFailure.TimeMappingUnavailable);
                return false;
            }
            long next;
            try { next = checked(sequence + 1); }
            catch (OverflowException)
            {
                Reject(actionId, realtime, InputCaptureFailure.SequenceOverflow);
                return false;
            }
            var command = new InputCommand(next, deviceId, mapping.Epoch, actionId, realtime,
                mapping.MechanicTime, mapping.JudgedSongTime, direction, phase, InputOrigin.Hardware);
            sequence = next; // Commit only after command validation succeeds.
            CommandCaptured?.Invoke(command);
            return true;
        }

        private void CaptureContext(InputAction.CallbackContext context, InputActionId actionId,
            InputPhase phase, InputVectorPlane plane, bool vector)
        {
            if (disposed) return;
            Float3 direction = Float3.Zero;
            if (vector && phase == InputPhase.Pressed)
            {
                Vector2 value = context.ReadValue<Vector2>();
                direction = plane == InputVectorPlane.XZ
                    ? new Float3(value.x, 0, value.y)
                    : new Float3(value.x, value.y, 0);
            }
            int deviceId = context.control != null && context.control.device != null ? context.control.device.deviceId : 0;
            CaptureRaw(actionId, phase, deviceId, context.time, direction);
        }

        private void ValidateBinding(InputAction action, InputActionId actionId, InputActionType requiredType)
        {
            ThrowIfDisposed();
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (actionId == InputActionId.None || !Enum.IsDefined(typeof(InputActionId), actionId))
                throw new ArgumentOutOfRangeException(nameof(actionId));
            if (boundActions.Contains(action)) throw new InvalidOperationException("InputAction is already bound to this adapter.");
            if (action.type != requiredType)
                throw new ArgumentException("This binding requires a default " + requiredType + " action.", nameof(action));
            if (!string.IsNullOrWhiteSpace(action.interactions))
                throw new ArgumentException("Custom action interactions are not supported by this capture contract.", nameof(action));
            foreach (InputBinding binding in action.bindings)
                if (!string.IsNullOrWhiteSpace(binding.effectiveInteractions))
                    throw new ArgumentException("Custom binding interactions require a separate capture contract.", nameof(action));
        }

        private void AddBinding(InputAction action, Binding binding)
        {
            boundActions.Add(action);
            bindings.Add(binding);
        }

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (!disposed && device != null) DeviceChanged?.Invoke(device.deviceId, change);
        }

        private void Reject(InputActionId action, double realtime, InputCaptureFailure failure)
        {
            CommandRejected?.Invoke(new InputCaptureRejection(action, realtime, failure));
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(UnityInputAdapter));
        }

        public void Dispose()
        {
            if (disposed) return;
            for (int i = bindings.Count - 1; i >= 0; i--) bindings[i].Dispose();
            bindings.Clear();
            boundActions.Clear();
            InputSystem.onDeviceChange -= OnDeviceChange;
            CommandCaptured = null;
            CommandRejected = null;
            DeviceChanged = null;
            disposed = true;
        }
    }
}
