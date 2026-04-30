using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityPatterns.WatchdogCoroutine;

namespace UnityPatterns.WatchdogCoroutine.Demo
{
    public class WatchdogRunnerDemo : MonoBehaviour
    {
        [SerializeField] private WatchdogRunner _watchdog;
        [SerializeField] private Slider _progressSlider;
        [SerializeField] private Text  _statusText;

        private const float TimeoutSec = 10f;

        private void Start()
        {
            _watchdog.StartWatchdog(TimeoutSec, OnTimeout);
            StartCoroutine(SimulateDownload());
        }

        private IEnumerator SimulateDownload()
        {
            float progress = 0f;
            _statusText.text = "다운로드 중...";

            while (progress < 1f)
            {
                yield return new WaitForSeconds(0.5f);
                progress += 0.05f;

                // 50% 지점에서 5초 멈춤 (네트워크 단절 시뮬레이션)
                if (progress > 0.5f && progress < 0.55f)
                {
                    _statusText.text = "[시뮬레이션] 네트워크 단절 — Watchdog이 감시 중...";
                    yield return new WaitForSeconds(12f); // TimeoutSec 초과
                }

                _progressSlider.value = progress;
                _watchdog.HandleProgress(progress, OnTimeout); // Watchdog 타이머 리셋
            }

            _watchdog.StopWatchdog();
            _statusText.text = "다운로드 완료!";
        }

        private void OnTimeout()
        {
            _statusText.text = "⚠ 타임아웃 — 네트워크를 확인하세요. 복구 후 자동 재시작됩니다.";
            Debug.LogWarning("[Watchdog] 타임아웃 발동");
        }
    }
}
