using System.Text;
using UnityEngine;
using UnityPatterns.TcpStreamDeserializer;

namespace UnityPatterns.TcpStreamDeserializer.Demo
{
    /// <summary>
    /// TCP 스트리밍 부분수신 상황을 시뮬레이션하는 데모.
    ///
    /// 시나리오:
    ///   1. 패킷 하나를 두 번에 나눠 수신 (부분수신 대응 확인)
    ///   2. 두 패킷이 이어붙어 수신 (연속 패킷 분리 확인)
    /// </summary>
    public class TcpStreamDeserializerDemo : MonoBehaviour
    {
        private TcpPacketParser _parser;

        private void Start()
        {
            _parser = new TcpPacketParser();
            RunDemo();
        }

        private void RunDemo()
        {
            // ── 시나리오 1: 패킷 하나가 두 번에 나눠 도착 ──────────
            Debug.Log("=== 시나리오 1: 부분수신 ===");

            byte[] payload1 = Encoding.UTF8.GetBytes("Hello, Server!");
            byte[] fullPacket = TcpPacketParser.Serialize(0x1000, 0x01, 0x02, 1, payload1);

            // 앞 절반만 먼저 수신
            byte[] half1 = new byte[fullPacket.Length / 2];
            byte[] half2 = new byte[fullPacket.Length - half1.Length];
            System.Array.Copy(fullPacket, 0,            half1, 0, half1.Length);
            System.Array.Copy(fullPacket, half1.Length, half2, 0, half2.Length);

            _parser.Feed(half1);
            if (_parser.TryDequeue(out _))
                Debug.LogError("버그: 절반만 왔는데 파싱됨");
            else
                Debug.Log("[OK] 절반 수신 → 파싱 보류");

            _parser.Feed(half2);
            if (_parser.TryDequeue(out var parsed1))
                Debug.Log($"[OK] 완성 후 파싱 성공: MessageId=0x{parsed1.MessageId:X4}, payload='{parsed1.PayloadAsUtf8}'");

            // ── 시나리오 2: 두 패킷이 연속으로 도착 ────────────────
            Debug.Log("=== 시나리오 2: 연속 패킷 ===");

            byte[] p2 = TcpPacketParser.Serialize(0x2000, 0x01, 0x02, 2, Encoding.UTF8.GetBytes("Packet A"));
            byte[] p3 = TcpPacketParser.Serialize(0x2001, 0x01, 0x02, 3, Encoding.UTF8.GetBytes("Packet B"));

            byte[] combined = new byte[p2.Length + p3.Length];
            System.Array.Copy(p2, 0,         combined, 0,         p2.Length);
            System.Array.Copy(p3, 0,         combined, p2.Length, p3.Length);

            _parser.Feed(combined);

            int count = 0;
            while (_parser.TryDequeue(out var p))
            {
                count++;
                Debug.Log($"[OK] 패킷 {count}: MessageId=0x{p.MessageId:X4}, payload='{p.PayloadAsUtf8}'");
            }
            Debug.Log($"연속 수신 총 {count}개 파싱 완료");
        }
    }
}
