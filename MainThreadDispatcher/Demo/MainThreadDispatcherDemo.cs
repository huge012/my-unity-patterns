using System.Threading;
using UnityEngine;
using UnityPatterns.MainThreadDispatcher;

namespace UnityPatterns.MainThreadDispatcher.Demo
{
    /// <summary>
    /// Queue 방식과 Flag 방식을 비교하는 데모.
    /// </summary>
    public class MainThreadDispatcherDemo : MonoBehaviour
    {
        [Header("Queue 방식 — 여러 이벤트 / 페이로드 전달")]
        [SerializeField] private MainThreadQueue _queue;

        [Header("Flag 방식 — 단순 신호 전달")]
        [SerializeField] private NetworkNoticeFlag _flag;

        private void Start()
        {
            // 백그라운드 스레드 시뮬레이션 (실제로는 소켓 콜백 등)
            SimulateBackgroundThread();
        }

        private void SimulateBackgroundThread()
        {
            // Queue 방식: 여러 패킷 이벤트를 순서대로 메인스레드에 전달
            new Thread(() =>
            {
                Thread.Sleep(500);
                _queue.Enqueue(() => Debug.Log("[Queue] 패킷 수신 처리 — 메인스레드"));
                Thread.Sleep(100);
                _queue.Enqueue(() => Debug.Log("[Queue] UI 갱신 — 메인스레드"));
            }).Start();

            // Flag 방식: "연결 끊김" 신호만 전달
            new Thread(() =>
            {
                Thread.Sleep(1000);
                _flag.SetFlag("네트워크가 연결되지 않았습니다.");
            }).Start();
        }
    }

    // Flag 방식 구체 구현 예시
    public class NetworkNoticeFlag : MainThreadFlag
    {
        [SerializeField] private GameObject _noticePanel;

        protected override void OnFlagRaised(string message)
        {
            if (_noticePanel != null)
                _noticePanel.SetActive(true);
            Debug.Log($"[NetworkNotice] {message}");
        }
    }
}
