using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityPatterns.HierarchicalScenario
{
    /// <summary>
    /// 챕터(작업 묶음) 기반 클래스. 내부에 QuestBase 리스트를 보유하고 순서를 관리.
    ///
    /// 출처: AXFactory (AXChapterBase.cs)
    ///
    /// 설계 원칙:
    ///   - 챕터 완료 시 nextChapter 콜백으로 ScenarioManager에 자동 통지 (느슨한 결합)
    ///   - QuestBase에 콜백을 주입해 퀘스트가 직접 챕터 흐름을 제어하지 않도록 함
    ///   - PrevQuest()가 첫 퀘스트에서 호출되면 false를 반환 → 이전 챕터로 이동 결정은 ScenarioManager에 위임
    /// </summary>
    public class ChapterBase : MonoBehaviour
    {
        public QuestBase CurrentQuest { get; private set; }
        public int CurrentQuestIndex  { get; private set; }

        protected List<QuestBase> _quests = new List<QuestBase>();

        private Action _onNextChapter;
        private Action _onPrevChapter;

        // ── 초기화 ───────────────────────────────────────────────────

        public virtual void InitChapter(Action onNext, Action onPrev)
        {
            _onNextChapter = onNext;
            _onPrevChapter = onPrev;

            _quests.AddRange(GetComponentsInChildren<QuestBase>(true));
            foreach (var quest in _quests)
                quest.InitQuest(NextQuest, PrevFromQuest);

            foreach (var quest in _quests)
                quest.gameObject.SetActive(false);
        }

        // ── 챕터 진입 / 종료 ────────────────────────────────────────

        public virtual void OpenChapter()
        {
            CurrentQuestIndex = 0;
            if (_quests.Count == 0) { _onNextChapter?.Invoke(); return; }
            CurrentQuest = _quests[0];
            CurrentQuest.QuestEnter();
        }

        public virtual void CloseChapter()
        {
            CurrentQuest?.QuestExit();
            CurrentQuest = null;
        }

        public void ResetChapter()
        {
            CloseChapter();
            OpenChapter();
        }

        // ── 퀘스트 이동 ─────────────────────────────────────────────

        public void NextQuest()
        {
            if (CurrentQuestIndex + 1 < _quests.Count)
            {
                CurrentQuest.QuestExit();
                CurrentQuest = _quests[++CurrentQuestIndex];
                CurrentQuest.QuestEnter();
            }
            else
            {
                ResetQuests();
                _onNextChapter?.Invoke(); // 챕터 완료 → ScenarioManager 통지
            }
        }

        /// <returns>이전 퀘스트가 있으면 true, 첫 퀘스트였으면 false (이전 챕터로 이동 필요)</returns>
        public bool PrevQuest()
        {
            if (CurrentQuestIndex > 0)
            {
                CurrentQuest.QuestExit();
                CurrentQuest = _quests[--CurrentQuestIndex];
                CurrentQuest.QuestEnter();
                return true;
            }
            return false;
        }

        private void PrevFromQuest()
        {
            if (!PrevQuest())
                _onPrevChapter?.Invoke(); // 첫 퀘스트 → ScenarioManager에 이전 챕터 요청
        }

        private void ResetQuests()
        {
            foreach (var quest in _quests) quest.gameObject.SetActive(false);
            CurrentQuestIndex = 0;
            CurrentQuest = null;
        }
    }
}
