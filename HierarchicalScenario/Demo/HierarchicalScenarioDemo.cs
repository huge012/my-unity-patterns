using UnityEngine;
using UnityPatterns.HierarchicalScenario;

namespace UnityPatterns.HierarchicalScenario.Demo
{
    /// <summary>
    /// 조작 확인 퀘스트 예시 — 버튼을 누르면 완료.
    /// </summary>
    public class ConfirmQuest : QuestBase
    {
        [SerializeField] private UnityEngine.UI.Button _confirmButton;

        public override void QuestEnter()
        {
            base.QuestEnter();
            _confirmButton.onClick.AddListener(CompleteQuest);
            Debug.Log($"[Quest] {gameObject.name} 진입");
        }

        public override void QuestExit()
        {
            _confirmButton.onClick.RemoveListener(CompleteQuest);
            base.QuestExit();
            Debug.Log($"[Quest] {gameObject.name} 종료");
        }
    }

    /// <summary>
    /// 타이머 퀘스트 예시 — N초 후 자동 완료.
    /// </summary>
    public class TimerQuest : QuestBase
    {
        [SerializeField] private float _duration = 3f;
        private float _elapsed;

        public override void QuestEnter()
        {
            base.QuestEnter();
            _elapsed = 0f;
            Debug.Log($"[Quest] {gameObject.name} 진입 — {_duration}초 후 자동 완료");
        }

        private void Update()
        {
            if (!gameObject.activeSelf) return;
            _elapsed += Time.deltaTime;
            if (_elapsed >= _duration) CompleteQuest();
        }
    }

    /// <summary>
    /// ScenarioManager 사용 예시 — Inspector에서 챕터/퀘스트 GameObject를 계층으로 구성한다.
    ///
    /// 씬 구조 예시:
    ///   ScenarioRoot (ScenarioManager)
    ///     └─ Chapter_01 (ChapterBase)
    ///          ├─ Quest_Confirm (ConfirmQuest)
    ///          └─ Quest_Timer   (TimerQuest)
    ///     └─ Chapter_02 (ChapterBase)
    ///          └─ Quest_Confirm (ConfirmQuest)
    /// </summary>
    public class HierarchicalScenarioDemo : MonoBehaviour
    {
        [SerializeField] private ScenarioManager _scenarioManager;
        [SerializeField] private UnityEngine.UI.Button _startButton;

        private void Start()
        {
            _startButton.onClick.AddListener(() => _scenarioManager.EnterScenario());
        }
    }
}
