using RCCom.Data;
using RCCom.Definitions.Tower;
using RCCom.Managers;
using RCCom.Runtime;
using UnityEngine;

namespace RCCom.UI
{
    /// <summary>
    /// 빌드 메뉴를 스크롤 가능한 목록으로 표시. TowerRoster 기준으로 버튼을 동적 생성해서,
    /// 몇 종류가 있든(기본 3종 + 카드로 해금된 신규 타워) 전부 스크롤로 보여준다 — 고정
    /// 버튼 개수를 미리 정해둘 필요가 없어짐.
    ///
    /// 카드로 새 타워가 해금될 때마다(CardManager.ChoiceResolved, 어떤 카드를 골라도 발생)
    /// 목록을 다시 채운다 — 매 프레임 폴링 대신 카드 선택 시점에만 갱신해 비용이 거의 없음.
    ///
    /// 프레임 그림은 타워 이름이 아니라 "종류(Attack/Skill/Power)"별로 미리 만들어둔 템플릿을
    /// 재사용한다 — 그래서 관통 사격(Attack)/빙결 오라(Skill)/재생의 축(Power)처럼 나중에
    /// 해금되는 타워도 kind만 맞으면 자동으로 알맞은 프레임을 쓰게 된다.
    /// </summary>
    public class TowerBuildMenuUI : MonoBehaviour
    {
        [SerializeField] private TowerRoster towerRoster;
        [SerializeField] private TowerBuildController buildController;
        [SerializeField] private CardManager cardManager;
        [SerializeField] private Transform contentParent;
        [SerializeField] private TowerBuildButton buttonPrefab;

        [Header("종류별 프레임 템플릿 (이름이 이미 그려진 이미지)")]
        [SerializeField] private Sprite attackFrameSprite;
        [SerializeField] private Sprite skillFrameSprite;
        [SerializeField] private Sprite powerFrameSprite;

        private void Awake()
        {
            // 원본 에셋이 아니라 세션 전용 복제본을 참조하도록 교체 — 카드가 Data를 직접
            // 수정해도 원본 .asset이 오염되지 않게 함 (TowerRoster.GetRuntimeInstance 참고).
            towerRoster = OperatorLoadoutSession.ResolveTowerRoster(towerRoster).GetRuntimeInstance();
        }

        private void OnEnable()
        {
            cardManager.ChoiceResolved += Rebuild;
            Rebuild();
        }

        private void OnDisable()
        {
            cardManager.ChoiceResolved -= Rebuild;
        }

        private void Rebuild()
        {
            foreach (Transform child in contentParent)
            {
                Destroy(child.gameObject);
            }

            for (int i = 0; i < towerRoster.towers.Count; i++)
            {
                int index = i;
                TowerDefinition definition = towerRoster.towers[i];
                Sprite frameSprite = GetFrameSprite(definition.Data.kind);

                TowerBuildButton button = Instantiate(buttonPrefab, contentParent);
                button.Setup(definition, frameSprite, () => buildController.SelectTower(index));
            }
        }

        private Sprite GetFrameSprite(TowerKind kind)
        {
            return kind switch
            {
                TowerKind.Attack => attackFrameSprite,
                TowerKind.Skill => skillFrameSprite,
                TowerKind.Power => powerFrameSprite,
                _ => null,
            };
        }
    }
}
