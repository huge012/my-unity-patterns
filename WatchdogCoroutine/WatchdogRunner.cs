using System;
using System.Collections;
using UnityEngine;

namespace UnityPatterns.WatchdogCoroutine
{
    /// <summary>
    /// 진행률(또는 임의의 값)이 일정 시간 동안 변화하지 않으면
    /// 타임아웃 콜백을 자동 발동하는 Watchdog 패턴.
    ///
    /// 출처: ARGlass Launcher V2 (OTAController.cs)
    ///
    /// 문제:
    ///   OTA 업데이트 중 네트워크 단절 시 진행률이 멈추지만 코루틴은 계속 실행된다.
    ///   타임아웃이 없으면 무한 대기 상태에 빠짐.
    ///
    /// 해결:
    ///   진행률 갱신 시마다 lastProgressTime을 리셋.
    ///   Watchdog이 1초마다 경과 시간을 체크해 임계값 초과 시 콜백 발동.
    ///   복구 후 HandleProgress()가 다시 호출되면 Watchdog이 자동 재기동.
    ///
    /// 사용 방법:
    ///   1. StartWatchdog(timeout, onTimeout) — 감시 시작
    ///   2. HandleProgress(currentValue) — 진행률 갱신 시마다 호출 (타이머 리셋)
    ///   3. StopWatchdog() — 정상 완료 시 호출
    /// </summary>
    public class WatchdogRunner : MonoBehaviour
    {
        private float _lastProgressTime;
        private float _lastProgressValue = -1f;
        private float _timeoutSeconds;
        private Action _onTimeout;
        private Coroutine _watchdog;

        public bool IsRunning => _watchdog != null;

        // ── 외부 인터페이스 ─────────────────────────────────────────

        public void StartWatchdog(float timeoutSeconds, Action onTimeout)
        {
            _timeoutSeconds   = timeoutSeconds;
            _onTimeout        = onTimeout;
            _lastProgressTime = Time.time;
            _lastProgressValue = -1f;

            if (_watchdog != null) StopCoroutine(_watchdog);
            _watchdog = StartCoroutine(WatchdogLoop());
        }

        /// <summary>
        /// 진행률이 갱신될 때마다 호출.
        /// 값이 바뀌었으면 타이머를 리셋.
        /// Watchdog이 꺼져 있었다면 자동 재기동 (복구 후 재시작 시나리오).
        /// </summary>
        public void HandleProgress(float currentValue, Action onTimeout = null)
        {
            if (Math.Abs(currentValue - _lastProgressValue) > float.Epsilon)
            {
                _lastProgressValue = currentValue;
                _lastProgressTime  = Time.time;
            }

            // Watchdog이 꺼져 있으면 자동 재기동 (타임아웃 후 복구 시나리오)
            if (_watchdog == null && onTimeout != null)
                StartWatchdog(_timeoutSeconds, onTimeout);
        }

        public void StopWatchdog()
        {
            if (_watchdog == null) return;
            StopCoroutine(_watchdog);
            _watchdog = null;
        }

        // ── 내부 루프 ────────────────────────────────────────────────

        private IEnumerator WatchdogLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(1f);

                if (Time.time - _lastProgressTime > _timeoutSeconds)
                {
                    _watchdog = null;
                    _onTimeout?.Invoke();
                    yield break;
                }
            }
        }
    }
}
