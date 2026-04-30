using System.Collections.Generic;
using UnityEngine;

namespace UnityPatterns.HierarchicalScenario
{
    /// <summary>
    /// 시나리오 전체 진행 상태를 관리하는 최상위 매니저.
    ///
    /// 출처: AXFactory (AXScenarioManager.cs)
    ///
    /// 계층 구조:
    ///   ScenarioManager (진행 상태 관리)
    ///     └─ ChapterBase (챕터 단위 작업 묶음)
    ///          └─ QuestBase (개별 작업 단계)
    ///
    /// 흐름:
    ///   EnterScenario() → Chapter[0].OpenChapter()
    ///   → Quest 완료 → NextQuest() → 챕터 완료 → NextChapter()
    ///   → 마지막 챕터 완료 → CompleteScenario()
    ///
    ///   PrevChapter(): 현재 챕터 첫 퀘스트면 이전 챕터로, 이미 첫 챕터면 리셋
    /// </summary>
    public class ScenarioManager : MonoBehaviour
    {
        public ChapterBase CurrentChapter { get; private set; }
        public bool IsRunning { get; private set; }

        private List<ChapterBase> _chapters = new List<ChapterBase>();
        private int _currentChapterIndex;

        private void Awake()
        {
            _chapters.AddRange(GetComponentsInChildren<ChapterBase>(true));
            InitChapters();
        }

        private void InitChapters()
        {
            for (int i = 0; i < _chapters.Count; i++)
                _chapters[i].InitChapter(NextChapter, PrevChapter);
        }

        // ── 시나리오 제어 ────────────────────────────────────────────

        public void EnterScenario()
        {
            if (IsRunning) return;
            IsRunning = true;
            _currentChapterIndex = 0;
            GoToChapter(0);
        }

        public void ExitScenario()
        {
            CurrentChapter?.CloseChapter();
            CurrentChapter = null;
            IsRunning = false;
        }

        // ── 챕터 이동 (ChapterBase 콜백에서 호출) ───────────────────

        private void NextChapter()
        {
            CurrentChapter?.CloseChapter();

            if (_currentChapterIndex + 1 < _chapters.Count)
                GoToChapter(++_currentChapterIndex);
            else
                CompleteScenario();
        }

        private void PrevChapter()
        {
            if (_currentChapterIndex > 0)
            {
                CurrentChapter?.CloseChapter();
                GoToChapter(--_currentChapterIndex);
            }
            else
            {
                CurrentChapter?.ResetChapter(); // 첫 챕터면 리셋
            }
        }

        private void GoToChapter(int index)
        {
            CurrentChapter = _chapters[index];
            CurrentChapter.OpenChapter();
        }

        private void CompleteScenario()
        {
            IsRunning = false;
            CurrentChapter = null;
            Debug.Log("[ScenarioManager] 시나리오 완료");
        }
    }
}
