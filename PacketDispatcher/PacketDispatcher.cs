using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityPatterns.PacketDispatcher
{
    /// <summary>
    /// 패킷 ID별 콜백을 동적으로 등록·해제·캐싱하는 이벤트 디스패처.
    ///
    /// 출처: ARGlass OPT / AXFactory / NavyECS
    /// 공통 문제: 수신 계층과 게임 로직 계층을 분리하되, 여러 컴포넌트가
    ///            같은 패킷을 독립적으로 구독할 수 있어야 함.
    ///
    /// 핵심 결정:
    ///   - Dictionary<TId, Action<byte[]>> 로 멀티캐스트 등록 (+=)
    ///   - 마지막 수신 페이로드를 캐싱해 늦게 구독한 컴포넌트도 즉시 조회 가능
    ///   - 수신(PacketReceiver) · 파싱(PacketDispatcher) · 로직(Handler) 3계층 분리
    /// </summary>
    public class PacketDispatcher<TPacketId> where TPacketId : Enum
    {
        private readonly Dictionary<TPacketId, Action<byte[]>> _handlers
            = new Dictionary<TPacketId, Action<byte[]>>();

        private readonly Dictionary<TPacketId, byte[]> _cachedPayloads
            = new Dictionary<TPacketId, byte[]>();

        // ── 등록 / 해제 ─────────────────────────────────────────────

        public void Register(TPacketId id, Action<byte[]> handler)
        {
            if (_handlers.ContainsKey(id))
                _handlers[id] += handler;
            else
                _handlers[id] = handler;
        }

        public void Unregister(TPacketId id, Action<byte[]> handler)
        {
            if (!_handlers.ContainsKey(id)) return;
            _handlers[id] -= handler;
            if (_handlers[id] == null)
                _handlers.Remove(id);
        }

        // ── 수신 · 디스패치 ─────────────────────────────────────────

        /// <summary>
        /// 수신된 패킷을 디스패치한다. 페이로드는 캐싱된다.
        /// 호출은 반드시 Unity 메인스레드에서 이뤄져야 함.
        /// (백그라운드 수신이라면 MainThreadDispatcher를 거쳐야 함.)
        /// </summary>
        public void Dispatch(TPacketId id, byte[] payload)
        {
            if (payload != null && payload.Length > 0)
                _cachedPayloads[id] = payload;

            if (_handlers.TryGetValue(id, out var handler))
                handler?.Invoke(payload);
            else
                Debug.LogWarning($"[PacketDispatcher] No handler registered for {id}");
        }

        // ── 캐시 조회 ────────────────────────────────────────────────

        /// <summary>마지막으로 수신된 페이로드를 반환. 없으면 null.</summary>
        public byte[] GetCached(TPacketId id)
            => _cachedPayloads.TryGetValue(id, out var cached) ? cached : null;

        public void ClearCache(TPacketId id) => _cachedPayloads.Remove(id);
        public void ClearAllCache()          => _cachedPayloads.Clear();
    }
}
