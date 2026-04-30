using System;
using UnityEngine;
using UnityPatterns.PacketDispatcher;

namespace UnityPatterns.PacketDispatcher.Demo
{
    // 사용 예시용 패킷 ID 열거형
    public enum DemoPacketId
    {
        Login      = 0x1000,
        ChatMessage= 0x1001,
        PlayerMove = 0x1002,
    }

    /// <summary>
    /// PacketDispatcher 사용 예시.
    ///
    /// 실제 사용 패턴:
    ///   1. 수신 계층(TCP 소켓)에서 raw bytes 수신
    ///   2. PacketDispatcher.Dispatch() 호출 (메인스레드에서)
    ///   3. 각 게임 로직 컴포넌트가 Register/Unregister로 독립 구독
    /// </summary>
    public class PacketDispatcherDemo : MonoBehaviour
    {
        private PacketDispatcher<DemoPacketId> _dispatcher;

        private void Awake()
        {
            _dispatcher = new PacketDispatcher<DemoPacketId>();
        }

        private void Start()
        {
            // 여러 컴포넌트가 같은 패킷 ID를 독립적으로 구독
            _dispatcher.Register(DemoPacketId.Login,       OnLogin);
            _dispatcher.Register(DemoPacketId.ChatMessage, OnChat);
            _dispatcher.Register(DemoPacketId.ChatMessage, OnChatLogger); // 멀티캐스트
        }

        private void OnDestroy()
        {
            _dispatcher.Unregister(DemoPacketId.Login,       OnLogin);
            _dispatcher.Unregister(DemoPacketId.ChatMessage, OnChat);
            _dispatcher.Unregister(DemoPacketId.ChatMessage, OnChatLogger);
        }

        // 수신 계층에서 호출 (메인스레드 보장 필요)
        public void SimulateReceive(DemoPacketId id, byte[] payload)
            => _dispatcher.Dispatch(id, payload);

        private void OnLogin(byte[] payload)
            => Debug.Log($"[Login] received {payload?.Length} bytes");

        private void OnChat(byte[] payload)
            => Debug.Log($"[Chat] {System.Text.Encoding.UTF8.GetString(payload ?? Array.Empty<byte>())}");

        private void OnChatLogger(byte[] payload)
            => Debug.Log($"[ChatLogger] archived {payload?.Length} bytes");
    }
}
