// /Assets/_Game/Scripts/Core/Foundation/CommonTypes.cs
// 공용코드 수정: F001 값 타입·단위·동등성. 시간/입력/후속 전투 계약의 기반.
using System;
using System.Globalization;

namespace RP.Core.Foundation
{
    public enum ResultReason { None, InvalidInput, InvalidState, StaleEpoch, Duplicate, CapacityExceeded, Expired, NotReady }

    public readonly struct Float3 : IEquatable<Float3>
    {
        public readonly float X, Y, Z;
        public Float3(float x, float y, float z)
        {
            NumericGuard.Finite(x, nameof(x)); NumericGuard.Finite(y, nameof(y)); NumericGuard.Finite(z, nameof(z));
            X = x; Y = y; Z = z;
        }
        public static Float3 Zero => default;
        public double LengthSquared => (double)X * X + (double)Y * Y + (double)Z * Z;
        public Float3 Normalized()
        {
            double length = Math.Sqrt(LengthSquared);
            return length <= 1e-12 ? Zero : new Float3((float)(X / length), (float)(Y / length), (float)(Z / length));
        }
        public static Float3 operator +(Float3 a, Float3 b) => new Float3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Float3 operator -(Float3 a, Float3 b) => new Float3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Float3 operator *(Float3 a, float scale) => new Float3(a.X * scale, a.Y * scale, a.Z * scale);
        public static double Dot(Float3 a, Float3 b) => (double)a.X * b.X + (double)a.Y * b.Y + (double)a.Z * b.Z;
        public bool Equals(Float3 other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
        public override bool Equals(object obj) => obj is Float3 other && Equals(other);
        public override int GetHashCode() { unchecked { return (X.GetHashCode() * 397 ^ Y.GetHashCode()) * 397 ^ Z.GetHashCode(); } }
        public static bool operator ==(Float3 a, Float3 b) => a.Equals(b);
        public static bool operator !=(Float3 a, Float3 b) => !a.Equals(b);
        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "({0},{1},{2})", X, Y, Z);
    }

    public readonly struct EntityId : IEquatable<EntityId>, IComparable<EntityId>
    {
        public ulong Value { get; }
        public bool IsValid => Value != 0;
        public static EntityId None => default;
        public EntityId(ulong value) { if (value == 0) throw new ArgumentOutOfRangeException(nameof(value)); Value = value; }
        public bool Equals(EntityId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is EntityId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public int CompareTo(EntityId other) => Value.CompareTo(other.Value);
        public static bool operator ==(EntityId a, EntityId b) => a.Equals(b);
        public static bool operator !=(EntityId a, EntityId b) => !a.Equals(b);
        public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
    }

    public readonly struct RuntimeId : IEquatable<RuntimeId>, IComparable<RuntimeId>
    {
        public ulong Value { get; }
        public bool IsValid => Value != 0;
        public static RuntimeId None => default;
        public RuntimeId(ulong value) { if (value == 0) throw new ArgumentOutOfRangeException(nameof(value)); Value = value; }
        public bool Equals(RuntimeId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is RuntimeId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public int CompareTo(RuntimeId other) => Value.CompareTo(other.Value);
        public static bool operator ==(RuntimeId a, RuntimeId b) => a.Equals(b);
        public static bool operator !=(RuntimeId a, RuntimeId b) => !a.Equals(b);
        public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
    }

    public readonly struct ContentId : IEquatable<ContentId>, IComparable<ContentId>
    {
        private readonly string value;
        public string Value => value ?? string.Empty;
        public bool IsValid => value != null;
        public ContentId(string value)
        {
            if (!IsValidText(value)) throw new ArgumentException("Content ID requires 3-80 ASCII letters/digits/_.-.", nameof(value));
            this.value = value.ToLowerInvariant();
        }
        public static bool IsValidText(string text)
        {
            if (text == null || text.Length < 3 || text.Length > 80) return false;
            foreach (char c in text)
                if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '.' || c == '-')) return false;
            return true;
        }
        public bool Equals(ContentId other) => StringComparer.Ordinal.Equals(value, other.value);
        public override bool Equals(object obj) => obj is ContentId other && Equals(other);
        public override int GetHashCode()
        {
            // Stable hash for reproducible content indexing, not a cryptographic signature.
            unchecked { uint hash = 2166136261; foreach (char c in Value) hash = (hash ^ c) * 16777619; return (int)hash; }
        }
        public int CompareTo(ContentId other) => StringComparer.Ordinal.Compare(value, other.value);
        public static bool operator ==(ContentId a, ContentId b) => a.Equals(b);
        public static bool operator !=(ContentId a, ContentId b) => !a.Equals(b);
        public override string ToString() => Value;
    }

    public readonly struct TimeRange
    {
        public double Start { get; }
        public double End { get; }
        public double Duration => End - Start;
        public TimeRange(double start, double end)
        {
            NumericGuard.NonNegative(start, nameof(start)); NumericGuard.NonNegative(end, nameof(end));
            if (end < start) throw new ArgumentException("End precedes start.");
            Start = start; End = end;
        }
        public bool Contains(double time) { NumericGuard.Finite(time, nameof(time)); return time >= Start && time < End; }
        public bool Overlaps(TimeRange other) => Start < End && other.Start < other.End && Start < other.End && other.Start < End;
    }
}
