using UnityEngine;

namespace RCCom.UI
{
    /// <summary>
    /// 오퍼레이터(카시아) 대사 전체를 모아두는 프로젝트 창 에셋. CardRoster/TowerRoster와 같은
    /// 이유로 SO화 — 텍스트뿐 아니라 상황별 초상화 스프라이트(Unity 에셋 참조)도 같이 담아야
    /// 해서 JSON 등 외부 파일로 분리하면 결국 절반은 여기로 다시 와야 함. 인스펙터에서 상황별로
    /// 대사 배열 + 스프라이트만 채우면 되는 순수 데이터 에셋.
    /// </summary>
    [CreateAssetMenu(menuName = "RCCom/UI/Operator Dialogue Set")]
    public class OperatorDialogueSet : ScriptableObject
    {
        [Header("로비 전신 스프라이트")]
        [Tooltip("로비에서 대사가 없을 때 Canvas/OperatorImage에 보여줄 기본 전신 스프라이트")]
        public Sprite lobbyIdleSprite;

        [Header("전투 포트레잇")]
        [Tooltip("전투 대사가 없을 때 왼쪽 상단에 보여줄 기본 포트레잇")]
        public Sprite idleSprite;

        [Header("0. 로비에서 오퍼레이터 클릭")]
        [Tooltip("비어 있으면 기존 게임 개시 대사를 임시로 사용한다.")]
        public OperatorLineSet lobbyInteraction;

        [Tooltip("전투에 참전한 오퍼레이터가 귀환 후 처음 클릭될 때 출력한다.")]
        public OperatorLineSet lobbyReturnTogether;

        [Tooltip("참전하지 않은 오퍼레이터가 귀환 후 처음 클릭될 때 출력한다.")]
        public OperatorLineSet lobbyReturn;

        [Header("0-1. 로비 터치 — 호감도 단계별")]
        public OperatorLineSet lobbyTouchUnfamiliar;
        public OperatorLineSet lobbyTouchFavorable;
        public OperatorLineSet lobbyTouchJoy;
        public OperatorLineSet lobbyTouchLove;

        [Tooltip("호감도 100에서만 사용하는 터치 대사")]
        public OperatorLineSet lobbyTouchEx;

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

        public void EnsureLineSets()
        {
            if (lobbyInteraction == null) { lobbyInteraction = new OperatorLineSet(); }
            if (lobbyReturnTogether == null) { lobbyReturnTogether = new OperatorLineSet(); }
            if (lobbyReturn == null) { lobbyReturn = new OperatorLineSet(); }
            if (lobbyTouchUnfamiliar == null) { lobbyTouchUnfamiliar = new OperatorLineSet(); }
            if (lobbyTouchFavorable == null) { lobbyTouchFavorable = new OperatorLineSet(); }
            if (lobbyTouchJoy == null) { lobbyTouchJoy = new OperatorLineSet(); }
            if (lobbyTouchLove == null) { lobbyTouchLove = new OperatorLineSet(); }
            if (lobbyTouchEx == null) { lobbyTouchEx = new OperatorLineSet(); }
            if (gameStart == null) { gameStart = new OperatorLineSet(); }
            if (skillUsed == null) { skillUsed = new OperatorLineSet(); }
            if (baseAttacked == null) { baseAttacked = new OperatorLineSet(); }
            if (playerHit == null) { playerHit = new OperatorLineSet(); }
            if (playerHitCritical == null) { playerHitCritical = new OperatorLineSet(); }
            if (insufficientGold == null) { insufficientGold = new OperatorLineSet(); }
            if (slotUnavailable == null) { slotUnavailable = new OperatorLineSet(); }
            if (playerDied == null) { playerDied = new OperatorLineSet(); }
            if (baseDestroyed == null) { baseDestroyed = new OperatorLineSet(); }
        }
    }
}
