using System;
using System.Text;
using NUnit.Framework;
using UnityPatterns.TcpStreamDeserializer;

namespace UnityPatterns.Tests
{
    [TestFixture]
    public class TcpPacketParserTests
    {
        private TcpPacketParser _parser;

        [SetUp]
        public void SetUp()
        {
            _parser = new TcpPacketParser();
        }

        // Serialize()를 통해 올바른 Big Endian 패킷을 만드는 헬퍼
        private static byte[] MakePacket(ushort msgId, ushort srcId, ushort dstId,
                                         ushort seq, byte[] payload = null)
            => TcpPacketParser.Serialize(msgId, srcId, dstId, seq,
                                         payload ?? Array.Empty<byte>());

        // ── 기본 동작 ────────────────────────────────────────────────

        [Test]
        public void TryDequeue_EmptyBuffer_ReturnsFalse()
        {
            Assert.IsFalse(_parser.TryDequeue(out _));
        }

        [Test]
        public void TryDequeue_CompletePacket_ReturnsTrueWithAllFields()
        {
            var payload = new byte[] { 0x01, 0x02, 0x03 };
            _parser.Feed(MakePacket(0x0010, 0x0001, 0x0002, 0x0005, payload));

            bool result = _parser.TryDequeue(out var packet);

            Assert.IsTrue(result);
            Assert.AreEqual(0x0010, packet.MessageId);
            Assert.AreEqual(0x0001, packet.SourceId);
            Assert.AreEqual(0x0002, packet.DestinationId);
            Assert.AreEqual(0x0005, packet.SequenceNumber);
            CollectionAssert.AreEqual(payload, packet.Payload);
        }

        [Test]
        public void TryDequeue_ZeroLengthPayload_ReturnsTrueWithEmptyPayload()
        {
            _parser.Feed(MakePacket(0x0001, 0, 0, 0));

            Assert.IsTrue(_parser.TryDequeue(out var packet));
            Assert.IsNotNull(packet.Payload);
            Assert.AreEqual(0, packet.Payload.Length);
        }

        // ── 부분 수신 ────────────────────────────────────────────────

        [Test]
        public void TryDequeue_PartialHeader_ReturnsFalse()
        {
            // HeaderSize(12) 미만 — 필드 파싱조차 시작 불가
            _parser.Feed(new byte[] { 0x00, 0x01, 0x00, 0x02 });

            Assert.IsFalse(_parser.TryDequeue(out _));
        }

        [Test]
        public void TryDequeue_CompleteHeaderPartialPayload_ReturnsFalse()
        {
            byte[] full = MakePacket(1, 0, 0, 0, new byte[] { 1, 2, 3, 4, 5 });
            // 마지막 1바이트를 제외하고 Feed
            byte[] partial = new byte[full.Length - 1];
            Array.Copy(full, partial, partial.Length);

            _parser.Feed(partial);

            Assert.IsFalse(_parser.TryDequeue(out _));
        }

        [Test]
        public void TryDequeue_PacketFedOneByteAtATime_FalseUntilLastByte()
        {
            var payload = new byte[] { 0x11, 0x22, 0x33 };
            byte[] full = MakePacket(0x0099, 0, 0, 0, payload);

            // 마지막 바이트 직전까지 1바이트씩 — 매번 false
            for (int i = 0; i < full.Length - 1; i++)
            {
                _parser.Feed(new[] { full[i] });
                Assert.IsFalse(_parser.TryDequeue(out _),
                    $"byte[{i}] 공급 후 미완성 패킷에서 true 반환됨");
            }

            // 마지막 바이트 공급 → 완성
            _parser.Feed(new[] { full[full.Length - 1] });
            Assert.IsTrue(_parser.TryDequeue(out var packet));
            CollectionAssert.AreEqual(payload, packet.Payload);
        }

        // ── 연속 패킷 ────────────────────────────────────────────────

        [Test]
        public void TryDequeue_TwoPacketsFedAtOnce_BothExtractedInOrder()
        {
            byte[] p1 = MakePacket(0x0001, 0, 0, 0, new byte[] { 0xAA });
            byte[] p2 = MakePacket(0x0002, 0, 0, 0, new byte[] { 0xBB });

            byte[] combined = new byte[p1.Length + p2.Length];
            Array.Copy(p1, 0, combined, 0,          p1.Length);
            Array.Copy(p2, 0, combined, p1.Length,  p2.Length);

            _parser.Feed(combined);

            Assert.IsTrue(_parser.TryDequeue(out var first));
            Assert.AreEqual(0x0001, first.MessageId);

            Assert.IsTrue(_parser.TryDequeue(out var second));
            Assert.AreEqual(0x0002, second.MessageId);

            Assert.IsFalse(_parser.TryDequeue(out _)); // 버퍼 비워짐
        }

        // ── 비정상 패킷 방어 ─────────────────────────────────────────

        [Test]
        public void TryDequeue_OversizedPayload_ClearsBufferReturnsFalse()
        {
            // PayloadLength = MaxPayloadBytes + 1 을 Big Endian으로 수동 조작
            byte[] bad = new byte[TcpPacketParser.HeaderSize];
            uint oversized = TcpPacketParser.MaxPayloadBytes + 1; // 0x00_10_00_01
            bad[8]  = (byte)((oversized >> 24) & 0xFF);
            bad[9]  = (byte)((oversized >> 16) & 0xFF);
            bad[10] = (byte)((oversized >>  8) & 0xFF);
            bad[11] = (byte)( oversized        & 0xFF);

            _parser.Feed(bad);

            Assert.IsFalse(_parser.TryDequeue(out _));

            // 버퍼가 초기화됐으므로 이후 정상 패킷을 즉시 처리할 수 있어야 한다
            _parser.Feed(MakePacket(0x0002, 0, 0, 0, new byte[] { 0xFF }));
            Assert.IsTrue(_parser.TryDequeue(out var recovery));
            Assert.AreEqual(0x0002, recovery.MessageId);
        }

        // ── 직렬화 / 역직렬화 왕복 ──────────────────────────────────

        [Test]
        public void Serialize_ThenFeedAndDequeue_RoundTrip()
        {
            var original = new byte[] { 10, 20, 30, 40, 50 };
            byte[] raw = TcpPacketParser.Serialize(0x00FF, 0x0001, 0x0002, 0x0003, original);

            _parser.Feed(raw);
            Assert.IsTrue(_parser.TryDequeue(out var packet));

            Assert.AreEqual(0x00FF, packet.MessageId);
            Assert.AreEqual(0x0001, packet.SourceId);
            Assert.AreEqual(0x0002, packet.DestinationId);
            Assert.AreEqual(0x0003, packet.SequenceNumber);
            CollectionAssert.AreEqual(original, packet.Payload);
        }

        [Test]
        public void PayloadAsUtf8_ReturnsCorrectString()
        {
            const string text = "hello";
            _parser.Feed(MakePacket(1, 0, 0, 0, Encoding.UTF8.GetBytes(text)));
            _parser.TryDequeue(out var packet);

            Assert.AreEqual(text, packet.PayloadAsUtf8);
        }
    }
}
