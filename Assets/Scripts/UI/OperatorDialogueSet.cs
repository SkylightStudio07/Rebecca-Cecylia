using System;
using UnityEngine;

namespace RCCom.UI
{
    /// <summary>
    /// 상황 1개에 대응하는 대사 후보 + 그때 보여줄 초상화. ScriptableObject가 아니라 그냥
    /// 직렬화되는 데이터 묶음 — OperatorDialogueSet 하나가 이 묶음을 상황별로 여러 개 가진다.
    /// </summary>
    [Serializable]
    public class OperatorLineSet
    {
        public Sprite portraitSprite;

        [TextArea(2, 4)]
        public string[] lines;
    }

    /// <summary>
    /// 오퍼레이터(카시아) 대사 전체를 모아두는 프로젝트 창 에셋. CardRoster/TowerRoster와 같은
    /// 이유로 SO화 — 텍스트뿐 아니라 상황별 초상화 스프라이트(Unity 에셋 참조)도 같이 담아야
    /// 해서 JSON 등 외부 파일로 분리하면 결국 절반은 여기로 다시 와야 함. 인스펙터에서 상황별로
    /// 대사 배열 + 스프라이트만 채우면 되는 순수 데이터 에셋.
    /// </summary>
    [CreateAssetMenu(menuName = "RCCom/UI/Operator Dialogue Set")]
    public class OperatorDialogueSet : ScriptableObject
    {
        [Tooltip("대사가 없을 때(평소) 보여줄 기본 초상화")]
        public Sprite idleSprite;

        [Header("0. 로비에서 오퍼레이터 클릭")]
        [Tooltip("비어 있으면 기존 게임 개시 대사를 임시로 사용한다.")]
        public OperatorLineSet lobbyInteraction;

        [Header("1. 게임 개시")]
        public OperatorLineSet gameStart;

        [Header("2. 스킬(오버드라이브 모드) 사용")]
        public OperatorLineSet skillUsed;

        [Header("3. 거점 피격")]
        public OperatorLineSet baseAttacked;

        [Header("4. 플레이어 피격 (일반)")]
        public OperatorLineSet playerHit;

        [Header("4-1. 플레이어 피격 (체력 30% 이하일 때)")]
        public OperatorLineSet playerHitCritical;

        [Header("5. 건설 실패 — 골드 부족")]
        public OperatorLineSet insufficientGold;

        [Header("6. 건설 실패 — 슬롯 부족")]
        public OperatorLineSet slotUnavailable;

        [Header("7. 플레이어 사망")]
        public OperatorLineSet playerDied;

        [Header("8. 거점 파괴")]
        public OperatorLineSet baseDestroyed;
    }
}
