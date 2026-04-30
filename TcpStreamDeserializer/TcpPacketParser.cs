using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace UnityPatterns.TcpStreamDeserializer
{
    /// <summary>
    /// TCP 스트리밍 환경에서 부분수신된 바이트를 누적해
    /// 완전한 패킷이 구성될 때만 파싱하는 패턴.
    ///
    /// 출처: AXFactory (TCPPacket.cs)
    ///
    /// 문제:
    ///   TCP는 스트림 기반이라 한 번의 수신 콜백에 패킷이 잘려서 오거나
    ///   여러 패킷이 붙어서 올 수 있음. 무시하면 파싱 오류 발생.
    ///
    /// 해결:
    ///   - 수신 바이트를 내부 버퍼에 누적.
    ///   - TryDequeue()가 헤더 크기 이상 도달했을 때 전체 패킷 크기를 계산.
    ///   - 전체 패킷이 버퍼에 들어왔을 때만 파싱하고 버퍼에서 제거.
    ///   - 한 번의 콜백에 여러 패킷이 있으면 루프로 모두 처리.
    ///
    /// 바이트 순서: Big Endian (서버 ICD 표준 준수)
    /// </summary>
    public class TcpPacketParser
    {
        private readonly List<byte> _buffer = new List<byte>();

        public static int HeaderSize => Marshal.SizeOf<PacketHeader>();

        // 단일 패킷 페이로드 최대 크기 (1 MB).
        // 이 값을 초과하면 버퍼 전체를 버린다 — 무제한 할당 방지.
        public const uint MaxPayloadBytes = 1024 * 1024;

        // ── 수신 데이터 누적 ────────────────────────────────────────

        /// <summary>수신된 raw bytes를 버퍼에 추가.</summary>
        public void Feed(byte[] received) => _buffer.AddRange(received);

        /// <summary>
        /// 버퍼에서 완성된 패킷을 하나씩 추출.
        /// 여러 패킷이 있으면 반복 호출해 모두 처리.
        /// </summary>
        public bool TryDequeue(out ParsedPacket packet)
        {
            packet = default;
            if (_buffer.Count < HeaderSize) return false;

            // 헤더 파싱 (Big Endian)
            ushort messageId  = ReadUShortBE(0);
            ushort sourceId   = ReadUShortBE(2);
            ushort destId     = ReadUShortBE(4);
            ushort seqNumber  = ReadUShortBE(6);
            uint   payloadLen = ReadUIntBE(8);

            if (payloadLen > MaxPayloadBytes)
            {
                // 비정상 패킷 — 버퍼 전체 폐기 후 동기화 재시도
                Debug.LogWarning($"[TcpPacketParser] 비정상 페이로드 크기: {payloadLen} bytes. 버퍼 초기화.");
                _buffer.Clear();
                return false;
            }

            int totalSize = HeaderSize + (int)payloadLen;
            if (_buffer.Count < totalSize) return false; // 아직 전체 미도착

            byte[] payload = new byte[payloadLen];
            _buffer.CopyTo(HeaderSize, payload, 0, (int)payloadLen);
            _buffer.RemoveRange(0, totalSize);

            packet = new ParsedPacket
            {
                MessageId    = messageId,
                SourceId     = sourceId,
                DestinationId= destId,
                SequenceNumber = seqNumber,
                Payload      = payload,
            };
            return true;
        }

        // ── Big Endian 헬퍼 ─────────────────────────────────────────

        private ushort ReadUShortBE(int offset)
            => (ushort)((_buffer[offset] << 8) | _buffer[offset + 1]);

        private uint ReadUIntBE(int offset)
            => (uint)((_buffer[offset] << 24) | (_buffer[offset + 1] << 16)
                    | (_buffer[offset + 2] << 8) |  _buffer[offset + 3]);

        // ── 송신 헬퍼 (Big Endian 직렬화) ───────────────────────────

        /// <summary>패킷을 Big Endian으로 직렬화해 송신 가능한 byte[]로 반환.</summary>
        public static byte[] Serialize(ushort messageId, ushort sourceId,
                                       ushort destId, ushort seqNum, byte[] payload)
        {
            payload ??= Array.Empty<byte>();
            byte[] buf = new byte[HeaderSize + payload.Length];
            int offset = 0;

            WriteUShortBE(buf, ref offset, messageId);
            WriteUShortBE(buf, ref offset, sourceId);
            WriteUShortBE(buf, ref offset, destId);
            WriteUShortBE(buf, ref offset, seqNum);
            WriteUIntBE  (buf, ref offset, (uint)payload.Length);
            Array.Copy(payload, 0, buf, offset, payload.Length);

            return buf;
        }

        private static void WriteUShortBE(byte[] buf, ref int offset, ushort v)
        {
            buf[offset++] = (byte)((v >> 8) & 0xFF);
            buf[offset++] = (byte)( v       & 0xFF);
        }

        private static void WriteUIntBE(byte[] buf, ref int offset, uint v)
        {
            buf[offset++] = (byte)((v >> 24) & 0xFF);
            buf[offset++] = (byte)((v >> 16) & 0xFF);
            buf[offset++] = (byte)((v >>  8) & 0xFF);
            buf[offset++] = (byte)( v        & 0xFF);
        }
    }

    // ── 데이터 구조 ──────────────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PacketHeader
    {
        public ushort MessageId;
        public ushort SourceId;
        public ushort DestinationId;
        public ushort SequenceNumber;
        public uint   PayloadLength;
    }

    public struct ParsedPacket
    {
        public ushort MessageId;
        public ushort SourceId;
        public ushort DestinationId;
        public ushort SequenceNumber;
        public byte[] Payload;

        public string PayloadAsUtf8
            => Payload != null ? Encoding.UTF8.GetString(Payload) : string.Empty;
    }
}
