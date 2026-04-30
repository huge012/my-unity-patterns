using UnityEngine;

namespace UnityPatterns.MainThreadDispatcher
{
    /// <summary>
    /// 단일 이벤트를 bool 플래그로 메인스레드에 전달하는 경량 패턴.
    ///
    /// 출처: NavyECS (소켓 수신 콜백 → UI 갱신)
    ///
    /// Queue 패턴 대비 장점:
    ///   - 할당 없음 (Action 박싱 비용 없음)
    ///   - 동일 이벤트가 프레임 내 여러 번 발생해도 중복 처리 없음
    ///   - 단순한 "신호" 전달에 적합
    ///
    /// 단점:
    ///   - 여러 종류의 이벤트를 처리하려면 Flag가 늘어남
    ///   - 페이로드 전달이 필요하면 별도 필드가 필요함 → 그 경우 Queue 패턴 권장
    /// </summary>
    public class MainThreadFlag : MonoBehaviour
    {
        // 백그라운드 스레드에서 세운다
        public volatile bool UpdateFlag = false;

        // 페이로드가 있다면 함께 저장 (volatile은 참조 타입에 직접 적용 안 됨 — lock 사용 권장)
        private readonly object _payloadLock = new object();
        private string _pendingMessage;

        /// <summary>백그라운드 스레드에서 호출: 플래그를 세우고 페이로드를 저장.</summary>
        public void SetFlag(string message = null)
        {
            lock (_payloadLock)
                _pendingMessage = message;
            UpdateFlag = true;
        }

        private void Update()
        {
            if (!UpdateFlag) return;
            UpdateFlag = false;

            string message;
            lock (_payloadLock)
                message = _pendingMessage;

            OnFlagRaised(message);
        }

        /// <summary>메인스레드에서 실행됨. 오버라이드해서 사용.</summary>
        protected virtual void OnFlagRaised(string message)
        {
            UnityEngine.Debug.Log($"[MainThreadFlag] raised: {message}");
        }
    }
}
