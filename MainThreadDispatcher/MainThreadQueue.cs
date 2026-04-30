using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityPatterns.MainThreadDispatcher
{
    /// <summary>
    /// 백그라운드 스레드(소켓 수신, Worker Thread 콜백 등)에서
    /// Unity 메인스레드로 Action을 안전하게 전달하는 큐 기반 디스패처.
    ///
    /// 출처: AXFactory (Netly 백그라운드 콜백), ARGlass Launcher V2 (STT Worker Thread)
    ///
    /// 사용 시나리오:
    ///   - Netly / 외부 네트워크 라이브러리가 백그라운드 스레드에서 콜백을 발동할 때
    ///   - STT 음성인식 콜백이 Worker Thread에서 도달할 때
    ///   - Unity API (UI 조작, GameObject 생성 등)는 메인스레드에서만 호출 가능하다는
    ///     제약을 구조적으로 해결.
    /// </summary>
    public class MainThreadQueue : MonoBehaviour
    {
        private readonly Queue<Action> _queue = new Queue<Action>();
        private readonly object _lock = new object();

        /// <summary>
        /// 백그라운드 스레드에서 호출.
        /// Action은 다음 Update()에서 메인스레드로 실행됨.
        /// </summary>
        public void Enqueue(Action action)
        {
            if (action == null) return;
            lock (_lock)
                _queue.Enqueue(action);
        }

        private void Update()
        {
            lock (_lock)
            {
                while (_queue.Count > 0)
                    _queue.Dequeue()?.Invoke();
            }
        }
    }
}
