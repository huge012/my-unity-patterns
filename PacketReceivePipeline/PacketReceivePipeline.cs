using System;
using System.Collections.Generic;
using UnityEngine;
using UnityPatterns.MainThreadDispatcher;
using UnityPatterns.PacketDispatcher;
using UnityPatterns.TcpStreamDeserializer;

namespace UnityPatterns.PacketReceivePipeline
{
    /// <summary>
    /// TCP 수신부터 게임 로직 콜백까지의 전체 파이프라인.
    ///
    /// 출처: AXFactory + NavyECS + ARGlass Launcher V2 의 수신 구조를 통합한 패턴.
    ///   - AXFactory     : TCPPacketReceiver + PacketHandlerManager 의 조합
    ///   - NavyECS       : LoginNetManager / WCNetManager 의 Flag 패턴
    ///   - Launcher V2   : STT Worker Thread → UnityMainThreadDispatcher 구조
    ///
    /// 파이프라인 흐름:
    ///   [백그라운드 수신 스레드]
    ///     └─ Feed(bytes)                         // TcpConnectionManager.OnReceived 에서 호출
    ///          └─ TcpPacketParser.Feed()          // 버퍼 누적
    ///          └─ TryDequeue() 루프               // 완성된 패킷 추출
    ///          └─ MainThreadQueue.Enqueue()       // 메인스레드로 디스패치 예약
    ///
    ///   [메인스레드 Update()]
    ///     └─ MainThreadQueue 처리
    ///          └─ PacketDispatcher.Dispatch()     // ID 기반 콜백 발동 + 캐싱
    ///               └─ 등록된 핸들러 호출          // 게임 로직
    ///
    /// 이 클래스 하나로 위 흐름 전체를 캡슐화.
    /// 외부에서 필요한 것: Feed() 호출 / Register() / GetCached()
    /// </summary>
    public class PacketReceivePipeline<TPacketId> : MonoBehaviour
        where TPacketId : Enum
    {
        [SerializeField] private MainThreadQueue _mainThread;

        private readonly TcpPacketParser          _parser     = new TcpPacketParser();
        private readonly PacketDispatcher<TPacketId> _dispatcher = new PacketDispatcher<TPacketId>();

        // 수신 통계 (디버그/모니터링용)
        public int TotalReceived { get; private set; }
        public int TotalDispatched { get; private set; }

        private void Awake()
        {
            if (_mainThread == null)
                _mainThread = GetComponent<MainThreadQueue>()
                           ?? gameObject.AddComponent<MainThreadQueue>();
        }

        // ── 수신 진입점 (백그라운드 스레드에서 호출) ───────────────

        /// <summary>
        /// TCP 수신 콜백에서 직접 호출 (백그라운드 스레드 OK).
        /// 내부적으로 버퍼를 누적하고, 완성된 패킷을 메인스레드로 디스패치.
        /// </summary>
        public void Feed(byte[] received)
        {
            if (received == null || received.Length == 0) return;

            TotalReceived += received.Length;
            _parser.Feed(received);

            // 완성된 패킷을 모두 꺼내 메인스레드 큐에 적재
            while (_parser.TryDequeue(out var packet))
            {
                // 캡처: 로컬 복사 필수 (클로저 캡처 버그 방지)
                var capturedPacket = packet;
                _mainThread.Enqueue(() =>
                {
                    TotalDispatched++;
                    _dispatcher.Dispatch(
                        (TPacketId)(object)(int)capturedPacket.MessageId,
                        capturedPacket.Payload);
                });
            }
        }

        // ── 구독 관리 (메인스레드에서 호출) ────────────────────────

        public void Register(TPacketId id, Action<byte[]> handler)
            => _dispatcher.Register(id, handler);

        public void Unregister(TPacketId id, Action<byte[]> handler)
            => _dispatcher.Unregister(id, handler);

        // ── 캐시 조회 ────────────────────────────────────────────────

        /// <summary>
        /// 마지막으로 수신된 패킷의 페이로드를 반환.
        /// 늦게 구독한 컴포넌트가 초기 상태를 즉시 조회할 때 유용.
        /// </summary>
        public byte[] GetCached(TPacketId id) => _dispatcher.GetCached(id);

        // ── 진단 ─────────────────────────────────────────────────────

        [ContextMenu("수신 통계 출력")]
        public void PrintStats()
            => Debug.Log($"[Pipeline] 수신 {TotalReceived}bytes / 디스패치 {TotalDispatched}건");
    }

    // ── 구체 타입 바인딩 헬퍼 ────────────────────────────────────────

    /// <summary>
    /// ushort 기반 패킷 ID를 사용하는 프로젝트를 위한 구체 클래스.
    /// Inspector에서 직접 사용 가능 (제네릭 MonoBehaviour는 Inspector에 표시 안 됨).
    ///
    /// 사용 예:
    ///   public enum MyPacketId : int { Login = 0x1000, Chat = 0x1001 }
    ///   public class MyPipeline : PacketReceivePipeline<MyPacketId> { }
    /// </summary>
    public abstract class PacketReceivePipelineBase : PacketReceivePipeline<PacketIdInt> { }

    /// <summary>int 기반 패킷 ID 래퍼. 직접 사용하지 말고 프로젝트 enum을 정의할 것.</summary>
    public enum PacketIdInt { }
}
