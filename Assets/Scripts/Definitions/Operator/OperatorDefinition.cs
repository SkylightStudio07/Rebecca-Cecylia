using RCCom.Data;
using RCCom.Definitions.Card;
using RCCom.Definitions.Tower;
using RCCom.UI;
using UnityEngine;

namespace RCCom.Definitions.Operator
{
    /// <summary>
    /// 오퍼레이터 한 명의 플레이 스타일을 조립하는 최상위 SO.
    /// 플레이어 수치, 타워 풀, 카드 풀, 대사와 선택 화면 아트를 한 자산에서 참조하게 해서
    /// 신규 오퍼레이터 추가가 전투 코드 수정이 아니라 Definition 에셋 조립으로 끝나게 한다.
    ///
    /// 원격 여부와 다운로드 주소는 이 에셋을 내려받기 전에도 선택 화면이 알아야 하므로
    /// 여기에 넣지 않고 후속 OperatorCatalog가 별도 메타데이터로 관리한다.
    /// </summary>
    [CreateAssetMenu(menuName = "RCCom/Operator/Operator Definition")]
    public class OperatorDefinition : ScriptableObject
    {
        [Header("식별 및 선택 화면")]
        [Tooltip("저장 데이터와 Addressable 주소에서 사용하는 영구 식별자. 표시 이름과 분리해 이름 변경이 세이브를 깨뜨리지 않게 한다.")]
        public string operatorId;

        public string displayName;

        [TextArea(2, 4)]
        [Tooltip("선택 화면에서 이 오퍼레이터의 플레이 스타일을 설명하는 문구")]
        public string playStyleDescription;

        [Tooltip("선택 화면용 초상화. 전투 중 상황별 초상화는 dialogueSet이 담당한다.")]
        public Sprite selectionPortrait;

        [Header("플레이어 로드아웃")]
        [Tooltip("오퍼레이터 선택 시 적용할 플레이어 기본 수치. 런타임 적용 단계에서 반드시 복제해 카드 강화가 이 원본을 수정하지 않게 할 것.")]
        public PlayerData playerData = new();

        [Header("콘텐츠 풀")]
        [Tooltip("이 오퍼레이터가 처음부터 사용할 수 있는 타워 목록")]
        public TowerRoster towerRoster;

        [Tooltip("이 오퍼레이터의 레벨업 카드 후보 목록")]
        public CardRoster cardRoster;

        [Header("연출")]
        [Tooltip("게임 상황별 대사와 전투 중 초상화 묶음")]
        public OperatorDialogueSet dialogueSet;

        [Header("해금")]
        [Min(0)]
        [Tooltip("PlayerProfile.bestWave가 이 값 이상일 때 해금. 0이면 처음부터 사용 가능.")]
        public int requiredBestWave;
    }
}
