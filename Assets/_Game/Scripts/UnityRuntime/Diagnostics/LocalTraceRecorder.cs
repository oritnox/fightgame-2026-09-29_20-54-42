// /Assets/_Game/Scripts/UnityRuntime/Diagnostics/LocalTraceRecorder.cs
// 공용코드 수정: F129 bounded local trace. 자동 업로드·무한 증가·민감정보 기록 금지.
using System;
using RP.Core.Foundation;

namespace RP.UnityRuntime.Diagnostics
{
    public enum TraceKind { Input, Timing, Action, Contact, Recovery, Diagnostic }

    public readonly struct LocalTraceEntry
    {
        public long Sequence { get; }
        public double CombatTime { get; }
        public TraceKind Kind { get; }
        public ContentId Code { get; }
        public long ValueA { get; }
        public long ValueB { get; }

        public LocalTraceEntry(long sequence, double combatTime, TraceKind kind, ContentId code, long valueA = 0, long valueB = 0)
        {
            if (sequence <= 0) throw new ArgumentOutOfRangeException(nameof(sequence));
            NumericGuard.NonNegative(combatTime, nameof(combatTime));
            if (!Enum.IsDefined(typeof(TraceKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (!code.IsValid) throw new ArgumentException("Trace code is required.", nameof(code));
            Sequence = sequence;
            CombatTime = combatTime;
            Kind = kind;
            Code = code;
            ValueA = valueA;
            ValueB = valueB;
        }
    }

    public sealed class LocalTraceRecorder
    {
        private readonly LocalTraceEntry[] entries;
        private int count;
        private int next;
        private long sequence;

        public int Capacity => entries.Length;
        public int Count => count;

        public LocalTraceRecorder(int capacity = 2048)
        {
            if (capacity < 16 || capacity > 65536) throw new ArgumentOutOfRangeException(nameof(capacity));
            entries = new LocalTraceEntry[capacity];
        }

        public LocalTraceEntry Record(double combatTime, TraceKind kind, ContentId code, long valueA = 0, long valueB = 0)
        {
            long id = checked(sequence + 1);
            sequence = id;
            var entry = new LocalTraceEntry(id, combatTime, kind, code, valueA, valueB);
            entries[next] = entry;
            next = (next + 1) % entries.Length;
            if (count < entries.Length) count++;
            return entry;
        }

        public LocalTraceEntry[] Snapshot()
        {
            var result = new LocalTraceEntry[count];
            int start = (next - count + entries.Length) % entries.Length;
            for (int i = 0; i < count; i++) result[i] = entries[(start + i) % entries.Length];
            return result;
        }

        public void Clear()
        {
            Array.Clear(entries, 0, entries.Length);
            count = 0;
            next = 0;
        }
    }
}
