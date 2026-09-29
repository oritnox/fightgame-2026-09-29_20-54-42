// /Assets/_Game/Tests/EditMode/UnityAdapters/LocalTraceRecorderTests.cs
// 공용코드 수정: F129 ring buffer bounded/local-only 회귀.
using NUnit.Framework;
using RP.Core.Foundation;
using RP.UnityRuntime.Diagnostics;

namespace RP.Tests.UnityAdapters
{
    public sealed class LocalTraceRecorderTests
    {
        [Test]
        public void SnapshotKeepsOldestToNewestWithinCapacity()
        {
            var recorder = new LocalTraceRecorder(16);
            for (int i = 0; i < 20; i++) recorder.Record(i, TraceKind.Timing, new ContentId("clock.sample"), i);
            LocalTraceEntry[] snapshot = recorder.Snapshot();
            Assert.That(snapshot.Length, Is.EqualTo(16));
            Assert.That(snapshot[0].Sequence, Is.EqualTo(5));
            Assert.That(snapshot[15].Sequence, Is.EqualTo(20));
        }

        [Test]
        public void ClearDropsEntriesButDoesNotReuseSequence()
        {
            var recorder = new LocalTraceRecorder(16);
            LocalTraceEntry first = recorder.Record(0, TraceKind.Input, new ContentId("input.test"));
            recorder.Clear();
            LocalTraceEntry next = recorder.Record(0, TraceKind.Input, new ContentId("input.test"));
            Assert.That(recorder.Count, Is.EqualTo(1));
            Assert.That(next.Sequence, Is.GreaterThan(first.Sequence));
        }

        [TestCase(0)]
        [TestCase(15)]
        [TestCase(65537)]
        public void InvalidCapacityIsRejected(int capacity)
        { Assert.Throws<System.ArgumentOutOfRangeException>(() => new LocalTraceRecorder(capacity)); }
    }
}
