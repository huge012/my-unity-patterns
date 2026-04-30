using System;
using NUnit.Framework;
using UnityPatterns.PacketDispatcher;

namespace UnityPatterns.Tests
{
    [TestFixture]
    public class PacketDispatcherTests
    {
        private enum TestId { Alpha, Beta, Gamma }

        private PacketDispatcher<TestId> _dispatcher;

        [SetUp]
        public void SetUp()
        {
            _dispatcher = new PacketDispatcher<TestId>();
        }

        // ── 등록 / 디스패치 ─────────────────────────────────────────

        [Test]
        public void Dispatch_RegisteredHandler_InvokedWithCorrectPayload()
        {
            byte[] received = null;
            _dispatcher.Register(TestId.Alpha, p => received = p);

            var payload = new byte[] { 1, 2, 3 };
            _dispatcher.Dispatch(TestId.Alpha, payload);

            Assert.AreEqual(payload, received);
        }

        [Test]
        public void Dispatch_MultipleHandlersSameId_AllInvoked()
        {
            int callCount = 0;
            _dispatcher.Register(TestId.Alpha, _ => callCount++);
            _dispatcher.Register(TestId.Alpha, _ => callCount++);

            _dispatcher.Dispatch(TestId.Alpha, new byte[] { 1 });

            Assert.AreEqual(2, callCount);
        }

        [Test]
        public void Dispatch_DifferentIds_OnlyMatchingHandlerInvoked()
        {
            bool alphaFired = false;
            bool betaFired  = false;
            _dispatcher.Register(TestId.Alpha, _ => alphaFired = true);
            _dispatcher.Register(TestId.Beta,  _ => betaFired  = true);

            _dispatcher.Dispatch(TestId.Alpha, new byte[] { 1 });

            Assert.IsTrue(alphaFired);
            Assert.IsFalse(betaFired);
        }

        [Test]
        public void Dispatch_NoHandlerRegistered_DoesNotThrow()
        {
            // 핸들러 없을 때 Debug.LogWarning을 출력하지만 예외는 던지지 않아야 한다
            Assert.DoesNotThrow(() =>
                _dispatcher.Dispatch(TestId.Beta, new byte[] { 1 }));
        }

        // ── 해제 ────────────────────────────────────────────────────

        [Test]
        public void Unregister_UnregisteredHandler_NotInvoked()
        {
            int callCount = 0;
            Action<byte[]> handler = _ => callCount++;

            _dispatcher.Register(TestId.Alpha, handler);
            _dispatcher.Unregister(TestId.Alpha, handler);
            _dispatcher.Dispatch(TestId.Alpha, new byte[] { 1 });

            Assert.AreEqual(0, callCount);
        }

        [Test]
        public void Unregister_OneOfTwoHandlers_OtherStillInvoked()
        {
            int callCount = 0;
            Action<byte[]> toRemove = _ => callCount += 10;
            _dispatcher.Register(TestId.Alpha, toRemove);
            _dispatcher.Register(TestId.Alpha, _ => callCount++);

            _dispatcher.Unregister(TestId.Alpha, toRemove);
            _dispatcher.Dispatch(TestId.Alpha, new byte[] { 1 });

            Assert.AreEqual(1, callCount);
        }

        // ── 캐시 ────────────────────────────────────────────────────

        [Test]
        public void GetCached_BeforeAnyDispatch_ReturnsNull()
        {
            Assert.IsNull(_dispatcher.GetCached(TestId.Alpha));
        }

        [Test]
        public void GetCached_AfterDispatch_ReturnsPayload()
        {
            var payload = new byte[] { 7, 8, 9 };
            _dispatcher.Register(TestId.Alpha, _ => { });
            _dispatcher.Dispatch(TestId.Alpha, payload);

            Assert.AreEqual(payload, _dispatcher.GetCached(TestId.Alpha));
        }

        [Test]
        public void GetCached_AfterMultipleDispatches_ReturnsLatest()
        {
            var first  = new byte[] { 1 };
            var second = new byte[] { 2 };
            _dispatcher.Register(TestId.Alpha, _ => { });
            _dispatcher.Dispatch(TestId.Alpha, first);
            _dispatcher.Dispatch(TestId.Alpha, second);

            Assert.AreEqual(second, _dispatcher.GetCached(TestId.Alpha));
        }

        [Test]
        public void ClearCache_RemovesCachedPayloadForId()
        {
            _dispatcher.Register(TestId.Alpha, _ => { });
            _dispatcher.Dispatch(TestId.Alpha, new byte[] { 1 });

            _dispatcher.ClearCache(TestId.Alpha);

            Assert.IsNull(_dispatcher.GetCached(TestId.Alpha));
        }

        [Test]
        public void ClearAllCache_RemovesAllCachedPayloads()
        {
            _dispatcher.Register(TestId.Alpha, _ => { });
            _dispatcher.Register(TestId.Beta,  _ => { });
            _dispatcher.Dispatch(TestId.Alpha, new byte[] { 1 });
            _dispatcher.Dispatch(TestId.Beta,  new byte[] { 2 });

            _dispatcher.ClearAllCache();

            Assert.IsNull(_dispatcher.GetCached(TestId.Alpha));
            Assert.IsNull(_dispatcher.GetCached(TestId.Beta));
        }
    }
}
