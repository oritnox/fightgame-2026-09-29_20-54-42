// /Assets/_Game/Scripts/UnityRuntime/Input/UnityInputAdapter.cs
// 공용코드 수정: F084 Input System callback timestamp를 보존해 Core InputCommand로 변환. F086 시간 매퍼 사용.
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
    /// Converts Unity InputAction callbacks into immutable Core commands. It does not enable or disable caller-owned actions.
    /// Button bindings capture started/canceled so Hold interactions do not move the original press timestamp to performed time.
    /// </summary>
    public sealed class UnityInputAdapter : IDisposable
    {
        private sealed class Binding : IDisposable
        {
            private readonly InputAction action;
            private readonly Action<InputAction.CallbackContext> pressOrValue;
            private readonly Action<InputAction.CallbackContext> release;
            private readonly bool vector;

            public Binding(InputAction action, Action<InputAction.CallbackContext> pressOrValue,
                Action<InputAction.CallbackContext> release, bool vector)
            {
                this.action = action;
                this.pressOrValue = pressOrValue;
                this.release = release;
                this.vector = vector;
                if (vector) action.performed += pressOrValue;
                else action.started += pressOrValue;
                action.canceled += release;
            }

            public void Dispose()
            {
                if (vector) action.performed -= pressOrValue;
                else action.started -= pressOrValue;
                action.canceled -= release;
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
            ValidateBinding(action, actionId);
            Action<InputAction.CallbackContext> press = ctx => CaptureContext(ctx, actionId, InputPhase.Pressed, InputVectorPlane.XY, false);
            Action<InputAction.CallbackContext> release = ctx => CaptureContext(ctx, actionId, InputPhase.Released, InputVectorPlane.XY, false);
            AddBinding(action, new Binding(action, press, release, false));
        }

        public void BindVector2(InputAction action, InputActionId actionId, InputVectorPlane plane)
        {
            ValidateBinding(action, actionId);
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
            sequence = next;
            var command = new InputCommand(next, deviceId, mapping.Epoch, actionId, realtime,
                mapping.MechanicTime, mapping.JudgedSongTime, direction, phase, InputOrigin.Hardware);
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

        private void ValidateBinding(InputAction action, InputActionId actionId)
        {
            ThrowIfDisposed();
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (actionId == InputActionId.None || !Enum.IsDefined(typeof(InputActionId), actionId))
                throw new ArgumentOutOfRangeException(nameof(actionId));
            if (boundActions.Contains(action)) throw new InvalidOperationException("InputAction is already bound to this adapter.");
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
