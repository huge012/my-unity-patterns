using System;
using UnityEngine;

namespace UnityPatterns.HierarchicalScenario
{
    /// <summary>
    /// 개별 퀘스트(작업 단계)의 추상 기반 클래스.
    ///
    /// 출처: AXFactory (AXQuestBase.cs)
    ///
    /// 설계 원칙:
    ///   - QuestEnter / QuestExit 를 virtual로 노출해 서브클래스가 override로 확장
    ///   - next / prev 콜백은 ChapterBase가 주입 — QuestBase는 "어떻게 이동하는지"를 모름
    ///   - gameObject 활성화는 기본 구현에 포함, 필요하면 override에서 super 호출 후 추가
    /// </summary>
    public abstract class QuestBase : MonoBehaviour
    {
        protected Action onNext;
        protected Action onPrev;

        /// <summary>ChapterBase에서 초기화 시 호출됨.</summary>
        public virtual void InitQuest(Action next, Action prev)
        {
            onNext = next;
            onPrev = prev;
        }

        /// <summary>퀘스트 진입 시 호출. override해서 진입 연출 등을 추가.</summary>
        public virtual void QuestEnter()
        {
            gameObject.SetActive(true);
        }

        /// <summary>퀘스트 종료 시 호출. override해서 정리 처리를 추가.</summary>
        public virtual void QuestExit()
        {
            gameObject.SetActive(false);
        }

        /// <summary>퀘스트 완료 조건을 만족했을 때 서브클래스에서 호출.</summary>
        protected void CompleteQuest() => onNext?.Invoke();

        /// <summary>이전 퀘스트로 돌아갈 때 서브클래스에서 호출.</summary>
        protected void GoToPrevQuest() => onPrev?.Invoke();
    }
}
